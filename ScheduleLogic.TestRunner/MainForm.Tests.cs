using System.Drawing;
using System.Text;
using System.Windows.Forms;
using ScheduleLogic.TestKit.Reporting;
using ScheduleLogic.TestKit.Testing;

using Runner = ScheduleLogic.TestKit.Testing.TestRunner;

namespace ScheduleLogic.TestRunner;

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
        var page = new TabPage("Tests");

        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4), WrapContents = true };
        var all = MakeButton("▶ All tests", async (_, _) => await RunTests(ApiTests.All));
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

        var selected = MakeButton("▶ Selected", async (_, _) => await RunTests(SelectedTests()));
        bar.Controls.Add(selected);
        _runControls.Add(selected);

        var stop = MakeButton("■ Stop", (_, _) => _cts?.Cancel());
        stop.Enabled = false;
        bar.Controls.Add(stop);
        _stopButtons.Add(stop);

        bar.Controls.Add(MakeButton("Reports folder", (_, _) => OpenFolder(ReportStore.TestResultsFolder)));

        _testSummary = MakeLabel("Double-click a row to run only that test. Every test creates its own temporary " +
                                 "user and deletes it at the end.", Color.DimGray);
        bar.Controls.Add(_testSummary);

        _testGrid = MakeGrid();
        AddColumn(_testGrid, "Id", "Id", 75);
        AddColumn(_testGrid, "Group", "Group", 130);
        AddColumn(_testGrid, "Name", "Test", 330);
        AddColumn(_testGrid, "Status", "Result", 95);
        AddColumn(_testGrid, "Time", "Time (ms)", 70);
        AddColumn(_testGrid, "Message", "Message", 200, fill: true);
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
        foreach (var t in list) SetRow(t.Id, "waiting", "", "", null);
        SetStatus($"Running {list.Count} tests: {target} …");

        var report = new TestRunReport { Label = target.Label, BaseUrl = target.BaseUrl, StartedAt = DateTime.Now };
        try
        {
            report.Results = await Task.Run(() => Runner.RunManyAsync(
                list, target,
                onStarted: t => Ui(() => SetRow(t.Id, "… running", "", "", RunningColor)),
                onFinished: r => Ui(() => ShowResult(r)),
                ct: token));
            report.FinishedAt = DateTime.Now;

            foreach (var t in list.Where(t => report.Results.All(r => r.TestId != t.Id)))
                SetRow(t.Id, "", "", "Not run (cancelled).", null);

            if (report.Results.Count > 0)
            {
                var file = ReportStore.Save(report);
                RefreshReportLists();
                var summary = $"{report.Passed} passed, {report.Failed} failed, {report.Skipped} skipped – {target.Label}";
                _testSummary.Text = summary + $"  (report: {Path.GetFileName(file)})";
                _testSummary.ForeColor = report.Failed > 0 ? Color.Firebrick : report.Skipped > 0 ? Color.DarkGoldenrod : Color.DarkGreen;
                SetStatus(summary + " – report saved: " + file);
            }
        }
        catch (Exception ex)
        {
            ShowError("The test run was interrupted: " + ex.Message);
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
            TestStatus.Passed => ("✔ passed", PassedColor),
            TestStatus.Failed => ("✖ failed", FailedColor),
            TestStatus.Skipped => ("– skipped", SkippedColor),
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
        if (t.RequiresSolver) sb.AppendLine("(This test needs the Python solver running: ScheduleSolver/Main.py)");
        sb.AppendLine();

        if (_lastResults.TryGetValue(t.Id, out var r))
        {
            var status = r.Status switch
            {
                TestStatus.Passed => "PASSED",
                TestStatus.Failed => "FAILED",
                TestStatus.Skipped => "SKIPPED",
                _ => r.Status.ToString(),
            };
            sb.AppendLine($"Result: {status} – {r.Message}");
            sb.AppendLine($"Time: {r.DurationMs:0} ms, started: {r.StartedAt:HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("Log (requests, responses):");
            foreach (var line in r.Log) sb.AppendLine(line);
        }
        else
        {
            sb.AppendLine("Not run yet.");
        }
        _testDetails.Text = sb.ToString().Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
    }
}
