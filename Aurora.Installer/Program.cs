using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aurora.Installer;

internal static class InstallationLocation
{
    public static string StateRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aurora");
    public static string DefaultApp => Path.Combine(StateRoot, "app");
    static string Marker => Path.Combine(StateRoot, "installation.json");

    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new IOException("Укажите папку установки Авроры.");
        string full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(full) || full.Equals(Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new IOException("Выберите отдельную папку, а не корень диска.");
        for (string? current = full; current != null; current = Path.GetDirectoryName(current))
        {
            if ((Directory.Exists(current) || File.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Нельзя устанавливать Аврору через символическую ссылку или junction.");
        }
        return full;
    }

    public static string InstalledApp()
    {
        if (!File.Exists(Marker)) return Normalize(DefaultApp); // Support installations made before the chooser existed.
        string? path = JsonSerializer.Deserialize<InstallRecord>(File.ReadAllText(Marker))?.AppPath;
        return Normalize(path ?? throw new IOException("Не удалось прочитать сохранённую папку установки."));
    }

    public static void Save(string path)
    {
        Directory.CreateDirectory(StateRoot);
        string tmp = Marker + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tmp, JsonSerializer.Serialize(new InstallRecord(Normalize(path))));
            File.Move(tmp, Marker, true);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    public static string SavedGame()
    {
        try
        {
            string filename = Path.Combine(StateRoot, "settings.json");
            if (!File.Exists(filename)) return "";
            using var json = JsonDocument.Parse(File.ReadAllText(filename));
            return json.RootElement.TryGetProperty("Game", out var game) ? game.GetString() ?? "" : "";
        }
        catch { return ""; }
    }

    public static void SaveGame(string game)
    {
        if (string.IsNullOrWhiteSpace(game)) return;
        string selected = Path.GetFullPath(game.Trim().Trim('"'));
        if (!File.Exists(Path.Combine(selected, "SOVIET64.exe")))
            throw new IOException("В папке игры не найден SOVIET64.exe.");
        string filename = Path.Combine(StateRoot, "settings.json");
        Directory.CreateDirectory(StateRoot);
        JsonObject settings;
        try { settings = File.Exists(filename) ? JsonNode.Parse(File.ReadAllText(filename)) as JsonObject ?? new() : new(); }
        catch (JsonException) { throw new IOException("Файл настроек Авроры повреждён. Исправьте его перед установкой."); }
        settings["Game"] = selected;
        string tmp = filename + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(tmp, settings.ToJsonString()); File.Move(tmp, filename, true); }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }
    sealed record InstallRecord(string AppPath);
}

static class Program
{
    [STAThread] static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            if (args.Length == 3 && args[0] == "--replace")
            {
                string expected = InstallationLocation.InstalledApp();
                string target = InstallationLocation.Normalize(args[1]);
                if (!target.Equals(expected, StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(Path.Combine(target, "Aurora.Launcher.exe")))
                    throw new IOException("Указанная папка не является зарегистрированной установкой Авроры.");
                try
                {
                    if (!int.TryParse(args[2], out int pid) || pid <= 0) throw new IOException("Некорректный PID для обновления.");
                    using var process = Process.GetProcessById(pid);
                    if (!process.WaitForExit(30000)) throw new IOException("Закройте Аврору перед обновлением.");
                }
                catch (ArgumentException) { /* The old launcher has already stopped. */ }
                Install(AppContext.BaseDirectory, target);
                Start(target);
                return;
            }
            Application.Run(new InstallWindow());
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Аврора", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    public static void Start(string target) => Process.Start(new ProcessStartInfo(Path.Combine(target, "Aurora.Launcher.exe"))
    { UseShellExecute = true, WorkingDirectory = target });

