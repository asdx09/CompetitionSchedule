using System.Drawing;
using System.Text;
using System.Windows.Forms;
using ScheduleLogic.TestKit.Reporting;
using ScheduleLogic.TestKit.Testing;

namespace ScheduleLogic.TestRunner;

/// <summary>
/// "Összehasonlítás" fül: két mentett futás (pl. .NET és PHP) egymás mellett.
/// Mivel a két backend ugyanazon a porton fut, a menete: futtatás az egyikkel,
/// backend csere, futtatás a másikkal, majd itt a két riport összevetése.
/// </summary>
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
        var page = new TabPage("Összehasonlítás");

        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4), WrapContents = true };
        _reportA = new ComboBox { Width = 380, DropDownStyle = ComboBoxStyle.DropDownList };
        _reportB = new ComboBox { Width = 380, DropDownStyle = ComboBoxStyle.DropDownList };
        _onlyDifferences = new CheckBox { Text = "Csak az eltérések", AutoSize = true, Margin = new Padding(10, 7, 3, 3) };
        _onlyDifferences.CheckedChanged += (_, _) => FillComparisonGrid();

        bar.Controls.Add(MakeLabel("A:"));
        bar.Controls.Add(_reportA);
        bar.Controls.Add(MakeLabel("B:"));
        bar.Controls.Add(_reportB);
        bar.Controls.Add(MakeButton("Összehasonlítás", (_, _) => CompareSelected()));
        bar.Controls.Add(MakeButton("Lista frissítése", (_, _) => RefreshReportLists()));
        bar.Controls.Add(_onlyDifferences);

        var help = MakeLabel(
            "Menete: 1) futtasd az összes tesztet a .NET backenddel, 2) állítsd le, indítsd el a PHP-t, 3) futtasd újra " +
            "PHP címkével, 4) itt válaszd ki a két riportot. Az összevetés a teszt eredményén túl minden kérés " +
            "státuszkódját és a válasz JSON-szerkezetét is nézi (azonosítók és időpontok nélkül).", Color.DimGray);
        help.MaximumSize = new Size(1200, 0);

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        top.Controls.Add(bar);
        top.Controls.Add(help);

        _cmpGrid = MakeGrid();
        AddColumn(_cmpGrid, "Id", "Azonosító", 75);
        AddColumn(_cmpGrid, "Name", "Teszt", 330);
        AddColumn(_cmpGrid, "A", "A", 110);
        AddColumn(_cmpGrid, "B", "B", 110);
        AddColumn(_cmpGrid, "Diff", "Eltérés", 200, fill: true);
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

    /// <summary>A mentett riportok újraolvasása; alapból A = legújabb, B = a legújabb más címkéjű.</summary>
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
            ShowError("Válassz ki két riportot. Riport a Tesztek fülön való futtatáskor készül.");
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
        SetStatus($"{_cmpRows.Count} teszt: {_cmpRows.Count - diff} azonos, {statusDiff} eltérő eredményű, " +
                  $"{diff - statusDiff} eltérő válaszú.");
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
        TestStatus.Passed => "✔ sikeres",
        TestStatus.Failed => "✖ hibás",
        TestStatus.Skipped => "– kihagyva",
        null => "(nem futott)",
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
            sb.AppendLine($"Átlagos válaszidő a tesztben: A {ReportComparer.AverageMs(ra):0.0} ms, B {ReportComparer.AverageMs(rb):0.0} ms " +
                          "(tájékoztató, a terheléses mérés a pontos)");
        sb.AppendLine();

        if (r.Differences.Count > 0)
        {
            sb.AppendLine("Eltérő kérések (metódus, útvonal, státusz, válaszszerkezet):");
            foreach (var d in r.Differences) sb.AppendLine("  " + d);
            sb.AppendLine();
        }

        void Exchanges(string title, TestResult? res)
        {
            sb.AppendLine(title);
            if (res == null) { sb.AppendLine("  (nem futott)"); return; }
            foreach (var e in res.Exchanges)
                sb.AppendLine($"  {e.Method,-6} {e.Path,-45} {e.Status,3}  {e.ElapsedMs,7:0.0} ms  {Shorten(e.Shape, 110)}");
        }
        Exchanges($"Kérések – A ({_cmpA.Label}):", ra);
        sb.AppendLine();
        Exchanges($"Kérések – B ({_cmpB.Label}):", rb);

        _cmpDetails.Text = sb.ToString().Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
    }

    private static string Shorten(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
