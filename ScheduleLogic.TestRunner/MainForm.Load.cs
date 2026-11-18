using System.Drawing;
using System.Windows.Forms;
using ScheduleLogic.TestKit.Http;
using ScheduleLogic.TestKit.Load;
using ScheduleLogic.TestKit.Reporting;

namespace ScheduleLogic.TestRunner;

/// <summary>"Terhelés" fül: lépcsős terheléses mérés élő grafikonnal és CSV-exporttal.</summary>
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
        (LoadScenario.CheckToken, "CheckToken – csak munkamenet-ellenőrzés (alapköltség)"),
        (LoadScenario.GetEvents, "GetEvents – versenylista"),
        (LoadScenario.GetEvent, "GetEvent – egy nagy verseny összes adata"),
        (LoadScenario.SaveEvent, "SaveEvent – nagy verseny mentése (tranzakció)"),
        (LoadScenario.Mixed, "Mixed – vegyes, valószerű használat"),
    };

    private TabPage BuildLoadPage()
    {
        var page = new TabPage("Terhelés");

        // --- bal oldali beállítások ---
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
        _mode.Items.Add("Zárt: párhuzamos felhasználók");
        _mode.Items.Add("Nyitott: kérés / másodperc");
        _mode.SelectedIndex = _settings.Mode == nameof(LoadMode.Open) ? 1 : 0;

        _levels = new TextBox { Text = _settings.Levels, Dock = DockStyle.Fill };
        _duration = Num(5, 3600, _settings.DurationSeconds);
        _warmUp = Num(0, 600, _settings.WarmUpSeconds);
        _pause = Num(0, 600, _settings.PauseSeconds);
        _users = Num(1, 500, _settings.Users);
        _eventSize = Num(1, 5000, _settings.EventSize);
        _processes = new TextBox { Text = _settings.MonitoredProcesses, Dock = DockStyle.Fill };

        AddSetting(settings, "Forgatókönyv:", _scenario);
        AddSetting(settings, "Terhelési modell:", _mode);
        AddSetting(settings, "Szintek (vesszővel):", _levels);
        AddSetting(settings, "Időtartam / szint (s):", _duration);
        AddSetting(settings, "Bemelegítés (s):", _warmUp);
        AddSetting(settings, "Szünet a szintek között (s):", _pause);
        AddSetting(settings, "Tesztfelhasználók:", _users);
        AddSetting(settings, "Verseny mérete (fő):", _eventSize);
        AddSetting(settings, "Figyelt folyamatok:", _processes);

        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        var start = MakeButton("▶ Mérés indítása", async (_, _) => await StartLoad());
        start.Font = new Font(Font, FontStyle.Bold);
        _runControls.Add(start);
        var stop = MakeButton("■ Leállítás", (_, _) => _cts?.Cancel());
        stop.Enabled = false;
        _stopButtons.Add(stop);
        buttons.Controls.Add(start);
        buttons.Controls.Add(stop);
        buttons.Controls.Add(MakeButton("Eredmények mappája", (_, _) => OpenFolder(_lastLoadFolder ?? ReportStore.LoadResultsFolder)));
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
                "Zárt modell: N felhasználó, mindegyik a válasz után azonnal újra kér (mennyit bír a szerver).\n" +
                "Nyitott modell: fix kérés/s; a késleltetés a tervezett indítástól számít, így a túlterhelés látszik.\n\n" +
                "Az előkészítés a megadott számú tesztfelhasználót hoz létre egy-egy nagy versennyel, a mérés " +
                "végén törli őket. Mentésnél egy felhasználó egyszerre egy mentést küld, ezért SaveEvent-nél " +
                "legyen legalább annyi tesztfelhasználó, mint a szint.\n\n" +
                "Figyelt folyamatok: a backend folyamatainak neve ugyanezen a gépen (.NET: ScheduleLogic.Server, " +
                "PHP: php-cgi / php-fpm / php). Csak akkor pontos, ha a backend ugyanazon a gépen fut.\n\n" +
                "Méréshez: Release build, PHP-nál bekapcsolt OPcache, kikapcsolt rate limit " +
                "(RateLimiting:Enabled = false). A PHP beépített szervere (php -S) egyszálú, ezért méréshez " +
                "php-cgi / php-fpm kell több munkafolyamattal.",
        };
        settings.Controls.Add(help, 0, settings.RowCount);
        settings.SetColumnSpan(help, 2);
        settings.RowCount++;

        // --- jobb oldal: élő állapot, grafikon, eredménytáblázat ---
        _loadLive = new Label
        {
            Dock = DockStyle.Top,
            Height = 26,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 0, 0),
            Font = new Font(Font, FontStyle.Bold),
            Text = "Nincs futó mérés.",
        };
        _chart = new SimpleChart { Dock = DockStyle.Fill };

        _loadGrid = MakeGrid();
        AddColumn(_loadGrid, "Backend", "Backend", 70);
        AddColumn(_loadGrid, "Scenario", "Forgatókönyv", 90);
        AddColumn(_loadGrid, "Level", "Szint", 55);
        AddColumn(_loadGrid, "Total", "Kérések", 65);
        AddColumn(_loadGrid, "Errors", "Hibák", 55);
        AddColumn(_loadGrid, "Rps", "Kérés/s", 65);
        AddColumn(_loadGrid, "Mean", "Átlag ms", 70);
        AddColumn(_loadGrid, "P50", "p50 ms", 65);
        AddColumn(_loadGrid, "P95", "p95 ms", 65);
        AddColumn(_loadGrid, "P99", "p99 ms", 65);
        AddColumn(_loadGrid, "Max", "Max ms", 65);
        AddColumn(_loadGrid, "Cpu", "CPU átl. %", 70);
        AddColumn(_loadGrid, "Mem", "Mem. max MB", 85);
        AddColumn(_loadGrid, "Codes", "Státuszkódok", 120, fill: true);
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
            ShowError("Adj meg legalább egy terhelési szintet, pl.: 1,5,10,25");
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
            _loadLive.Text = "Előkészítés…";
            var setupLog = new Progress<string>(s => _loadLive.Text = s);
            fx = await Task.Run(() => LoadFixture.CreateAsync(target, users, eventSize, template.RequestTimeout, setupLog, token));

            var progress = new Progress<TimelinePoint>(p =>
            {
                _chart.Add(p);
                _loadLive.Text =
                    $"{target.Label} – {levelIndex + 1}/{levels.Count}. szint ({currentLevel}) – {p.Second}. mp" +
                    (p.WarmUp ? " (bemelegítés)" : "") +
                    $": {p.Requests} kérés/s, p50 {p.P50Ms:0.0} ms, p95 {p.P95Ms:0.0} ms, hibák {p.Errors}" +
                    (p.CpuPercent != null ? $", CPU {p.CpuPercent:0.0}%, memória {p.MemoryMb:0} MB" : "");
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
            SetStatus("A mérés megszakítva.");
        }
        catch (Exception ex)
        {
            ShowError("A mérés megszakadt: " + ex.Message);
        }
        finally
        {
            if (fx != null)
            {
                _loadLive.Text = "Takarítás (tesztfelhasználók törlése)…";
                await fx.DisposeAsync();
            }

            // Az utolsó szint eredménye is beérjen (Ui() BeginInvoke-kal fut).
            await Task.Yield();
            if (results.Count > 0)
            {
                try
                {
                    _lastLoadFolder = LoadCsv.SaveRun(results);
                    SetStatus($"Mérés kész ({results.Count} szint). CSV: {_lastLoadFolder}");
                }
                catch (Exception ex)
                {
                    ShowError("A CSV mentése nem sikerült: " + ex.Message);
                }
            }
            _loadLive.Text = results.Count > 0 ? $"Kész: {results.Count} szint mérve." : "Nincs futó mérés.";
            SetBusy(false);
        }
    }

    private void AddLoadRow(LoadSummary s)
    {
        int i = _loadGrid.Rows.Add(
            s.Label, s.Scenario.ToString(),
            s.Mode == LoadMode.Closed ? $"{s.Level} felh." : $"{s.Level}/s",
            s.Total, s.Errors, s.Rps.ToString("0.0"), s.MeanMs.ToString("0.0"),
            s.P50Ms.ToString("0.0"), s.P95Ms.ToString("0.0"), s.P99Ms.ToString("0.0"), s.MaxMs.ToString("0.0"),
            s.CpuAvgPercent?.ToString("0.0") ?? "–", s.MemoryMaxMb?.ToString("0") ?? "–", s.StatusCounts);
        if (s.Errors > 0) _loadGrid.Rows[i].DefaultCellStyle.BackColor = FailedColor;
        try { _loadGrid.FirstDisplayedScrollingRowIndex = i; }
        catch (InvalidOperationException) { /* a fül éppen nem látszik */ }
    }
}
