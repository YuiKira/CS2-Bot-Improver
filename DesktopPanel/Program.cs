using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CS2BotTools;

internal static class Program
{
    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [STAThread]
    private static void Main()
    {
        try { SetProcessDPIAware(); } catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    private readonly WebView2 _webView = new() { Dock = DockStyle.Fill };
    private readonly string _appDirectory = AppDomain.CurrentDomain.BaseDirectory;
    private readonly UTF8Encoding _utf8 = new(false);
    private string _selectedRoot = string.Empty;

    public MainForm()
    {
        Text = "CS2 Bot Tools";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1380, 840);
        MinimumSize = new Size(1080, 680);
        BackColor = Color.FromArgb(238, 240, 243);
        Controls.Add(_webView);
        Shown += OnShown;
    }

    private async void OnShown(object? sender, EventArgs e)
    {
        Shown -= OnShown;
        try
        {
            var webRoot = Path.Combine(_appDirectory, "BotEquipmentPanel");
            if (!File.Exists(Path.Combine(webRoot, "index.html"))
                || !File.Exists(Path.Combine(webRoot, "catalog.json"))
                || !File.Exists(Path.Combine(webRoot, "knife-catalog.json")))
                throw new FileNotFoundException("装备面板文件不完整，请重新解压完整发布包。");

            var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var userData = Path.Combine(localData, "CS2BotImprover", "WebView2");
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userData);
            await _webView.EnsureCoreWebView2Async(environment);

            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "app.local",
                webRoot,
                CoreWebView2HostResourceAccessKind.Allow);
            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            _webView.Source = new Uri("https://app.local/index.html");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "桌面面板启动失败。请确认 Windows 已安装 Microsoft Edge WebView2 Runtime。\r\n\r\n" + ex.Message,
                "CS2 Bot Tools",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            Close();
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var id = 0;
        try
        {
            var request = JObject.Parse(e.TryGetWebMessageAsString());
            id = request.Value<int>("id");
            var path = request.Value<string>("path") ?? string.Empty;
            var body = request["body"] as JObject ?? new JObject();

            switch (path)
            {
                case "/api/bootstrap":
                    _selectedRoot = DetectDefaultRoot();
                    Reply(id, new JObject
                    {
                        ["root"] = _selectedRoot,
                        ["config"] = ReadConfig(_selectedRoot),
                        ["status"] = GetInstallationStatus(_selectedRoot),
                        ["original_panel_available"] = File.Exists(Path.Combine(_appDirectory, "Panel v1.4.2.exe"))
                    });
                    break;
                case "/api/load":
                    _selectedRoot = body.Value<string>("root") ?? string.Empty;
                    ReplyWithConfig(id);
                    break;
                case "/api/save":
                    _selectedRoot = body.Value<string>("root") ?? string.Empty;
                    var savedPath = WriteConfig(_selectedRoot, body["config"] ?? new JObject());
                    Reply(id, new JObject
                    {
                        ["ok"] = true,
                        ["path"] = savedPath,
                        ["status"] = GetInstallationStatus(_selectedRoot)
                    });
                    break;
                case "/api/enable-bot-mode":
                    _selectedRoot = body.Value<string>("root") ?? string.Empty;
                    EnableBotMode(_selectedRoot);
                    Reply(id, new JObject
                    {
                        ["ok"] = true,
                        ["status"] = GetInstallationStatus(_selectedRoot)
                    });
                    break;
                case "/api/browse":
                    BrowseForRoot();
                    ReplyWithConfig(id);
                    break;
                case "/api/launch-original":
                    LaunchOriginalPanel();
                    Reply(id, new JObject { ["ok"] = true });
                    break;
                case "/api/shutdown":
                    Reply(id, new JObject { ["ok"] = true });
                    BeginInvoke(new Action(Close));
                    break;
                default:
                    throw new InvalidOperationException("未知的桌面面板请求：" + path);
            }
        }
        catch (Exception ex)
        {
            ReplyError(id, ex.Message);
        }
    }

    private string DetectDefaultRoot()
    {
        var plugin = Path.Combine(_appDirectory, "addons", "counterstrikesharp", "plugins", "BotRandomizer");
        return Directory.Exists(plugin) ? _appDirectory.TrimEnd(Path.DirectorySeparatorChar) : string.Empty;
    }

    private static string ConfigPath(string root) => Path.Combine(
        root,
        "addons",
        "counterstrikesharp",
        "plugins",
        "BotRandomizer",
        "BotRandomizer.custom.json");

    private JToken ReadConfig(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !File.Exists(ConfigPath(root)))
            return DefaultConfig();
        try
        {
            return JToken.Parse(File.ReadAllText(ConfigPath(root), Encoding.UTF8));
        }
        catch (Exception ex)
        {
            throw new InvalidDataException("配置文件格式无效：" + ConfigPath(root), ex);
        }
    }

    private string WriteConfig(string root, JToken config)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("请先选择 CS2 game/csgo 文件夹。");
        var pluginDirectory = Path.GetDirectoryName(ConfigPath(root))!;
        if (!Directory.Exists(pluginDirectory))
            throw new DirectoryNotFoundException("所选文件夹中没有找到 BotRandomizer 插件。");
        var path = ConfigPath(root);
        File.WriteAllText(path, config.ToString(Formatting.Indented), _utf8);
        return path;
    }

    private static JObject GetInstallationStatus(string root)
    {
        var missing = new JArray();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return new JObject
            {
                ["root_valid"] = false,
                ["bot_mode_active"] = false,
                ["names_ready"] = false,
                ["can_enable_bot_mode"] = false,
                ["missing"] = missing
            };

        var gameInfoPath = Path.Combine(root, "gameinfo.gi");
        var gameInfo = File.Exists(gameInfoPath)
            ? File.ReadAllText(gameInfoPath, Encoding.UTF8)
            : string.Empty;
        var botModeActive = gameInfo.IndexOf("csgo/addons/metamod", StringComparison.OrdinalIgnoreCase) >= 0
            && gameInfo.IndexOf("csgo/overrides/botprofile.vpk", StringComparison.OrdinalIgnoreCase) >= 0;

        var requiredNameFiles = new[]
        {
            Path.Combine("addons", "metamod", "bin", "server.dll"),
            Path.Combine("addons", "counterstrikesharp", "bin", "win64", "counterstrikesharp.dll"),
            Path.Combine("addons", "counterstrikesharp", "plugins", "BotRandomizer", "BotRandomizer.dll"),
            Path.Combine("addons", "BotHider", "bin", "win64", "BotHider.dll"),
            Path.Combine("addons", "BotHider", "bot_info.json"),
            Path.Combine("addons", "metamod", "BotHider.vdf"),
            Path.Combine("overrides", "botprofile.vpk")
        };
        foreach (var relativePath in requiredNameFiles)
        {
            if (!File.Exists(Path.Combine(root, relativePath)))
                missing.Add(relativePath.Replace(Path.DirectorySeparatorChar, '/'));
        }

        return new JObject
        {
            ["root_valid"] = true,
            ["bot_mode_active"] = botModeActive,
            ["names_ready"] = botModeActive && missing.Count == 0,
            ["can_enable_bot_mode"] = File.Exists(Path.Combine(root, "backup", "WithBots", "gameinfo.gi")),
            ["missing"] = missing
        };
    }

    private static void EnableBotMode(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            throw new DirectoryNotFoundException("请先选择正确的 CS2 game/csgo 文件夹。");

        var source = Path.Combine(root, "backup", "WithBots", "gameinfo.gi");
        if (!File.Exists(source))
            throw new FileNotFoundException("没有找到 Bot 模式备份文件，请重新覆盖安装完整发布包。", source);

        File.Copy(source, Path.Combine(root, "gameinfo.gi"), true);
    }

    private void BrowseForRoot()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择 CS2 game/csgo 文件夹",
            SelectedPath = Directory.Exists(_selectedRoot) ? _selectedRoot : string.Empty,
            ShowNewFolderButton = false
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _selectedRoot = dialog.SelectedPath;
    }

    private void LaunchOriginalPanel()
    {
        var path = Path.Combine(_appDirectory, "Panel v1.4.2.exe");
        if (!File.Exists(path))
            throw new FileNotFoundException("没有找到 Panel v1.4.2.exe。", path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, WorkingDirectory = _appDirectory });
    }

    private void ReplyWithConfig(int id) => Reply(id, new JObject
    {
        ["root"] = _selectedRoot,
        ["config"] = ReadConfig(_selectedRoot),
        ["status"] = GetInstallationStatus(_selectedRoot)
    });

    private void Reply(int id, JToken data)
    {
        if (_webView.CoreWebView2 == null) return;
        _webView.CoreWebView2.PostWebMessageAsString(new JObject
        {
            ["id"] = id,
            ["ok"] = true,
            ["data"] = data
        }.ToString(Formatting.None));
    }

    private void ReplyError(int id, string message)
    {
        if (_webView.CoreWebView2 == null) return;
        _webView.CoreWebView2.PostWebMessageAsString(new JObject
        {
            ["id"] = id,
            ["ok"] = false,
            ["error"] = message
        }.ToString(Formatting.None));
    }

    private static JObject DefaultConfig() => new()
    {
        ["knife_def_indexes"] = new JArray(507, 515),
        ["knife_paint_kits"] = new JArray(415, 416),
        ["knife_skin_settings"] = new JObject(),
        ["knife_paint_kits_by_def_index"] = new JObject
        {
            ["507"] = new JArray(415, 416),
            ["515"] = new JArray(415)
        },
        ["knife_skin_settings_by_def_index"] = new JObject(),
        ["weapon_paint_kits"] = new JObject(),
        ["weapon_skin_settings"] = new JObject(),
        ["auto_drop_bot_knife_copy"] = true,
        ["drop_delay_seconds"] = 1.0
    };
}
