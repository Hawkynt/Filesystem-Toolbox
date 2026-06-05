using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Filesystem_Toolbox {

  /// <summary>
  /// A small hand-drawn chart (pie or bars) - zero dependencies, works on net48 and
  /// net8.0-windows alike. Set data via <see cref="SetPieData"/> or <see cref="SetBarData"/>.
  /// </summary>
  internal sealed class ChartControl : Control {

    private enum ChartKind { None, Pie, Bar }

    private ChartKind _kind = ChartKind.None;
    private (string Label, double Value, Color Color)[] _data = new (string, double, Color)[0];

    public string Title { get; set; } = string.Empty;

    public ChartControl() {
      this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
      this.BackColor = SystemColors.Window;
    }

    public void SetPieData(IEnumerable<(string Label, double Value, Color Color)> data) {
      this._kind = ChartKind.Pie;
      this._data = (data ?? Enumerable.Empty<(string, double, Color)>()).Where(d => d.Value > 0).ToArray();
      this.Invalidate();
    }

    public void SetBarData(IEnumerable<(string Label, double Value, Color Color)> data) {
      this._kind = ChartKind.Bar;
      this._data = (data ?? Enumerable.Empty<(string, double, Color)>()).ToArray();
      this.Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e) {
      base.OnPaint(e);
      var graphics = e.Graphics;
      graphics.SmoothingMode = SmoothingMode.AntiAlias;

      var bounds = this.ClientRectangle;
      bounds.Inflate(-8, -8);

      if (!string.IsNullOrEmpty(this.Title)) {
        using (var bold = new Font(this.Font, FontStyle.Bold))
          graphics.DrawString(this.Title, bold, SystemBrushes.ControlText, bounds.Left, bounds.Top);

        bounds.Y += 20;
        bounds.Height -= 20;
      }

      if (this._data.Length == 0 || bounds.Width < 40 || bounds.Height < 40) {
        graphics.DrawString("no data", this.Font, SystemBrushes.GrayText, bounds, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        return;
      }

      switch (this._kind) {
        case ChartKind.Pie:
          this._PaintPie(graphics, bounds);
          break;

        case ChartKind.Bar:
          this._PaintBars(graphics, bounds);
          break;
      }
    }

    private void _PaintPie(Graphics graphics, Rectangle bounds) {
      const int LEGEND_WIDTH = 130;
      var diameter = Math.Min(bounds.Height, Math.Max(40, bounds.Width - LEGEND_WIDTH));
      var pieRect = new Rectangle(bounds.Left, bounds.Top + (bounds.Height - diameter) / 2, diameter, diameter);

      var total = this._data.Sum(d => d.Value);
      var startAngle = -90f;
      foreach (var (label, value, color) in this._data) {
        var sweep = (float)(value / total * 360.0);
        using (var brush = new SolidBrush(color))
          graphics.FillPie(brush, pieRect, startAngle, sweep);

        graphics.DrawPie(SystemPens.ControlDark, pieRect, startAngle, sweep);
        startAngle += sweep;
      }

      // legend
      var y = bounds.Top;
      foreach (var (label, value, color) in this._data) {
        var swatch = new Rectangle(pieRect.Right + 12, y + 2, 10, 10);
        using (var brush = new SolidBrush(color))
          graphics.FillRectangle(brush, swatch);

        graphics.DrawRectangle(SystemPens.ControlDark, swatch);
        graphics.DrawString($"{label} ({value / total:P0})", this.Font, SystemBrushes.ControlText, swatch.Right + 4, y);
        y += 17;
        if (y > bounds.Bottom - 14)
          break;
      }
    }

    private void _PaintBars(Graphics graphics, Rectangle bounds) {
      const int AXIS_HEIGHT = 16;
      var plot = new Rectangle(bounds.Left, bounds.Top, bounds.Width, bounds.Height - AXIS_HEIGHT);
      var maxValue = Math.Max(1.0, this._data.Max(d => d.Value));

      var barSlot = (float)plot.Width / this._data.Length;
      var barWidth = Math.Max(2f, barSlot * 0.7f);

      for (var i = 0; i < this._data.Length; ++i) {
        var (label, value, color) = this._data[i];
        var barHeight = (float)(value / maxValue * (plot.Height - 14));
        var x = plot.Left + i * barSlot + (barSlot - barWidth) / 2;
        var bar = new RectangleF(x, plot.Bottom - barHeight, barWidth, barHeight);

        using (var brush = new SolidBrush(color))
          graphics.FillRectangle(brush, bar);

        if (value > 0)
          graphics.DrawString(value.ToString("0"), this.Font, SystemBrushes.ControlText, x + barWidth / 2 - 8, bar.Top - 14);

        // x labels: draw every other one when crowded
        if (barSlot >= 24 || i % 2 == 0)
          graphics.DrawString(label, this.Font, SystemBrushes.GrayText, x - 4, plot.Bottom + 2);
      }

      graphics.DrawLine(SystemPens.ControlDark, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
    }

  }
}
