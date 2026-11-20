using System.Drawing;
using System.Windows.Forms;
using ScheduleLogic.TestKit.Http;
using ScheduleLogic.TestKit.Load;
using ScheduleLogic.TestKit.Reporting;

namespace ScheduleLogic.TestRunner;

internal sealed partial class MainForm
{
    private ComboBox _scenario = null!;
    private ComboBox _mode = null!;
    private TextBox _levels = null!;
    private NumericUpDown _duration = null!;
    private NumericUpDown _warmUp = null!;
    private NumericUpDown _pause = null!;
    private NumericUpDown _users = null!;
    private NumericUpDown _eventSize = null!;
    private TextBox _processes = null!;
    private Label _loadLive = null!;
    private SimpleChart _chart = null!;
    private DataGridView _loadGrid = null!;
    private string? _lastLoadFolder;

    private static readonly (LoadScenario Value, string Text)[] Scenarios =
    {
        (LoadScenario.CheckToken, "CheckToken – session check only (base cost)"),
        (LoadScenario.GetEvents, "GetEvents – event list"),
        (LoadScenario.GetEvent, "GetEvent – all data of a large event"),
        (LoadScenario.SaveEvent, "SaveEvent – saving a large event (transaction)"),
        (LoadScenario.Mixed, "Mixed – mixed, realistic usage"),
    };

    private TabPage BuildLoadPage()
    {
        var page = new TabPage("Load");

        var settings = new TableLayoutPanel
        {
            Dock = DockStyle.Left,
            Width = 360,
            ColumnCount = 2,
            Padding = new Padding(8),
            AutoScroll = true,
        };
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _scenario = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        foreach (var s in Scenarios) _scenario.Items.Add(s.Text);
        _scenario.SelectedIndex = Math.Max(0, Array.FindIndex(Scenarios, s => s.Value.ToString() == _settings.Scenario));

        _mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        _mode.Items.Add("Closed: concurrent users");
        _mode.Items.Add("Open: requests / second");
        _mode.SelectedIndex = _settings.Mode == nameof(LoadMode.Open) ? 1 : 0;

        _levels = new TextBox { Text = _settings.Levels, Dock = DockStyle.Fill };
        _duration = Num(5, 3600, _settings.DurationSeconds);
        _warmUp = Num(0, 600, _settings.WarmUpSeconds);
        _pause = Num(0, 600, _settings.PauseSeconds);
        _users = Num(1, 500, _settings.Users);
        _eventSize = Num(1, 5000, _settings.EventSize);
        _processes = new TextBox { Text = _settings.MonitoredProcesses, Dock = DockStyle.Fill };

        AddSetting(settings, "Scenario:", _scenario);
        AddSetting(settings, "Load model:", _mode);
        AddSetting(settings, "Levels (comma-separated):", _levels);
        AddSetting(settings, "Duration / level (s):", _duration);
        AddSetting(settings, "Warm-up (s):", _warmUp);
        AddSetting(settings, "Pause between levels (s):", _pause);
        AddSetting(settings, "Test users:", _users);
        AddSetting(settings, "Event size (competitors):", _eventSize);
        AddSetting(settings, "Monitored processes:", _processes);

        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        var start = MakeButton("▶ Start measurement", async (_, _) => await StartLoad());
        start.Font = new Font(Font, FontStyle.Bold);
        _runControls.Add(start);
        var stop = MakeButton("■ Stop", (_, _) => _cts?.Cancel());
        stop.Enabled = false;
        _stopButtons.Add(stop);
        buttons.Controls.Add(start);
        buttons.Controls.Add(stop);
        buttons.Controls.Add(MakeButton("Results folder", (_, _) => OpenFolder(_lastLoadFolder ?? ReportStore.LoadResultsFolder)));
        settings.Controls.Add(buttons, 0, settings.RowCount);
        settings.SetColumnSpan(buttons, 2);
        settings.RowCount++;

        var help = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(330, 0),
            ForeColor = Color.DimGray,
            Margin = new Padding(3, 10, 3, 3),
            Text =
                "Closed model: N users, each sends the next request right after the response (how much the server can handle).\n" +
                "Open model: a fixed request rate; latency counts from the scheduled start, so overload becomes visible.\n\n" +
                "Preparation creates the given number of test users, each with one large event, and deletes them " +
                "at the end. A user sends only one save at a time, so for SaveEvent use at least as many " +
                "test users as the level.\n\n" +
                "Monitored processes: names of the backend processes on this machine (.NET: ScheduleLogic.Server, " +
                "PHP: php-cgi / php-fpm / php). Only accurate if the backend runs on the same machine.\n\n" +
                "For measurements: Release build, OPcache enabled for PHP, rate limit disabled " +
                "(RateLimiting:Enabled = false). PHP's built-in server (php -S) is single-threaded, so measurements " +
                "need php-cgi / php-fpm with several workers.",
        };
        settings.Controls.Add(help, 0, settings.RowCount);
        settings.SetColumnSpan(help, 2);
        settings.RowCount++;