    public static void Install(string source, string target)
    {
        target = InstallationLocation.Normalize(target);
        foreach (var name in new[] { "Aurora.Launcher", "Aurora.Editor", "Aurora.ModelBackend" })
        foreach (var process in Process.GetProcessesByName(name))
        {
            using (process)
            {
                try
                {
                    if (process.MainModule?.FileName is string running &&
                        Path.GetFullPath(running).StartsWith(target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        throw new IOException("Закройте остальные окна Авроры и редактора перед обновлением.");
                }
                catch (System.ComponentModel.Win32Exception)
                { throw new IOException("Не удалось проверить запущенные окна Авроры. Закройте приложение перед установкой."); }
            }
        }

        Directory.CreateDirectory(target);
        string backup = Path.Combine(InstallationLocation.StateRoot, "backups",
            DateTime.UtcNow.ToString("yyyyMMddHHmmss") + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backup);
        var changed = new List<(string Path, string? Backup)>();
        try
        {
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked files not supported");
                var relative = Path.GetRelativePath(source, file);
                if (relative.Equals("Aurora.Installer.exe", StringComparison.OrdinalIgnoreCase)) continue;
                var destination = Path.GetFullPath(Path.Combine(target, relative));
                if (!destination.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Unsafe install path");
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                if (File.Exists(destination) && new FileInfo(file).Length == new FileInfo(destination).Length)
                {
                    using var incoming = File.OpenRead(file);
                    using var installed = File.OpenRead(destination);
                    if (SHA256.HashData(incoming).AsSpan().SequenceEqual(SHA256.HashData(installed)))
                        continue; // Preserve unchanged files and avoid an unnecessary backup.
                }
                var previous = File.Exists(destination) ? Path.Combine(backup, changed.Count + ".bak") : null;
                if (previous != null) File.Copy(destination, previous);
                string staged = destination + ".aurora-new-" + Guid.NewGuid().ToString("N");
                try
                {
                    File.Copy(file, staged);
                    File.Move(staged, destination, true);
                    changed.Add((destination, previous));
                }
                finally { if (File.Exists(staged)) File.Delete(staged); }
            }
            File.WriteAllText(Path.Combine(backup, "receipt.json"),
                JsonSerializer.Serialize(changed.Select(x => new { x.Path, x.Backup })));
        }
        catch
        {
            foreach (var item in changed.AsEnumerable().Reverse())
            {
                if (item.Backup != null) File.Copy(item.Backup, item.Path, true);
                else if (File.Exists(item.Path)) File.Delete(item.Path);
            }
            throw;
        }
    }
}

sealed class InstallWindow : Form
{
    readonly TextBox installPath = new() { Dock = DockStyle.Fill };
    readonly TextBox gamePath = new() { Dock = DockStyle.Fill };
    readonly Button install = new() { Text = "Установить Аврору", Width = 184, Height = 38 };
    readonly Label status = new() { AutoSize = true, ForeColor = Color.DimGray };

    public InstallWindow()
    {
        Text = "Установка Авроры";
        Font = new Font("Segoe UI", 10);
        ClientSize = new Size(700, 450);
        MinimumSize = new Size(640, 440);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = Color.FromArgb(246, 246, 246);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(26, 22, 26, 18), ColumnCount = 1, RowCount = 6 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 75));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        Controls.Add(root);
        var title = new Label { Text = "Аврора", Dock = DockStyle.Top, Font = new Font("Segoe UI Semibold", 22), AutoSize = true };
        var subtitle = new Label { Text = "Лаунчер модов и редактор моделей", AutoSize = true, ForeColor = Color.DimGray };
        var heading = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        heading.Controls.Add(title); heading.Controls.Add(subtitle);
        root.Controls.Add(heading, 0, 0);

        installPath.Text = InstallationLocation.InstalledApp();
        gamePath.Text = InstallationLocation.SavedGame();
        root.Controls.Add(FolderRow("Куда установить Аврору", installPath, true), 0, 1);
        root.Controls.Add(FolderRow("Папка игры (необязательно)", gamePath, false), 0, 2);

        var note = new Label {
            Text = "Моды устанавливаются в папку игры из настроек Авроры, а не в папку самого лаунчера. Папку игры можно указать позднее.",
            Dock = DockStyle.Fill, AutoSize = false, ForeColor = Color.DimGray
        };
        root.Controls.Add(note, 0, 3);
        status.Text = "Настройки существующей игры и модов сохраняются.";
        root.Controls.Add(status, 0, 4);

        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        footer.Controls.Add(install);
        root.Controls.Add(footer, 0, 5);
        AcceptButton = install;
        install.Click += (_, _) => InstallClicked();
    }

