using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CS2BotTools;

internal static class Uninstaller
{
    private const string WorkerSwitch = "--uninstall-worker";
    private const string ManifestFileName = "uninstall-manifest.json";

    public static bool IsWorkerInvocation(string[] args) =>
        args.Length >= 4 && string.Equals(args[0], WorkerSwitch, StringComparison.OrdinalIgnoreCase);

    public static JObject GetInfo(string appDirectory, string selectedRoot)
    {
        var root = ValidateInstallRoot(appDirectory, selectedRoot);
        var manifest = LoadManifest(Path.Combine(appDirectory, ManifestFileName));
        var blockers = FindBlockingProcesses();

        return new JObject
        {
            ["root"] = root,
            ["file_count"] = CountExistingFiles(root, manifest),
            ["blockers"] = new JArray(blockers),
            ["can_uninstall"] = blockers.Count == 0
        };
    }

    public static void Start(string appDirectory, string selectedRoot)
    {
        var root = ValidateInstallRoot(appDirectory, selectedRoot);
        var manifestPath = Path.Combine(appDirectory, ManifestFileName);
        LoadManifest(manifestPath);

        var blockers = FindBlockingProcesses();
        if (blockers.Count != 0)
            throw new InvalidOperationException("请先退出以下程序后再卸载：" + string.Join("、", blockers));

        var workerDirectory = Path.Combine(
            Path.GetTempPath(),
            "CS2BotImprover-Uninstall-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workerDirectory);

        var sourceExe = Application.ExecutablePath;
        var workerExe = Path.Combine(workerDirectory, "CS2BotImprover-Uninstaller.exe");
        var workerManifest = Path.Combine(workerDirectory, ManifestFileName);
        File.Copy(sourceExe, workerExe, true);
        File.Copy(manifestPath, workerManifest, true);
        CopyIfPresent(sourceExe + ".config", workerExe + ".config");
        foreach (var dependency in new[]
        {
            "Newtonsoft.Json.dll",
            "Microsoft.Web.WebView2.Core.dll",
            "Microsoft.Web.WebView2.WinForms.dll",
            "WebView2Loader.dll"
        })
        {
            CopyIfPresent(
                Path.Combine(appDirectory, dependency),
                Path.Combine(workerDirectory, dependency));
        }

        var arguments = string.Join(" ", new[]
        {
            WorkerSwitch,
            Encode(root),
            Encode(workerManifest),
            Process.GetCurrentProcess().Id.ToString()
        });
        Process.Start(new ProcessStartInfo(workerExe, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workerDirectory
        });
    }

    public static void RunWorker(string[] args)
    {
        var silent = args.Any(arg => string.Equals(arg, "--silent", StringComparison.OrdinalIgnoreCase));
        var failures = new List<string>();
        var deleted = 0;
        string? workerDirectory = null;

        try
        {
            var root = NormalizeDirectory(Decode(args[1]));
            var manifestPath = Decode(args[2]);
            workerDirectory = Path.GetDirectoryName(manifestPath);
            var parentPid = int.Parse(args[3]);
            var manifest = LoadManifest(manifestPath);

            WaitForParent(parentPid);
            RestoreFiles(root, manifest, failures);

            foreach (var relativePath in manifest.RuntimeFiles.Concat(manifest.ProductOwnedFiles).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var path = ResolveUnderRoot(root, relativePath);
                if (DeleteFileWithRetry(path, failures)) deleted++;
            }

            RemoveEmptyOwnedDirectories(root, manifest);
            DeleteLocalAppData(manifest, failures);
        }
        catch (Exception ex)
        {
            failures.Add(ex.Message);
        }

        if (silent && failures.Count != 0)
        {
            foreach (var failure in failures) Console.Error.WriteLine(failure);
        }
        else if (!silent)
        {
            var message = failures.Count == 0
                ? $"卸载完成，已删除 {deleted} 个项目文件。\r\n\r\n下一步请在 Steam 中打开 CS2 属性 -> 已安装文件 -> 验证游戏文件的完整性。"
                : $"卸载已执行，但有 {failures.Count} 项未能处理。\r\n\r\n{string.Join("\r\n", failures.Take(6))}\r\n\r\n请重启电脑后再次运行卸载，并在 Steam 中验证游戏文件的完整性。";
            MessageBox.Show(
                message,
                "CS2-Bot-Improver 卸载",
                MessageBoxButtons.OK,
                failures.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        if (!silent && !string.IsNullOrWhiteSpace(workerDirectory))
            ScheduleWorkerCleanup(workerDirectory!);

        Environment.ExitCode = failures.Count == 0 ? 0 : 1;
    }

    private static string ValidateInstallRoot(string appDirectory, string selectedRoot)
    {
        if (string.IsNullOrWhiteSpace(selectedRoot) || !Directory.Exists(selectedRoot))
            throw new DirectoryNotFoundException("请先选择正确的 CS2 game/csgo 文件夹。");

        var appRoot = NormalizeDirectory(appDirectory);
        var root = NormalizeDirectory(selectedRoot);
        if (!string.Equals(appRoot, root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("为避免误删文件，请从需要卸载的 CS2 game/csgo 文件夹中运行本工具。");
        if (!File.Exists(Path.Combine(root, ManifestFileName)))
            throw new FileNotFoundException("卸载清单缺失，请重新覆盖安装完整发布包后再卸载。");
        if (!Directory.Exists(Path.Combine(root, "addons")))
            throw new DirectoryNotFoundException("所选文件夹中没有找到插件目录，已停止卸载。");
        return root;
    }

    private static UninstallManifest LoadManifest(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("卸载清单缺失。", path);
        var manifest = JsonConvert.DeserializeObject<UninstallManifest>(File.ReadAllText(path, Encoding.UTF8));
        if (manifest == null || manifest.ProductOwnedFiles.Count == 0)
            throw new InvalidDataException("卸载清单无效或没有包含项目文件。");
        foreach (var pathEntry in manifest.ProductOwnedFiles.Concat(manifest.RuntimeFiles).Concat(manifest.SteamManagedFiles))
            ValidateRelativePath(pathEntry);
        foreach (var item in manifest.RestoreFiles)
        {
            ValidateRelativePath(item.Source);
            ValidateRelativePath(item.Destination);
        }
        return manifest;
    }

    private static List<string> FindBlockingProcesses()
    {
        var blockers = new List<string>();
        if (Process.GetProcessesByName("cs2").Length > 0) blockers.Add("Counter-Strike 2");
        if (Process.GetProcessesByName("Panel v1.4.2").Length > 0) blockers.Add("原始控制面板");
        return blockers;
    }

    private static int CountExistingFiles(string root, UninstallManifest manifest) =>
        manifest.ProductOwnedFiles.Concat(manifest.RuntimeFiles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count(path => File.Exists(ResolveUnderRoot(root, path)));

    private static void RestoreFiles(string root, UninstallManifest manifest, List<string> failures)
    {
        foreach (var item in manifest.RestoreFiles)
        {
            var source = ResolveUnderRoot(root, item.Source);
            var destination = ResolveUnderRoot(root, item.Destination);
            if (!File.Exists(source))
            {
                failures.Add("未找到原版备份：" + item.Source);
                continue;
            }
            try
            {
                File.Copy(source, destination, true);
            }
            catch (Exception ex)
            {
                failures.Add("恢复失败 " + item.Destination + "：" + ex.Message);
            }
        }
    }

    private static bool DeleteFileWithRetry(string path, List<string> failures)
    {
        if (!File.Exists(path)) return false;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            try
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                return true;
            }
            catch when (attempt < 7)
            {
                Thread.Sleep(250);
            }
            catch (Exception ex)
            {
                failures.Add("删除失败 " + path + "：" + ex.Message);
            }
        }
        return false;
    }

    private static void RemoveEmptyOwnedDirectories(string root, UninstallManifest manifest)
    {
        var directories = manifest.ProductOwnedFiles.Concat(manifest.RuntimeFiles)
            .Select(path => Path.GetDirectoryName(ResolveUnderRoot(root, path)))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(path => path!.Length);
        foreach (var directory in directories)
        {
            var current = directory;
            while (!string.IsNullOrWhiteSpace(current)
                && !string.Equals(current, root, StringComparison.OrdinalIgnoreCase)
                && IsUnderRoot(root, current))
            {
                try
                {
                    if (!Directory.Exists(current) || Directory.EnumerateFileSystemEntries(current).Any()) break;
                    Directory.Delete(current);
                    current = Path.GetDirectoryName(current);
                }
                catch { break; }
            }
        }
    }

    private static void DeleteLocalAppData(UninstallManifest manifest, List<string> failures)
    {
        var localRoot = NormalizeDirectory(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        foreach (var relativePath in manifest.LocalAppDataDirectories)
        {
            ValidateRelativePath(relativePath);
            var path = ResolveUnderRoot(localRoot, relativePath);
            if (!Directory.Exists(path)) continue;
            try
            {
                Directory.Delete(path, true);
            }
            catch (Exception ex)
            {
                failures.Add("清理本地数据失败 " + path + "：" + ex.Message);
            }
        }
    }

    private static void WaitForParent(int parentPid)
    {
        if (parentPid <= 0) return;
        try
        {
            using var parent = Process.GetProcessById(parentPid);
            if (!parent.WaitForExit(30000))
                throw new InvalidOperationException("启动器未能正常退出，已停止删除文件。请关闭启动器后重试卸载。");
        }
        catch (ArgumentException) { }
    }

    private static void ScheduleWorkerCleanup(string workerDirectory)
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT) return;
        var command = $"/d /c ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"{workerDirectory}\"";
        Process.Start(new ProcessStartInfo("cmd.exe", command)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    private static string ResolveUnderRoot(string root, string relativePath)
    {
        ValidateRelativePath(relativePath);
        var path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsUnderRoot(root, path)) throw new InvalidDataException("卸载清单包含越界路径：" + relativePath);
        return path;
    }

    private static bool IsUnderRoot(string root, string path) =>
        path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static void ValidateRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)
            || path.Split('/', '\\').Any(part => part == ".."))
            throw new InvalidDataException("卸载清单包含无效路径：" + path);
    }

    private static string NormalizeDirectory(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static string Encode(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static string Decode(string value) =>
        Encoding.UTF8.GetString(Convert.FromBase64String(value));

    private static void CopyIfPresent(string source, string destination)
    {
        if (File.Exists(source)) File.Copy(source, destination, true);
    }

    private sealed class UninstallManifest
    {
        [JsonProperty("product_owned_files")]
        public List<string> ProductOwnedFiles { get; set; } = new();

        [JsonProperty("runtime_files")]
        public List<string> RuntimeFiles { get; set; } = new();

        [JsonProperty("steam_managed_files")]
        public List<string> SteamManagedFiles { get; set; } = new();

        [JsonProperty("restore_files")]
        public List<RestoreFile> RestoreFiles { get; set; } = new();

        [JsonProperty("local_app_data_directories")]
        public List<string> LocalAppDataDirectories { get; set; } = new();
    }

    private sealed class RestoreFile
    {
        [JsonProperty("source")]
        public string Source { get; set; } = string.Empty;

        [JsonProperty("destination")]
        public string Destination { get; set; } = string.Empty;
    }
}