        _loadLive = new Label
        {
            Dock = DockStyle.Top,
            Height = 26,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 0, 0),
            Font = new Font(Font, FontStyle.Bold),
            Text = "No measurement running.",
        };
        _chart = new SimpleChart { Dock = DockStyle.Fill };

        _loadGrid = MakeGrid();
        AddColumn(_loadGrid, "Backend", "Backend", 70);
        AddColumn(_loadGrid, "Scenario", "Scenario", 90);
        AddColumn(_loadGrid, "Level", "Level", 55);
        AddColumn(_loadGrid, "Total", "Requests", 65);
        AddColumn(_loadGrid, "Errors", "Errors", 55);
        AddColumn(_loadGrid, "Rps", "Req/s", 65);
        AddColumn(_loadGrid, "Mean", "Mean ms", 70);
        AddColumn(_loadGrid, "P50", "p50 ms", 65);
        AddColumn(_loadGrid, "P95", "p95 ms", 65);
        AddColumn(_loadGrid, "P99", "p99 ms", 65);
        AddColumn(_loadGrid, "Max", "Max ms", 65);
        AddColumn(_loadGrid, "Cpu", "CPU avg %", 70);
        AddColumn(_loadGrid, "Mem", "Mem max MB", 85);
        AddColumn(_loadGrid, "Codes", "Status codes", 120, fill: true);
        foreach (DataGridViewColumn c in _loadGrid.Columns)
            if (c.Index >= 2 && c.Name != "Codes") c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

        var chartPanel = new Panel { Dock = DockStyle.Fill };
        chartPanel.Controls.Add(_chart);
        chartPanel.Controls.Add(_loadLive);

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        split.Panel1.Controls.Add(chartPanel);
        split.Panel2.Controls.Add(_loadGrid);
        RegisterSplitter(split, 0.6);

        page.Controls.Add(split);
        page.Controls.Add(settings);
        return page;
    }

    private static NumericUpDown Num(int min, int max, int value) => new()
    {
        Minimum = min,
        Maximum = max,
        Value = Math.Clamp(value, min, max),
        Dock = DockStyle.Fill,
    };

    private static void AddSetting(TableLayoutPanel panel, string label, Control control)
    {
        var l = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 3, 3) };
        panel.Controls.Add(l, 0, panel.RowCount);
        panel.Controls.Add(control, 1, panel.RowCount);
        panel.RowCount++;
    }

    private void SaveLoadSettings()
    {
        _settings.Scenario = Scenarios[Math.Max(0, _scenario.SelectedIndex)].Value.ToString();
        _settings.Mode = _mode.SelectedIndex == 1 ? nameof(LoadMode.Open) : nameof(LoadMode.Closed);
        _settings.Levels = _levels.Text;
        _settings.DurationSeconds = (int)_duration.Value;
        _settings.WarmUpSeconds = (int)_warmUp.Value;
        _settings.PauseSeconds = (int)_pause.Value;
        _settings.Users = (int)_users.Value;
        _settings.EventSize = (int)_eventSize.Value;
        _settings.MonitoredProcesses = _processes.Text;
    }

    private async Task StartLoad()
    {
        if (_busy) return;
        BackendTarget target;
        try { target = CurrentTarget(); }
        catch (ArgumentException ex) { ShowError(ex.Message); return; }

        var levels = LoadSeries.ParseLevels(_levels.Text);
        if (levels.Count == 0)
        {
            ShowError("Enter at least one load level, e.g.: 1,5,10,25");
            return;
        }
        SaveSettings();

        var template = new LoadOptions
        {
            Scenario = Scenarios[Math.Max(0, _scenario.SelectedIndex)].Value,
            Mode = _mode.SelectedIndex == 1 ? LoadMode.Open : LoadMode.Closed,
            Duration = TimeSpan.FromSeconds((double)_duration.Value),
            WarmUp = TimeSpan.FromSeconds((double)_warmUp.Value),
        };
        int users = (int)_users.Value;
        int eventSize = (int)_eventSize.Value;
        var pause = TimeSpan.FromSeconds((double)_pause.Value);
        var monitor = ProcessMonitor.FromList(_processes.Text);

        SetBusy(true);
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var results = new List<LoadResult>();
        LoadFixture? fx = null;
        int currentLevel = 0, levelIndex = 0;

        try
        {
            _loadLive.Text = "Preparing…";
            var setupLog = new Progress<string>(s => _loadLive.Text = s);
            fx = await Task.Run(() => LoadFixture.CreateAsync(target, users, eventSize, template.RequestTimeout, setupLog, token));

            var progress = new Progress<TimelinePoint>(p =>
            {
                _chart.Add(p);
                _loadLive.Text =
                    $"{target.Label} – level {levelIndex + 1}/{levels.Count} ({currentLevel}) – second {p.Second}" +
                    (p.WarmUp ? " (warm-up)" : "") +
                    $": {p.Requests} req/s, p50 {p.P50Ms:0.0} ms, p95 {p.P95Ms:0.0} ms, errors {p.Errors}" +
                    (p.CpuPercent != null ? $", CPU {p.CpuPercent:0.0}%, memory {p.MemoryMb:0} MB" : "");
            });

            await Task.Run(() => LoadSeries.RunAsync(fx, template, levels, pause, monitor,
                onLevelStarted: (i, o) => Ui(() =>
                {
                    levelIndex = i;
                    currentLevel = o.Level;
                    _chart.Clear($"{target.Label}: {o.Describe()}");
                }),
                progress: progress,
                onLevelFinished: r => Ui(() =>
                {
                    results.Add(r);
                    AddLoadRow(r.Summary);
                }),
                ct: token));
        }
        catch (OperationCanceledException)
        {
            SetStatus("Measurement cancelled.");
        }
        catch (Exception ex)
        {
            ShowError("The measurement was interrupted: " + ex.Message);
        }
        finally
        {
            if (fx != null)
            {
                _loadLive.Text = "Cleaning up (deleting test users)…";
                await fx.DisposeAsync();
            }

            await Task.Yield();
            if (results.Count > 0)
            {
                try
                {
                    _lastLoadFolder = LoadCsv.SaveRun(results);
                    SetStatus($"Measurement finished ({results.Count} levels). CSV: {_lastLoadFolder}");
                }
                catch (Exception ex)
                {
                    ShowError("Saving the CSV failed: " + ex.Message);
                }
            }
            _loadLive.Text = results.Count > 0 ? $"Done: {results.Count} levels measured." : "No measurement running.";
            SetBusy(false);
        }
    }

    private void AddLoadRow(LoadSummary s)
    {
        int i = _loadGrid.Rows.Add(
            s.Label, s.Scenario.ToString(),
            s.Mode == LoadMode.Closed ? $"{s.Level} users" : $"{s.Level}/s",
            s.Total, s.Errors, s.Rps.ToString("0.0"), s.MeanMs.ToString("0.0"),
            s.P50Ms.ToString("0.0"), s.P95Ms.ToString("0.0"), s.P99Ms.ToString("0.0"), s.MaxMs.ToString("0.0"),
            s.CpuAvgPercent?.ToString("0.0") ?? "–", s.MemoryMaxMb?.ToString("0") ?? "–", s.StatusCounts);
        if (s.Errors > 0) _loadGrid.Rows[i].DefaultCellStyle.BackColor = FailedColor;
        try { _loadGrid.FirstDisplayedScrollingRowIndex = i; }
        catch (InvalidOperationException) {  }
    }
}
