// The launcher's look, drawn in code (no game art): a drifting starfield, a hex grid, scanlines, cut-corner panels,
// the launcher's own emblem and a "decrypting" text reveal.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace APSetup;

// Slowly drifting, twinkling stars in three depth layers; the nearest layer is tinted by Accent.
sealed class Starfield : FrameworkElement
{
    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(nameof(Accent), typeof(Color), typeof(Starfield),
        new FrameworkPropertyMetadata(Colors.White, FrameworkPropertyMetadataOptions.AffectsRender));
    public Color Accent { get => (Color)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }

    struct Star { public double X, Y, Size, Speed, Phase, Depth; }
    readonly Star[] _stars;
    readonly DispatcherTimer _timer;
    double _t;

    public Starfield()
    {
        IsHitTestVisible = false;
        var random = new Random(7);
        _stars = new Star[220];
        for (int i = 0; i < _stars.Length; i++)
        {
            var depth = random.NextDouble();
            _stars[i] = new Star { X = random.NextDouble(), Y = random.NextDouble(), Depth = depth, Size = 0.4 + depth * 1.4,
                                   Speed = 0.002 + depth * 0.010, Phase = random.NextDouble() * Math.PI * 2 };
        }
        // Background priority: the stars must never starve input or other timers
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(40) };
        _timer.Tick += (_, _) => { _t += 0.04; InvalidateVisual(); };
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        var white = Brushes.AliceBlue;
        var tint = new SolidColorBrush(Accent);
        tint.Freeze();
        foreach (var s in _stars)
        {
            var x = ((s.X - _t * s.Speed) % 1 + 1) % 1 * w;
            var twinkle = 0.55 + 0.45 * Math.Sin(_t * (1.2 + s.Depth * 2) + s.Phase);
            var brush = s.Depth > 0.85 ? tint : white;
            dc.PushOpacity((0.15 + 0.6 * s.Depth) * twinkle);
            dc.DrawEllipse(brush, null, new Point(x, s.Y * h), s.Size, s.Size);
            dc.Pop();
        }
    }
}

