using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Timers;
using Game_Manager.Data;

namespace Game_Manager.Services
{
    public class ProcessMonitorService : IDisposable
    {
        // events for consumers (ViewModels) to react to start/stop
        public event Action<int>? GameStarted;
        public event Action<int>? GameStopped;
        private readonly IDatabaseManager _db;
        private readonly ConcurrentDictionary<int, MonitoredGame> _monitors = new();
        private bool _disposed;

        private class MonitoredGame
        {
            public Process? Process { get; set; }
            public System.Timers.Timer? HeartbeatTimer { get; set; }
            public DateTime SessionStartUtc { get; set; }
            public long LastSavedSeconds { get; set; }
            public readonly object Lock = new();
        }

        public ProcessMonitorService(IDatabaseManager db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public void StartGame(string exePath, int gameId)
        {
            if (string.IsNullOrWhiteSpace(exePath)) throw new ArgumentNullException(nameof(exePath));

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty
            };

            Process? proc = null;
            try
            {
                proc = Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                // surface but do not crash caller
                Debug.WriteLine($"StartGame failed: {ex}");
                return;
            }

            if (proc == null)
            {
                Debug.WriteLine("Process.Start returned null");
                return;
            }

            proc.EnableRaisingEvents = true;

            var monitored = new MonitoredGame
            {
                Process = proc,
                SessionStartUtc = DateTime.UtcNow,
                LastSavedSeconds = 0
            };

            // create heartbeat timer
            var timer = new System.Timers.Timer(TimeSpan.FromSeconds(30).TotalMilliseconds)
            {
                AutoReset = true,
                Enabled = true
            };

            timer.Elapsed += (s, e) => OnHeartbeat(gameId);
            monitored.HeartbeatTimer = timer;

            proc.Exited += (s, e) => OnProcessExited(gameId);

            _monitors[gameId] = monitored;

            try
            {
                // update DB: mark running
                var record = _db.GetGameById(gameId);
                if (record != null)
                {
                    record.IsRunning = true;
                    record.ProcessId = proc.Id;
                    record.CurrentSessionTime = 0;
                    _db.UpdateGame(record);
                }

                // notify subscribers that game started
                try { GameStarted?.Invoke(gameId); } catch { }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to update DB on StartGame: {ex}");
            }
        }

        public void RecoverRunningGames(IEnumerable<GameRecord> runningGames)
        {
            if (runningGames == null) return;

            foreach (var record in runningGames)
            {
                try
                {
                    RecoverRunningGame(record);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"RecoverRunningGames failed for {record?.Id}: {ex}");
                }
            }
        }

        private void RecoverRunningGame(GameRecord record)
        {
            if (record == null || !record.IsRunning || !record.ProcessId.HasValue)
            {
                return;
            }

            Process? process = null;
            try
            {
                process = Process.GetProcessById(record.ProcessId.Value);
            }
            catch
            {
                process = null;
            }

            var isAlive = false;
            if (process != null)
            {
                try
                {
                    isAlive = !process.HasExited;
                }
                catch
                {
                    isAlive = false;
                }
            }

            if (!isAlive)
            {
                record.TotalPlayTime += record.CurrentSessionTime;
                record.CurrentSessionTime = 0;
                record.IsRunning = false;
                record.ProcessId = null;
                record.LastPlayed = DateTime.UtcNow;
                _db.UpdateGame(record);
                try { process?.Dispose(); } catch { }
                return;
            }

            // attach to the existing live process and resume monitoring
            var gameId = record.Id;
            try
            {
                var monitored = new MonitoredGame
                {
                    Process = process,
                    SessionStartUtc = DateTime.UtcNow.AddSeconds(-record.CurrentSessionTime),
                    LastSavedSeconds = record.CurrentSessionTime
                };

                var timer = new System.Timers.Timer(TimeSpan.FromSeconds(30).TotalMilliseconds)
                {
                    AutoReset = true,
                    Enabled = true
                };

                timer.Elapsed += (s, e) => OnHeartbeat(gameId);
                monitored.HeartbeatTimer = timer;

                process!.EnableRaisingEvents = true;
                process!.Exited += (s, e) => OnProcessExited(gameId);

                _monitors[gameId] = monitored;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to recover running process for {gameId}: {ex}");
                try { process?.Dispose(); } catch { }
            }
        }

