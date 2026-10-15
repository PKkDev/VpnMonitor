using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace VpnMonitor.WUI.Controls
{
    /// <summary>
    /// Stacked bars, one per period; the exact numbers are in the tooltip of a bar.
    /// </summary>
    public sealed class BarChart : UserControl
    {
        private const double LeftMargin = 76;
        private const double RightMargin = 8;
        private const double TopMargin = 8;
        private const double BottomMargin = 22;
        private const double MinLabelSpacing = 44;

        private readonly Canvas _canvas = new();

        public static readonly DependencyProperty BarsProperty = DependencyProperty.Register(
            nameof(Bars), typeof(IReadOnlyList<ChartBar>), typeof(BarChart), new PropertyMetadata(null, OnChanged));

        public static readonly DependencyProperty ValueKindProperty = DependencyProperty.Register(
            nameof(ValueKind), typeof(ChartValueKind), typeof(BarChart), new PropertyMetadata(ChartValueKind.Bytes, OnChanged));

        public IReadOnlyList<ChartBar>? Bars { get => (IReadOnlyList<ChartBar>?)GetValue(BarsProperty); set => SetValue(BarsProperty, value); }

        public ChartValueKind ValueKind { get => (ChartValueKind)GetValue(ValueKindProperty); set => SetValue(ValueKindProperty, value); }

        public BarChart()
        {
            Height = 180;
            Content = new Grid { Background = new SolidColorBrush(Colors.Transparent), Children = { _canvas } };

            SizeChanged += (_, _) => Redraw();
            ActualThemeChanged += (_, _) => Redraw();
        }

        private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((BarChart)d).Redraw();
        }

        private void Redraw()
        {
            _canvas.Children.Clear();

            if (ActualWidth <= 0 || ActualHeight <= 0)
                return;

            var bars = Bars ?? Array.Empty<ChartBar>();
            var plotWidth = Math.Max(1, ActualWidth - LeftMargin - RightMargin);
            var plotHeight = Math.Max(1, ActualHeight - TopMargin - BottomMargin);
            var max = ChartFormat.NiceCeiling(bars.Select(x => x.Values.Sum()).DefaultIfEmpty(0).Max(), ValueKind);

            var labelBrush = Brush("TextFillColorSecondaryBrush");
            var gridBrush = Brush("DividerStrokeColorDefaultBrush");

            foreach (var fraction in new[] { 0, 0.5, 1 })
            {
                var y = TopMargin + plotHeight - fraction * plotHeight;
                _canvas.Children.Add(new Line { X1 = LeftMargin, X2 = LeftMargin + plotWidth, Y1 = y, Y2 = y, Stroke = gridBrush, StrokeThickness = 1 });
                AddText(ChartFormat.Value(max * fraction, ValueKind), 0, y - 8, LeftMargin - 6, TextAlignment.Right, labelBrush);
            }

            if (bars.Count == 0)
            {
                AddText("Нет данных", LeftMargin, TopMargin + plotHeight / 2 - 10, plotWidth, TextAlignment.Center, labelBrush);
                return;
            }

            var slot = plotWidth / bars.Count;
            var barWidth = Math.Max(1, slot * 0.7);
            var labelEvery = (int)Math.Ceiling(MinLabelSpacing / slot);

            for (var i = 0; i < bars.Count; i++)
            {
                var bar = bars[i];
                var left = LeftMargin + i * slot + (slot - barWidth) / 2;
                var bottom = TopMargin + plotHeight;

                // Transparent full-height hit area: thin or empty bars still show their tooltip
                var hitArea = new Rectangle { Width = slot, Height = plotHeight, Fill = new SolidColorBrush(Colors.Transparent) };
                Canvas.SetLeft(hitArea, LeftMargin + i * slot);
                Canvas.SetTop(hitArea, TopMargin);
                ToolTipService.SetToolTip(hitArea, bar.ToolTip);
                _canvas.Children.Add(hitArea);

                for (var part = 0; part < bar.Values.Count; part++)
                {
                    var height = bar.Values[part] / max * plotHeight;
                    if (height <= 0)
                        continue;

                    bottom -= height;
                    var color = part < ChartColors.Stack.Length ? ChartColors.Stack[part] : ChartColors.Fallback;
                    var rectangle = new Rectangle { Width = barWidth, Height = height, Fill = new SolidColorBrush(color), IsHitTestVisible = false };
                    Canvas.SetLeft(rectangle, left);
                    Canvas.SetTop(rectangle, bottom);
                    _canvas.Children.Add(rectangle);
                }

                if (i % labelEvery == 0)
                    AddText(bar.Label, LeftMargin + i * slot + slot / 2 - 30, TopMargin + plotHeight + 3, 60, TextAlignment.Center, labelBrush);
            }
        }

        private void AddText(string text, double left, double top, double width, TextAlignment alignment, Brush brush)
        {
            var block = new TextBlock { Text = text, FontSize = 11, Width = width, TextAlignment = alignment, Foreground = brush, IsHitTestVisible = false };
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
    }
}
