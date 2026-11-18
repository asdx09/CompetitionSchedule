using System.Drawing;
using System.Text;
using System.Windows.Forms;
using ScheduleLogic.TestKit.Reporting;
using ScheduleLogic.TestKit.Testing;

// A projekt névtere (ScheduleLogic.TestRunner) elfedné a TestKit TestRunner osztályát.
using Runner = ScheduleLogic.TestKit.Testing.TestRunner;

namespace ScheduleLogic.TestRunner;

/// <summary>"Tesztek" fül: gombok csoportonként, táblázat, részletes napló.</summary>
internal sealed partial class MainForm
{
    private DataGridView _testGrid = null!;
    private TextBox _testDetails = null!;
    private Label _testSummary = null!;
    private readonly Dictionary<string, TestResult> _lastResults = new();

    private static readonly Color PassedColor = Color.FromArgb(220, 245, 220);
    private static readonly Color FailedColor = Color.FromArgb(255, 222, 222);
    private static readonly Color SkippedColor = Color.FromArgb(255, 248, 210);
    private static readonly Color RunningColor = Color.FromArgb(220, 235, 255);

    private TabPage BuildTestsPage()
    {
        var page = new TabPage("Tesztek");

        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4), WrapContents = true };
        var all = MakeButton("▶ Összes teszt", async (_, _) => await RunTests(ApiTests.All));
        all.Font = new Font(Font, FontStyle.Bold);
        bar.Controls.Add(all);
        _runControls.Add(all);

        foreach (var group in ApiTests.Groups)
        {
            var g = group;
            var b = MakeButton("▶ " + g, async (_, _) => await RunTests(ApiTests.All.Where(t => t.Group == g)));
            bar.Controls.Add(b);
            _runControls.Add(b);
        }

        var selected = MakeButton("▶ Kijelöltek", async (_, _) => await RunTests(SelectedTests()));
        bar.Controls.Add(selected);
        _runControls.Add(selected);

        var stop = MakeButton("■ Megszakítás", (_, _) => _cts?.Cancel());
        stop.Enabled = false;
        bar.Controls.Add(stop);
        _stopButtons.Add(stop);

        bar.Controls.Add(MakeButton("Riportok mappája", (_, _) => OpenFolder(ReportStore.TestResultsFolder)));

        _testSummary = MakeLabel("Dupla kattintás egy soron: csak az a teszt fut le. Minden teszt saját, ideiglenes " +
                                 "felhasználót hoz létre, és a végén törli.", Color.DimGray);
        bar.Controls.Add(_testSummary);

        _testGrid = MakeGrid();
        AddColumn(_testGrid, "Id", "Azonosító", 75);
        AddColumn(_testGrid, "Group", "Csoport", 130);
        AddColumn(_testGrid, "Name", "Teszt", 330);
        AddColumn(_testGrid, "Status", "Eredmény", 95);
        AddColumn(_testGrid, "Time", "Idő (ms)", 70);
        AddColumn(_testGrid, "Message", "Üzenet", 200, fill: true);
        _testGrid.Columns["Time"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

        foreach (var t in ApiTests.All)
        {
            int i = _testGrid.Rows.Add(t.Id, t.Group, t.Name + (t.RequiresSolver ? "  (solver)" : ""), "", "", "");
            _testGrid.Rows[i].Tag = t;
        }
        _testGrid.SelectionChanged += (_, _) => ShowSelectedTestDetails();
        _testGrid.CellDoubleClick += async (_, e) =>
        {
            if (e.RowIndex >= 0 && !_busy && _testGrid.Rows[e.RowIndex].Tag is TestCase t)
                await RunTests(new[] { t });
        };

        _testDetails = MakeDetailsBox();

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        split.Panel1.Controls.Add(_testGrid);
        split.Panel2.Controls.Add(_testDetails);
        RegisterSplitter(split, 0.58);

        page.Controls.Add(split);
        page.Controls.Add(bar);
        return page;
    }

    private IEnumerable<TestCase> SelectedTests() =>
        _testGrid.SelectedRows.Cast<DataGridViewRow>()
                 .OrderBy(r => r.Index)
                 .Select(r => r.Tag)
                 .OfType<TestCase>()
                 .ToList();

    private async Task RunTests(IEnumerable<TestCase> tests)
    {
        var list = tests.ToList();
        if (_busy || list.Count == 0) return;

        ScheduleLogic.TestKit.Http.BackendTarget target;
        try { target = CurrentTarget(); }
        catch (ArgumentException ex) { ShowError(ex.Message); return; }
        SaveSettings();

        SetBusy(true);
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        foreach (var t in list) SetRow(t.Id, "várakozik", "", "", null);
        SetStatus($"{list.Count} teszt futtatása: {target} …");

        var report = new TestRunReport { Label = target.Label, BaseUrl = target.BaseUrl, StartedAt = DateTime.Now };
        try
        {
            report.Results = await Task.Run(() => Runner.RunManyAsync(
                list, target,
                onStarted: t => Ui(() => SetRow(t.Id, "… fut", "", "", RunningColor)),
                onFinished: r => Ui(() => ShowResult(r)),
                ct: token));
            report.FinishedAt = DateTime.Now;

            foreach (var t in list.Where(t => report.Results.All(r => r.TestId != t.Id)))
                SetRow(t.Id, "", "", "Nem futott (megszakítva).", null);

            if (report.Results.Count > 0)
            {
                var file = ReportStore.Save(report);
                RefreshReportLists();
                var summary = $"{report.Passed} sikeres, {report.Failed} hibás, {report.Skipped} kihagyva – {target.Label}";
                _testSummary.Text = summary + $"  (riport: {Path.GetFileName(file)})";
                _testSummary.ForeColor = report.Failed > 0 ? Color.Firebrick : report.Skipped > 0 ? Color.DarkGoldenrod : Color.DarkGreen;
                SetStatus(summary + " – riport mentve: " + file);
            }
        }
        catch (Exception ex)
        {
            ShowError("A tesztek futtatása megszakadt: " + ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ShowResult(TestResult r)
    {
        _lastResults[r.TestId] = r;
        var (text, color) = r.Status switch
        {
            TestStatus.Passed => ("✔ sikeres", PassedColor),
            TestStatus.Failed => ("✖ hibás", FailedColor),
            TestStatus.Skipped => ("– kihagyva", SkippedColor),
            _ => ("", Color.Empty),
        };
        SetRow(r.TestId, text, r.DurationMs.ToString("0"), r.Status == TestStatus.Passed ? "" : r.Message, color);
        ShowSelectedTestDetails();
    }

    private void SetRow(string id, string status, string time, string message, Color? color)
    {
        foreach (DataGridViewRow row in _testGrid.Rows)
        {
            if (row.Tag is not TestCase t || t.Id != id) continue;
            row.Cells["Status"].Value = status;
            row.Cells["Time"].Value = time;
            row.Cells["Message"].Value = message;
            row.DefaultCellStyle.BackColor = color is { IsEmpty: false } c ? c : _testGrid.DefaultCellStyle.BackColor;
            row.DefaultCellStyle.SelectionBackColor = color is { IsEmpty: false } c2
                ? ControlPaint.Dark(c2, 0.15f)
                : _testGrid.DefaultCellStyle.SelectionBackColor;
            row.DefaultCellStyle.SelectionForeColor = color is { IsEmpty: false } ? Color.Black : _testGrid.DefaultCellStyle.SelectionForeColor;
            return;
        }
    }

    private void ShowSelectedTestDetails()
    {
        if (_testGrid.CurrentRow?.Tag is not TestCase t)
        {
            _testDetails.Text = "";
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"{t.Id} – {t.Name}   [{t.Group}]");
        sb.AppendLine(t.Description);
        if (t.RequiresSolver) sb.AppendLine("(A teszthez futó Python solver kell: ScheduleSolver/Main.py)");
        sb.AppendLine();

        if (_lastResults.TryGetValue(t.Id, out var r))
        {
            var status = r.Status switch
            {
                TestStatus.Passed => "SIKERES",
                TestStatus.Failed => "HIBÁS",
                TestStatus.Skipped => "KIHAGYVA",
                _ => r.Status.ToString(),
            };
            sb.AppendLine($"Eredmény: {status} – {r.Message}");
            sb.AppendLine($"Idő: {r.DurationMs:0} ms, indítva: {r.StartedAt:HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("Napló (kérések, válaszok):");
            foreach (var line in r.Log) sb.AppendLine(line);
        }
        else
        {
            sb.AppendLine("Még nem futott.");
        }
        _testDetails.Text = sb.ToString().Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
    }
}