        private void OnHeartbeat(int gameId)
        {
            if (!_monitors.TryGetValue(gameId, out var info)) return;
            lock (info.Lock)
            {
                try
                {
                    // if process exited but Exited event not fired, handle it here
                    if (info.Process != null)
                    {
                        bool exited = false;
                        try { exited = info.Process.HasExited; } catch { }
                        if (exited)
                        {
                            OnProcessExited(gameId);
                            return;
                        }
                    }

                    var elapsed = (long)(DateTime.UtcNow - info.SessionStartUtc).TotalSeconds;
                    if (elapsed <= info.LastSavedSeconds) return;

                    // write current session time to DB
                    var record = _db.GetGameById(gameId);
                    if (record != null)
                    {
                        record.CurrentSessionTime = elapsed;
                        _db.UpdateGame(record);
                        info.LastSavedSeconds = elapsed;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Heartbeat error for {gameId}: {ex}");
                }
            }
        }

        private void OnProcessExited(int gameId)
        {
            if (!_monitors.TryRemove(gameId, out var info)) return;

            lock (info.Lock)
            {
                try
                {
                    info.HeartbeatTimer?.Stop();
                    info.HeartbeatTimer?.Dispose();

                    var finalSeconds = (long)(DateTime.UtcNow - info.SessionStartUtc).TotalSeconds;

                    var record = _db.GetGameById(gameId);
                    if (record != null)
                    {
                        // total play time should include the full final session duration
                        record.TotalPlayTime += finalSeconds;
                        record.CurrentSessionTime = 0;
                        record.IsRunning = false;
                        record.ProcessId = null;
                        record.LastPlayed = DateTime.UtcNow;
                        _db.UpdateGame(record);

                        // notify subscribers that game stopped
                        try { GameStopped?.Invoke(gameId); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OnProcessExited error for {gameId}: {ex}");
                }
                finally
                {
                    try { info.Process?.Dispose(); } catch { }
                }
            }
        }

        public void StopMonitoring(int gameId)
        {
            if (!_monitors.TryRemove(gameId, out var info)) return;

            lock (info.Lock)
            {
                try
                {
                    info.HeartbeatTimer?.Stop();
                    info.HeartbeatTimer?.Dispose();

                    // if process already exited, handle as exited
                    if (info.Process == null || info.Process.HasExited)
                    {
                        OnProcessExited(gameId);
                        return;
                    }

                    // process still running: persist current session time and stop monitoring
                    var elapsed = (long)(DateTime.UtcNow - info.SessionStartUtc).TotalSeconds;
                    var record = _db.GetGameById(gameId);
                    if (record != null)
                    {
                        record.CurrentSessionTime = elapsed;
                        // keep IsRunning and ProcessId so next launcher can resume
                        _db.UpdateGame(record);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"StopMonitoring error for {gameId}: {ex}");
                }
            }
        }

        public void Shutdown()
        {
            foreach (var kv in _monitors)
            {
                var gameId = kv.Key;
                var info = kv.Value;

                lock (info.Lock)
                {
                    try
                    {
                        info.HeartbeatTimer?.Stop();
                        info.HeartbeatTimer?.Dispose();

                        if (info.Process == null || info.Process.HasExited)
                        {
                            // handle exit
                            OnProcessExited(gameId);
                            continue;
                        }

                        // process still running: persist current session time but leave IsRunning=true
                        var elapsed = (long)(DateTime.UtcNow - info.SessionStartUtc).TotalSeconds;
                        var record = _db.GetGameById(gameId);
                        if (record != null)
                        {
                            record.CurrentSessionTime = elapsed;
                            record.IsRunning = true;
                            record.ProcessId = info.Process.Id;
                            _db.UpdateGame(record);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Shutdown error for {gameId}: {ex}");
                    }
                    finally
                    {
                        try { info.Process?.Dispose(); } catch { }
                    }
                }
            }

            _monitors.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            Shutdown();
            _disposed = true;
        }
    }
}
