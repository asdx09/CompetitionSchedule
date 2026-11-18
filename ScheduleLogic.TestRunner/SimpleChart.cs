using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ScheduleLogic.TestKit.Load;

namespace ScheduleLogic.TestRunner;

/// <summary>
/// Egyszerű élő grafikon a terheléses méréshez: másodpercenként a p95
/// késleltetés (bal tengely, ms) és a kérés/másodperc (jobb tengely).
/// Külső grafikonkönyvtár nélkül, GDI+-szal rajzolva.
/// </summary>
internal sealed class SimpleChart : Control
{
    private readonly List<TimelinePoint> _points = new();
    private static readonly Color LatencyColor = Color.FromArgb(220, 80, 60);
    private static readonly Color RpsColor = Color.FromArgb(40, 110, 200);
    private static readonly Color WarmUpColor = Color.FromArgb(245, 245, 245);

    public string Title { get; set; } = "";

    public SimpleChart()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 8.5f);
    }

    public void Clear(string title)
    {
        _points.Clear();
        Title = title;
        Invalidate();
    }

    public void Add(TimelinePoint p)
    {
        _points.Add(p);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        var plot = new Rectangle(55, 28, Math.Max(10, Width - 115), Math.Max(10, Height - 58));
        using var axisPen = new Pen(Color.FromArgb(200, 200, 200));
        using var textBrush = new SolidBrush(Color.FromArgb(60, 60, 60));
        using var titleFont = new Font(Font, FontStyle.Bold);

        g.DrawString(Title, titleFont, textBrush, 8, 6);

        if (_points.Count == 0)
        {
            g.DrawRectangle(axisPen, plot);
            g.DrawString("A mérés indítása után itt jelenik meg a másodpercenkénti p95 késleltetés és a kérés/s.",
                Font, textBrush, plot.Left + 10, plot.Top + 10);
            return;
        }

        int maxSecond = Math.Max(10, _points.Max(p => p.Second));
        double maxLat = NiceMax(_points.Max(p => p.P95Ms));
        double maxRps = NiceMax(_points.Max(p => (double)p.Requests));

        float X(int second) => plot.Left + (float)(second - 1) / Math.Max(1, maxSecond - 1) * plot.Width;
        float YLat(double v) => plot.Bottom - (float)(v / maxLat) * plot.Height;
        float YRps(double v) => plot.Bottom - (float)(v / maxRps) * plot.Height;

        // bemelegítés sávja
        var warm = _points.Where(p => p.WarmUp).ToList();
        if (warm.Count > 0)
        {
            using var warmBrush = new SolidBrush(WarmUpColor);
            float right = X(warm.Max(p => p.Second));
            g.FillRectangle(warmBrush, plot.Left, plot.Top, Math.Max(1, right - plot.Left), plot.Height);
            g.DrawString("bemelegítés", Font, textBrush, plot.Left + 4, plot.Bottom - 18);
        }

        // rácsvonalak és tengelyfeliratok
        for (int i = 0; i <= 4; i++)
        {
            float y = plot.Bottom - i * plot.Height / 4f;
            g.DrawLine(axisPen, plot.Left, y, plot.Right, y);
            using var latBrush = new SolidBrush(LatencyColor);
            using var rpsBrush = new SolidBrush(RpsColor);
            var latText = (maxLat * i / 4).ToString("0");
            var latSize = g.MeasureString(latText, Font);
            g.DrawString(latText, Font, latBrush, plot.Left - latSize.Width - 4, y - latSize.Height / 2);
            g.DrawString((maxRps * i / 4).ToString("0"), Font, rpsBrush, plot.Right + 4, y - 7);
        }
        g.DrawRectangle(axisPen, plot);
        g.DrawString("mp", Font, textBrush, plot.Right - 16, plot.Bottom + 4);
        g.DrawString("1", Font, textBrush, plot.Left - 3, plot.Bottom + 4);
        g.DrawString(maxSecond.ToString(), Font, textBrush, plot.Right - 30, plot.Bottom + 4);

        DrawSeries(g, _points.Select(p => new PointF(X(p.Second), YRps(p.Requests))).ToArray(), RpsColor);
        DrawSeries(g, _points.Select(p => new PointF(X(p.Second), YLat(p.P95Ms))).ToArray(), LatencyColor);

        // jelmagyarázat
        using (var b1 = new SolidBrush(LatencyColor)) g.DrawString("■ p95 késleltetés (ms, bal)", Font, b1, plot.Right - 330, 8);
        using (var b2 = new SolidBrush(RpsColor)) g.DrawString("■ kérés/s (jobb)", Font, b2, plot.Right - 140, 8);
    }

    private static void DrawSeries(Graphics g, PointF[] pts, Color color)
    {
        using var pen = new Pen(color, 2f);
        if (pts.Length >= 2) g.DrawLines(pen, pts);
        using var brush = new SolidBrush(color);
        foreach (var p in pts) g.FillEllipse(brush, p.X - 2.5f, p.Y - 2.5f, 5, 5);
    }

    /// <summary>"Szép" tengelymaximum: 1, 2, 5 × 10^n.</summary>
    private static double NiceMax(double v)
    {
        if (v <= 0) return 1;
        double exp = Math.Pow(10, Math.Floor(Math.Log10(v)));
        foreach (var m in new[] { 1.0, 2.0, 5.0, 10.0 })
            if (m * exp >= v) return m * exp;
        return 10 * exp;
    }
}
