using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;

namespace SimpleHackintosh.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed record CheckResult(string Severity, string Title, string Detail);

internal sealed class MainForm : Form
{
    private static readonly Color Background = Color.FromArgb(11, 15, 20);
    private static readonly Color Sidebar = Color.FromArgb(14, 20, 27);
    private static readonly Color Panel = Color.FromArgb(17, 24, 33);
    private static readonly Color PanelAlt = Color.FromArgb(21, 31, 42);
    private static readonly Color Line = Color.FromArgb(39, 53, 64);
    private static readonly Color TextMain = Color.FromArgb(238, 244, 246);
    private static readonly Color TextMuted = Color.FromArgb(154, 170, 181);
    private static readonly Color TextSoft = Color.FromArgb(102, 119, 130);
    private static readonly Color Accent = Color.FromArgb(184, 243, 107);
    private static readonly Color Blue = Color.FromArgb(124, 201, 238);
    private static readonly Color Green = Color.FromArgb(158, 231, 174);
    private static readonly Color Yellow = Color.FromArgb(244, 197, 107);
    private static readonly Color Red = Color.FromArgb(255, 141, 135);

    private readonly Panel _content = new() { Dock = DockStyle.Fill, BackColor = Background, Padding = new Padding(28, 20, 28, 24) };
    private readonly Label _pageTitle = new() { AutoSize = true, ForeColor = TextMain, Font = new Font("Segoe UI", 16, FontStyle.Bold), Location = new Point(28, 17) };
    private readonly Label _pageSubtitle = new() { AutoSize = true, ForeColor = TextMuted, Font = new Font("Segoe UI", 9), Location = new Point(30, 48) };
    private readonly Label _status = new() { AutoSize = true, ForeColor = TextSoft, Font = new Font("Segoe UI", 8), Anchor = AnchorStyles.Top | AnchorStyles.Right };
    private readonly Dictionary<string, Button> _nav = new();
    private string _selectedSource = "";
    private TextBox? _hardwareReportBox;
    private CancellationTokenSource? _installerDownloadCts;
    private CancellationTokenSource? _opencoreBuildCts;
    private CancellationTokenSource? _ocvalidateCts;