// A panel or button face with cut corners (top-left and bottom-right), a thin edge and an optional corner tick.
sealed class Chamfer : Decorator
{
    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(nameof(Fill), typeof(Brush), typeof(Chamfer),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(nameof(Stroke), typeof(Brush), typeof(Chamfer),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty CutProperty = DependencyProperty.Register(nameof(Cut), typeof(double), typeof(Chamfer),
        new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty PaddingProperty = DependencyProperty.Register(nameof(Padding), typeof(Thickness), typeof(Chamfer),
        new FrameworkPropertyMetadata(new Thickness(0), FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty TicksProperty = DependencyProperty.Register(nameof(Ticks), typeof(bool), typeof(Chamfer),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public double Cut { get => (double)GetValue(CutProperty); set => SetValue(CutProperty, value); }
    public Thickness Padding { get => (Thickness)GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }
    public bool Ticks { get => (bool)GetValue(TicksProperty); set => SetValue(TicksProperty, value); }

    protected override Size MeasureOverride(Size available)
    {
        var p = Padding;
        var pad = new Size(p.Left + p.Right, p.Top + p.Bottom);
        if (Child == null) return pad;
        Child.Measure(new Size(Math.Max(0, available.Width - pad.Width), Math.Max(0, available.Height - pad.Height)));
        return new Size(Child.DesiredSize.Width + pad.Width, Child.DesiredSize.Height + pad.Height);
    }

    protected override Size ArrangeOverride(Size final)
    {
        var p = Padding;
        Child?.Arrange(new Rect(p.Left, p.Top, Math.Max(0, final.Width - p.Left - p.Right), Math.Max(0, final.Height - p.Top - p.Bottom)));
        return final;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight, c = Math.Min(Cut, Math.Min(w, h) / 2);
        var shape = new StreamGeometry();
        using (var g = shape.Open())
        {
            g.BeginFigure(new Point(c, 0.5), true, true);
            g.LineTo(new Point(w - 0.5, 0.5), true, false);
            g.LineTo(new Point(w - 0.5, h - c), true, false);
            g.LineTo(new Point(w - c, h - 0.5), true, false);
            g.LineTo(new Point(0.5, h - 0.5), true, false);
            g.LineTo(new Point(0.5, c), true, false);
        }
        shape.Freeze();
        dc.DrawGeometry(Fill, Stroke == null ? null : new Pen(Stroke, 1), shape);
        if (Ticks && Stroke != null)
        {
            var pen = new Pen(Stroke, 2);
            dc.DrawLine(pen, new Point(w - 18, 2), new Point(w - 2, 2));
            dc.DrawLine(pen, new Point(2, h - 2), new Point(18, h - 2));
        }
    }
}

// The launcher's emblem: two orbit rings around a core with three swept blades (an original mark, not a game logo).
sealed class Emblem : FrameworkElement
{
    public static readonly DependencyProperty BrushProperty = DependencyProperty.Register(nameof(Brush), typeof(Brush), typeof(Emblem),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));
    public Brush Brush { get => (Brush)GetValue(BrushProperty); set => SetValue(BrushProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        double s = Math.Min(ActualWidth, ActualHeight), r = s / 2;
        if (s <= 0) return;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        dc.DrawEllipse(null, new Pen(Brush, s * 0.05), center, r * 0.92, r * 0.92);
        dc.PushOpacity(0.55);
        dc.DrawEllipse(null, new Pen(Brush, s * 0.025), center, r * 0.70, r * 0.70);
        dc.Pop();
        for (int i = 0; i < 3; i++)
        {
            var a = i * 2 * Math.PI / 3 - Math.PI / 2;
            Point P(double radius, double turn) => new(center.X + radius * Math.Cos(a + turn), center.Y + radius * Math.Sin(a + turn));
            var blade = new StreamGeometry();
            using (var g = blade.Open())
            {
                g.BeginFigure(P(r * 0.18, -0.5), true, true);
                g.LineTo(P(r * 0.62, -0.15), true, false);
                g.LineTo(P(r * 0.50, 0.35), true, false);
                g.LineTo(P(r * 0.18, 0.5), true, false);
            }
            blade.Freeze();
            dc.DrawGeometry(Brush, null, blade);
        }
        dc.DrawEllipse(Brush, null, center, r * 0.1, r * 0.1);
    }
}

static class Fx
{
    // A tile of hexagon outlines, for a faint grid over the background.
    public static DrawingBrush HexGrid(Color color, double size = 28)
    {
        double w = Math.Sqrt(3) * size, h = 2 * size;
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            void Hex(double cx, double cy)
            {
                for (int i = 0; i < 6; i++)
                {
                    var a = Math.PI / 180 * (60 * i - 30);
                    var p = new Point(cx + size * Math.Cos(a), cy + size * Math.Sin(a));
                    if (i == 0) g.BeginFigure(p, false, true); else g.LineTo(p, true, false);
                }
            }
            Hex(w / 2, size);
            Hex(0, size * 2.5);
            Hex(w, size * 2.5);
        }
        geometry.Freeze();
        var drawing = new GeometryDrawing(null, new Pen(new SolidColorBrush(color), 0.8), geometry);
        return new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile, Viewport = new Rect(0, 0, w, h * 1.5), ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, size * 0.0, w, h * 1.5), ViewboxUnits = BrushMappingMode.Absolute,
        };
    }

    // Horizontal scanlines, one dark line every 3 pixels.
    public static DrawingBrush Scanlines()
    {
        var drawing = new GeometryDrawing(new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), null, new RectangleGeometry(new Rect(0, 0, 4, 1)));
        return new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 4, 3), ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, 4, 3), ViewboxUnits = BrushMappingMode.Absolute, Stretch = Stretch.None,
        };
    }

    const string Glyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789#/<>=+";

    // Reveals text left to right out of random glyphs, like a terminal decrypting it.
    public static void Decrypt(TextBlock block, string text, int ms = 520)
    {
        var random = new Random();
        var start = DateTime.UtcNow;
        if (block.Tag is DispatcherTimer old) old.Stop();
        var timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(30) };
        block.Tag = timer;
        timer.Tick += (_, _) =>
        {
            var shown = (int)(text.Length * Math.Min(1, (DateTime.UtcNow - start).TotalMilliseconds / ms));
            block.Text = text[..shown] + new string(text.Skip(shown).Select(c => c == ' ' ? ' ' : Glyphs[random.Next(Glyphs.Length)]).ToArray());
            if (shown >= text.Length) { timer.Stop(); block.Text = text; }
        };
        timer.Start();
    }
}
