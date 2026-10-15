using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;

namespace VpnMonitor.WUI.Controls
{
    /// <summary>
    /// Lightweight time chart: lines with gaps, three grid lines and a hover readout.
    /// </summary>
    public sealed class LineChart : UserControl
    {
        private const double LeftMargin = 76;
        private const double RightMargin = 8;
        private const double TopMargin = 8;
        private const double BottomMargin = 22;

        private readonly Canvas _canvas = new();
        private readonly Canvas _hoverLayer = new() { IsHitTestVisible = false };
        private readonly Line _hoverLine = new() { StrokeThickness = 1, Visibility = Visibility.Collapsed };
        private readonly Border _hoverBox = new() { Padding = new Thickness(6, 3, 6, 3), CornerRadius = new CornerRadius(4), Visibility = Visibility.Collapsed };
        private readonly TextBlock _hoverText = new() { FontSize = 12 };

        public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(
            nameof(Series), typeof(IReadOnlyList<ChartSeries>), typeof(LineChart), new PropertyMetadata(null, OnChanged));

        public static readonly DependencyProperty RangeStartProperty = DependencyProperty.Register(
            nameof(RangeStart), typeof(DateTime), typeof(LineChart), new PropertyMetadata(default(DateTime), OnChanged));

        public static readonly DependencyProperty RangeEndProperty = DependencyProperty.Register(
            nameof(RangeEnd), typeof(DateTime), typeof(LineChart), new PropertyMetadata(default(DateTime), OnChanged));

        public static readonly DependencyProperty ValueKindProperty = DependencyProperty.Register(
            nameof(ValueKind), typeof(ChartValueKind), typeof(LineChart), new PropertyMetadata(ChartValueKind.Count, OnChanged));

        public IReadOnlyList<ChartSeries>? Series { get => (IReadOnlyList<ChartSeries>?)GetValue(SeriesProperty); set => SetValue(SeriesProperty, value); }

        /// <summary>UTC.</summary>
        public DateTime RangeStart { get => (DateTime)GetValue(RangeStartProperty); set => SetValue(RangeStartProperty, value); }

        /// <summary>UTC.</summary>
        public DateTime RangeEnd { get => (DateTime)GetValue(RangeEndProperty); set => SetValue(RangeEndProperty, value); }

        public ChartValueKind ValueKind { get => (ChartValueKind)GetValue(ValueKindProperty); set => SetValue(ValueKindProperty, value); }

        public LineChart()
        {
            Height = 180;

            _hoverBox.Child = _hoverText;
            _hoverLayer.Children.Add(_hoverLine);
            _hoverLayer.Children.Add(_hoverBox);

            var root = new Grid { Background = new SolidColorBrush(Colors.Transparent) };
            root.Children.Add(_canvas);
            root.Children.Add(_hoverLayer);
            Content = root;

            SizeChanged += (_, _) => Redraw();
            ActualThemeChanged += (_, _) => Redraw();
            root.PointerMoved += (_, e) => ShowHover(e.GetCurrentPoint(root).Position);
            root.PointerExited += (_, _) => HideHover();
        }

