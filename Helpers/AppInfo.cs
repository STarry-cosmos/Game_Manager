using System;
using System.Reflection;

namespace Game_Manager.Helpers
{
    /// <summary>
    /// 应用版本与项目元信息的唯一来源。
    /// 版本号从 csproj 的 &lt;Version&gt; 读取（编译进程序集），界面上不要写死字符串。
    /// </summary>
    public static class AppInfo
    {
        public const string ProjectUrl = "https://github.com/STarry-cosmos/Game_Manager";

        public const string LicenseName = "MIT License";

        /// <summary>语义版本号，例如 "0.20.3"。</summary>
        public static string Version { get; } = ResolveVersion();

        /// <summary>用于界面展示的版本号，例如 "v0.20.3"。</summary>
        public static string VersionDisplay => "v" + Version;

        /// <summary>四段式程序集版本，例如 "0.20.3.0"。</summary>
        public static string AssemblyVersion { get; } =
            typeof(AppInfo).Assembly.GetName().Version?.ToString() ?? "未知";

        private static string ResolveVersion()
        {
            var assembly = typeof(AppInfo).Assembly;

            // csproj 的 <Version> 会写入 InformationalVersion；
            // 若开启了 SourceLink 等，末尾可能带 "+<commit>"，展示前截掉。
            var informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informational))
            {
                var plusIndex = informational.IndexOf('+');
                return plusIndex >= 0 ? informational.Substring(0, plusIndex) : informational;
            }

            var fileVersion = assembly
                .GetCustomAttribute<AssemblyFileVersionAttribute>()?
                .Version;

            if (!string.IsNullOrWhiteSpace(fileVersion))
            {
                return fileVersion!;
            }

            return assembly.GetName().Version?.ToString() ?? "未知";
        }
    }
}
