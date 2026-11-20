using System.Drawing;
using System.Text;
using System.Windows.Forms;
using ScheduleLogic.TestKit.Reporting;
using ScheduleLogic.TestKit.Testing;

namespace ScheduleLogic.TestRunner;

internal sealed partial class MainForm
{
    private ComboBox _reportA = null!;
    private ComboBox _reportB = null!;
    private CheckBox _onlyDifferences = null!;
    private DataGridView _cmpGrid = null!;
    private TextBox _cmpDetails = null!;
    private List<ComparisonRow> _cmpRows = new();
    private TestRunReport? _cmpA, _cmpB;

    private TabPage BuildComparePage()
    {
        var page = new TabPage("Compare");

        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4), WrapContents = true };
        _reportA = new ComboBox { Width = 380, DropDownStyle = ComboBoxStyle.DropDownList };
        _reportB = new ComboBox { Width = 380, DropDownStyle = ComboBoxStyle.DropDownList };
        _onlyDifferences = new CheckBox { Text = "Differences only", AutoSize = true, Margin = new Padding(10, 7, 3, 3) };
        _onlyDifferences.CheckedChanged += (_, _) => FillComparisonGrid();

        bar.Controls.Add(MakeLabel("A:"));
        bar.Controls.Add(_reportA);
        bar.Controls.Add(MakeLabel("B:"));
        bar.Controls.Add(_reportB);
        bar.Controls.Add(MakeButton("Compare", (_, _) => CompareSelected()));
        bar.Controls.Add(MakeButton("Refresh list", (_, _) => RefreshReportLists()));
        bar.Controls.Add(_onlyDifferences);

        var help = MakeLabel(
            "Workflow: 1) run all tests against the .NET backend, 2) stop it and start the PHP one, 3) run again " +
            "with the PHP label, 4) select the two reports here. Besides the test result, the comparison also checks " +
            "the status code and the JSON structure of every response (ignoring ids and timestamps).", Color.DimGray);
        help.MaximumSize = new Size(1200, 0);

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        top.Controls.Add(bar);
        top.Controls.Add(help);

        _cmpGrid = MakeGrid();
        AddColumn(_cmpGrid, "Id", "Id", 75);
        AddColumn(_cmpGrid, "Name", "Test", 330);
        AddColumn(_cmpGrid, "A", "A", 110);
        AddColumn(_cmpGrid, "B", "B", 110);
        AddColumn(_cmpGrid, "Diff", "Difference", 200, fill: true);
        _cmpGrid.SelectionChanged += (_, _) => ShowComparisonDetails();

        _cmpDetails = MakeDetailsBox();

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        split.Panel1.Controls.Add(_cmpGrid);
        split.Panel2.Controls.Add(_cmpDetails);
        RegisterSplitter(split, 0.55);

        page.Controls.Add(split);
        page.Controls.Add(top);
        return page;
    }

        private void RefreshReportLists()
    {
        var reports = ReportStore.List();
        var prevA = (_reportA.SelectedItem as TestRunReport)?.FilePath;
        var prevB = (_reportB.SelectedItem as TestRunReport)?.FilePath;

        foreach (var combo in new[] { _reportA, _reportB })
        {
            combo.BeginUpdate();
            combo.Items.Clear();
            foreach (var r in reports) combo.Items.Add(r);
            combo.EndUpdate();
        }
        if (reports.Count == 0) return;

        int ia = prevA != null ? reports.FindIndex(r => r.FilePath == prevA) : -1;
        if (ia < 0) ia = 0;
        int ib = prevB != null ? reports.FindIndex(r => r.FilePath == prevB) : -1;
        if (ib < 0) ib = reports.FindIndex(r => r.Label != reports[ia].Label);
        if (ib < 0) ib = Math.Min(1, reports.Count - 1);

        _reportA.SelectedIndex = ia;
        _reportB.SelectedIndex = ib;
    }

    private void CompareSelected()
    {
        if (_reportA.SelectedItem is not TestRunReport a || _reportB.SelectedItem is not TestRunReport b)
        {
            ShowError("Select two reports. A report is created when tests are run on the Tests tab.");
            return;
        }
        _cmpA = a;
        _cmpB = b;
        _cmpRows = ReportComparer.Compare(a, b);
        _cmpGrid.Columns["A"]!.HeaderText = "A: " + a.Label;
        _cmpGrid.Columns["B"]!.HeaderText = "B: " + b.Label;
        FillComparisonGrid();

        int diff = _cmpRows.Count(r => r.HasDifference);
        int statusDiff = _cmpRows.Count(r => r.StatusDiffers);
        SetStatus($"{_cmpRows.Count} tests: {_cmpRows.Count - diff} identical, {statusDiff} with a different result, " +
                  $"{diff - statusDiff} with different responses.");
    }

    private void FillComparisonGrid()
    {
        _cmpGrid.Rows.Clear();
        foreach (var r in _cmpRows.Where(r => !_onlyDifferences.Checked || r.HasDifference))
        {
            int i = _cmpGrid.Rows.Add(r.TestId, r.Name, StatusText(r.StatusA), StatusText(r.StatusB), r.Summary);
            var row = _cmpGrid.Rows[i];
            row.Tag = r;
            if (r.StatusDiffers) row.DefaultCellStyle.BackColor = FailedColor;
            else if (r.HasDifference) row.DefaultCellStyle.BackColor = SkippedColor;
        }
    }

    private static string StatusText(TestStatus? s) => s switch
    {
        TestStatus.Passed => "✔ passed",
        TestStatus.Failed => "✖ failed",
        TestStatus.Skipped => "– skipped",
        null => "(not run)",
        _ => s.ToString()!,
    };

    private void ShowComparisonDetails()
    {
        if (_cmpGrid.CurrentRow?.Tag is not ComparisonRow r || _cmpA == null || _cmpB == null)
        {
            _cmpDetails.Text = "";
            return;
        }

        var ra = _cmpA.Results.FirstOrDefault(x => x.TestId == r.TestId);
        var rb = _cmpB.Results.FirstOrDefault(x => x.TestId == r.TestId);

        var sb = new StringBuilder();
        sb.AppendLine($"{r.TestId} – {r.Name}");
        sb.AppendLine();
        sb.AppendLine($"A ({_cmpA.Label}): {StatusText(r.StatusA)} – {r.MessageA}");
        sb.AppendLine($"B ({_cmpB.Label}): {StatusText(r.StatusB)} – {r.MessageB}");
        if (ra != null && rb != null)
            sb.AppendLine($"Average response time in the test: A {ReportComparer.AverageMs(ra):0.0} ms, B {ReportComparer.AverageMs(rb):0.0} ms " +
                          "(indicative only; use the load test for accurate numbers)");
        sb.AppendLine();

        if (r.Differences.Count > 0)
        {
            sb.AppendLine("Differing requests (method, path, status, response structure):");
            foreach (var d in r.Differences) sb.AppendLine("  " + d);
            sb.AppendLine();
        }

        void Exchanges(string title, TestResult? res)
        {
            sb.AppendLine(title);
            if (res == null) { sb.AppendLine("  (not run)"); return; }
            foreach (var e in res.Exchanges)
                sb.AppendLine($"  {e.Method,-6} {e.Path,-45} {e.Status,3}  {e.ElapsedMs,7:0.0} ms  {Shorten(e.Shape, 110)}");
        }
        Exchanges($"Requests – A ({_cmpA.Label}):", ra);
        sb.AppendLine();
        Exchanges($"Requests – B ({_cmpB.Label}):", rb);

        _cmpDetails.Text = sb.ToString().Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
    }

    private static string Shorten(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
