using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;
using NeoruInstallerSkin;

[assembly: AssemblyTitle("Frontier Tale - Русификатор")]
[assembly: AssemblyDescription("Установщик русификатора текста для Frontier Tale")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("neoru-droid")]
[assembly: AssemblyProduct("Frontier Tale - Русификатор")]
[assembly: AssemblyCopyright("Copyright (c) 2026 neoru-droid")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("1.5.0.0")]
[assembly: AssemblyFileVersion("1.5.0.0")]

namespace FrontierTaleRuSetup
{
    internal static class Program
    {
        public const int TargetAppId = 4829780;
        public const string BoostyUrl = "https://boosty.to/cd_tixo/posts/486ebb53-422d-4023-9da7-b1fdbc0b2a7f";
        public const string SiteUrl = "https://neoru-droid.github.io/";
        public const string GithubUrl = "https://github.com/neoru-droid/frontier-tale-ru";
        public const string TelegramUrl = "https://t.me/neoru_droid";
        public const string CampaignSlug = "frontier_tale";

        public static SkinSpec CreateSkin()
        {
            return SkinSpec.CreateParchmentGold(
                Color.FromArgb(0x11, 0x1A, 0x16),
                Color.FromArgb(0x19, 0x26, 0x20),
                Color.FromArgb(0x0E, 0x17, 0x13),
                Color.FromArgb(0x36, 0x4E, 0x3F),
                Color.FromArgb(0xE0, 0xAA, 0x4E),
                Color.FromArgb(0x1A, 0x12, 0x05));
        }

        public static string BuildBoostyUrl()
        {
            return SkinLinks.BuildUtmUrl(BoostyUrl, SkinLinks.DefaultBoostyUrl, CampaignSlug);
        }

        public static string BuildSiteUrl()
        {
            return SkinLinks.BuildUtmUrl(SiteUrl, SkinLinks.DefaultSiteUrl, CampaignSlug);
        }

        public static string GetTelegramUrl()
        {
            string raw = TelegramUrl;
            return string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim();
        }

        private const int AttachParentProcess = -1;
        private const int StdOutputHandle = -11;

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [STAThread]
        private static int Main(string[] args)
        {
            ConfigureTls();
            string appBaseDir = SafeTrimTrailingSlash(AppDomain.CurrentDomain.BaseDirectory);

            CliArguments cli;
            string parseError;
            if (!CliArguments.TryParse(args, out cli, out parseError))
            {
                EnsureConsoleAttached();
                ConfigureUtf8StdStreams();
                Console.Error.WriteLine("ОШИБКА: " + parseError);
                PrintCliHelp();
                return 1;
            }

            if (cli.ShowHelp)
            {
                EnsureConsoleAttached();
                ConfigureUtf8StdStreams();
                PrintCliHelp();
                return 0;
            }

            if (cli.Mode == CliMode.Status || cli.Mode == CliMode.Install || cli.Mode == CliMode.Uninstall)
            {
                return RunHeadlessCli(appBaseDir, cli);
            }
            if (cli.Mode == CliMode.RenderPreview)
            {
                try { SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (SetupForm preview = new SetupForm(null, appBaseDir, cli.ExplicitGameDir, cli.ExplicitSteamDir, CliMode.DefaultGui))
                {
                    preview.PreparePreviewSamplePath();
                    PreviewRenderer.RenderFormToPng(preview, cli.PreviewInitPath);
                    preview.ApplyInstallSuccessVisualState();
                    PreviewRenderer.RenderFormToPng(preview, cli.PreviewDonePath);
                }
                return 0;
            }

            try
            {
                SetProcessDPIAware();
            }
            catch
            {
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            PayloadData payload;
            try
            {
                payload = PayloadLoader.LoadEmbeddedPayload();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не удалось загрузить встроенные ресурсы русификатора:\n" + ex.Message,
                    "Frontier Tale - Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return 1;
            }


            Application.Run(new SetupForm(payload, appBaseDir, cli.ExplicitGameDir, cli.ExplicitSteamDir, cli.Mode));
            return 0;
        }

        private static int RunHeadlessCli(string appBaseDir, CliArguments cli)
        {
            EnsureConsoleAttached();
            ConfigureUtf8StdStreams();

            try
            {
                PayloadData payload = PayloadLoader.LoadEmbeddedPayload();

                if (cli.Mode == CliMode.Status)
                {
                    return InstallerService.RunStatusCli(payload, appBaseDir, cli.ExplicitGameDir, cli.ExplicitSteamDir);
                }
                if (cli.Mode == CliMode.Install)
                {
                    string steamDir = SteamLocator.DetectSteamDirectory(cli.ExplicitSteamDir, cli.ExplicitGameDir);
                    string gameDir = SteamLocator.ResolveRequiredGameDirectory(cli.ExplicitGameDir, steamDir, appBaseDir);
                    if (string.IsNullOrEmpty(steamDir))
                    {
                        steamDir = SteamLocator.DetectSteamDirectory(null, gameDir);
                    }
                    InstallerService.Install(payload, gameDir, steamDir, Console.WriteLine);
                    return 0;
                }
                if (cli.Mode == CliMode.Uninstall)
                {
                    string steamDir = SteamLocator.DetectSteamDirectory(cli.ExplicitSteamDir, cli.ExplicitGameDir);
                    string gameDir = SteamLocator.ResolveRequiredGameDirectory(cli.ExplicitGameDir, steamDir, appBaseDir);
                    if (string.IsNullOrEmpty(steamDir))
                    {
                        steamDir = SteamLocator.DetectSteamDirectory(null, gameDir);
                    }
                    InstallerService.Uninstall(gameDir, steamDir, Console.WriteLine);
                    return 0;
                }
                return 1;
            }
            catch (UnauthorizedAccessException uaEx)
            {
                Console.Error.WriteLine("ОШИБКА: Отказано в доступе к файлам игры (" + uaEx.Message + "). Запустите установщик или терминал от имени администратора.");
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ОШИБКА: " + ex.Message);
                return 1;
            }
        }

        public static void ConfigureTls()
        {
            try
            {
                ServicePointManager.DefaultConnectionLimit = 16;
                ServicePointManager.Expect100Continue = false;
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)768 | (SecurityProtocolType)3072;
            }
            catch
            {
            }
        }

        public static void OpenBrowserUrl(string url)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(url);
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не удалось открыть ссылку в браузере:\n" + url + "\n\n" + ex.Message,
                    "Открытие ссылки",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        public static string SafeTrimTrailingSlash(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }
            if (path.Length <= 3 && path.EndsWith(":\\", StringComparison.Ordinal))
            {
                return path;
            }
            return path.TrimEnd('\\', '/');
        }

        private static void EnsureConsoleAttached()
        {
            try
            {
                IntPtr stdOut = GetStdHandle(StdOutputHandle);
                if (stdOut == IntPtr.Zero)
                {
                    AttachConsole(AttachParentProcess);
                }
            }
            catch
            {
            }
        }

        private static void ConfigureUtf8StdStreams()
        {
            UTF8Encoding utf8NoBom = new UTF8Encoding(false);
            try { Console.OutputEncoding = utf8NoBom; } catch { }
            try
            {
                Stream rawOut = Console.OpenStandardOutput();
                if (rawOut != null && rawOut != Stream.Null)
                {
                    StreamWriter outWriter = new StreamWriter(rawOut, utf8NoBom);
                    outWriter.AutoFlush = true;
                    Console.SetOut(outWriter);
                }
            }
            catch { }
            try
            {
                Stream rawErr = Console.OpenStandardError();
                if (rawErr != null && rawErr != Stream.Null)
                {
                    StreamWriter errWriter = new StreamWriter(rawErr, utf8NoBom);
                    errWriter.AutoFlush = true;
                    Console.SetError(errWriter);
                }
            }
            catch { }
        }

        private static void PrintCliHelp()
        {
            Console.WriteLine("Frontier Tale - Русификатор v1.5 (neoru-droid)");
            Console.WriteLine();
            Console.WriteLine("Использование:");
            Console.WriteLine("  FrontierTale-RU-Setup.exe                        Графическое окно установщика");
            Console.WriteLine("  FrontierTale-RU-Setup.exe --status [параметры]   Проверка состояния и путей");
            Console.WriteLine("  FrontierTale-RU-Setup.exe --install [параметры]  Установка русификатора");
            Console.WriteLine("  FrontierTale-RU-Setup.exe --uninstall [параметры] Возврат оригинальных файлов");
            Console.WriteLine();
            Console.WriteLine("Параметры:");
            Console.WriteLine("  --game-dir <путь>   Путь к папке FrontierTale");
            Console.WriteLine("  --steam-dir <путь>  Путь к папке клиента Steam");
        }
    }

    internal enum CliMode
    {
        DefaultGui,
        UacInstallGui,
        UacUninstallGui,
        Status,
        Install,
        Uninstall,
        RenderPreview
    }

    internal sealed class CliArguments
    {
        public CliMode Mode = CliMode.DefaultGui;
        public bool ShowHelp;
        public string ExplicitGameDir;
        public string ExplicitSteamDir;
        public string PreviewInitPath;
        public string PreviewDonePath;

        public static bool TryParse(string[] args, out CliArguments parsed, out string error)
        {
            parsed = new CliArguments();
            error = null;
            if (args == null || args.Length == 0)
            {
                return true;
            }

            int modeCount = 0;
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i] ?? string.Empty;
                if (EqualsFlag(arg, "--help") || EqualsFlag(arg, "-h") || EqualsFlag(arg, "/?"))
                {
                    parsed.ShowHelp = true;
                }
                else if (EqualsFlag(arg, "--status"))
                {
                    parsed.Mode = CliMode.Status;
                    modeCount++;
                }
                else if (EqualsFlag(arg, "--install"))
                {
                    parsed.Mode = CliMode.Install;
                    modeCount++;
                }
                else if (EqualsFlag(arg, "--uninstall") || EqualsFlag(arg, "--restore"))
                {
                    parsed.Mode = CliMode.Uninstall;
                    modeCount++;
                }
                else if (EqualsFlag(arg, "--uac-install"))
                {
                    parsed.Mode = CliMode.UacInstallGui;
                    modeCount++;
                }
                else if (EqualsFlag(arg, "--uac-uninstall"))
                {
                    parsed.Mode = CliMode.UacUninstallGui;
                    modeCount++;
                }
                else if (EqualsFlag(arg, "--render-preview"))
                {
                    if (i + 2 >= args.Length)
                    {
                        error = "После --render-preview укажите пути <init.png> <done.png>.";
                        return false;
                    }
                    parsed.Mode = CliMode.RenderPreview;
                    parsed.PreviewInitPath = args[++i];
                    parsed.PreviewDonePath = args[++i];
                    modeCount++;
                }
                else if (EqualsFlag(arg, "--game-dir"))
                {
                    if (i + 1 >= args.Length)
                    {
                        error = "После флага --game-dir необходимо указать путь к папке игры.";
                        return false;
                    }
                    parsed.ExplicitGameDir = args[++i];
                }
                else if (arg.StartsWith("--game-dir=", StringComparison.OrdinalIgnoreCase))
                {
                    parsed.ExplicitGameDir = arg.Substring("--game-dir=".Length);
                }
                else if (EqualsFlag(arg, "--steam-dir"))
                {
                    if (i + 1 >= args.Length)
                    {
                        error = "После флага --steam-dir необходимо указать путь к папке Steam.";
                        return false;
                    }
                    parsed.ExplicitSteamDir = args[++i];
                }
                else if (arg.StartsWith("--steam-dir=", StringComparison.OrdinalIgnoreCase))
                {
                    parsed.ExplicitSteamDir = arg.Substring("--steam-dir=".Length);
                }
                else
                {
                    error = "Неизвестный аргумент командной строки: " + arg;
                    return false;
                }
            }

            if (!parsed.ShowHelp && modeCount > 1)
            {
                error = "Укажите только один режим работы (--status, --install, или --uninstall).";
                return false;
            }

