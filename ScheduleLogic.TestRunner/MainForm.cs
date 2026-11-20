using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using ScheduleLogic.TestKit.Http;
using ScheduleLogic.TestKit.Testing;

// The ScheduleLogic.TestRunner namespace would hide this class.
using Runner = ScheduleLogic.TestKit.Testing.TestRunner;

namespace ScheduleLogic.TestRunner;

internal sealed partial class MainForm : Form
{
    private readonly RunnerSettings _settings = RunnerSettings.Load();

    private ComboBox _label = null!;
    private TextBox _url = null!;
    private CheckBox _ignoreCert = null!;
    private ToolStripStatusLabel _status = null!;

    private readonly List<Control> _runControls = new();
    private readonly List<Button> _stopButtons = new();
    private CancellationTokenSource? _cts;
    private bool _busy;

    private static readonly Font MonoFont = new("Consolas", 9f);

    public MainForm()
    {
        Text = "ScheduleLogic – API tests and load testing";
        Width = 1280;
        Height = 860;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildTestsPage());
        tabs.TabPages.Add(BuildComparePage());
        tabs.TabPages.Add(BuildLoadPage());

        var statusStrip = new StatusStrip();
        _status = new ToolStripStatusLabel("Ready.") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        statusStrip.Items.Add(_status);

        Controls.Add(tabs);
        Controls.Add(BuildTargetBar());
        Controls.Add(statusStrip);

        Shown += (_, _) =>
        {
            InitSplitters();
            RefreshReportLists();
        };
        FormClosing += (_, _) =>
        {
            _cts?.Cancel();
            SaveSettings();
        };
    }

    private Control BuildTargetBar()
    {
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(6, 6, 6, 2),
            WrapContents = false,
        };

        _label = new ComboBox { Width = 90, DropDownStyle = ComboBoxStyle.DropDown };
        _label.Items.AddRange(new object[] { ".NET", "PHP" });
        _label.Text = _settings.Label;

        _url = new TextBox { Width = 260, Text = _settings.Url };
        _ignoreCert = new CheckBox
        {
            Text = "Ignore certificate errors (development certificate)",
            Checked = _settings.IgnoreCertificateErrors,
            AutoSize = true,
            Margin = new Padding(10, 6, 3, 3),
        };

        var check = MakeButton("Check connection", async (_, _) => await CheckConnection());
        _runControls.Add(check);

        bar.Controls.Add(MakeLabel("Backend:"));
        bar.Controls.Add(_label);
        bar.Controls.Add(MakeLabel("Address:"));
        bar.Controls.Add(_url);
        bar.Controls.Add(_ignoreCert);
        bar.Controls.Add(check);
        bar.Controls.Add(MakeLabel("The .NET and PHP backends run on the same port: the label tells which one was started.",
                                   Color.DimGray));
        return bar;
    }

    private BackendTarget CurrentTarget()
    {
        var label = string.IsNullOrWhiteSpace(_label.Text) ? "backend" : _label.Text.Trim();
        var url = _url.Text.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            throw new ArgumentException($"Invalid address: '{url}'. Example: https://localhost:7098");
        return new BackendTarget(label, url, _ignoreCert.Checked);
    }

    private async Task CheckConnection()
    {
        BackendTarget target;
        try { target = CurrentTarget(); }
        catch (ArgumentException ex) { ShowError(ex.Message); return; }

        SetBusy(true);
        try
        {
            var smoke = ApiTests.All.First(t => t.Id == "SMK-01");
            var r = await Task.Run(() => Runner.RunAsync(smoke, target));
            if (r.Status == TestStatus.Passed)
                SetStatus($"The backend is reachable: {target}.");
            else
                ShowError($"The backend does not respond correctly ({target}):\n\n{r.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

        private void SetBusy(bool busy)
    {
        _busy = busy;
        foreach (var c in _runControls) c.Enabled = !busy;
        foreach (var b in _stopButtons) b.Enabled = busy;
        _label.Enabled = _url.Enabled = _ignoreCert.Enabled = !busy;
        UseWaitCursor = false;
        if (!busy)
        {
            _cts?.Dispose();
            _cts = null;
        }
    }

        private void Ui(Action action)
    {
        if (IsDisposed || Disposing) return;
        if (InvokeRequired) BeginInvoke(action);
        else action();
    }

    private void SetStatus(string text) => Ui(() => _status.Text = text);

    private void ShowError(string message) =>
        Ui(() => MessageBox.Show(this, message, "ScheduleLogic test runner", MessageBoxButtons.OK, MessageBoxIcon.Warning));

    private void SaveSettings()
    {
        _settings.Label = _label.Text;
        _settings.Url = _url.Text;
        _settings.IgnoreCertificateErrors = _ignoreCert.Checked;
        SaveLoadSettings();
        _settings.Save();
    }

    private static Button MakeButton(string text, EventHandler onClick)
    {
        var b = new Button { Text = text, AutoSize = true, Padding = new Padding(4, 2, 4, 2), Margin = new Padding(3) };
        b.Click += onClick;
        return b;
    }

    private static Label MakeLabel(string text, Color? color = null) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(6, 7, 3, 3),
        ForeColor = color ?? SystemColors.ControlText,
    };

    private static DataGridView MakeGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = true,
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.None,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
        };
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        return grid;
    }

    private static void AddColumn(DataGridView grid, string name, string header, int width, bool fill = false)
    {
        var col = new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = header,
            Width = width,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        };
        if (fill) col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        grid.Columns.Add(col);
    }

    private static TextBox MakeDetailsBox() => new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = false,
        Font = MonoFont,
        BackColor = SystemColors.Window,
    };

    private static void OpenFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Cannot open the folder: {folder}\n{ex.Message}");
        }
    }

    private static readonly List<(SplitContainer Split, double Ratio)> Splitters = new();

        private static void RegisterSplitter(SplitContainer split, double ratio) => Splitters.Add((split, ratio));

    private static void InitSplitters()
    {
        foreach (var (split, ratio) in Splitters)
        {
            try
            {
                int size = split.Orientation == Orientation.Horizontal ? split.Height : split.Width;
                split.SplitterDistance = Math.Max(split.Panel1MinSize, (int)(size * ratio));
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