    public MainForm()
    {
        Text = "Simple Hackintosh";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1060, 700);
        Size = new Size(1280, 820);
        BackColor = Background;
        ForeColor = TextMain;
        Font = new Font("Segoe UI", 9);
        BuildShell();
        ShowPage("home");
    }

    private void BuildShell()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Background, Margin = Padding.Empty };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 238));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var sidebar = new Panel { Dock = DockStyle.Fill, BackColor = Sidebar, Padding = new Padding(18, 22, 16, 18) };
        root.Controls.Add(sidebar, 0, 0);

        var brand = new Label { AutoSize = true, Text = "SH_", ForeColor = Accent, Font = new Font("Consolas", 25, FontStyle.Bold), Location = new Point(20, 20) };
        sidebar.Controls.Add(brand);
        sidebar.Controls.Add(new Label { AutoSize = true, Text = "SIMPLE HACKINTOSH", ForeColor = TextMain, Font = new Font("Segoe UI", 9, FontStyle.Bold), Location = new Point(22, 58) });
        sidebar.Controls.Add(new Label { AutoSize = true, Text = "LOCAL DESKTOP TOOLKIT", ForeColor = TextSoft, Font = new Font("Consolas", 7), Location = new Point(22, 77) });
        sidebar.Controls.Add(new Label { AutoSize = true, Text = "WORKSPACE", ForeColor = TextSoft, Font = new Font("Consolas", 8, FontStyle.Bold), Location = new Point(22, 127) });

        AddNav(sidebar, "home", "⌂   Overview", 154);
        AddNav(sidebar, "efi", "01   EFI Checker", 202);
        AddNav(sidebar, "config", "02   Config Validator", 250);
        AddNav(sidebar, "hardware", "03   Hardware Inspector", 298);
        AddNav(sidebar, "opencore", "04   OpenCore Builder", 346);
        AddNav(sidebar, "installer", "05   macOS Downloader", 394);

        var privacy = new Panel { BackColor = Panel, BorderStyle = BorderStyle.FixedSingle, Location = new Point(18, 500), Size = new Size(194, 116), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
        privacy.Controls.Add(new Label { Text = "●  LOCAL PROCESSING", ForeColor = Green, Font = new Font("Consolas", 8, FontStyle.Bold), Location = new Point(12, 13), AutoSize = true });
        privacy.Controls.Add(new Label { Text = "Files are inspected on this PC.\r\nNothing is uploaded by this app.", ForeColor = TextMuted, Font = new Font("Segoe UI", 8), Location = new Point(12, 39), Size = new Size(164, 43) });
        privacy.Controls.Add(new Label { Text = "SIMPLE HACKINTOSH  ·  0.3.0", ForeColor = TextSoft, Font = new Font("Consolas", 7), Location = new Point(12, 88), AutoSize = true });
        sidebar.Controls.Add(privacy);

        var main = new Panel { Dock = DockStyle.Fill, BackColor = Background };
        root.Controls.Add(main, 1, 0);

        var header = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = Background };
        header.Controls.Add(_pageTitle);
        header.Controls.Add(_pageSubtitle);
        _status.Text = "●  READY  /  FILES STAY LOCAL";
        _status.ForeColor = Green;
        _status.Location = new Point(700, 31);
        header.Controls.Add(_status);
        header.Resize += (_, _) => _status.Location = new Point(Math.Max(28, header.ClientSize.Width - _status.Width - 30), 32);
        main.Controls.Add(_content);
        main.Controls.Add(header);
    }

    private void AddNav(Control parent, string key, string text, int y)
    {
        var button = new Button
        {
            Text = text, Tag = key, Location = new Point(12, y), Size = new Size(200, 39),
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0),
            FlatStyle = FlatStyle.Flat, BackColor = Panel, ForeColor = TextMuted,
            Font = new Font("Segoe UI", 9, FontStyle.Regular), Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Sidebar;
        button.FlatAppearance.MouseOverBackColor = PanelAlt;
        button.Click += (_, _) => ShowPage(key);
        _nav[key] = button;
        parent.Controls.Add(button);
    }

    private void ShowPage(string key)
    {
        foreach (var item in _nav)
        {
            bool active = item.Key == key;
            item.Value.BackColor = active ? Color.FromArgb(34, 49, 42) : Panel;
            item.Value.ForeColor = active ? Accent : TextMuted;
            item.Value.FlatAppearance.BorderColor = active ? Color.FromArgb(63, 91, 57) : Sidebar;
        }

        _content.Controls.Clear();
        Control page;
        switch (key)
        {
            case "efi":
                _pageTitle.Text = "EFI Checker";
                _pageSubtitle.Text = "Inspect an EFI folder or ZIP and check the files referenced by config.plist.";
                page = BuildEfiPage();
                break;
            case "config":
                _pageTitle.Text = "Config Validator";
                _pageSubtitle.Text = "Check XML syntax and common OpenCore configuration structure.";
                page = BuildConfigPage();
                break;
            case "hardware":
                _pageTitle.Text = "Hardware Inspector";
                _pageSubtitle.Text = "Collect local system information and export a hardware report.";
                page = BuildHardwarePage();
                break;
            case "opencore":
                _pageTitle.Text = "OpenCore Builder";
                _pageSubtitle.Text = "Download the official release, verify its SHA-256 digest, and create a safe starter EFI.";
                page = BuildOpenCoreBuilderPage();
                break;
            case "installer":
                _pageTitle.Text = "macOS Downloader";
                _pageSubtitle.Text = "Download Apple's full InstallAssistant package directly to a selected drive.";
                page = BuildMacOSDownloaderPage();
                break;
            default:
                _pageTitle.Text = "Simple Hackintosh";
                _pageSubtitle.Text = "A practical local workspace for OpenCore preparation and diagnostics.";
                page = BuildHomePage();
                break;
        }
        page.Dock = DockStyle.Fill;
        _content.Controls.Add(page);
    }

    private Panel NewPage() => new() { Dock = DockStyle.Fill, BackColor = Background, AutoScroll = true };

    private Control BuildHomePage()
    {
        var page = NewPage();
        var hero = new Panel { Location = new Point(0, 0), Size = new Size(920, 220), BackColor = Panel, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Padding = new Padding(28) };
        hero.Controls.Add(new Label { Text = "OPENCORE  /  DIAGNOSTIC WORKSPACE", ForeColor = Accent, Font = new Font("Consolas", 9, FontStyle.Bold), Location = new Point(28, 28), AutoSize = true });
        hero.Controls.Add(new Label { Text = "Check first.\r\nChange with evidence.", ForeColor = TextMain, Font = new Font("Segoe UI", 26, FontStyle.Bold), Location = new Point(26, 57), Size = new Size(520, 83) });
        hero.Controls.Add(new Label { Text = "Inspect EFI packages, review config.plist, and document your hardware — without uploading personal files.", ForeColor = TextMuted, Font = new Font("Segoe UI", 10), Location = new Point(29, 153), Size = new Size(720, 38) });
        page.Controls.Add(hero);

        var section = new Label { Text = "TOOLS", ForeColor = TextSoft, Font = new Font("Consolas", 9, FontStyle.Bold), Location = new Point(2, 245), AutoSize = true };
        page.Controls.Add(section);
        var cards = new[]
        {
            ("01", "EFI Checker", "Inspect EFI folders and ZIP archives. Compare enabled file references.", "efi"),
            ("02", "Config Validator", "Review XML config.plist structure and required sections.", "config"),
            ("03", "Hardware Inspector", "Collect CPU, GPU, board, storage and network details.", "hardware")
        };
        int x = 0;
        foreach (var card in cards)
        {
            var panel = new Panel { Location = new Point(x, 280), Size = new Size(278, 188), BackColor = Panel, BorderStyle = BorderStyle.FixedSingle };
            panel.Controls.Add(new Label { Text = card.Item1, ForeColor = Accent, Font = new Font("Consolas", 10, FontStyle.Bold), Location = new Point(17, 16), AutoSize = true });
            panel.Controls.Add(new Label { Text = card.Item2, ForeColor = TextMain, Font = new Font("Segoe UI", 13, FontStyle.Bold), Location = new Point(17, 48), AutoSize = true });
            panel.Controls.Add(new Label { Text = card.Item3, ForeColor = TextMuted, Font = new Font("Segoe UI", 8), Location = new Point(18, 83), Size = new Size(235, 48) });
            var open = MakeButton("Open tool  →", true, new Point(17, 140), new Size(240, 32));
            open.Click += (_, _) => ShowPage(card.Item4);
            panel.Controls.Add(open);
            page.Controls.Add(panel);
            x += 295;
        }
        var note = new Panel { Location = new Point(0, 500), Size = new Size(900, 94), BackColor = Color.FromArgb(17, 27, 34), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        note.Controls.Add(new Label { Text = "IMPORTANT", ForeColor = Yellow, Font = new Font("Consolas", 8, FontStyle.Bold), Location = new Point(18, 15), AutoSize = true });
        note.Controls.Add(new Label { Text = "A clean structural report does not prove that an EFI will boot or that the hardware is compatible. Use the official OpenCore documentation and keep a known-good backup.", ForeColor = TextMuted, Font = new Font("Segoe UI", 9), Location = new Point(18, 39), Size = new Size(840, 40) });
        page.Controls.Add(note);
        return page;
    }

    private Control BuildEfiPage()
    {
        var page = NewPage();
        var select = new Panel { Location = new Point(0, 0), Size = new Size(920, 185), BackColor = Panel, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        select.Controls.Add(new Label { Text = "SELECT AN EFI PACKAGE", ForeColor = Accent, Font = new Font("Consolas", 9, FontStyle.Bold), Location = new Point(20, 18), AutoSize = true });
        select.Controls.Add(new Label { Text = "Choose an extracted EFI directory or a ZIP archive. The app reads files in place and never extracts them to disk.", ForeColor = TextMuted, Font = new Font("Segoe UI", 9), Location = new Point(20, 44), Size = new Size(850, 30) });
        var zipButton = MakeButton("Choose ZIP", false, new Point(20, 91), new Size(125, 36));
        var folderButton = MakeButton("Choose folder", false, new Point(155, 91), new Size(135, 36));
        var analyzeButton = MakeButton("Analyze EFI  →", true, new Point(300, 91), new Size(145, 36));
        analyzeButton.Enabled = false;
        var chosen = new Label { Text = "No package selected", ForeColor = TextSoft, Font = new Font("Consolas", 8), Location = new Point(20, 143), Size = new Size(850, 20), AutoEllipsis = true };
        select.Controls.AddRange(new Control[] { zipButton, folderButton, analyzeButton, chosen });
        page.Controls.Add(select);

        var results = NewResultsPanel(new Point(0, 205), new Size(920, 520));
        page.Controls.Add(results);
        zipButton.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "ZIP archives (*.zip)|*.zip", Title = "Select an EFI ZIP archive" };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _selectedSource = dialog.FileName;
                chosen.Text = _selectedSource;
                analyzeButton.Enabled = true;
                results.Controls.Clear();
            }
        };
        folderButton.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { Description = "Select the EFI folder (or a directory containing EFI)" };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _selectedSource = dialog.SelectedPath;
                chosen.Text = _selectedSource;
                analyzeButton.Enabled = true;
                results.Controls.Clear();
            }
        };
        analyzeButton.Click += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_selectedSource)) return;
            analyzeButton.Enabled = false;
            analyzeButton.Text = "Analyzing…";
            _status.Text = "●  ANALYZING LOCALLY";
            _status.ForeColor = Blue;
            try
            {
                var report = await Task.Run(() => EfiAnalyzer.Analyze(_selectedSource));
                RenderResults(results, report, "EFI inspection");
                _status.Text = "●  ANALYSIS COMPLETE";
                _status.ForeColor = Green;
            }
            catch (Exception ex)
            {
                RenderResults(results, new List<CheckResult> { new("error", "Analysis failed", ex.Message) }, "EFI inspection");
                _status.Text = "●  CHECK INPUT";
                _status.ForeColor = Yellow;
            }
            finally
            {
                analyzeButton.Enabled = true;
                analyzeButton.Text = "Analyze EFI  →";
            }
        };
        return page;
    }

    private Control BuildConfigPage()
    {
        var page = NewPage();
        var fileButton = MakeButton("Open config.plist", false, new Point(0, 0), new Size(160, 36));
        var validateButton = MakeButton("Validate configuration  →", true, new Point(170, 0), new Size(220, 36));
        var officialValidateButton = MakeButton("Official ocvalidate", false, new Point(400, 0), new Size(190, 36));
        var cancelOfficialButton = MakeButton("Cancel", false, new Point(600, 0), new Size(90, 36));
        cancelOfficialButton.Enabled = false;
        var input = new TextBox
        {
            Multiline = true, AcceptsReturn = true, AcceptsTab = true, WordWrap = false,
            ScrollBars = ScrollBars.Both, Font = new Font("Consolas", 9),
            BackColor = Color.FromArgb(8, 13, 18), ForeColor = Color.FromArgb(215, 228, 233),
            BorderStyle = BorderStyle.FixedSingle, Location = new Point(0, 54),
            Size = new Size(920, 340), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        var hint = new Label
        {
            Text = "The local check is structural. Official ocvalidate downloads the latest stable Acidanthera validator and must only be used with a config.plist from that exact OpenCore version.",
            ForeColor = TextSoft, Font = new Font("Segoe UI", 8), Location = new Point(2, 402), Size = new Size(920, 38)
        };
        var results = NewResultsPanel(new Point(0, 448), new Size(920, 380));
        page.Controls.AddRange(new Control[] { fileButton, validateButton, officialValidateButton, cancelOfficialButton, input, hint, results });

        fileButton.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "XML plist (*.plist;*.xml)|*.plist;*.xml|All files (*.*)|*.*", Title = "Open config.plist" };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    var info = new FileInfo(dialog.FileName);
                    if (info.Length > 5 * 1024 * 1024) throw new InvalidDataException("The selected file exceeds the 5 MB safety limit.");
                    input.Text = File.ReadAllText(dialog.FileName);
                    results.Controls.Clear();
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not open plist", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        };
        validateButton.Click += (_, _) =>
        {
            try
            {
                var config = PlistParser.Parse(input.Text);
                var report = PlistParser.Validate(config);
                RenderResults(results, report, "Configuration review");
                _status.Text = "●  VALIDATION COMPLETE";
                _status.ForeColor = Green;
            }
            catch (Exception ex)
            {
                RenderResults(results, new List<CheckResult> { new("error", "Invalid XML property list", ex.Message) }, "Configuration review");
                _status.Text = "●  INVALID INPUT";
                _status.ForeColor = Red;
            }
        };

        cancelOfficialButton.Click += (_, _) => _ocvalidateCts?.Cancel();
        officialValidateButton.Click += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(input.Text) || _ocvalidateCts is not null) return;
            _ocvalidateCts = new CancellationTokenSource();
            fileButton.Enabled = false;
            validateButton.Enabled = false;
            officialValidateButton.Enabled = false;
            cancelOfficialButton.Enabled = true;
            input.ReadOnly = true;
            _status.Text = "●  PREPARING OFFICIAL VALIDATOR";
            _status.ForeColor = Blue;
            var reporter = new Progress<OpenCoreProgress>(value =>
            {
                _status.Text = "●  " + value.Stage;
                _status.ForeColor = Blue;
            });
            try
            {
                var result = await OpenCoreReleaseService.ValidateWithLatestOfficialAsync(input.Text, reporter, _ocvalidateCts.Token);
                var details = $"OpenCore version: {result.Version}\r\nValidator exit code: {result.ExitCode}\r\n\r\n{result.Output}";
                RenderResults(results,
                    new List<CheckResult>
                    {
                        new(result.Success ? "ok" : "error",
                            result.Success ? "Official ocvalidate: no issues found" : "Official ocvalidate: configuration needs attention",
                            details)
                    },
                    "Official OpenCore validation");
                _status.Text = result.Success ? "●  OFFICIAL VALIDATION PASSED" : "●  OFFICIAL VALIDATION FOUND ISSUES";
                _status.ForeColor = result.Success ? Green : Yellow;
            }
            catch (OperationCanceledException)
            {
                _status.Text = "●  OFFICIAL VALIDATION CANCELLED";
                _status.ForeColor = TextSoft;
                RenderResults(results, new List<CheckResult> { new("warning", "Validation cancelled", "The download or validator process was cancelled. Temporary files were removed.") }, "Official OpenCore validation");
            }
            catch (Exception ex)
            {
                _status.Text = "●  OFFICIAL VALIDATOR UNAVAILABLE";
                _status.ForeColor = Yellow;
                RenderResults(results, new List<CheckResult> { new("error", "Official validation could not run", ex.Message) }, "Official OpenCore validation");
            }
            finally
            {
                _ocvalidateCts.Dispose();
                _ocvalidateCts = null;
                fileButton.Enabled = true;
                validateButton.Enabled = true;
                officialValidateButton.Enabled = true;
                cancelOfficialButton.Enabled = false;
                input.ReadOnly = false;
            }
        };
        return page;
    }

    private Control BuildMacOSDownloaderPage()
    {
        var page = NewPage();
        var versions = new ComboBox { Location = new Point(0, 20), Size = new Size(550, 34), DropDownStyle = ComboBoxStyle.DropDownList };
        var refresh = MakeButton("Load Apple catalog", true, new Point(565, 18), new Size(180, 36));
        var choose = MakeButton("Choose drive/folder", false, new Point(0, 72), new Size(180, 36));
        var download = MakeButton("Download full installer", true, new Point(190, 72), new Size(200, 36));
        var cancel = MakeButton("Cancel", false, new Point(400, 72), new Size(100, 36));
        var destination = new Label { Text = "No destination selected", ForeColor = TextMuted, Location = new Point(0, 122), Size = new Size(850, 24), AutoEllipsis = true };
        var progress = new ProgressBar { Location = new Point(0, 154), Size = new Size(850, 18), Minimum = 0, Maximum = 100 };
        var status = new Label { Text = "Downloads Apple's full InstallAssistant.pkg directly to your selected drive. No Recovery image.", ForeColor = TextMuted, Location = new Point(0, 184), Size = new Size(850, 52) };
        page.Controls.AddRange(new Control[] { versions, refresh, choose, download, cancel, destination, progress, status });
        string? folder = null;
        download.Enabled = false;
        cancel.Enabled = false;

        choose.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { Description = "Select the drive or folder for the macOS installer package." };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                folder = dialog.SelectedPath;
                destination.Text = folder;
                download.Enabled = versions.SelectedItem is AppleInstallerOption;
            }
        };
        refresh.Click += async (_, _) =>
        {
            refresh.Enabled = false;
            status.Text = "Reading Apple's public full-installer catalog…";
            try
            {
                var entries = await AppleInstallerCatalog.GetInstallersAsync();
                versions.DataSource = entries.ToList();
                versions.DisplayMember = nameof(AppleInstallerOption.Title);
                versions.ValueMember = nameof(AppleInstallerOption.ProductId);
                status.Text = $"Loaded {entries.Count} full installer(s) from Apple.";
                download.Enabled = entries.Count > 0 && folder is not null;
            }
            catch (Exception ex) { status.Text = "Catalog error: " + ex.Message; }
            finally { refresh.Enabled = true; }
        };
        versions.SelectedIndexChanged += (_, _) => download.Enabled = versions.SelectedItem is AppleInstallerOption && folder is not null && _installerDownloadCts is null;
        cancel.Click += (_, _) => _installerDownloadCts?.Cancel();
        download.Click += async (_, _) =>
        {
            if (versions.SelectedItem is not AppleInstallerOption selected || folder is null) return;
            _installerDownloadCts = new CancellationTokenSource();
            download.Enabled = false; refresh.Enabled = false; choose.Enabled = false; cancel.Enabled = true;
            var reporter = new Progress<(long Received, long? Expected)>(value =>
            {
                if (value.Expected is > 0)
                {
                    progress.Style = ProgressBarStyle.Continuous;
                    progress.Value = (int)Math.Clamp(value.Received * 100L / value.Expected.Value, 0, 100);
                    status.Text = $"{value.Received / 1073741824d:0.00} GB / {value.Expected.Value / 1073741824d:0.00} GB";
                }
                else { progress.Style = ProgressBarStyle.Marquee; status.Text = $"{value.Received / 1073741824d:0.00} GB downloaded"; }
            });
            try
            {
                var path = await AppleInstallerCatalog.DownloadAsync(selected, folder, reporter, _installerDownloadCts.Token);
                status.Text = "Saved to " + path;
                MessageBox.Show(this, "Full installer package saved to:\n\n" + path, "Download complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException) { status.Text = "Download cancelled. Partial data was removed."; }
            catch (Exception ex) { status.Text = "Download failed: " + ex.Message; }
            finally
            {
                _installerDownloadCts.Dispose(); _installerDownloadCts = null;
                download.Enabled = versions.SelectedItem is AppleInstallerOption && folder is not null;
                refresh.Enabled = true; choose.Enabled = true; cancel.Enabled = false;
                progress.Style = ProgressBarStyle.Continuous;
            }
        };
        return page;
    }

    private Control BuildOpenCoreBuilderPage()
    {
        var page = NewPage();
        var intro = new Label
        {
            Text = "Fetch the latest stable OpenCore RELEASE from Acidanthera's official GitHub repository. The archive digest is checked before any EFI folder is created.",
            ForeColor = TextMuted, Font = new Font("Segoe UI", 9), Location = new Point(0, 8), Size = new Size(900, 42)
        };
        var check = MakeButton("Check official release", false, new Point(0, 64), new Size(180, 36));
        var choose = MakeButton("Choose destination", false, new Point(190, 64), new Size(170, 36));
        var build = MakeButton("Download + build starter", true, new Point(370, 64), new Size(205, 36));
        var cancel = MakeButton("Cancel", false, new Point(585, 64), new Size(100, 36));
        var selected = new Label { Text = "No destination selected", ForeColor = TextSoft, Font = new Font("Consolas", 8), Location = new Point(0, 112), Size = new Size(880, 24), AutoEllipsis = true };
        var releaseLabel = new Label { Text = "Release not checked yet", ForeColor = TextMuted, Font = new Font("Segoe UI", 9), Location = new Point(0, 146), Size = new Size(900, 52), AutoEllipsis = true };
        var progress = new ProgressBar { Location = new Point(0, 208), Size = new Size(880, 18), Minimum = 0, Maximum = 100 };
        var status = new Label
        {
            Text = "The app will not overwrite an existing EFI and will not modify partitions, firmware settings or boot entries.",
            ForeColor = TextMuted, Font = new Font("Segoe UI", 9), Location = new Point(0, 238), Size = new Size(900, 64)
        };
        var warning = new Panel { Location = new Point(0, 320), Size = new Size(880, 150), BackColor = Panel };
        warning.Controls.Add(new Label { Text = "STARTER EFI — NOT READY TO BOOT", ForeColor = Yellow, Font = new Font("Consolas", 9, FontStyle.Bold), Location = new Point(18, 18), AutoSize = true });
        warning.Controls.Add(new Label
        {
            Text = "The builder copies the official x64 EFI folder and the matching Sample.plist. It does not choose ACPI, Kexts, SMBIOS, kernel quirks or firmware drivers for your hardware. Configure the result for the target PC and run the matching ocvalidate before testing from removable media.",
            ForeColor = TextMuted, Font = new Font("Segoe UI", 9), Location = new Point(18, 48), Size = new Size(835, 82)
        });
        page.Controls.AddRange(new Control[] { intro, check, choose, build, cancel, selected, releaseLabel, progress, status, warning });

        string? destination = null;
        OpenCoreReleaseInfo? latest = null;
        build.Enabled = false;
        cancel.Enabled = false;

        choose.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { Description = "Choose an existing folder where a new EFI folder can be created." };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                destination = dialog.SelectedPath;
                selected.Text = destination;
                build.Enabled = latest is not null && _opencoreBuildCts is null;
            }
        };

        check.Click += async (_, _) =>
        {
            check.Enabled = false;
            status.Text = "Checking the official Acidanthera release metadata…";
            try
            {
                latest = await OpenCoreReleaseService.GetLatestReleaseAsync();
                releaseLabel.Text = $"Latest stable release: OpenCore {latest.Version}   ·   {latest.AssetName}\r\nSHA-256: {latest.Sha256}";
                status.Text = "Official GitHub release metadata loaded. The digest will be checked again against the downloaded archive.";
                build.Enabled = destination is not null && _opencoreBuildCts is null;
            }
            catch (Exception ex)
            {
                latest = null;
                releaseLabel.Text = "Could not verify official release metadata.";
                status.Text = "Release check failed: " + ex.Message;
                build.Enabled = false;
            }
            finally { check.Enabled = true; }
        };

        cancel.Click += (_, _) => _opencoreBuildCts?.Cancel();

        build.Click += async (_, _) =>
        {
            if (destination is null || latest is null || _opencoreBuildCts is not null) return;
            _opencoreBuildCts = new CancellationTokenSource();
            build.Enabled = false; check.Enabled = false; choose.Enabled = false; cancel.Enabled = true;
            progress.Style = ProgressBarStyle.Continuous;
            progress.Value = 0;
            var reporter = new Progress<OpenCoreProgress>(value =>
            {
                status.Text = value.Stage;
                if (value.Expected is > 0)
                {
                    progress.Style = ProgressBarStyle.Continuous;
                    progress.Value = (int)Math.Clamp(value.Received * 100L / value.Expected.Value, 0, 100);
                }
                else progress.Style = ProgressBarStyle.Marquee;
            });
            try
            {
                var result = await OpenCoreReleaseService.BuildStarterAsync(destination, reporter, _opencoreBuildCts.Token);
                releaseLabel.Text = $"Created OpenCore {result.Version} starter EFI   ·   SHA-256: {result.Sha256}";
                status.Text = "Starter created at: " + result.EfiPath;
                MessageBox.Show(this,
                    "The verified OpenCore starter was created at:\r\n\r\n" + result.EfiPath +
                    "\r\n\r\nIt is a template, not a ready-to-boot EFI. Read EFI/READ-ME-FIRST.txt.",
                    "OpenCore starter created", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException) { status.Text = "Build cancelled. Temporary files were removed; no EFI was created."; }
            catch (Exception ex) { status.Text = "OpenCore build failed: " + ex.Message; }
            finally
            {
                _opencoreBuildCts.Dispose();
                _opencoreBuildCts = null;
                build.Enabled = destination is not null && latest is not null;
                check.Enabled = true; choose.Enabled = true; cancel.Enabled = false;
                progress.Style = ProgressBarStyle.Continuous;
            }
        };
        return page;
    }

    private Control BuildHardwarePage()
    {
        var page = NewPage();
        page.Controls.Add(new Label { Text = "Read hardware information from Windows using built-in CIM queries. No third-party utility is installed.", ForeColor = TextMuted, Font = new Font("Segoe UI", 9), Location = new Point(0, 0), Size = new Size(900, 32) });
        var detect = MakeButton("Detect this PC  →", true, new Point(0, 48), new Size(170, 36));
        var copy = MakeButton("Copy report", false, new Point(182, 48), new Size(120, 36));
        var save = MakeButton("Save Markdown", false, new Point(312, 48), new Size(140, 36));
        var exportProfile = MakeButton("Export profile JSON", false, new Point(465, 48), new Size(150, 36));
        var importProfile = MakeButton("Import profile JSON", false, new Point(625, 48), new Size(150, 36));
        _hardwareReportBox = new TextBox
        {
            Multiline = true, ReadOnly = true, WordWrap = false, ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", 9), BackColor = Color.FromArgb(8, 13, 18),
            ForeColor = Color.FromArgb(215, 228, 233), BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(0, 102), Size = new Size(920, 500),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _hardwareReportBox.Text = "Select “Detect this PC” to collect hardware details.\r\n\r\nThe Windows hardware query is read-only. Some device details may be unavailable without vendor-specific tools.";
        page.Controls.AddRange(new Control[] { detect, copy, save, exportProfile, importProfile, _hardwareReportBox });

        detect.Click += async (_, _) =>
        {
            detect.Enabled = false;
            detect.Text = "Detecting…";
            _status.Text = "●  QUERYING WINDOWS";
            _status.ForeColor = Blue;
            try
            {
                _hardwareReportBox.Text = await HardwareInspector.CollectAsync();
                _status.Text = "●  REPORT READY";
                _status.ForeColor = Green;
            }
            catch (Exception ex)
            {
                _hardwareReportBox.Text = "Hardware collection could not be completed.\r\n\r\n" + ex.Message + "\r\n\r\nYou can still add information manually in the report template or use the website Hardware Inspector.";
                _status.Text = "●  DETECTION UNAVAILABLE";
                _status.ForeColor = Yellow;
            }
            finally { detect.Enabled = true; detect.Text = "Detect this PC  →"; }
        };
        copy.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_hardwareReportBox.Text))
            {
                Clipboard.SetText(_hardwareReportBox.Text);
                _status.Text = "●  REPORT COPIED";
                _status.ForeColor = Green;
            }
        };
        exportProfile.Click += async (_, _) =>
        {
            exportProfile.Enabled = false;
            _status.Text = "●  EXPORTING HARDWARE PROFILE";
            _status.ForeColor = Blue;
            try
            {
                string json = await HardwareProfileService.CollectProfileAsync();
                using var dialog = new SaveFileDialog
                {
                    Filter = "Hardware profile JSON (*.json)|*.json",
                    FileName = "simple-hackintosh-hardware-profile-v1.json",
                    Title = "Export privacy-conscious hardware profile"
                };
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    File.WriteAllText(dialog.FileName, json, new UTF8Encoding(false));
                    _status.Text = "●  PROFILE EXPORTED";
                    _status.ForeColor = Green;
                    _hardwareReportBox.Text = HardwareProfileService.BuildSummary(json) + Environment.NewLine + Environment.NewLine +
                        "Saved to: " + dialog.FileName + Environment.NewLine +
                        "Sensitive serial, UUID and MAC-address fields are not included.";
                }
                else
                {
                    _status.Text = "●  EXPORT CANCELLED";
                    _status.ForeColor = TextSoft;
                }
            }
            catch (Exception ex)
            {
                _status.Text = "●  PROFILE EXPORT FAILED";
                _status.ForeColor = Yellow;
                MessageBox.Show(this, ex.Message, "Could not export hardware profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { exportProfile.Enabled = true; }
        };

        importProfile.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Hardware profile JSON (*.json)|*.json",
                Title = "Import Hardware Profile Schema v1"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                var info = new FileInfo(dialog.FileName);
                if (info.Length > 2 * 1024 * 1024) throw new InvalidDataException("The selected profile exceeds the 2 MB safety limit.");
                string json = File.ReadAllText(dialog.FileName, Encoding.UTF8);
                string validation = HardwareProfileService.ValidateProfile(json);
                _hardwareReportBox.Text = HardwareProfileService.BuildSummary(json) + Environment.NewLine + Environment.NewLine +
                    validation + Environment.NewLine + "Imported from: " + dialog.FileName;
                _status.Text = "●  PROFILE IMPORTED / SCHEMA V1";
                _status.ForeColor = Green;
            }
            catch (Exception ex)
            {
                _status.Text = "●  PROFILE REJECTED";
                _status.ForeColor = Yellow;
                MessageBox.Show(this, ex.Message, "Invalid hardware profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_hardwareReportBox.Text)) return;
            using var dialog = new SaveFileDialog { Filter = "Markdown report (*.md)|*.md|Text file (*.txt)|*.txt", FileName = "simple-hackintosh-hardware-report.md", Title = "Save hardware report" };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                File.WriteAllText(dialog.FileName, _hardwareReportBox.Text, new UTF8Encoding(false));
                _status.Text = "●  REPORT SAVED";
                _status.ForeColor = Green;
            }
        };
        return page;
    }

    private static FlowLayoutPanel NewResultsPanel(Point location, Size size)
    {
        return new FlowLayoutPanel
        {
            Location = location, Size = size, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true,
            BackColor = Background, Padding = new Padding(0, 4, 8, 4)
        };
    }

    private static void RenderResults(FlowLayoutPanel target, IReadOnlyList<CheckResult> results, string title)
    {
        target.SuspendLayout();
        target.Controls.Clear();
        var failures = results.Count(x => x.Severity == "error");
        var warnings = results.Count(x => x.Severity == "warning");
        var passed = results.Count(x => x.Severity == "ok");
        var heading = new Label
        {
            Text = $"{title}     {passed} passed   ·   {warnings} warnings   ·   {failures} issues",
            ForeColor = TextMain, Font = new Font("Segoe UI", 10, FontStyle.Bold),
            AutoSize = true, Margin = new Padding(0, 0, 0, 12)
        };
        target.Controls.Add(heading);
        foreach (var result in results)
        {
            var color = result.Severity switch { "ok" => Green, "warning" => Yellow, _ => Red };
            var card = new Panel
            {
                Width = Math.Max(600, target.ClientSize.Width - 32), Height = 66, BackColor = Panel,
                Margin = new Padding(0, 0, 0, 7), Padding = new Padding(12)
            };
            card.Controls.Add(new Label { Text = result.Severity == "ok" ? "✓" : result.Severity == "warning" ? "!" : "×", ForeColor = color, Font = new Font("Segoe UI", 12, FontStyle.Bold), Location = new Point(10, 10), Size = new Size(22, 25) });
            card.Controls.Add(new Label { Text = result.Title, ForeColor = TextMain, Font = new Font("Segoe UI", 9, FontStyle.Bold), Location = new Point(38, 8), Size = new Size(card.Width - 58, 20), AutoEllipsis = true });
            card.Controls.Add(new Label { Text = result.Detail, ForeColor = TextMuted, Font = new Font("Segoe UI", 8), Location = new Point(38, 30), Size = new Size(card.Width - 58, 30), AutoEllipsis = true });
            target.Controls.Add(card);
        }
        target.ResumeLayout();
    }

    private static Button MakeButton(string text, bool primary, Point location, Size size)
    {
        var button = new Button
        {
            Text = text, Location = location, Size = size, Cursor = Cursors.Hand,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold),
            BackColor = primary ? Accent : PanelAlt, ForeColor = primary ? Color.FromArgb(21, 32, 11) : TextMain
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = primary ? Accent : Line;
        button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(203, 255, 139) : Color.FromArgb(31, 46, 59);
        button.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(163, 221, 83) : Panel;
        return button;
    }
}

