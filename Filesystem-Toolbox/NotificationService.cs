using System;
using System.Windows.Forms;

namespace Filesystem_Toolbox {

  /// <summary>Decouples toast notifications from the tray icon so handlers stay testable.</summary>
  internal interface INotifier {
    void Info(string title, string text);
    void Warning(string title, string text);
    void Error(string title, string text);
  }

  /// <summary>
  /// Balloon notifications on the tray icon. Semantics follow the protection workflow:
  /// findings and successful repairs are warnings (something DID happen to the medium),
  /// only an unrestorable file is an error.
  /// </summary>
  internal sealed class NotificationService : INotifier {

    private const int _TIMEOUT_MILLISECONDS = 5000;

    private readonly NotifyIcon _icon;
    private readonly Func<bool> _enabled;

    public NotificationService(NotifyIcon icon, Func<bool> enabled = null) {
      this._icon = icon ?? throw new ArgumentNullException(nameof(icon));
      this._enabled = enabled ?? (() => true);
    }

    public void Info(string title, string text) => this._Show(title, text, ToolTipIcon.Info);
    public void Warning(string title, string text) => this._Show(title, text, ToolTipIcon.Warning);
    public void Error(string title, string text) => this._Show(title, text, ToolTipIcon.Error);

    private void _Show(string title, string text, ToolTipIcon icon) {
      if (!this._enabled())
        return;

      try {
        this._icon.ShowBalloonTip(_TIMEOUT_MILLISECONDS, title, text, icon);
      } catch (Exception) {
        ; // a failed balloon must never break the pipeline that raised it
      }
    }

  }
}