            return true;
        }

        private static bool EqualsFlag(string actual, string expected)
        {
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class PayloadData
    {
        public string Version;
        public int AppId;
        public Dictionary<string, string> Lang;
    }

    internal static class PayloadLoader
    {
        private const string EmbeddedResourceName = "ft_ru_payload.gz";

        public static PayloadData LoadEmbeddedPayload()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            Stream resStream = asm.GetManifestResourceStream(EmbeddedResourceName);
            if (resStream == null)
            {
                string[] names = asm.GetManifestResourceNames();
                for (int i = 0; i < names.Length; i++)
                {
                    if (names[i].EndsWith("ru_payload.gz", StringComparison.OrdinalIgnoreCase))
                    {
                        resStream = asm.GetManifestResourceStream(names[i]);
                        break;
                    }
                }
            }
            if (resStream == null)
            {
                throw new InvalidDataException("Встроенный ресурс ft_ru_payload.gz не найден в исполняемом файле.");
            }

            using (resStream)
            {
                return LoadFromGzipStream(resStream);
            }
        }

        public static PayloadData LoadFromGzipStream(Stream gzStream)
        {
            string jsonText;
            using (GZipStream gz = new GZipStream(gzStream, CompressionMode.Decompress))
            using (StreamReader reader = new StreamReader(gz, Encoding.UTF8))
            {
                jsonText = reader.ReadToEnd();
            }

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            serializer.RecursionLimit = 64;

            IDictionary<string, object> root = serializer.DeserializeObject(jsonText) as IDictionary<string, object>;
            if (root == null)
            {
                throw new InvalidDataException("Некорректный JSON внутри ft_ru_payload.gz.");
            }

            PayloadData data = new PayloadData();
            data.Version = GetString(root, "version", "1.0.0");
            data.AppId = GetInt(root, "app_id", Program.TargetAppId);
            data.Lang = ParseLangMap(root);
            return data;
        }

        private static Dictionary<string, string> ParseLangMap(IDictionary<string, object> root)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.Ordinal);
            IDictionary<string, object> rawLang = GetDict(root, "lang");
            if (rawLang == null)
            {
                throw new InvalidDataException("В ресурсе русификатора отсутствует секция lang.");
            }
            foreach (KeyValuePair<string, object> kv in rawLang)
            {
                map[kv.Key] = kv.Value == null ? string.Empty : Convert.ToString(kv.Value, CultureInfo.InvariantCulture);
            }
            return map;
        }

        private static IDictionary<string, object> GetDict(IDictionary<string, object> parent, string key)
        {
            object val;
            if (parent.TryGetValue(key, out val))
            {
                return val as IDictionary<string, object>;
            }
            return null;
        }

        private static string GetString(IDictionary<string, object> dict, string key, string fallback)
        {
            object val;
            if (dict.TryGetValue(key, out val) && val != null)
            {
                return Convert.ToString(val, CultureInfo.InvariantCulture);
            }
            return fallback;
        }

        private static int GetInt(IDictionary<string, object> dict, string key, int fallback)
        {
            object val;
            if (dict.TryGetValue(key, out val) && val != null)
            {
                return Convert.ToInt32(val, CultureInfo.InvariantCulture);
            }
            return fallback;
        }
    }

    internal static class SteamLocator
    {
        public const string DefaultGameFolderName = "FrontierTale";
        public const string AltGameFolderName = "Frontier Tale";
        public const string GameExeName = "FrontierTale.exe";
        public const string EnJsonRelPath = @"FrontierTale_Data\StreamingAssets\Localization\en_US.json";

        public static bool IsValidGameDirectory(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir))
            {
                return false;
            }
            try
            {
                string exePath = Path.Combine(dir, GameExeName);
                string enJsonPath = Path.Combine(dir, EnJsonRelPath);
                return File.Exists(exePath) && File.Exists(enJsonPath);
            }
            catch
            {
                return false;
            }
        }

        public static string NormalizePickedGameDirectory(string pickedDir)
        {
            if (string.IsNullOrWhiteSpace(pickedDir))
            {
                return null;
            }
            string clean = Program.SafeTrimTrailingSlash(pickedDir.Trim().Trim('"'));
            if (IsValidGameDirectory(clean))
            {
                return SafeFullPath(clean);
            }
            try
            {
                string cur = clean;
                for (int depth = 0; depth < 4 && !string.IsNullOrEmpty(cur); depth++)
                {
                    cur = Path.GetDirectoryName(cur);
                    if (!string.IsNullOrEmpty(cur) && IsValidGameDirectory(cur))
                    {
                        return SafeFullPath(cur);
                    }
                }
            }
            catch
            {
            }
            return clean;
        }

        public static string DetectSteamDirectory(string explicitSteamDir, string hintGameDir)
        {
            if (!string.IsNullOrWhiteSpace(explicitSteamDir))
            {
                string trimmed = Program.SafeTrimTrailingSlash(explicitSteamDir.Trim().Trim('"'));
                if (SafeDirectoryExists(trimmed))
                {
                    return SafeFullPath(trimmed);
                }
                return null;
            }

            List<string> candidates = new List<string>();
            AddRegistrySteamCandidates(candidates);
            candidates.Add(@"C:\Program Files (x86)\Steam");
            candidates.Add(@"C:\Program Files\Steam");
            AddProtonSteamCandidates(candidates);

            if (!string.IsNullOrWhiteSpace(hintGameDir))
            {
                TryAddSteamRootFromGameDir(candidates, hintGameDir);
            }

            List<string> driveRoots = GetSafeLocalDriveRoots();
            for (int i = 0; i < driveRoots.Count; i++)
            {
                candidates.Add(Path.Combine(driveRoots[i], "Steam"));
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                string cand = candidates[i];
                if (SafeDirectoryExists(cand))
                {
                    string statsDir = Path.Combine(cand, @"appcache\stats");
                    string steamExe = Path.Combine(cand, "steam.exe");
                    string steamSh = Path.Combine(cand, "steam.sh");
                    string vdf = Path.Combine(cand, @"steamapps\libraryfolders.vdf");
                    if (SafeDirectoryExists(statsDir) || SafeFileExists(steamExe) || SafeFileExists(steamSh) || SafeFileExists(vdf))
                    {
                        return SafeFullPath(cand);
                    }
                }
            }
            return null;
        }

        public static string AutoDetectGameDirectory(string steamDir, string appBaseDir)
        {
            List<string> candidates = new List<string>();

            if (!string.IsNullOrWhiteSpace(steamDir))
            {
                AddLibraryCandidates(candidates, steamDir);
                AddVdfLibraryCandidates(candidates, Path.Combine(steamDir, @"steamapps\libraryfolders.vdf"));
            }

            List<string> regRoots = new List<string>();
            AddRegistrySteamCandidates(regRoots);
            for (int i = 0; i < regRoots.Count; i++)
            {
                AddLibraryCandidates(candidates, regRoots[i]);
                AddVdfLibraryCandidates(candidates, Path.Combine(regRoots[i], @"steamapps\libraryfolders.vdf"));
            }

            AddLibraryCandidates(candidates, @"C:\Program Files (x86)\Steam");
            AddVdfLibraryCandidates(candidates, @"C:\Program Files (x86)\Steam\steamapps\libraryfolders.vdf");
            AddProtonLibraryCandidates(candidates);

            List<string> driveRoots = GetSafeLocalDriveRoots();
            for (int i = 0; i < driveRoots.Count; i++)
            {
                string root = driveRoots[i];
                string steamLib = Path.Combine(root, "SteamLibrary");
                string steamRoot = Path.Combine(root, "Steam");
                string gamesSteam = Path.Combine(root, @"Games\Steam");

                AddLibraryCandidates(candidates, steamLib);
                AddVdfLibraryCandidates(candidates, Path.Combine(steamLib, @"steamapps\libraryfolders.vdf"));
                AddLibraryCandidates(candidates, steamRoot);
                AddVdfLibraryCandidates(candidates, Path.Combine(steamRoot, @"steamapps\libraryfolders.vdf"));
                AddLibraryCandidates(candidates, gamesSteam);
                candidates.Add(Path.Combine(root, @"Games\" + DefaultGameFolderName));
                candidates.Add(Path.Combine(root, @"Games\" + AltGameFolderName));
            }

            if (!string.IsNullOrWhiteSpace(appBaseDir))
            {
                candidates.Add(appBaseDir);
                try
                {
                    string parent = Path.GetDirectoryName(appBaseDir);
                    if (!string.IsNullOrEmpty(parent))
                    {
                        candidates.Add(parent);
                    }
                }
                catch
                {
                }
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < candidates.Count; i++)
            {
                string cand = candidates[i];
                if (string.IsNullOrWhiteSpace(cand) || !seen.Add(cand))
                {
                    continue;
                }
                if (IsValidGameDirectory(cand))
                {
                    return SafeFullPath(cand);
                }
            }
            return null;
        }

        public static string ResolveRequiredGameDirectory(string explicitGameDir, string steamDir, string appBaseDir)
        {
            if (!string.IsNullOrWhiteSpace(explicitGameDir))
            {
                string norm = NormalizePickedGameDirectory(explicitGameDir);
                if (!IsValidGameDirectory(norm))
                {
                    throw new DirectoryNotFoundException(
                        "В указанной папке не найдены файлы Frontier Tale (FrontierTale.exe и Localization\\en_US.json): " + norm);
                }
                return norm;
            }

            string detected = AutoDetectGameDirectory(steamDir, appBaseDir);
            if (string.IsNullOrEmpty(detected))
            {
                throw new DirectoryNotFoundException(
                    "Не удалось автоматически найти папку игры Frontier Tale. Укажите путь вручную через --game-dir или кнопку «Обзор...».");
            }
            return detected;
        }

        public static void AssertGameClosed(string resolvedGameDir)
        {
            string targetExe = Path.Combine(resolvedGameDir, GameExeName);
            Process[] procs;
            try
            {
                procs = Process.GetProcessesByName("FrontierTale");
            }
            catch
            {
                return;
            }

            for (int i = 0; i < procs.Length; i++)
            {
                Process p = procs[i];
                try
                {
                    string moduleFile = null;
                    try
                    {
                        if (p.MainModule != null)
                        {
                            moduleFile = p.MainModule.FileName;
                        }
                    }
                    catch
                    {
                        moduleFile = null;
                    }

                    if (moduleFile == null || string.Equals(moduleFile, targetExe, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            "Игра Frontier Tale сейчас запущена (PID " + p.Id + "). Закройте игру перед установкой или возвратом оригинала.");
                    }
                }
                finally
                {
                    p.Dispose();
                }
            }
        }

        private static void AddRegistrySteamCandidates(List<string> list)
        {
            string[][] probes = new string[][]
            {
                new string[] { @"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath" },
                new string[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath" },
                new string[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath" }
            };

            for (int i = 0; i < probes.Length; i++)
            {
                try
                {
                    string raw = Registry.GetValue(probes[i][0], probes[i][1], null) as string;
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        list.Add(Program.SafeTrimTrailingSlash(raw.Replace('/', '\\')));
                    }
                }
                catch
                {
                }
            }
        }

        private static void AddLibraryCandidates(List<string> candidates, string libraryRoot)
        {
            if (string.IsNullOrWhiteSpace(libraryRoot))
            {
                return;
            }
            try
            {
                string steamapps = Path.Combine(libraryRoot, "steamapps");
                string manifestPath = Path.Combine(steamapps, "appmanifest_4829780.acf");
                if (SafeFileExists(manifestPath))
                {
                    string acf = File.ReadAllText(manifestPath, Encoding.UTF8);
                    Match m = Regex.Match(acf, "\"installdir\"\\s+\"([^\"]+)\"", RegexOptions.CultureInvariant);
                    if (m.Success && !string.IsNullOrWhiteSpace(m.Groups[1].Value))
                    {
                        candidates.Add(Path.Combine(steamapps, Path.Combine("common", m.Groups[1].Value)));
                    }
                }
                candidates.Add(Path.Combine(steamapps, @"common\" + DefaultGameFolderName));
                candidates.Add(Path.Combine(steamapps, @"common\" + AltGameFolderName));
            }
            catch
            {
            }
        }

        private static void AddProtonSteamCandidates(List<string> list)
        {
            list.Add(@"Z:\home\deck\.local\share\Steam");
            list.Add(@"Z:\home\deck\.steam\steam");
            list.Add(@"Z:\home\deck\.steam\root");
            list.Add(@"Z:\home\deck\.var\app\com.valvesoftware.Steam\.local\share\Steam");

            try
            {
                if (SafeDirectoryExists(@"Z:\home"))
                {
                    string[] users = Directory.GetDirectories(@"Z:\home");
                    for (int i = 0; i < users.Length; i++)
                    {
                        list.Add(Path.Combine(users[i], @".local\share\Steam"));
                        list.Add(Path.Combine(users[i], @".steam\steam"));
                        list.Add(Path.Combine(users[i], @".var\app\com.valvesoftware.Steam\.local\share\Steam"));
                    }
                }
            }
            catch
            {
            }
        }

        private static void AddProtonLibraryCandidates(List<string> candidates)
        {
            List<string> protonRoots = new List<string>();
            AddProtonSteamCandidates(protonRoots);
            for (int i = 0; i < protonRoots.Count; i++)
            {
                AddLibraryCandidates(candidates, protonRoots[i]);
                AddVdfLibraryCandidates(candidates, Path.Combine(protonRoots[i], @"steamapps\libraryfolders.vdf"));
            }

            string[] sdRoots = new string[]
            {
                @"Z:\run\media\mmcblk0p1",
                @"Z:\run\media\mmcblk0p1\SteamLibrary"
            };
            for (int i = 0; i < sdRoots.Length; i++)
            {
                AddLibraryCandidates(candidates, sdRoots[i]);
            }

            try
            {
                if (SafeDirectoryExists(@"Z:\run\media"))
                {
                    string[] mediaEntries = Directory.GetDirectories(@"Z:\run\media");
                    for (int i = 0; i < mediaEntries.Length; i++)
                    {
                        AddLibraryCandidates(candidates, mediaEntries[i]);
                        AddLibraryCandidates(candidates, Path.Combine(mediaEntries[i], "SteamLibrary"));
                        try
                        {
                            string[] subMounts = Directory.GetDirectories(mediaEntries[i]);
                            for (int j = 0; j < subMounts.Length; j++)
                            {
                                AddLibraryCandidates(candidates, subMounts[j]);
                                AddLibraryCandidates(candidates, Path.Combine(subMounts[j], "SteamLibrary"));
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static void AddVdfLibraryCandidates(List<string> candidates, string vdfPath)
        {
            if (!SafeFileExists(vdfPath))
            {
                return;
            }
            try
            {
                string text = File.ReadAllText(vdfPath, Encoding.UTF8);
                MatchCollection matches = Regex.Matches(text, "\"path\"\\s+\"([^\"]+)\"", RegexOptions.CultureInvariant);
                foreach (Match match in matches)
                {
                    string rawVal = match.Groups[1].Value;
                    string libRoot = Program.SafeTrimTrailingSlash(rawVal.Replace("\\\\", "\\"));
                    AddLibraryCandidates(candidates, libRoot);
                    if (rawVal.StartsWith("/", StringComparison.Ordinal))
                    {
                        string protonLib = @"Z:" + Program.SafeTrimTrailingSlash(rawVal.Replace('/', '\\'));
                        AddLibraryCandidates(candidates, protonLib);
                    }
                }
            }
            catch
            {
            }
        }

        private static void TryAddSteamRootFromGameDir(List<string> candidates, string gameDir)
        {
            try
            {
                string clean = Program.SafeTrimTrailingSlash(gameDir.Trim().Trim('"'));
                DirectoryInfo commonDir = Directory.GetParent(clean);
                if (commonDir != null && string.Equals(commonDir.Name, "common", StringComparison.OrdinalIgnoreCase))
                {
                    DirectoryInfo steamappsDir = commonDir.Parent;
                    if (steamappsDir != null && string.Equals(steamappsDir.Name, "steamapps", StringComparison.OrdinalIgnoreCase))
                    {
                        DirectoryInfo rootDir = steamappsDir.Parent;
                        if (rootDir != null)
                        {
                            candidates.Add(rootDir.FullName);
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static List<string> GetSafeLocalDriveRoots()
        {
            List<string> roots = new List<string>();
            HashSet<string> added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                for (int i = 0; i < drives.Length; i++)
                {
                    DriveInfo d = drives[i];
                    if ((d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable) && d.IsReady)
                    {
                        if (added.Add(d.Name))
                        {
                            roots.Add(d.Name);
                        }
                    }
                }
            }
            catch
            {
            }

            for (char c = 'C'; c <= 'Z'; c++)
            {
                string root = c + @":\";
                if (!added.Contains(root) && SafeDirectoryExists(root))
                {
                    added.Add(root);
                    roots.Add(root);
                }
            }

            return roots;
        }

        public static bool SafeFileExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }
            try
            {
                return File.Exists(path);
            }
            catch
            {
                return false;
            }
        }

        public static bool SafeDirectoryExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }
            try
            {
                return Directory.Exists(path);
            }
            catch
            {
                return false;
            }
        }

        private static string SafeFullPath(string path)
        {
            try
            {
                string full = Program.SafeTrimTrailingSlash(Path.GetFullPath(path));
                return NormalizeExactCasePath(full);
            }
            catch
            {
                return Program.SafeTrimTrailingSlash(path);
            }
        }

        private static string NormalizeExactCasePath(string fullPath)
        {
            try
            {
                if (!Directory.Exists(fullPath) && !File.Exists(fullPath))
                {
                    return fullPath;
                }
                DirectoryInfo di = new DirectoryInfo(fullPath);
                List<string> parts = new List<string>();
                DirectoryInfo cur = di;
                while (cur.Parent != null)
                {
                    FileSystemInfo[] entries = cur.Parent.GetFileSystemInfos(cur.Name);
                    parts.Add(entries.Length > 0 ? entries[0].Name : cur.Name);
                    cur = cur.Parent;
                }
                parts.Reverse();
                string root = cur.FullName.ToUpperInvariant();
                for (int i = 0; i < parts.Count; i++)
                {
                    root = Path.Combine(root, parts[i]);
                }
                return Program.SafeTrimTrailingSlash(root);
            }
            catch
            {
                return fullPath;
            }
        }
    }

    internal struct JsonPatchStats
    {
        public int TotalKeys;
        public int TranslatedKeys;
        public int PreservedEnglishKeys;
    }

    internal static class JsonLocalizationPatcher
    {
        private static readonly Regex PlaceholderRegex = new Regex(@"\{[^{}]+\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex RichTagRegex = new Regex(@"<[^>]+>", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        // Оригинальный en_US.json содержит одиночные кириллические символы в эмотиконах
        // (например, "[○･｀Д´･ ○]"), поэтому признаком русификации считаем слова из 3+ букв
        // минимум в 1% непустых строк.
        private static readonly Regex CyrillicWordRegex = new Regex(@"[А-Яа-яЁё]{3,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        public static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        public static bool ContainsCyrillicValues(byte[] jsonBytes)
        {
            try
            {
                string text = Utf8NoBom.GetString(jsonBytes);
                JavaScriptSerializer serializer = CreateSerializer();
                IDictionary<string, object> dict = serializer.DeserializeObject(text) as IDictionary<string, object>;
                if (dict == null)
                {
                    return false;
                }
                int values = 0;
                int hits = 0;
                foreach (KeyValuePair<string, object> kv in dict)
                {
                    string val = kv.Value as string;
                    if (string.IsNullOrEmpty(val))
                    {
                        continue;
                    }
                    values++;
                    if (CyrillicWordRegex.IsMatch(val))
                    {
                        hits++;
                    }
                }
                return hits >= Math.Max(1, values / 100);
            }
            catch
            {
                return false;
            }
        }

        public static byte[] MergeRussianIntoEnJson(
            byte[] cleanEnJsonBytes,
            Dictionary<string, string> payloadLang,
            out JsonPatchStats stats)
        {
            stats = new JsonPatchStats();
            string cleanText = StripUtf8Bom(Utf8NoBom.GetString(cleanEnJsonBytes));
            JavaScriptSerializer serializer = CreateSerializer();
            IDictionary<string, object> baseMap = serializer.DeserializeObject(cleanText) as IDictionary<string, object>;
            if (baseMap == null)
            {
                throw new InvalidDataException("Некорректный формат JSON в Localization\\en_US.json.");
            }

            List<string> orderedKeys = new List<string>(baseMap.Keys);
            StringBuilder sb = new StringBuilder(cleanText.Length + 131072);
            sb.Append("{\n");

            for (int i = 0; i < orderedKeys.Count; i++)
            {
                string key = orderedKeys[i];
                object rawVal = baseMap[key];
                string enVal = rawVal == null ? string.Empty : Convert.ToString(rawVal, CultureInfo.InvariantCulture);
                stats.TotalKeys++;

                string chosenVal;
                string ruCand;
                if (payloadLang != null
                    && payloadLang.TryGetValue(key, out ruCand)
                    && IsValidReplacement(enVal, ruCand))
                {
                    chosenVal = ruCand;
                    stats.TranslatedKeys++;
                }
                else
                {
                    chosenVal = enVal;
                    stats.PreservedEnglishKeys++;
                }

                sb.Append("  ");
                AppendJsonEscapedString(sb, key);
                sb.Append(": ");
                AppendJsonEscapedString(sb, chosenVal);
                if (i < orderedKeys.Count - 1)
                {
                    sb.Append(',');
                }
                sb.Append('\n');
            }

            sb.Append("}\n");
            return Utf8NoBom.GetBytes(sb.ToString());
        }

        public static bool IsValidReplacement(string enVal, string ruVal)
        {
            if (ruVal == null)
            {
                return false;
            }
            if (enVal.Length > 0 && ruVal.Length == 0)
            {
                return false;
            }
            return RegexMultisetEqual(PlaceholderRegex, enVal, ruVal)
                && RegexMultisetEqual(RichTagRegex, enVal, ruVal);
        }

        private static bool RegexMultisetEqual(Regex pattern, string a, string b)
        {
            MatchCollection ma = pattern.Matches(a ?? string.Empty);
            MatchCollection mb = pattern.Matches(b ?? string.Empty);
            if (ma.Count != mb.Count)
            {
                return false;
            }
            if (ma.Count == 0)
            {
                return true;
            }
            string[] ta = new string[ma.Count];
            string[] tb = new string[mb.Count];
            for (int i = 0; i < ma.Count; i++)
            {
                ta[i] = ma[i].Value;
                tb[i] = mb[i].Value;
            }
            Array.Sort(ta, StringComparer.Ordinal);
            Array.Sort(tb, StringComparer.Ordinal);
            for (int i = 0; i < ta.Length; i++)
            {
                if (!string.Equals(ta[i], tb[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        private static void AppendJsonEscapedString(StringBuilder sb, string value)
        {
            sb.Append('"');
            if (!string.IsNullOrEmpty(value))
            {
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\b': sb.Append("\\b"); break;
                        case '\f': sb.Append("\\f"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default:
                            if (c < 0x20)
                            {
                                sb.AppendFormat(CultureInfo.InvariantCulture, "\\u{0:x4}", (int)c);
                            }
                            else
                            {
                                sb.Append(c);
                            }
                            break;
                    }
                }
            }
            sb.Append('"');
        }

        private static string StripUtf8Bom(string s)
        {
            if (!string.IsNullOrEmpty(s) && s[0] == '\uFEFF')
            {
                return s.Substring(1);
            }
            return s;
        }

        private static JavaScriptSerializer CreateSerializer()
        {
            JavaScriptSerializer s = new JavaScriptSerializer();
            s.MaxJsonLength = int.MaxValue;
            s.RecursionLimit = 64;
            return s;
        }
    }

    internal sealed class BackupFileRecord
    {
        public string TargetKind;
        public string RelPath;
        public string BackupRelPath;
        public string OrigSha256;
        public string PatchedSha256;
        public bool ExistedOriginally;
    }

    internal sealed class BackupManifestData
    {
        public int ManifestVersion = 1;
        public string InstallerVersion = "1.0.0";
        public int AppId = Program.TargetAppId;
        public string CreatedUtc;
        public string UpdatedUtc;
        public string SteamSchemaSourcePath;
        public List<BackupFileRecord> Files = new List<BackupFileRecord>();
    }

    internal static class BackupManifestStore
    {
        public const string BackupFolderName = "ru_backup_orig";
        public const string ManifestFileName = "backup_manifest.json";

        public static string GetBackupDirectory(string gameDir)
        {
            return Path.Combine(gameDir, BackupFolderName);
        }

        public static string GetManifestPath(string gameDir)
        {
            return Path.Combine(GetBackupDirectory(gameDir), ManifestFileName);
        }

        public static bool HasValidBackup(string gameDir)
        {
            if (!SteamLocator.IsValidGameDirectory(gameDir))
            {
                return false;
            }
            string manifestPath = GetManifestPath(gameDir);
            if (!SteamLocator.SafeFileExists(manifestPath))
            {
                return false;
            }
            try
            {
                BackupManifestData m = Load(manifestPath);
                return m != null && m.Files != null && m.Files.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        public static BackupManifestData Load(string manifestPath)
        {
            string json = File.ReadAllText(manifestPath, Encoding.UTF8);
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            IDictionary<string, object> root = serializer.DeserializeObject(json) as IDictionary<string, object>;
            if (root == null)
            {
                throw new InvalidDataException("Повреждён файл backup_manifest.json.");
            }

            BackupManifestData m = new BackupManifestData();
            m.ManifestVersion = ReadInt(root, "manifest_version", 1);
            m.InstallerVersion = ReadString(root, "installer_version", "1.0.0");
            m.AppId = ReadInt(root, "app_id", Program.TargetAppId);
            m.CreatedUtc = ReadString(root, "created_utc", string.Empty);
            m.UpdatedUtc = ReadString(root, "updated_utc", string.Empty);
            m.SteamSchemaSourcePath = ReadString(root, "steam_schema_source_path", null);

            object filesObj;
            if (root.TryGetValue("files", out filesObj) && filesObj is IEnumerable)
            {
                foreach (object item in (IEnumerable)filesObj)
                {
                    IDictionary<string, object> d = item as IDictionary<string, object>;
                    if (d == null)
                    {
                        continue;
                    }
                    BackupFileRecord rec = new BackupFileRecord();
                    rec.TargetKind = ReadString(d, "target_kind", "game");
                    rec.RelPath = ReadString(d, "rel_path", string.Empty).Replace('/', '\\');
                    rec.BackupRelPath = ReadString(d, "backup_rel_path", string.Empty).Replace('/', '\\');
                    rec.OrigSha256 = ReadString(d, "orig_sha256", string.Empty);
                    rec.PatchedSha256 = ReadString(d, "patched_sha256", string.Empty);
                    rec.ExistedOriginally = ReadBool(d, "existed_originally", true);
                    m.Files.Add(rec);
                }
            }
            return m;
        }

        public static void Save(string manifestPath, BackupManifestData m)
        {
            Dictionary<string, object> root = new Dictionary<string, object>();
            root["manifest_version"] = m.ManifestVersion;
            root["installer_version"] = m.InstallerVersion;
            root["app_id"] = m.AppId;
            root["created_utc"] = m.CreatedUtc ?? DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            root["updated_utc"] = m.UpdatedUtc ?? DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            root["steam_schema_source_path"] = m.SteamSchemaSourcePath;

            List<Dictionary<string, object>> filesList = new List<Dictionary<string, object>>(m.Files.Count);
            for (int i = 0; i < m.Files.Count; i++)
            {
                BackupFileRecord r = m.Files[i];
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["target_kind"] = r.TargetKind;
                d["rel_path"] = r.RelPath;
                d["backup_rel_path"] = r.BackupRelPath;
                d["orig_sha256"] = r.OrigSha256;
                d["patched_sha256"] = r.PatchedSha256;
                d["existed_originally"] = r.ExistedOriginally;
                filesList.Add(d);
            }
            root["files"] = filesList;

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            string rawJson = serializer.Serialize(root);
            string formatted = FormatCompactJson(rawJson);

            string dir = Path.GetDirectoryName(manifestPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllBytes(manifestPath, JsonLocalizationPatcher.Utf8NoBom.GetBytes(formatted));
        }

        public static string ComputeSha256Hex(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                }
                return sb.ToString();
            }
        }

        private static string FormatCompactJson(string json)
        {
            StringBuilder sb = new StringBuilder(json.Length + 256);
            int indent = 0;
            bool inQuotes = false;
            bool escaped = false;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (escaped)
                {
                    sb.Append(c);
                    escaped = false;
                    continue;
                }
                if (c == '\\' && inQuotes)
                {
                    sb.Append(c);
                    escaped = true;
                    continue;
                }
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    sb.Append(c);
                    continue;
                }
                if (inQuotes)
                {
                    sb.Append(c);
                    continue;
                }

                if (c == '{' || c == '[')
                {
                    sb.Append(c);
                    sb.Append("\r\n");
                    indent++;
                    AppendIndent(sb, indent);
                }
                else if (c == '}' || c == ']')
                {
                    sb.Append("\r\n");
                    indent = Math.Max(0, indent - 1);
                    AppendIndent(sb, indent);
                    sb.Append(c);
                }
                else if (c == ',')
                {
                    sb.Append(",\r\n");
                    AppendIndent(sb, indent);
                }
                else if (c == ':')
                {
                    sb.Append(": ");
                }
                else
                {
                    sb.Append(c);
                }
            }
            sb.Append("\r\n");
            return sb.ToString();
        }

        private static void AppendIndent(StringBuilder sb, int level)
        {
            for (int i = 0; i < level; i++)
            {
                sb.Append("  ");
            }
        }

        private static string ReadString(IDictionary<string, object> d, string key, string fallback)
        {
            object v;
            if (d.TryGetValue(key, out v) && v != null)
            {
                return Convert.ToString(v, CultureInfo.InvariantCulture);
            }
            return fallback;
        }

        private static int ReadInt(IDictionary<string, object> d, string key, int fallback)
        {
            object v;
            if (d.TryGetValue(key, out v) && v != null)
            {
                return Convert.ToInt32(v, CultureInfo.InvariantCulture);
            }
            return fallback;
        }

        private static bool ReadBool(IDictionary<string, object> d, string key, bool fallback)
        {
            object v;
            if (d.TryGetValue(key, out v) && v != null)
            {
                return Convert.ToBoolean(v, CultureInfo.InvariantCulture);
            }
            return fallback;
        }
    }

    internal static class ElevationHelper
    {
        public static bool IsProcessElevated()
        {
            try
            {
                using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                {
                    if (id == null)
                    {
                        return false;
                    }
                    WindowsPrincipal principal = new WindowsPrincipal(id);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool NeedsElevationForGameDir(string gameDir)
        {
            if (string.IsNullOrWhiteSpace(gameDir) || IsProcessElevated())
            {
                return false;
            }
            if (!CanProbeDirectoryWrite(gameDir))
            {
                return true;
            }
            string enJsonPath = Path.Combine(gameDir, SteamLocator.EnJsonRelPath);
            string locDir = Path.GetDirectoryName(enJsonPath);
            if (!string.IsNullOrEmpty(locDir) && SteamLocator.SafeDirectoryExists(locDir) && !CanProbeDirectoryWrite(locDir))
            {
                return true;
            }
            if (SteamLocator.SafeFileExists(enJsonPath) && !CanOpenExistingFileForWrite(enJsonPath))
            {
                return true;
            }
            return false;
        }

        public static string BuildUacArguments(string modeFlag, string gameDir, string steamDir)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(modeFlag);
            if (!string.IsNullOrWhiteSpace(gameDir))
            {
                sb.Append(" --game-dir ");
                sb.Append(QuoteCliValue(gameDir));
            }
            if (!string.IsNullOrWhiteSpace(steamDir))
            {
                sb.Append(" --steam-dir ");
                sb.Append(QuoteCliValue(steamDir));
            }
            return sb.ToString();
        }

        public static bool TryRelaunchElevated(
            string modeFlag,
            string gameDir,
            string steamDir,
            out bool cancelledByUser,
            out string errorMessage)
        {
            cancelledByUser = false;
            errorMessage = null;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = Application.ExecutablePath;
                psi.Arguments = BuildUacArguments(modeFlag, gameDir, steamDir);
                psi.Verb = "runas";
                psi.UseShellExecute = true;
                Process proc = Process.Start(psi);
                return proc != null;
            }
            catch (Win32Exception wex)
            {
                if (wex.NativeErrorCode == 1223)
                {
                    cancelledByUser = true;
                    return false;
                }
                errorMessage = wex.Message;
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private static string QuoteCliValue(string raw)
        {
            string clean = Program.SafeTrimTrailingSlash((raw ?? string.Empty).Trim().Trim('"'));
            if (clean.EndsWith(@"\", StringComparison.Ordinal))
            {
                clean += @"\";
            }
            return "\"" + clean.Replace("\"", "\\\"") + "\"";
        }

        private static bool CanProbeDirectoryWrite(string dirPath)
        {
            string probePath = Path.Combine(dirPath, ".ft_uac_probe_" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (FileStream fs = new FileStream(probePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                }
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch
            {
                return true;
            }
            finally
            {
                try
                {
                    if (File.Exists(probePath))
                    {
                        File.Delete(probePath);
                    }
                }
                catch
                {
                }
            }
        }

        private static bool CanOpenExistingFileForWrite(string filePath)
        {
            try
            {
                FileAttributes attrs = File.GetAttributes(filePath);
                if ((attrs & FileAttributes.ReadOnly) != 0)
                {
                    return true;
                }
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                }
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch
            {
                return true;
            }
        }
    }

    internal static class InstallerService
    {
        public static int RunStatusCli(PayloadData payload, string appBaseDir, string explicitGameDir, string explicitSteamDir)
        {
            string steamDir = SteamLocator.DetectSteamDirectory(explicitSteamDir, explicitGameDir);
            string gameDir = !string.IsNullOrWhiteSpace(explicitGameDir)
                ? SteamLocator.NormalizePickedGameDirectory(explicitGameDir)
                : SteamLocator.AutoDetectGameDirectory(steamDir, appBaseDir);

            bool validGame = SteamLocator.IsValidGameDirectory(gameDir);
            if (validGame && string.IsNullOrEmpty(steamDir))
            {
                steamDir = SteamLocator.DetectSteamDirectory(null, gameDir);
            }

            bool installed = validGame && IsRussifierInstalled(gameDir);
            bool hasManifest = validGame && BackupManifestStore.HasValidBackup(gameDir);


            Console.WriteLine("GameDir: " + (validGame ? gameDir : "NOT_FOUND"));
            Console.WriteLine("SteamDir: " + (!string.IsNullOrEmpty(steamDir) ? steamDir : "NOT_FOUND"));
            Console.WriteLine("Installed: " + (installed ? "true" : "false"));
            Console.WriteLine("BackupManifest: " + (hasManifest ? "present" : "absent"));

            return validGame ? 0 : 2;
        }

        public static bool IsRussifierInstalled(string gameDir)
        {
            if (!BackupManifestStore.HasValidBackup(gameDir))
            {
                return false;
            }
            try
            {
                BackupManifestData manifest = BackupManifestStore.Load(BackupManifestStore.GetManifestPath(gameDir));
                for (int i = 0; i < manifest.Files.Count; i++)
                {
                    BackupFileRecord rec = manifest.Files[i];
                    if (string.Equals(rec.RelPath, SteamLocator.EnJsonRelPath, StringComparison.OrdinalIgnoreCase))
                    {
                        string liveEn = Path.Combine(gameDir, SteamLocator.EnJsonRelPath);
                        if (!SteamLocator.SafeFileExists(liveEn))
                        {
                            return false;
                        }
                        string liveHash = BackupManifestStore.ComputeSha256Hex(File.ReadAllBytes(liveEn));
                        return string.Equals(liveHash, rec.PatchedSha256, StringComparison.OrdinalIgnoreCase);
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static void Install(PayloadData payload, string gameDir, string steamDir, Action<string> log)
        {
            if (log == null)
            {
                log = delegate { };
            }
            if (!SteamLocator.IsValidGameDirectory(gameDir))
            {
                throw new DirectoryNotFoundException("Папка игры не содержит FrontierTale.exe и Localization\\en_US.json: " + gameDir);
            }

            SteamLocator.AssertGameClosed(gameDir);
            log("Проверка папки игры: " + gameDir);

            string backupRoot = BackupManifestStore.GetBackupDirectory(gameDir);
            string manifestPath = BackupManifestStore.GetManifestPath(gameDir);
            string stagingRoot = Path.Combine(backupRoot, "_staging");

            BackupManifestData existingManifest = null;
            if (SteamLocator.SafeFileExists(manifestPath))
            {
                try
                {
                    existingManifest = BackupManifestStore.Load(manifestPath);
                }
                catch
                {
                    existingManifest = null;
                }
            }

            BackupManifestData nextManifest = new BackupManifestData();
            nextManifest.InstallerVersion = payload.Version ?? "1.0.0";
            nextManifest.AppId = payload.AppId;
            nextManifest.CreatedUtc = (existingManifest != null && !string.IsNullOrEmpty(existingManifest.CreatedUtc))
                ? existingManifest.CreatedUtc
                : DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            nextManifest.UpdatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

            EnsureEmptyDirectory(stagingRoot);

            try
            {
                // 1. Back up clean English en_US.json & apply key-based Russian merge
                BackupFileRecord enRec = EnsureEnJsonBackedUp(gameDir, backupRoot, existingManifest, log);
                byte[] cleanEnBytes = File.ReadAllBytes(Path.Combine(backupRoot, enRec.BackupRelPath));

                JsonPatchStats jsonStats;
                byte[] patchedEnJson = JsonLocalizationPatcher.MergeRussianIntoEnJson(cleanEnBytes, payload.Lang, out jsonStats);
                WriteViaStaging(stagingRoot, SteamLocator.EnJsonRelPath, Path.Combine(gameDir, SteamLocator.EnJsonRelPath), patchedEnJson);
                enRec.PatchedSha256 = BackupManifestStore.ComputeSha256Hex(patchedEnJson);
                nextManifest.Files.Add(enRec);

                if (jsonStats.PreservedEnglishKeys > 0)
                {
                    log(string.Format(
                        CultureInfo.InvariantCulture,
                        "Обновлён Localization\\en_US.json: переведено {0} из {1} ключей (сохранено новых строк обновления: {2}).",
                        jsonStats.TranslatedKeys,
                        jsonStats.TotalKeys,
                        jsonStats.PreservedEnglishKeys));
                }
                else
                {
                    log(string.Format(
                        CultureInfo.InvariantCulture,
                        "Обновлён Localization\\en_US.json: все ключи ({0}) переведены и проверены.",
                        jsonStats.TranslatedKeys));
                }

                // Preserve legacy steam_schema backup record when updating from v1.0-v1.2.
                BackupFileRecord legacy = FindRecord(existingManifest, "steam_schema", "UserGameStatsSchema_4829780.bin");
                if (legacy != null)
                {
                    nextManifest.SteamSchemaSourcePath = existingManifest.SteamSchemaSourcePath;
                    nextManifest.Files.Add(legacy);
                }
                BackupManifestStore.Save(manifestPath, nextManifest);
            }
            finally
            {
                TryDeleteDirectory(stagingRoot);
            }

            AssertNoLeftoverFilesInLocalizationDir(gameDir);
            log("Готово: Русификатор установлен.");
        }

        public static void Uninstall(string gameDir, string steamDir, Action<string> log)
        {
            if (log == null)
            {
                log = delegate { };
            }
            if (!SteamLocator.IsValidGameDirectory(gameDir))
            {
                throw new DirectoryNotFoundException("Папка игры не содержит FrontierTale.exe и Localization\\en_US.json: " + gameDir);
            }

            SteamLocator.AssertGameClosed(gameDir);

            string backupRoot = BackupManifestStore.GetBackupDirectory(gameDir);
            string manifestPath = BackupManifestStore.GetManifestPath(gameDir);
            if (!SteamLocator.SafeFileExists(manifestPath))
            {
                throw new InvalidOperationException("Резервная копия ru_backup_orig не найдена: оригинальные файлы уже используются.");
            }

            BackupManifestData manifest = BackupManifestStore.Load(manifestPath);
            string stagingRoot = Path.Combine(backupRoot, "_staging");
            EnsureEmptyDirectory(stagingRoot);

            try
            {
                for (int i = 0; i < manifest.Files.Count; i++)
                {
                    BackupFileRecord rec = manifest.Files[i];
                    if (string.Equals(rec.TargetKind, "game", StringComparison.OrdinalIgnoreCase))
                    {
                        string targetPath = Path.Combine(gameDir, rec.RelPath);
                        if (!rec.ExistedOriginally)
                        {
                            TryDeleteFile(targetPath);
                            continue;
                        }

                        string backupFilePath = Path.Combine(backupRoot, rec.BackupRelPath);
                        if (!SteamLocator.SafeFileExists(backupFilePath))
                        {
                            throw new FileNotFoundException("Отсутствует файл резервной копии: " + backupFilePath);
                        }
                        byte[] origBytes = File.ReadAllBytes(backupFilePath);
                        VerifyExpectedHash(rec.RelPath, origBytes, rec.OrigSha256);
                        WriteViaStaging(stagingRoot, rec.RelPath, targetPath, origBytes);
                        VerifyExpectedHash(rec.RelPath, File.ReadAllBytes(targetPath), rec.OrigSha256);
                    }
                    else if (string.Equals(rec.TargetKind, "steam_schema", StringComparison.OrdinalIgnoreCase))
                    {
                        string statsDir = string.IsNullOrEmpty(steamDir) ? null : Path.Combine(steamDir, @"appcache\stats");
                        string schemaTarget = (!string.IsNullOrEmpty(statsDir) && SteamLocator.SafeDirectoryExists(statsDir))
                            ? Path.Combine(statsDir, "UserGameStatsSchema_4829780.bin")
                            : manifest.SteamSchemaSourcePath;

                        if (!string.IsNullOrEmpty(schemaTarget))
                        {
                            string schemaParent = Path.GetDirectoryName(schemaTarget);
                            string backupSchemaPath = Path.Combine(backupRoot, rec.BackupRelPath);
                            if (!string.IsNullOrEmpty(schemaParent)
                                && SteamLocator.SafeDirectoryExists(schemaParent)
                                && SteamLocator.SafeFileExists(backupSchemaPath))
                            {
                                byte[] origSchema = File.ReadAllBytes(backupSchemaPath);
                                VerifyExpectedHash("UserGameStatsSchema_4829780.bin", origSchema, rec.OrigSha256);
                                try
                                {
                                    WriteAtomicTarget(stagingRoot, rec.BackupRelPath, schemaTarget, origSchema);
                                    VerifyExpectedHash("UserGameStatsSchema_4829780.bin", File.ReadAllBytes(schemaTarget), rec.OrigSha256);
                                    log("Восстановлена оригинальная схема достижений Steam.");
                                }
                                catch (Exception schemaRestoreEx)
                                {
                                    if (!(schemaRestoreEx is UnauthorizedAccessException || schemaRestoreEx is IOException))
                                    {
                                        throw;
                                    }
                                    log("Примечание: нет прав записи в папку Steam (" + schemaTarget + "). Оригинальные файлы самой игры полностью восстановлены.");
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                TryDeleteDirectory(stagingRoot);
            }

            TryDeleteDirectory(backupRoot);
            if (SteamLocator.SafeDirectoryExists(backupRoot))
            {
                throw new IOException("Не удалось полностью удалить временную папку резервной копии: " + backupRoot);
            }

            AssertNoLeftoverFilesInLocalizationDir(gameDir);
            log("Готово: Оригинальные файлы игры полностью восстановлены.");
        }

        private static BackupFileRecord EnsureEnJsonBackedUp(
            string gameDir,
            string backupRoot,
            BackupManifestData existingManifest,
            Action<string> log)
        {
            string relPath = SteamLocator.EnJsonRelPath;
            string livePath = Path.Combine(gameDir, relPath);
            string backupPath = Path.Combine(backupRoot, relPath);
            BackupFileRecord prev = FindRecord(existingManifest, "game", relPath);

            byte[] liveBytes = File.ReadAllBytes(livePath);
            string liveSha = BackupManifestStore.ComputeSha256Hex(liveBytes);
            bool liveHasCyrillic = JsonLocalizationPatcher.ContainsCyrillicValues(liveBytes);

            bool reuseExistingBackup = false;
            if (prev != null && prev.ExistedOriginally && SteamLocator.SafeFileExists(backupPath))
            {
                byte[] backupBytes = File.ReadAllBytes(backupPath);
                string backupSha = BackupManifestStore.ComputeSha256Hex(backupBytes);
                if (string.Equals(backupSha, prev.OrigSha256, StringComparison.OrdinalIgnoreCase))
                {
                    if (liveHasCyrillic
                        || string.Equals(liveSha, prev.PatchedSha256, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(liveSha, prev.OrigSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        reuseExistingBackup = true;
                    }
                    else
                    {
                        log("Обнаружено обновление игры в Steam для Localization\\en_US.json: обновляется резервная копия.");
                    }
                }
            }

            if (!reuseExistingBackup && liveHasCyrillic)
            {
                if (SteamLocator.SafeFileExists(backupPath))
                {
                    byte[] savedBytes = File.ReadAllBytes(backupPath);
                    if (!JsonLocalizationPatcher.ContainsCyrillicValues(savedBytes))
                    {
                        reuseExistingBackup = true;
                    }
                }
                if (!reuseExistingBackup)
                {
                    throw new InvalidOperationException(
                        "Файл Localization\\en_US.json уже содержит русский текст, а чистая резервная копия оригинала отсутствует. Выполните проверку целостности файлов игры в Steam и запустите установщик снова.");
                }
            }

            if (!reuseExistingBackup)
            {
                EnsureParentDirectory(backupPath);
                File.WriteAllBytes(backupPath, liveBytes);
            }

            byte[] finalOrigBytes = File.ReadAllBytes(backupPath);
            BackupFileRecord rec = new BackupFileRecord();
            rec.TargetKind = "game";
            rec.RelPath = relPath;
            rec.BackupRelPath = relPath;
            rec.ExistedOriginally = true;
            rec.OrigSha256 = BackupManifestStore.ComputeSha256Hex(finalOrigBytes);
            return rec;
        }

        private static BackupFileRecord FindRecord(BackupManifestData m, string kind, string relPath)
        {
            if (m == null || m.Files == null)
            {
                return null;
            }
            for (int i = 0; i < m.Files.Count; i++)
            {
                BackupFileRecord r = m.Files[i];
                if (string.Equals(r.TargetKind, kind, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(r.RelPath, relPath, StringComparison.OrdinalIgnoreCase))
                {
                    return r;
                }
            }
            return null;
        }

        private static void WriteViaStaging(string stagingRoot, string relName, string targetPath, byte[] content)
        {
            string stageFile = Path.Combine(stagingRoot, relName);
            EnsureParentDirectory(stageFile);
            File.WriteAllBytes(stageFile, content);

            EnsureParentDirectory(targetPath);
            if (SteamLocator.SafeFileExists(targetPath))
            {
                FileAttributes attrs = File.GetAttributes(targetPath);
                if ((attrs & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(targetPath, attrs & ~FileAttributes.ReadOnly);
                }
            }
            File.Copy(stageFile, targetPath, true);
        }

        private static void WriteAtomicTarget(string stagingRoot, string relName, string targetPath, byte[] content)
        {
            string stageFile = Path.Combine(stagingRoot, relName);
            EnsureParentDirectory(stageFile);
            File.WriteAllBytes(stageFile, content);

            EnsureParentDirectory(targetPath);
            string sameVolumeTmp = targetPath + ".ft_tmp";
            try
            {
                File.WriteAllBytes(sameVolumeTmp, content);
                if (SteamLocator.SafeFileExists(targetPath))
                {
                    try
                    {
                        File.Replace(sameVolumeTmp, targetPath, null, true);
                        return;
                    }
                    catch
                    {
                    }
                }
                File.Copy(sameVolumeTmp, targetPath, true);
            }
            finally
            {
                TryDeleteFile(sameVolumeTmp);
            }
        }

        private static void VerifyExpectedHash(string label, byte[] actualBytes, string expectedHash)
        {
            if (string.IsNullOrEmpty(expectedHash))
            {
                return;
            }
            string actualHash = BackupManifestStore.ComputeSha256Hex(actualBytes);
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Несовпадение контрольной суммы SHA-256 при проверке " + label);
            }
        }

        private static void AssertNoLeftoverFilesInLocalizationDir(string gameDir)
        {
            string sub = Path.Combine(gameDir, @"FrontierTale_Data\StreamingAssets\Localization");
            if (!SteamLocator.SafeDirectoryExists(sub))
            {
                return;
            }
            string[] files = Directory.GetFiles(sub, "*", SearchOption.AllDirectories);
            for (int f = 0; f < files.Length; f++)
            {
                string ext = Path.GetExtension(files[f]);
                if (string.Equals(ext, ".bak", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(ext, ".tmp", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(ext, ".orig", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Обнаружен недопустимый временный файл в каталоге локализации: " + files[f]);
                }
            }
        }

        private static void EnsureParentDirectory(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        private static void EnsureEmptyDirectory(string dirPath)
        {
            TryDeleteDirectory(dirPath);
            Directory.CreateDirectory(dirPath);
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (SteamLocator.SafeFileExists(path))
                {
                    FileAttributes attrs = File.GetAttributes(path);
                    if ((attrs & FileAttributes.ReadOnly) != 0)
                    {
                        File.SetAttributes(path, attrs & ~FileAttributes.ReadOnly);
                    }
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private static void TryDeleteDirectory(string dirPath)
        {
            try
            {
                if (SteamLocator.SafeDirectoryExists(dirPath))
                {
                    string[] allFiles = Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories);
                    for (int i = 0; i < allFiles.Length; i++)
                    {
                        FileAttributes attrs = File.GetAttributes(allFiles[i]);
                        if ((attrs & FileAttributes.ReadOnly) != 0)
                        {
                            File.SetAttributes(allFiles[i], attrs & ~FileAttributes.ReadOnly);
                        }
                    }
                    Directory.Delete(dirPath, true);
                }
            }
            catch
            {
            }
        }
    }

    internal static class TacticalTheme
    {
        public static readonly Color BgDeep = Color.FromArgb(0x0F, 0x14, 0x19);
        public static readonly Color CardSurface = Color.FromArgb(0x18, 0x21, 0x2A);
        public static readonly Color CardLocked = Color.FromArgb(0x13, 0x1B, 0x22);
        public static readonly Color RecessedBg = Color.FromArgb(0x10, 0x16, 0x1D);
        public static readonly Color BorderCrisp = Color.FromArgb(0x2A, 0x38, 0x46);
        public static readonly Color BorderLocked = Color.FromArgb(0x22, 0x2E, 0x3A);
        public static readonly Color BorderEmerald = Color.FromArgb(0x26, 0x5A, 0x43);

        public static readonly Color EmeraldPrimary = Color.FromArgb(0x2E, 0xCC, 0x71);
        public static readonly Color EmeraldHover = Color.FromArgb(0x3B, 0xD9, 0x7E);
        public static readonly Color EmeraldPressed = Color.FromArgb(0x24, 0xB2, 0x60);
        public static readonly Color EmeraldDarkText = Color.FromArgb(0x08, 0x15, 0x0E);
        public static readonly Color EmeraldPillBg = Color.FromArgb(0x13, 0x2D, 0x22);

        public static readonly Color SecondaryBtnBg = Color.FromArgb(0x24, 0x31, 0x3D);
        public static readonly Color SecondaryBtnHover = Color.FromArgb(0x2E, 0x3E, 0x4D);
        public static readonly Color SecondaryBtnPressed = Color.FromArgb(0x1C, 0x27, 0x31);
        public static readonly Color SecondaryBtnBorder = Color.FromArgb(0x35, 0x47, 0x59);

        public static readonly Color TextPrimary = Color.FromArgb(0xE8, 0xEE, 0xF4);
        public static readonly Color TextSecondary = Color.FromArgb(0x93, 0xA6, 0xB8);
        public static readonly Color TextDim = Color.FromArgb(0x68, 0x7B, 0x8E);
        public static readonly Color GoldAccent = Color.FromArgb(0xEB, 0xC2, 0x47);
        public static readonly Color WarnAmber = Color.FromArgb(0xE6, 0x9F, 0x3E);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        public static float DetectDpiScale(Control control)
        {
            try
            {
                using (Graphics g = control != null ? control.CreateGraphics() : Graphics.FromHwnd(IntPtr.Zero))
                {
                    if (g.DpiX > 0f)
                    {
                        float s = g.DpiX / 96f;
                        if (s < 1f) return 1f;
                        if (s > 3.5f) return 3.5f;
                        return s;
                    }
                }
            }
            catch
            {
            }
            return 1f;
        }

        public static void ClampWindowToWorkingArea(Form form, int marginPx, bool shrinkToFit)
        {
            if (form == null)
            {
                return;
            }
            try
            {
                Screen scr = form.Owner != null
                    ? Screen.FromControl(form.Owner)
                    : Screen.FromControl(form);
                if (scr == null) scr = Screen.PrimaryScreen;
                if (scr == null) return;

                Rectangle wa = scr.WorkingArea;
                int w = form.Width;
                int h = form.Height;

                if (shrinkToFit)
                {
                    int maxW = Math.Max(360, wa.Width - marginPx * 2);
                    int maxH = Math.Max(320, wa.Height - marginPx * 2);
                    if (w > maxW) w = maxW;
                    if (h > maxH) h = maxH;
                }

                int x = wa.Left + (wa.Width - w) / 2;
                int y = wa.Top + (wa.Height - h) / 2;

                if (x + w > wa.Right - marginPx) x = wa.Right - marginPx - w;
                if (x < wa.Left + marginPx) x = Math.Max(wa.Left, wa.Left + (wa.Width - w) / 2);

                if (y + h > wa.Bottom - marginPx) y = wa.Bottom - marginPx - h;
                if (y < wa.Top + marginPx) y = Math.Max(wa.Top, wa.Top + (wa.Height - h) / 2);

                form.Bounds = new Rectangle(x, y, w, h);
            }
            catch
            {
            }
        }

        public static void EnsureWithinWorkingArea(Form form, int marginPx, bool shrinkToFit)
        {
            if (form == null) return;
            try
            {
                Screen scr = Screen.FromControl(form) ?? Screen.PrimaryScreen;
                if (scr == null) return;
                Rectangle wa = scr.WorkingArea;
                if (form.Left < wa.Left
                    || form.Top < wa.Top
                    || form.Right > wa.Right - marginPx
                    || form.Bottom > wa.Bottom - marginPx)
                {
                    ClampWindowToWorkingArea(form, marginPx, shrinkToFit);
                }
            }
            catch
            {
            }
        }

        public static GraphicsPath CreateRoundedRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0 || r.Width <= 2 || r.Height <= 2)
            {
                path.AddRectangle(r);
                return path;
            }
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void DrawCard(Graphics g, Rectangle rect, Color fill, Color border, int radius)
        {
            Rectangle borderRect = new Rectangle(rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
            using (GraphicsPath path = CreateRoundedRect(borderRect, radius))
            using (SolidBrush b = new SolidBrush(fill))
            using (Pen p = new Pen(border, 1f))
            {
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }
        }

        public static Icon CreateWindowIcon()
        {
            try
            {
                using (Bitmap bmp = new Bitmap(64, 64))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.Clear(Color.Transparent);
                        DrawPioneerCompassBadge(g, new Rectangle(2, 2, 60, 60));
                    }
                    IntPtr hIcon = bmp.GetHicon();
                    try
                    {
                        using (Icon tmp = Icon.FromHandle(hIcon))
                        {
                            return (Icon)tmp.Clone();
                        }
                    }
                    finally
                    {
                        DestroyIcon(hIcon);
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        public static void DrawPioneerCompassBadge(Graphics g, Rectangle bounds)
        {
            DrawCard(g, bounds, RecessedBg, BorderEmerald, Math.Max(6, bounds.Width / 6));

            float cx = bounds.X + bounds.Width * 0.5f;
            float cy = bounds.Y + bounds.Height * 0.5f;
            float rOuter = bounds.Width * 0.34f;
            float rInner = bounds.Width * 0.11f;

            using (Pen ringPen = new Pen(Color.FromArgb(145, EmeraldPrimary), Math.Max(1.3f, bounds.Width * 0.032f)))
            using (SolidBrush starBrush = new SolidBrush(GoldAccent))
            using (SolidBrush centerBrush = new SolidBrush(EmeraldPrimary))
            {
                g.DrawEllipse(ringPen, cx - rOuter, cy - rOuter, rOuter * 2f, rOuter * 2f);
                PointF[] compass = BuildFourPointCompass(cx, cy, rOuter * 0.92f, rInner);
                g.FillPolygon(starBrush, compass);
                float dotR = Math.Max(2f, bounds.Width * 0.06f);
                g.FillEllipse(centerBrush, cx - dotR, cy - dotR, dotR * 2f, dotR * 2f);
            }
        }

        private static PointF[] BuildFourPointCompass(float cx, float cy, float outerR, float innerR)
        {
            PointF[] pts = new PointF[8];
            double angle = -Math.PI / 2.0;
            double step = Math.PI / 4.0;
            for (int i = 0; i < 8; i++)
            {
                float r = (i % 2 == 0) ? outerR : innerR;
                pts[i] = new PointF(
                    cx + (float)(Math.Cos(angle) * r),
                    cy + (float)(Math.Sin(angle) * r));
                angle += step;
            }
            return pts;
        }

    }

    internal sealed class SetupForm : Form
    {
        private readonly PayloadData _payload;
        private readonly string _appBaseDir;
        private readonly CliMode _launchMode;
        private readonly float _scale;

        private string _steamDir;
        private bool _isBusy;
        private bool _uacAutoTriggered;

        private readonly SkinSpec _skin;
        private Image _bannerImg;
        private Image _logoImg;
        private PrivateFontCollection _embeddedFontCollection;
        private FontFamily _embeddedFontFamily;

        private string _statusBadgeText = string.Empty;
        private Color _statusBadgeFg;
        private Color _statusBadgeBg;
        private Color _statusBadgeBorder;
        private string _statusRibbonText = "Готов к установке русификатора.";
        private bool _installSucceeded;

        private Font _fontBrand;
        private Font _fontBadge;
        private Font _fontSub;
        private Font _fontSection;
        private Font _fontBody;
        private Font _fontSmall;
        private Font _fontPrimaryBtn;
        private Font _fontSecondaryBtn;
        private Font _fontLog;
        private Icon _winIcon;

        private Rectangle _headerRect;
        private Rectangle _pathCardRect;
        private Rectangle _pathInputRect;
        private Rectangle _statusRibbonRect;
        private Rectangle _logCardRect;

        private TextBox _pathBox;
        private SkinnedButton _browseBtn;
        private SkinnedButton _installBtn;
        private SkinnedButton _uninstallBtn;
        private TextBox _logBox;
        private SkinnedButton _detailsBtn;
        private bool _detailsOpen;
        private SkinnedButton _boostyLinkBtn;
        private SkinnedButton _siteLinkBtn;
        private SkinnedButton _githubLinkBtn;
        private SkinnedButton _telegramLinkBtn;

        public SetupForm(
            PayloadData payload,
            string appBaseDir,
            string explicitGameDir,
            string explicitSteamDir,
            CliMode launchMode = CliMode.DefaultGui)
        {
            _payload = payload;
            _appBaseDir = appBaseDir;
            _launchMode = launchMode;
            _skin = Program.CreateSkin();
            _statusBadgeFg = _skin.TextMuted;
            _statusBadgeBg = _skin.RecessedBg;
            _statusBadgeBorder = _skin.Border;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint,
                true);

            AutoScaleMode = AutoScaleMode.Dpi;
            _scale = TacticalTheme.DetectDpiScale(this);

            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = _skin.BgDeep;
            ForeColor = _skin.TextPrimary;
            Text = "Frontier Tale - Русификатор v1.5";
            ClientSize = new Size(S(728), S(388));

            _bannerImg = SkinResources.LoadEmbeddedImage(SkinResources.BannerResourceName);
            _logoImg = SkinResources.LoadEmbeddedImage(SkinResources.LogoResourceName);
            _embeddedFontFamily = SkinResources.LoadEmbeddedFontFamily(SkinResources.TitleFontResourceName, out _embeddedFontCollection);
            _winIcon = SkinResources.TryExtractExeIcon() ?? TacticalTheme.CreateWindowIcon();
            if (_winIcon != null)
            {
                Icon = _winIcon;
            }

            InitFonts();
            BuildControls();
            SetDetailsOpen(false);
            InitDetection(explicitGameDir, explicitSteamDir);
            ActiveControl = _installBtn;
        }

        public void PreparePreviewSamplePath()
        {
            if (string.IsNullOrWhiteSpace(_pathBox.Text))
            {
                _pathBox.Text = @"C:\Program Files (x86)\Steam\steamapps\common\" + SteamLocator.DefaultGameFolderName;
            }
            _installSucceeded = false;
            _statusBadgeText = "Готов к установке";
            _statusBadgeFg = _skin.TextPrimary;
            _statusBadgeBg = _skin.CardSurface;
            _statusBadgeBorder = _skin.Border;
            _statusRibbonText = "Папка игры найдена. Нажмите «Установить русификатор».";
            _installBtn.Text = "Установить русификатор";
            _installBtn.Enabled = true;
            _uninstallBtn.Enabled = false;
            Invalidate();
        }

        public void ApplyInstallSuccessVisualState()
        {
            _installSucceeded = true;
            _statusBadgeText = "Установлен";
            _statusBadgeFg = _skin.SuccessFg;
            _statusBadgeBg = _skin.SuccessBg;
            _statusBadgeBorder = _skin.SuccessBorder;
            _statusRibbonText = "Готово! Русификатор установлен. Обновления под патчи Steam выходят на Boosty.";
            _installBtn.Text = "Обновить русификатор";
            _installBtn.Enabled = true;
            _uninstallBtn.Enabled = true;
            _boostyLinkBtn.Kind = SkinButtonKind.BoostyHighlight;
            _boostyLinkBtn.Text = "Boosty · патчи и бонусы";
            _boostyLinkBtn.Invalidate();
            Invalidate();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            TacticalTheme.ClampWindowToWorkingArea(this, S(12), false);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            TacticalTheme.EnsureWithinWorkingArea(this, S(12), false);

            if (!_uacAutoTriggered)
            {
                _uacAutoTriggered = true;
                if (_launchMode == CliMode.UacInstallGui)
                {
                    BeginInvoke((MethodInvoker)delegate { StartInstallFlow(true); });
                }
                else if (_launchMode == CliMode.UacUninstallGui)
                {
                    BeginInvoke((MethodInvoker)delegate { StartUninstallFlow(true); });
                }
            }
        }

        private int S(int px)
        {
            return (int)Math.Round(px * _scale);
        }

        private void InitFonts()
        {
            string uiFont = _skin.Style == SkinStyle.Win95Bevel ? "Tahoma" : "Segoe UI";
            string fallbackHead = _skin.Style == SkinStyle.ParchmentGold ? "Georgia" : uiFont;
            _fontBrand = SkinResources.CreateHeadingFont(_embeddedFontFamily, fallbackHead, S(24), FontStyle.Bold);
            _fontBadge = new Font(uiFont, S(11), FontStyle.Bold, GraphicsUnit.Pixel);
            _fontSub = new Font(uiFont, S(12), FontStyle.Regular, GraphicsUnit.Pixel);
            _fontSection = new Font(uiFont, S(11), FontStyle.Bold, GraphicsUnit.Pixel);
            _fontBody = new Font(uiFont, S(13), FontStyle.Regular, GraphicsUnit.Pixel);
            _fontSmall = new Font(uiFont, S(11), FontStyle.Regular, GraphicsUnit.Pixel);
            _fontPrimaryBtn = SkinResources.CreateHeadingFont(_embeddedFontFamily, uiFont, S(14), FontStyle.Bold);
            _fontSecondaryBtn = new Font(uiFont, S(13), FontStyle.Bold, GraphicsUnit.Pixel);
            _fontLog = new Font("Consolas", S(12), FontStyle.Regular, GraphicsUnit.Pixel);
        }

        private void BuildControls()
        {
            int pad = S(20);
            int contentW = ClientSize.Width - pad * 2;

            _headerRect = new Rectangle(pad, S(16), contentW, S(128));
            _pathCardRect = new Rectangle(pad, S(156), contentW, S(82));

            int browseW = S(114);
            int inputX = _pathCardRect.X + S(16);
            int inputY = _pathCardRect.Y + S(33);
            int inputH = S(36);
            int inputW = _pathCardRect.Width - S(32) - browseW - S(10);
            _pathInputRect = new Rectangle(inputX, inputY, inputW, inputH);

            _pathBox = new TextBox();
            _pathBox.Multiline = true;
            _pathBox.WordWrap = false;
            _pathBox.AcceptsReturn = false;
            _pathBox.BorderStyle = BorderStyle.None;
            _pathBox.BackColor = _skin.RecessedBg;
            _pathBox.ForeColor = _skin.TextPrimary;
            _pathBox.Font = _fontBody;
            int lineH = _fontBody.Height + S(2);
            int tbTop = inputY + Math.Max(S(4), (inputH - lineH) / 2);
            _pathBox.Bounds = new Rectangle(inputX + S(10), tbTop, inputW - S(20), lineH);
            _pathBox.KeyDown += delegate(object s, KeyEventArgs ke)
            {
                if (ke.KeyCode == Keys.Enter || ke.KeyCode == Keys.Return)
                {
                    ke.SuppressKeyPress = true;
                    ke.Handled = true;
                }
            };
            _pathBox.TextChanged += delegate
            {
                string raw = _pathBox.Text ?? string.Empty;
                if (raw.IndexOf('\r') >= 0 || raw.IndexOf('\n') >= 0)
                {
                    _pathBox.Text = raw.Replace("\r", string.Empty).Replace("\n", string.Empty);
                    return;
                }
                RefreshUiState();
            };
            _pathBox.AccessibleName = "Папка игры";
            Controls.Add(_pathBox);

            _browseBtn = new SkinnedButton();
            _browseBtn.Spec = _skin;
            _browseBtn.Kind = SkinButtonKind.Secondary;
            _browseBtn.Text = "Обзор...";
            _browseBtn.Font = _fontSecondaryBtn;
            _browseBtn.CornerRadius = S(6);
            _browseBtn.Bounds = new Rectangle(inputX + inputW + S(10), inputY, browseW, inputH);
            _browseBtn.Click += OnBrowseClicked;
            Controls.Add(_browseBtn);

            int btnY = S(250);
            int btnH = S(44);
            int gap = S(12);
            int restoreW = S(240);
            int installW = contentW - restoreW - gap;

            _installBtn = new SkinnedButton();
            _installBtn.Spec = _skin;
            _installBtn.Kind = SkinButtonKind.Primary;
            _installBtn.Text = "Установить русификатор";
            _installBtn.Font = _fontPrimaryBtn;
            _installBtn.CornerRadius = S(6);
            _installBtn.Bounds = new Rectangle(pad, btnY, installW, btnH);
            _installBtn.Click += delegate { StartInstallFlow(_launchMode == CliMode.UacInstallGui); };
            Controls.Add(_installBtn);

            _uninstallBtn = new SkinnedButton();
            _uninstallBtn.Spec = _skin;
            _uninstallBtn.Kind = SkinButtonKind.Secondary;
            _uninstallBtn.Text = "Вернуть оригинал";
            _uninstallBtn.Font = _fontSecondaryBtn;
            _uninstallBtn.CornerRadius = S(6);
            _uninstallBtn.Bounds = new Rectangle(pad + installW + gap, btnY, restoreW, btnH);
            _uninstallBtn.Click += delegate { StartUninstallFlow(_launchMode == CliMode.UacUninstallGui); };
            Controls.Add(_uninstallBtn);

            _statusRibbonRect = new Rectangle(pad, S(304), contentW, S(28));

            int barY = S(342);
            int barH = S(30);
            int detailsW = S(144);
            int linkGap = S(8);
            int tgW = S(82);
            int ghW = S(72);
            int siteW = S(86);
            int boostyW = S(220);

            string telegramUrl = Program.GetTelegramUrl();
            bool hasTelegram = !string.IsNullOrWhiteSpace(telegramUrl);

            int rightEdge = ClientSize.Width - pad;
            int tgX = rightEdge - tgW;
            int ghX = hasTelegram ? (tgX - linkGap - ghW) : (rightEdge - ghW);
            int siteX = ghX - linkGap - siteW;
            int boostyX = siteX - linkGap - boostyW;

            _detailsBtn = new SkinnedButton();
            _detailsBtn.Spec = _skin;
            _detailsBtn.Kind = SkinButtonKind.SubtleLink;
            _detailsBtn.Font = _fontSecondaryBtn;
            _detailsBtn.CornerRadius = S(5);
            _detailsBtn.Bounds = new Rectangle(pad, barY, detailsW, barH);
            _detailsBtn.Click += delegate { SetDetailsOpen(!_detailsOpen); };
            Controls.Add(_detailsBtn);

            _boostyLinkBtn = new SkinnedButton();
            _boostyLinkBtn.Spec = _skin;
            _boostyLinkBtn.Kind = SkinButtonKind.SubtleLink;
            _boostyLinkBtn.Text = "Boosty (патчи и заказ игр)";
            _boostyLinkBtn.Font = _fontSmall;
            _boostyLinkBtn.CornerRadius = S(5);
            _boostyLinkBtn.Bounds = new Rectangle(boostyX, barY, boostyW, barH);
            _boostyLinkBtn.Click += delegate { Program.OpenBrowserUrl(Program.BuildBoostyUrl()); };
            Controls.Add(_boostyLinkBtn);

            _siteLinkBtn = new SkinnedButton();
            _siteLinkBtn.Spec = _skin;
            _siteLinkBtn.Kind = SkinButtonKind.SubtleLink;
            _siteLinkBtn.Text = "Все игры";
            _siteLinkBtn.Font = _fontSmall;
            _siteLinkBtn.CornerRadius = S(5);
            _siteLinkBtn.Bounds = new Rectangle(siteX, barY, siteW, barH);
            _siteLinkBtn.Click += delegate { Program.OpenBrowserUrl(Program.BuildSiteUrl()); };
            Controls.Add(_siteLinkBtn);

            _githubLinkBtn = new SkinnedButton();
            _githubLinkBtn.Spec = _skin;
            _githubLinkBtn.Kind = SkinButtonKind.SubtleLink;
            _githubLinkBtn.Text = "GitHub";
            _githubLinkBtn.Font = _fontSmall;
            _githubLinkBtn.CornerRadius = S(5);
            _githubLinkBtn.Bounds = new Rectangle(ghX, barY, ghW, barH);
            _githubLinkBtn.Click += delegate { Program.OpenBrowserUrl(Program.GithubUrl); };
            Controls.Add(_githubLinkBtn);

            if (hasTelegram)
            {
                _telegramLinkBtn = new SkinnedButton();
                _telegramLinkBtn.Spec = _skin;
                _telegramLinkBtn.Kind = SkinButtonKind.SubtleLink;
                _telegramLinkBtn.Text = "Telegram";
                _telegramLinkBtn.Font = _fontSmall;
                _telegramLinkBtn.CornerRadius = S(5);
                _telegramLinkBtn.Bounds = new Rectangle(tgX, barY, tgW, barH);
                _telegramLinkBtn.Click += delegate { Program.OpenBrowserUrl(telegramUrl); };
                Controls.Add(_telegramLinkBtn);
            }

            _logCardRect = new Rectangle(pad, S(384), contentW, S(96));
            _logBox = new TextBox();
            _logBox.AccessibleName = "Журнал установки";
            _logBox.Multiline = true;
            _logBox.ReadOnly = true;
            _logBox.BorderStyle = BorderStyle.None;
            _logBox.ScrollBars = ScrollBars.None;
            _logBox.BackColor = _skin.RecessedBg;
            _logBox.ForeColor = _skin.TextPrimary;
            _logBox.Font = _fontLog;
            _logBox.Location = new Point(_logCardRect.X + S(16), _logCardRect.Y + S(33));
            _logBox.Size = new Size(_logCardRect.Width - S(32), _logCardRect.Height - S(46));
            Controls.Add(_logBox);
        }

        private void SetDetailsOpen(bool open)
        {
            _detailsOpen = open;
            _detailsBtn.Text = open ? "Скрыть журнал" : "Подробности";
            _logBox.Visible = open;
            ClientSize = new Size(ClientSize.Width, S(open ? 500 : 388));
            if (Visible)
            {
                TacticalTheme.ClampWindowToWorkingArea(this, S(12), false);
            }
            Invalidate();
        }

        private void InitDetection(string explicitGameDir, string explicitSteamDir)
        {
            _steamDir = SteamLocator.DetectSteamDirectory(explicitSteamDir, explicitGameDir);
            string detectedGame = !string.IsNullOrWhiteSpace(explicitGameDir)
                ? SteamLocator.NormalizePickedGameDirectory(explicitGameDir)
                : SteamLocator.AutoDetectGameDirectory(_steamDir, _appBaseDir);

            if (!string.IsNullOrEmpty(detectedGame))
            {
                _pathBox.Text = detectedGame;
                if (string.IsNullOrEmpty(_steamDir))
                {
                    _steamDir = SteamLocator.DetectSteamDirectory(null, detectedGame);
                }
            }

            RefreshUiState();

            if (SteamLocator.IsValidGameDirectory(_pathBox.Text))
            {
                AppendLog(InstallerService.IsRussifierInstalled(_pathBox.Text)
                    ? "Русификатор установлен."
                    : "Готов к установке.");
            }
            else
            {
                AppendLog("Выберите папку с игрой кнопкой «Обзор...».");
            }
        }

        private void RefreshUiState()
        {
            string currentGameDir = (_pathBox.Text ?? string.Empty).Trim();
            bool validGame = SteamLocator.IsValidGameDirectory(currentGameDir);
            if (validGame && string.IsNullOrEmpty(_steamDir))
            {
                _steamDir = SteamLocator.DetectSteamDirectory(null, currentGameDir);
            }

            bool hasBackup = validGame && BackupManifestStore.HasValidBackup(currentGameDir);
            bool installed = validGame && InstallerService.IsRussifierInstalled(currentGameDir);

            if (!validGame)
            {
                _statusBadgeText = "Укажите папку игры";
                _statusBadgeFg = _skin.WarnFg;
                _statusBadgeBg = _skin.WarnBg;
                _statusBadgeBorder = _skin.WarnBorder;
                if (!_installSucceeded)
                {
                    _statusRibbonText = "Выберите папку с игрой кнопкой «Обзор...».";
                }
            }
            else if (installed)
            {
                _statusBadgeText = "Установлен";
                _statusBadgeFg = _skin.SuccessFg;
                _statusBadgeBg = _skin.SuccessBg;
                _statusBadgeBorder = _skin.SuccessBorder;
                if (!_installSucceeded)
                {
                    _statusRibbonText = "Русификатор уже установлен. Можно обновить или вернуть оригинал.";
                }
            }
            else
            {
                _statusBadgeText = "Не установлен";
                _statusBadgeFg = _skin.TextMuted;
                _statusBadgeBg = _skin.RecessedBg;
                _statusBadgeBorder = _skin.Border;
                if (!_installSucceeded)
                {
                    _statusRibbonText = "Папка игры найдена. Нажмите «Установить русификатор».";
                }
            }

            _installBtn.Text = _isBusy ? "Подождите..." : (installed ? "Обновить русификатор" : "Установить русификатор");
            _installBtn.Enabled = !_isBusy && validGame;
            _uninstallBtn.Enabled = !_isBusy && hasBackup;
            _browseBtn.Enabled = !_isBusy;
            Invalidate();
        }

        private void OnBrowseClicked(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Выберите папку установки Frontier Tale (содержащую FrontierTale.exe)";
                dlg.ShowNewFolderButton = false;
                if (SteamLocator.SafeDirectoryExists(_pathBox.Text))
                {
                    dlg.SelectedPath = _pathBox.Text;
                }
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    string picked = SteamLocator.NormalizePickedGameDirectory(dlg.SelectedPath);
                    _pathBox.Text = picked;
                    RefreshUiState();
                    if (SteamLocator.IsValidGameDirectory(picked))
                    {
                        AppendLog("Выбрана папка игры: " + picked);
                    }
                    else
                    {
                        AppendLog("В выбранной папке не найден FrontierTale.exe: " + picked);
                    }
                }
            }
        }

        private void StartInstallFlow(bool skipUacRelaunch)
        {
            if (_isBusy) return;
            string gameDir = SteamLocator.NormalizePickedGameDirectory(_pathBox.Text);
            string steamDir = _steamDir;
            if (!CheckReadyBeforeAction(gameDir)) return;
            if (!skipUacRelaunch && ElevationHelper.NeedsElevationForGameDir(gameDir))
            {
                TryElevateAndClose("--uac-install", gameDir, steamDir);
                return;
            }

            RunWorkerAction(
                "Установка русификатора...",
                delegate(Action<string> logger)
                {
                    InstallerService.Install(_payload, gameDir, steamDir, logger);
                },
                delegate(Exception failure)
                {
                    HandleWorkerFinished(failure, true, skipUacRelaunch, gameDir, steamDir);
                });
        }

        private void StartUninstallFlow(bool skipUacRelaunch)
        {
            if (_isBusy) return;
            string gameDir = SteamLocator.NormalizePickedGameDirectory(_pathBox.Text);
            string steamDir = _steamDir;
            if (!CheckReadyBeforeAction(gameDir)) return;
            if (!skipUacRelaunch && ElevationHelper.NeedsElevationForGameDir(gameDir))
            {
                TryElevateAndClose("--uac-uninstall", gameDir, steamDir);
                return;
            }

            RunWorkerAction(
                "Возврат оригинальных файлов...",
                delegate(Action<string> logger)
                {
                    InstallerService.Uninstall(gameDir, steamDir, logger);
                },
                delegate(Exception failure)
                {
                    HandleWorkerFinished(failure, false, skipUacRelaunch, gameDir, steamDir);
                });
        }

        private bool CheckReadyBeforeAction(string gameDir)
        {
            if (!SteamLocator.IsValidGameDirectory(gameDir))
            {
                string msg = "Папка игры не содержит FrontierTale.exe и Localization\\en_US.json: " + gameDir;
                AppendLog("ОШИБКА: " + msg);
                MessageBox.Show(this, msg, "Папка игры не найдена", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            try
            {
                SteamLocator.AssertGameClosed(gameDir);
                return true;
            }
            catch (Exception ex)
            {
                AppendLog("ОШИБКА: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Игра запущена", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        private bool TryElevateAndClose(string modeFlag, string gameDir, string steamDir)
        {
            AppendLog("Запрос прав администратора Windows (UAC)...");
            bool cancelled;
            string errorMsg;
            if (ElevationHelper.TryRelaunchElevated(modeFlag, gameDir, steamDir, out cancelled, out errorMsg))
            {
                Close();
                return true;
            }
            if (cancelled)
            {
                AppendLog("Запрос прав администратора отменён пользователем.");
            }
            else if (!string.IsNullOrEmpty(errorMsg))
            {
                AppendLog("Не удалось запустить с правами администратора: " + errorMsg);
                MessageBox.Show(this, errorMsg, "Ошибка UAC", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            RefreshUiState();
            return false;
        }

        private void HandleWorkerFinished(
            Exception failure,
            bool isInstall,
            bool skipUacRelaunch,
            string gameDir,
            string steamDir)
        {
            string uacFlag = isInstall ? "--uac-install" : "--uac-uninstall";
            if (failure is UnauthorizedAccessException && !skipUacRelaunch && !ElevationHelper.IsProcessElevated())
            {
                TryElevateAndClose(uacFlag, gameDir, steamDir);
                return;
            }
            if (failure != null)
            {
                AppendLog("ОШИБКА: " + failure.Message);
                MessageBox.Show(this, failure.Message, "Ошибка операции", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else if (isInstall)
            {
                ApplyInstallSuccessVisualState();
            }
            else
            {
                _installSucceeded = false;
                _boostyLinkBtn.Kind = SkinButtonKind.SubtleLink;
                _boostyLinkBtn.Text = "Boosty (патчи и заказ игр)";
                _statusRibbonText = "Оригинальные файлы игры восстановлены.";
                _boostyLinkBtn.Invalidate();
                Invalidate();
            }
        }

        private void RunWorkerAction(string startHeader, Action<Action<string>> work, Action<Exception> onCompleted)
        {
            _isBusy = true;
            RefreshUiState();
            AppendLog("--------------------------------------------------");
            AppendLog(startHeader);

            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception failure = null;
                try
                {
                    work(delegate(string line)
                    {
                        SafeInvoke(delegate { AppendLog(line); });
                    });
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                SafeInvoke(delegate
                {
                    _isBusy = false;
                    RefreshUiState();
                    if (onCompleted != null)
                    {
                        onCompleted(failure);
                    }
                });
            });
        }

        private void SafeInvoke(MethodInvoker action)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                if (InvokeRequired) BeginInvoke(action);
                else action();
            }
            catch
            {
            }
        }

        private void AppendLog(string message)
        {
            if (!_detailsOpen && message.StartsWith("Ошибка", StringComparison.OrdinalIgnoreCase))
            {
                SetDetailsOpen(true);
            }
            string stamp = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            string line = "[" + stamp + "] " + message;
            if (_logBox.TextLength > 0) _logBox.AppendText("\r\n" + line);
            else _logBox.Text = line;
            if (_logBox.ScrollBars == ScrollBars.None && _logBox.Lines.Length > 3)
            {
                _logBox.ScrollBars = ScrollBars.Vertical;
            }
            _logBox.SelectionStart = _logBox.TextLength;
            _logBox.ScrollToCaret();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            SkinPainter.DrawWindowBackdrop(g, ClientRectangle, _skin);
            PaintHeaderCard(g);
            PaintPathCard(g);
            SkinPainter.DrawStatusRibbon(g, _statusRibbonRect, _skin, _installSucceeded, _statusRibbonText, _fontSub, _scale);
            if (_detailsOpen)
            {
                PaintLogCard(g);
            }
        }

        private void PaintHeaderCard(Graphics g)
        {
            SkinPainter.DrawHeroBanner(
                g,
                _headerRect,
                _bannerImg,
                _logoImg,
                _skin,
                "FRONTIER TALE",
                "v1.5",
                "Русский текст для версии игры 2.2.1",
                _statusBadgeText,
                _statusBadgeFg,
                _statusBadgeBg,
                _statusBadgeBorder,
                _fontBrand,
                _fontBadge,
                _fontSub,
                _scale);
        }

        private void PaintPathCard(Graphics g)
        {
            SkinPainter.DrawCard(g, _pathCardRect, _skin, S(8));
            TextRenderer.DrawText(
                g,
                "ПАПКА С ИГРОЙ",
                _fontSection,
                new Point(_pathCardRect.X + S(16), _pathCardRect.Y + S(11)),
                _skin.TextMuted,
                TextFormatFlags.NoPrefix);
            SkinPainter.DrawInputFrame(g, _pathInputRect, _skin, S(5));
        }

        private void PaintLogCard(Graphics g)
        {
            SkinPainter.DrawCard(g, _logCardRect, _skin, S(8));
            TextRenderer.DrawText(
                g,
                "ЖУРНАЛ ОПЕРАЦИЙ",
                _fontSection,
                new Point(_logCardRect.X + S(16), _logCardRect.Y + S(9)),
                _skin.TextMuted,
                TextFormatFlags.NoPrefix);

            Rectangle logInner = new Rectangle(
                _logCardRect.X + S(12),
                _logCardRect.Y + S(28),
                _logCardRect.Width - S(24),
                _logCardRect.Height - S(38));
            SkinPainter.DrawInputFrame(g, logInner, _skin, S(5));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_bannerImg != null) { _bannerImg.Dispose(); _bannerImg = null; }
                if (_logoImg != null) { _logoImg.Dispose(); _logoImg = null; }
                if (_embeddedFontCollection != null) { _embeddedFontCollection.Dispose(); _embeddedFontCollection = null; }
                if (_fontBrand != null) _fontBrand.Dispose();
                if (_fontBadge != null) _fontBadge.Dispose();
                if (_fontSub != null) _fontSub.Dispose();
                if (_fontSection != null) _fontSection.Dispose();
                if (_fontBody != null) _fontBody.Dispose();
                if (_fontSmall != null) _fontSmall.Dispose();
                if (_fontPrimaryBtn != null) _fontPrimaryBtn.Dispose();
                if (_fontSecondaryBtn != null) _fontSecondaryBtn.Dispose();
                if (_fontLog != null) _fontLog.Dispose();
                if (_winIcon != null) _winIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }

}