internal static class PlistParser
{
    public static Dictionary<string, object?> Parse(string xmlText)
    {
        if (string.IsNullOrWhiteSpace(xmlText)) throw new InvalidDataException("The plist is empty.");
        if (xmlText.Length > 5 * 1024 * 1024) throw new InvalidDataException("The XML exceeds the 5 MB safety limit.");
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 5 * 1024 * 1024 };
        using var stringReader = new StringReader(xmlText);
        using var reader = XmlReader.Create(stringReader, settings);
        var document = XDocument.Load(reader, LoadOptions.None);
        var root = document.Root;
        if (root?.Name.LocalName != "plist") throw new InvalidDataException("The XML root element must be <plist>.");
        var value = root.Elements().FirstOrDefault();
        if (value?.Name.LocalName != "dict") throw new InvalidDataException("The top-level plist value must be a dictionary (<dict>).");
        return (Dictionary<string, object?>)ParseValue(value);
    }

    private static object ParseValue(XElement element)
    {
        switch (element.Name.LocalName)
        {
            case "dict":
                var dictionary = new Dictionary<string, object?>(StringComparer.Ordinal);
                var children = element.Elements().ToArray();
                if (children.Length % 2 != 0) throw new InvalidDataException("A plist dictionary contains a key without a matching value.");
                for (int i = 0; i < children.Length; i += 2)
                {
                    if (children[i].Name.LocalName != "key") throw new InvalidDataException("A plist dictionary contains a value where a key was expected.");
                    var key = children[i].Value;
                    if (!dictionary.TryAdd(key, ParseValue(children[i + 1]))) throw new InvalidDataException($"Duplicate plist key: {key}");
                }
                return dictionary;
            case "array":
                return element.Elements().Select(ParseValue).ToList();
            case "true":
                return true;
            case "false":
                return false;
            case "integer":
                return long.TryParse(element.Value, out var integer) ? integer : element.Value;
            case "real":
                return double.TryParse(element.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var real) ? real : element.Value;
            case "data":
            case "date":
            case "string":
                return element.Value;
            default:
                throw new InvalidDataException($"Unsupported plist value type: {element.Name.LocalName}");
        }
    }

    public static List<CheckResult> Validate(Dictionary<string, object?> config)
    {
        var checks = new List<CheckResult>();
        string[] required = ["ACPI", "Booter", "DeviceProperties", "Kernel", "Misc", "NVRAM", "PlatformInfo", "UEFI"];
        foreach (var key in required)
        {
            bool valid = config.TryGetValue(key, out var value) && value is Dictionary<string, object?>;
            checks.Add(new CheckResult(valid ? "ok" : "error", $"Top-level section: {key}", valid ? "Section exists as a dictionary." : "Required OpenCore section is missing or is not a dictionary."));
        }

        (string Section, string Key, string Type)[] pairs =
        [
            ("ACPI", "Add", "array"), ("Booter", "Quirks", "dict"), ("DeviceProperties", "Add", "dict"),
            ("Kernel", "Add", "array"), ("Kernel", "Quirks", "dict"), ("Misc", "Boot", "dict"),
            ("Misc", "Security", "dict"), ("NVRAM", "Add", "dict"), ("PlatformInfo", "Generic", "dict"),
            ("UEFI", "Drivers", "array"), ("UEFI", "Output", "dict"), ("UEFI", "Quirks", "dict")
        ];

        foreach (var (section, key, type) in pairs)
        {
            var value = GetNested(config, section, key);
            bool valid = type == "array" ? value is List<object?> : value is Dictionary<string, object?>;
            checks.Add(new CheckResult(valid ? "ok" : "warning", $"{section} → {key}", valid ? "Expected setting is present with a compatible container type." : "Setting is absent or has an unexpected type. Confirm it against the schema for your OpenCore version."));
        }

        if (GetNested(config, "Kernel", "Add") is List<object?> kexts)
        {
            int enabled = kexts.OfType<Dictionary<string, object?>>().Count(item => item.TryGetValue("Enabled", out var v) && v is true);
            checks.Add(new CheckResult("ok", "Kernel → Add entries", $"{kexts.Count} entries found; {enabled} currently enabled."));
        }
        if (GetNested(config, "UEFI", "Drivers") is List<object?> drivers)
        {
            var enabled = drivers.OfType<Dictionary<string, object?>>()
                .Where(item => item.TryGetValue("Enabled", out var v) && v is true)
                .Select(item => item.TryGetValue("Path", out var path) ? path?.ToString() : null)
                .Where(path => !string.IsNullOrWhiteSpace(path));
            checks.Add(new CheckResult(enabled.Any() ? "ok" : "warning", "Enabled UEFI drivers", enabled.Any() ? string.Join(", ", enabled) : "No enabled driver path was found."));
        }
        return checks;
    }

    public static object? GetNested(Dictionary<string, object?> root, string section, string key)
    {
        if (root.TryGetValue(section, out var parent) && parent is Dictionary<string, object?> dictionary && dictionary.TryGetValue(key, out var value))
            return value;
        return null;
    }
}