        private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LineChart)d).Redraw();
        }

        private double PlotWidth => Math.Max(1, ActualWidth - LeftMargin - RightMargin);
        private double PlotHeight => Math.Max(1, ActualHeight - TopMargin - BottomMargin);

        private double _max = 1;

        private double X(DateTime time)
        {
            var range = (RangeEnd - RangeStart).TotalSeconds;
            return LeftMargin + (range <= 0 ? 0 : (time - RangeStart).TotalSeconds / range * PlotWidth);
        }

        private double Y(double value)
        {
            return TopMargin + PlotHeight - value / _max * PlotHeight;
        }

        private void Redraw()
        {
            _canvas.Children.Clear();
            HideHover();

            if (ActualWidth <= 0 || ActualHeight <= 0 || RangeEnd <= RangeStart)
                return;

            var series = Series ?? Array.Empty<ChartSeries>();
            var maxValue = series.SelectMany(x => x.Points).Select(x => x.Value).DefaultIfEmpty(0).Max();
            _max = ChartFormat.NiceCeiling(maxValue, ValueKind);

            var labelBrush = Brush("TextFillColorSecondaryBrush");
            var gridBrush = Brush("DividerStrokeColorDefaultBrush");

            foreach (var fraction in new[] { 0, 0.5, 1 })
            {
                var y = Y(_max * fraction);
                _canvas.Children.Add(new Line { X1 = LeftMargin, X2 = LeftMargin + PlotWidth, Y1 = y, Y2 = y, Stroke = gridBrush, StrokeThickness = 1 });
                AddText(ChartFormat.Value(_max * fraction, ValueKind), 0, y - 8, LeftMargin - 6, TextAlignment.Right, labelBrush);
            }

            var midpoint = RangeStart + (RangeEnd - RangeStart) / 2;
            var timeFormat = RangeEnd - RangeStart > TimeSpan.FromDays(2) ? "dd.MM" : "HH:mm";
            AddText(Local(RangeStart).ToString(timeFormat), LeftMargin, TopMargin + PlotHeight + 3, 60, TextAlignment.Left, labelBrush);
            AddText(Local(midpoint).ToString(timeFormat), X(midpoint) - 30, TopMargin + PlotHeight + 3, 60, TextAlignment.Center, labelBrush);
            AddText(Local(RangeEnd).ToString(timeFormat), LeftMargin + PlotWidth - 60, TopMargin + PlotHeight + 3, 60, TextAlignment.Right, labelBrush);

            if (!series.Any(x => x.Points.Count > 0))
            {
                AddText("Нет данных", LeftMargin, TopMargin + PlotHeight / 2 - 10, PlotWidth, TextAlignment.Center, labelBrush);
                return;
            }

            foreach (var item in series)
                DrawSeries(item);
        }

        private void DrawSeries(ChartSeries series)
        {
            var brush = new SolidColorBrush(series.Color);

            foreach (var segment in Segments(series.Points))
            {
                var points = Downsample(segment);
                if (points.Count == 1)
                {
                    var dot = new Ellipse { Width = 4, Height = 4, Fill = brush };
                    Canvas.SetLeft(dot, points[0].X - 2);
                    Canvas.SetTop(dot, points[0].Y - 2);
                    _canvas.Children.Add(dot);
                    continue;
                }

                var line = new Polyline { Stroke = brush, StrokeThickness = 1.5, StrokeLineJoin = PenLineJoin.Round };
                foreach (var point in points)
                    line.Points.Add(point);

                _canvas.Children.Add(line);
            }
        }

        private static IEnumerable<List<ChartPoint>> Segments(IReadOnlyList<ChartPoint> points)
        {
            var segment = new List<ChartPoint>();
            foreach (var point in points)
            {
                if (point.StartsSegment && segment.Count > 0)
                {
                    yield return segment;
                    segment = new List<ChartPoint>();
                }

                segment.Add(point);
            }

            if (segment.Count > 0)
                yield return segment;
        }

        /// <summary>
        /// A line of thousands of points is slow and unreadable: one point per pixel column is kept, the highest, so peaks stay visible.
        /// </summary>
        private List<Point> Downsample(List<ChartPoint> segment)
        {
            if (segment.Count <= PlotWidth)
                return segment.Select(x => new Point(X(x.Time), Y(x.Value))).ToList();

            return segment
                .GroupBy(x => (int)X(x.Time))
                .Select(x => new Point(x.Key, Y(x.Max(y => y.Value))))
                .ToList();
        }

        private void ShowHover(Point position)
        {
            var series = Series;
            if (series == null || position.X < LeftMargin || position.X > LeftMargin + PlotWidth || RangeEnd <= RangeStart)
            {
                HideHover();
                return;
            }

            var time = RangeStart + (RangeEnd - RangeStart) * ((position.X - LeftMargin) / PlotWidth);

            var lines = new List<string>();
            DateTime? shownTime = null;
            foreach (var item in series)
            {
                if (item.Points.Count == 0)
                    continue;

                var nearest = item.Points.MinBy(x => Math.Abs((x.Time - time).Ticks));

                // Far from any point means no data there
                if (Math.Abs(X(nearest.Time) - position.X) > 12)
                    continue;

                shownTime ??= nearest.Time;
                lines.Add($"{item.Name}: {ChartFormat.Value(nearest.Value, ValueKind)}");
            }

            if (shownTime == null)
            {
                HideHover();
                return;
            }

            var format = RangeEnd - RangeStart > TimeSpan.FromDays(2) ? "dd.MM HH:mm" : "HH:mm";
            _hoverText.Text = Local(shownTime.Value).ToString(format) + "\n" + string.Join("\n", lines);
            _hoverText.Foreground = Brush("TextFillColorPrimaryBrush");
            _hoverBox.Background = Brush("AcrylicInAppFillColorDefaultBrush");
            _hoverBox.BorderBrush = Brush("DividerStrokeColorDefaultBrush");
            _hoverBox.BorderThickness = new Thickness(1);

            var x = X(shownTime.Value);
            _hoverLine.X1 = _hoverLine.X2 = x;
            _hoverLine.Y1 = TopMargin;
            _hoverLine.Y2 = TopMargin + PlotHeight;
            _hoverLine.Stroke = Brush("TextFillColorSecondaryBrush");
            _hoverLine.Visibility = Visibility.Visible;

            _hoverBox.Visibility = Visibility.Visible;
            _hoverBox.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var left = x + 8 + _hoverBox.DesiredSize.Width > ActualWidth ? x - 8 - _hoverBox.DesiredSize.Width : x + 8;
            Canvas.SetLeft(_hoverBox, Math.Max(0, left));
            Canvas.SetTop(_hoverBox, TopMargin);
        }

        private void HideHover()
        {
            _hoverLine.Visibility = Visibility.Collapsed;
            _hoverBox.Visibility = Visibility.Collapsed;
        }

        private void AddText(string text, double left, double top, double width, TextAlignment alignment, Brush brush)
        {
            var block = new TextBlock { Text = text, FontSize = 11, Width = width, TextAlignment = alignment, Foreground = brush };
            Canvas.SetLeft(block, left);
            Canvas.SetTop(block, top);
            _canvas.Children.Add(block);
        }

        private static Brush Brush(string key)
        {
            return Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush
                ? brush
                : new SolidColorBrush(ChartColors.Fallback);
        }

        private static DateTime Local(DateTime utc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.Local);
        }
    }
}
