using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PigeonPost.VpnClientView.Controls;

public class SpeedChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>?> SentDataProperty =
        AvaloniaProperty.Register<SpeedChart, IReadOnlyList<double>?>(nameof(SentData));

    public static readonly StyledProperty<IReadOnlyList<double>?> ReceivedDataProperty =
        AvaloniaProperty.Register<SpeedChart, IReadOnlyList<double>?>(nameof(ReceivedData));

    public IReadOnlyList<double>? SentData
    {
        get => GetValue(SentDataProperty);
        set => SetValue(SentDataProperty, value);
    }

    public IReadOnlyList<double>? ReceivedData
    {
        get => GetValue(ReceivedDataProperty);
        set => SetValue(ReceivedDataProperty, value);
    }

    static SpeedChart()
    {
        SentDataProperty.Changed.AddClassHandler<SpeedChart>((c, _) => c.InvalidateVisual());
        ReceivedDataProperty.Changed.AddClassHandler<SpeedChart>((c, _) => c.InvalidateVisual());
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
            return;

        var sent = SentData;
        var received = ReceivedData;
        var count = Math.Max(sent?.Count ?? 0, received?.Count ?? 0);
        if (count == 0)
            return;

        var w = size.Width;
        var h = size.Height;

        var padding = 4.0;
        var chartLeft = padding;
        var chartRight = w - padding;
        var chartTop = padding;
        var chartBottom = h - padding;
        var chartWidth = chartRight - chartLeft;
        var chartHeight = chartBottom - chartTop;

        if (chartWidth <= 0 || chartHeight <= 0)
            return;

        double maxValue = 1;
        void UpdateMax(IReadOnlyList<double>? data)
        {
            if (data is null) return;
            foreach (var v in data)
            {
                if (v > maxValue) maxValue = v;
            }
        }
        UpdateMax(sent);
        UpdateMax(received);

        if (maxValue <= 0)
            maxValue = 1;

        var sentPen = new Pen(new SolidColorBrush(Color.FromRgb(76, 175, 80)), 1.5);
        var receivedPen = new Pen(new SolidColorBrush(Color.FromRgb(33, 150, 243)), 1.5);
        var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(60, 60, 60)), 0.5);

        for (var i = 0; i < 4; i++)
        {
            var y = chartTop + (chartHeight / 3.0) * i;
            context.DrawLine(gridPen, new Point(chartLeft, y), new Point(chartRight, y));
        }

        void DrawLine(IReadOnlyList<double>? data, IPen pen)
        {
            if (data is null || data.Count < 2)
                return;

            var points = new Point[data.Count];
            for (var i = 0; i < data.Count; i++)
            {
                var x = chartLeft + (i / (double)(data.Count - 1)) * chartWidth;
                var y = chartBottom - (data[i] / maxValue) * chartHeight;
                points[i] = new Point(x, y);
            }

            for (var i = 0; i < points.Length - 1; i++)
                context.DrawLine(pen, points[i], points[i + 1]);
        }

        DrawLine(received, receivedPen);
        DrawLine(sent, sentPen);
    }
}