    Control FolderRow(string heading, TextBox edit, bool createFolder)
    {
        var group = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 2, Margin = new Padding(0, 0, 0, 7) };
        group.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        group.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        group.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        group.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        group.Controls.Add(new Label { Text = heading, AutoSize = true, Font = new Font("Segoe UI Semibold", 10) }, 0, 0);
        var browse = new Button { Text = "Обзор...", Dock = DockStyle.Fill };
        browse.Click += (_, _) =>
        {
            using var picker = new FolderBrowserDialog { Description = heading, ShowNewFolderButton = createFolder };
            string existing = edit.Text;
            if (Directory.Exists(existing)) picker.InitialDirectory = existing;
            if (picker.ShowDialog(this) == DialogResult.OK) edit.Text = picker.SelectedPath;
        };
        group.Controls.Add(edit, 0, 1);
        group.Controls.Add(browse, 1, 1);
        return group;
    }

    async void InstallClicked()
    {
        string? temp = null;
        install.Enabled = false;
        installPath.ReadOnly = gamePath.ReadOnly = true;
        try
        {
            string target = InstallationLocation.Normalize(installPath.Text);
            string game = gamePath.Text.Trim();
            if (game.Length > 0)
            {
                game = Path.GetFullPath(game.Trim('"'));
                if (!File.Exists(Path.Combine(game, "SOVIET64.exe")))
                    throw new IOException("Выберите папку игры, содержащую SOVIET64.exe, либо оставьте поле пустым.");
                if (target.Equals(game, StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith(game.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Аврору нужно устанавливать отдельно от папки самой игры.");
            }

            var progress = new Progress<string>(message => status.Text = message);
            temp = Path.Combine(Path.GetTempPath(), "Aurora-Setup-" + Guid.NewGuid().ToString("N"));
            string staged = temp;
            await Task.Run(() =>
            {
                using var embedded = Assembly.GetExecutingAssembly().GetManifestResourceStream("Aurora.Payload")
                    ?? throw new IOException("В установщике отсутствует пакет файлов Авроры.");
                Directory.CreateDirectory(staged);
                ((IProgress<string>)progress).Report("Распаковка файлов...");
                using (var zip = new ZipArchive(embedded, ZipArchiveMode.Read)) zip.ExtractToDirectory(staged);
                ((IProgress<string>)progress).Report("Установка Авроры...");
                Program.Install(staged, target);
                InstallationLocation.Save(target);
                InstallationLocation.SaveGame(game);
            });

            string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            Directory.CreateDirectory(programs);
            string shortcut = Path.Combine(programs, "Аврора.url");
            File.WriteAllText(shortcut, "[InternetShortcut]\r\nURL=" +
                new Uri(Path.Combine(target, "Aurora.Launcher.exe")).AbsoluteUri +
                "\r\nIconFile=" + Path.Combine(target, "Aurora.Launcher.exe") + "\r\nIconIndex=0\r\n");
            Program.Start(target);
            Close();
        }
        catch (Exception ex)
        {
            status.Text = "Установка не завершена.";
            MessageBox.Show(ex.Message, "Аврора", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            install.Enabled = true;
            installPath.ReadOnly = gamePath.ReadOnly = false;
            if (temp != null && Directory.Exists(temp))
                try { await Task.Run(() => Directory.Delete(temp, true)); }
                catch { /* Do not mask the installation outcome. */ }
        }
    }
}
