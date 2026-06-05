using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Filesystem_Toolbox.Core;
using Filesystem_Toolbox.Core.Statistics;

namespace Filesystem_Toolbox {

  /// <summary>
  /// Health overview per watched root: KPI numbers (errors found/corrected, MTBF), the
  /// degradation badge, a best-effort SMART readout and two charts - the status
  /// distribution of the last verify run and corrected errors per month.
  /// </summary>
  internal sealed class StatisticsForm : Form {

    private readonly ToolboxService _logic;
    private readonly ComboBox _cbRoot;
    private readonly Label _lblKpis;
    private readonly Label _lblDegradation;
    private readonly Label _lblSmart;
    private readonly ChartControl _pie;
    private readonly ChartControl _bars;

    public StatisticsForm(ToolboxService logic) {
      this._logic = logic ?? throw new ArgumentNullException(nameof(logic));

      this.Text = @"Statistics";
      this.StartPosition = FormStartPosition.CenterParent;
      this.ClientSize = new Size(720, 420);
      this.MinimumSize = new Size(560, 360);

      var lblRoot = new Label { Text = @"Folder:", Location = new Point(12, 15), AutoSize = true };
      this._cbRoot = new ComboBox {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Location = new Point(60, 12),
        Size = new Size(380, 21),
        Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
      };
      this._cbRoot.SelectedIndexChanged += (_, __) => this._Refresh();

      this._lblSmart = new Label {
        Location = new Point(450, 15),
        Size = new Size(258, 16),
        TextAlign = ContentAlignment.MiddleRight,
        Anchor = AnchorStyles.Top | AnchorStyles.Right,
      };

      this._lblKpis = new Label { Location = new Point(12, 44), Size = new Size(440, 64), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
      this._lblDegradation = new Label {
        Location = new Point(450, 44),
        Size = new Size(258, 20),
        Font = new Font(this.Font, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleRight,
        Anchor = AnchorStyles.Top | AnchorStyles.Right,
      };

      this._pie = new ChartControl {
        Title = @"Last verify run",
        Location = new Point(12, 116),
        Size = new Size(340, 290),
        Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left,
      };
      this._bars = new ChartControl {
        Title = @"Errors corrected per month",
        Location = new Point(364, 116),
        Size = new Size(344, 290),
        Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
      };

      this.Controls.AddRange(new Control[] { lblRoot, this._cbRoot, this._lblSmart, this._lblKpis, this._lblDegradation, this._pie, this._bars });

      foreach (var root in this._logic.Resolver.WatchRoots)
        this._cbRoot.Items.Add(root.Path);

      if (this._cbRoot.Items.Count > 0)
        this._cbRoot.SelectedIndex = 0;
      else
        this._Refresh();
    }

    private void _Refresh() {
      var rootPath = this._cbRoot.SelectedItem as string;
      if (rootPath == null) {
        this._lblKpis.Text = @"No folders are being watched yet.";
        this._lblDegradation.Text = string.Empty;
        this._lblSmart.Text = string.Empty;
        this._pie.SetPieData(null);
        this._bars.SetBarData(null);
        return;
      }

      var threshold = this._logic.Resolver.Resolve(rootPath).DegradationWarningErrorsPerMonth;
      var statistics = new StatisticsService(this._logic.Events).For(rootPath, threshold);

      this._lblKpis.Text =
        $"Errors found (total / 30 days / 7 days):  {statistics.ErrorsFoundTotal} / {statistics.ErrorsFound30d} / {statistics.ErrorsFound7d}\r\n" +
        $"Errors corrected (total / 30 days / 7 days):  {statistics.ErrorsCorrectedTotal} / {statistics.ErrorsCorrected30d} / {statistics.ErrorsCorrected7d}\r\n" +
        $"Mean time between failures:  {statistics.MeanTimeBetweenFailuresHuman}";

      this._lblDegradation.Text = $@"Status: {statistics.Degradation}";
      this._lblDegradation.ForeColor = statistics.Degradation switch {
        DegradationStatus.Healthy => Color.DarkGreen,
        DegradationStatus.Degrading => Color.DarkOrange,
        _ => Color.DarkRed,
      };

      var smart = SmartService.ForRoot(new DirectoryInfo(rootPath));
      this._lblSmart.Text = $@"SMART: {smart switch {
        SmartStatus.Ok => "Ok",
        SmartStatus.PredictingFailure => "PREDICTING FAILURE",
        _ => "unavailable",
      }}";
      this._lblSmart.ForeColor = smart == SmartStatus.PredictingFailure ? Color.DarkRed : SystemColors.ControlText;

      this._pie.SetPieData(statistics.LastVerifyDistribution.Select(p => (
        p.Key,
        (double)p.Value,
        _StatusColor(p.Key)
      )));

      this._bars.SetBarData(statistics.ByMonthLast12.Select(m => (
        new DateTime(m.Year, m.Month, 1).ToString("MMM", CultureInfo.InvariantCulture),
        (double)m.Corrected,
        Color.SteelBlue
      )));
    }

    private static Color _StatusColor(string status) => status switch {
      "Ok" => Color.MediumSeaGreen,
      "BitRot" => Color.IndianRed,
      "Modified" => Color.SteelBlue,
      "New" => Color.MediumPurple,
      "Missing" => Color.DarkOrange,
      "ParityStale" => Color.Khaki,
      _ => Color.Gray,
    };

  }
}
