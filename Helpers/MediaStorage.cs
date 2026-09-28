using System;
using System.IO;
using Game_Manager.Data;

namespace Game_Manager.Helpers
{
    /// <summary>
    /// Stores cover / memory-scene images under %LocalAppData%\GameLauncher\Media\.
    /// Database paths are stored as paths relative to the Media root when possible.
    /// </summary>
    public static class MediaStorage
    {
        public static string MediaRoot =>
            Path.Combine(DatabaseManager.DatabaseDirectoryPath, "Media");

        public static void EnsureMediaRoot()
        {
            Directory.CreateDirectory(MediaRoot);
        }

        /// <summary>
        /// Copies an image into Media and returns a relative path for DB storage.
        /// </summary>
        public static string ImportImage(string sourcePath, string category, int? gameId = null)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("Source path is empty.", nameof(sourcePath));
            }

            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("Source image not found.", sourcePath);
            }

            EnsureMediaRoot();

            var relativeDir = gameId.HasValue
                ? Path.Combine(category, gameId.Value.ToString())
                : category;
            var absoluteDir = Path.Combine(MediaRoot, relativeDir);
            Directory.CreateDirectory(absoluteDir);

            var extension = Path.GetExtension(sourcePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".png";
            }

            var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var absolutePath = Path.Combine(absoluteDir, fileName);
            File.Copy(sourcePath, absolutePath, overwrite: false);

            return Path.Combine(relativeDir, fileName);
        }

        /// <summary>
        /// Resolves a DB-stored path (relative or absolute) to an existing absolute file path.
        /// </summary>
        public static string? ResolvePath(string? storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath))
            {
                return null;
            }

            if (Path.IsPathRooted(storedPath))
            {
                return File.Exists(storedPath) ? storedPath : null;
            }

            var absolute = Path.Combine(MediaRoot, storedPath);
            return File.Exists(absolute) ? absolute : null;
        }

        public static bool IsManagedFile(string? absoluteOrStoredPath)
        {
            var absolute = ResolvePath(absoluteOrStoredPath) ?? absoluteOrStoredPath;
            if (string.IsNullOrWhiteSpace(absolute) || !Path.IsPathRooted(absolute))
            {
                // Relative stored paths are always under Media.
                return !string.IsNullOrWhiteSpace(absoluteOrStoredPath) && !Path.IsPathRooted(absoluteOrStoredPath);
            }

            try
            {
                var mediaRoot = Path.GetFullPath(MediaRoot)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                var full = Path.GetFullPath(absolute);
                return full.StartsWith(mediaRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static void TryDeleteManagedFile(string? storedPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(storedPath))
                {
                    return;
                }

                // Only delete files we own under Media.
                if (Path.IsPathRooted(storedPath) && !IsManagedFile(storedPath))
                {
                    return;
                }

                if (!Path.IsPathRooted(storedPath) && !IsManagedFile(storedPath))
                {
                    return;
                }

                var absolute = ResolvePath(storedPath);
                if (absolute != null && File.Exists(absolute) && IsManagedFile(absolute))
                {
                    File.Delete(absolute);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        /// <summary>
        /// If the stored path points at an external existing file, copy it into Media and return the new relative path.
        /// Returns null when no migration is needed or migration is not possible.
        /// </summary>
        public static string? TryMigrateExternalFile(string? storedPath, string category, int? gameId = null)
        {
            if (string.IsNullOrWhiteSpace(storedPath) || !Path.IsPathRooted(storedPath))
            {
                return null;
            }

            if (!File.Exists(storedPath))
            {
                return null;
            }

            if (IsManagedFile(storedPath))
            {
                // Already under Media but stored as absolute — normalize to relative.
                var mediaRoot = Path.GetFullPath(MediaRoot)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                var full = Path.GetFullPath(storedPath);
                if (full.StartsWith(mediaRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return full.Substring(mediaRoot.Length);
                }

                return null;
            }

            return ImportImage(storedPath, category, gameId);
        }
    }
}