internal static class EfiAnalyzer
{
    public static List<CheckResult> Analyze(string source)
    {
        if (File.Exists(source) && string.Equals(Path.GetExtension(source), ".zip", StringComparison.OrdinalIgnoreCase))
            return AnalyzeZip(source);
        if (Directory.Exists(source))
            return AnalyzeFolder(source);
        throw new FileNotFoundException("The selected EFI folder or ZIP archive could not be found.");
    }

    private static List<CheckResult> AnalyzeFolder(string folder)
    {
        var files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).ToArray();
        var paths = files.Select(file => Normalize(Path.GetRelativePath(folder, file))).ToArray();
        var configIndex = Array.FindIndex(paths, path => path.EndsWith("OC/config.plist", StringComparison.OrdinalIgnoreCase));
        if (configIndex < 0) return MissingConfig();
        string configText = File.ReadAllText(files[configIndex]);
        return AnalyzePaths(paths, configText, paths[configIndex]);
    }

    private static List<CheckResult> AnalyzeZip(string zipPath)
    {
        var info = new FileInfo(zipPath);
        if (info.Length > 150L * 1024 * 1024) throw new InvalidDataException("The ZIP archive is larger than the 150 MB safety limit.");
        using var archive = ZipFile.OpenRead(zipPath);
        if (archive.Entries.Count > 12000) throw new InvalidDataException("The archive contains too many entries.");
        var entries = archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)).ToArray();
        var configEntry = entries.FirstOrDefault(entry => Normalize(entry.FullName).EndsWith("OC/config.plist", StringComparison.OrdinalIgnoreCase));
        if (configEntry is null) return MissingConfig();
        if (configEntry.Length > 5 * 1024 * 1024) throw new InvalidDataException("config.plist is larger than the 5 MB safety limit.");
        using var reader = new StreamReader(configEntry.Open(), Encoding.UTF8, true);
        string configText = reader.ReadToEnd();
        var paths = entries.Select(entry => Normalize(entry.FullName)).ToArray();
        return AnalyzePaths(paths, configText, Normalize(configEntry.FullName));
    }

    private static List<CheckResult> AnalyzePaths(IReadOnlyList<string> paths, string configText, string configPath)
    {
        var checks = new List<CheckResult>();
        int marker = configPath.LastIndexOf("OC/config.plist", StringComparison.OrdinalIgnoreCase);
        string ocPrefix = marker >= 0 ? configPath[..marker] + "OC/" : "OC/";
        var pathSet = new HashSet<string>(paths.Select(path => Normalize(path).ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);
        bool Has(string relative) => pathSet.Contains(Normalize(ocPrefix + relative).ToLowerInvariant()) ||
                                     pathSet.Any(item => item.StartsWith(Normalize(ocPrefix + relative).ToLowerInvariant() + "/", StringComparison.OrdinalIgnoreCase));

        checks.Add(new CheckResult(Has("OpenCore.efi") ? "ok" : "error", "OC/OpenCore.efi", Has("OpenCore.efi") ? "OpenCore.efi was found in the OC directory." : "OpenCore.efi is missing from the OC directory."));
        bool bootloader = paths.Any(path => path.EndsWith("BOOT/BOOTx64.efi", StringComparison.OrdinalIgnoreCase));
        checks.Add(new CheckResult(bootloader ? "ok" : "warning", "BOOT/BOOTx64.efi", bootloader ? "The x64 UEFI fallback loader was found." : "The x64 fallback loader was not found. Confirm the selected package layout."));
        checks.Add(new CheckResult(Has("Drivers/OpenRuntime.efi") ? "ok" : "warning", "OpenRuntime.efi", Has("Drivers/OpenRuntime.efi") ? "Found in OC/Drivers." : "Not found in OC/Drivers. Confirm whether it is required for your setup."));

        int drivers = paths.Count(path => path.StartsWith(ocPrefix + "Drivers/", StringComparison.OrdinalIgnoreCase) && path.EndsWith(".efi", StringComparison.OrdinalIgnoreCase));
        int acpi = paths.Count(path => path.StartsWith(ocPrefix + "ACPI/", StringComparison.OrdinalIgnoreCase) && path.EndsWith(".aml", StringComparison.OrdinalIgnoreCase));
        int kextInfo = paths.Count(path => path.Contains(".kext/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("Info.plist", StringComparison.OrdinalIgnoreCase));
        checks.Add(new CheckResult(drivers > 0 ? "ok" : "warning", "OC/Drivers", $"{drivers} EFI driver file(s) found."));
        checks.Add(new CheckResult(kextInfo > 0 ? "ok" : "warning", "Kext bundles", $"{kextInfo} Info.plist file(s) inside Kext bundles found."));
        checks.Add(new CheckResult(acpi > 0 ? "ok" : "warning", "ACPI tables", $"{acpi} AML file(s) found. An empty ACPI folder can be legitimate for some configurations."));

        try
        {
            var config = PlistParser.Parse(configText);
            checks.Add(new CheckResult("ok", "config.plist XML", "The file parses as an XML property list with a top-level dictionary."));
            AddReferenceChecks(checks, config, pathSet, ocPrefix);
        }
        catch (Exception ex)
        {
            checks.Add(new CheckResult("error", "config.plist parsing", ex.Message));
        }
        return checks;
    }

    private static void AddReferenceChecks(List<CheckResult> checks, Dictionary<string, object?> config, HashSet<string> pathSet, string ocPrefix)
    {
        void CheckReferences(string section, string key, string property, string subfolder, string label)
        {
            var value = PlistParser.GetNested(config, section, key);
            if (value is not List<object?> list) return;
            foreach (var item in list.OfType<Dictionary<string, object?>>())
            {
                bool enabled = item.TryGetValue("Enabled", out var enabledValue) && enabledValue is true;
                if (!enabled || !item.TryGetValue(property, out var pathValue) || string.IsNullOrWhiteSpace(pathValue?.ToString())) continue;
                string relative = Normalize(subfolder + "/" + pathValue);
                string expected = Normalize(ocPrefix + relative).ToLowerInvariant();
                bool found = pathSet.Contains(expected) || pathSet.Any(path => path.StartsWith(expected + "/", StringComparison.OrdinalIgnoreCase));
                checks.Add(new CheckResult(found ? "ok" : "error", $"{label} path: {pathValue}", found ? $"Found under OC/{subfolder}." : $"Enabled entry was not found under OC/{subfolder}."));
            }
        }

        CheckReferences("ACPI", "Add", "Path", "ACPI", "ACPI");
        CheckReferences("Kernel", "Add", "BundlePath", "Kexts", "Kext");
        CheckReferences("UEFI", "Drivers", "Path", "Drivers", "Driver");
    }

    private static List<CheckResult> MissingConfig() =>
        [new CheckResult("error", "OC/config.plist", "Could not find OC/config.plist. Select the EFI folder itself or a ZIP containing EFI/OC/config.plist.")];

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');
}

internal static class HardwareInspector
{
    private const string PowerShellScript = "$ErrorActionPreference='SilentlyContinue'; " +
        "$cpu=(Get-CimInstance Win32_Processor | Select-Object -First 1 -ExpandProperty Name); " +
        "$gpu=(Get-CimInstance Win32_VideoController | ForEach-Object { $_.Name + ' [' + $_.PNPDeviceID + ']' }) -join '; '; " +
        "$board=(Get-CimInstance Win32_BaseBoard | ForEach-Object { $_.Manufacturer + ' ' + $_.Product }) -join '; '; " +
        "$bios=(Get-CimInstance Win32_BIOS | Select-Object -First 1 -ExpandProperty SMBIOSBIOSVersion); " +
        "$disks=(Get-CimInstance Win32_DiskDrive | ForEach-Object { $_.Model + ' (' + $_.InterfaceType + ')' }) -join '; '; " +
        "$net=(Get-CimInstance Win32_NetworkAdapter | Where-Object { $_.PhysicalAdapter -and $_.PNPDeviceID } | ForEach-Object { $_.Name + ' [' + $_.PNPDeviceID + ']' }) -join '; '; " +
        "$audio=(Get-CimInstance Win32_SoundDevice | ForEach-Object { $_.Name }) -join '; '; " +
        "$os=(Get-CimInstance Win32_OperatingSystem | Select-Object -First 1 -ExpandProperty Caption); " +
        "[pscustomobject]@{Processor=$cpu;Graphics=$gpu;BaseBoard=$board;Firmware=$bios;Storage=$disks;Network=$net;Audio=$audio;OperatingSystem=$os} | ConvertTo-Json -Compress";

    public static async Task<string> CollectAsync()
    {
        var start = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add(PowerShellScript);

        using var process = new Process { StartInfo = start };
        if (!process.Start()) throw new InvalidOperationException("Could not start Windows PowerShell.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { }
            throw new TimeoutException("Windows hardware queries exceeded 25 seconds.");
        }

        string output = await outputTask;
        string error = await errorTask;
        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "Windows returned no hardware information." : error.Trim());

        using var document = JsonDocument.Parse(output);
        var root = document.RootElement;
        string Read(string key) => root.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : "Not reported";
        var lines = new List<string>
        {
            "# Simple Hackintosh — Windows Hardware Report",
            "",
            $"- Generated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}",
            "- Collection method: Windows CIM queries (read-only)",
            "",
            "## System",
            $"- Operating system: {Read("OperatingSystem")}",
            "",
            "## Hardware inventory",
            $"- CPU: {Read("Processor")}",
            $"- Graphics: {Read("Graphics")}",
            $"- Motherboard: {Read("BaseBoard")}",
            $"- BIOS: {Read("Firmware")}",
            $"- Storage: {Read("Storage")}",
            $"- Network adapters: {Read("Network")}",
            $"- Audio: {Read("Audio")}",
            "",
            "## Notes",
            "- This report describes what Windows reports; missing or generic device names may require checking hardware IDs manually.",
            "- PCI IDs are included where Windows exposes PNPDeviceID values.",
            "- This is not a compatibility verdict and does not generate or modify an EFI."
        };

        string report = string.Join(Environment.NewLine, lines);
        return report;
    }
}
