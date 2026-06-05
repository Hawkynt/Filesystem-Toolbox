using System;
using System.Windows.Forms;

namespace Filesystem_Toolbox {
  static class Program {
    /// <summary>
    /// Der Haupteinstiegspunkt für die Anwendung.
    /// </summary>
    [STAThread]
    static void Main() {
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);

      AppDomain.CurrentDomain.FirstChanceException += (s, e) => {
        ;
      };

      using (var logic = new MainLogic()) {
        logic.LoadConfiguration();
        // TODO: allow configuring folders from within gui
        // TODO: allow configuring automatic checks from gui

        using (var mainForm = new MainForm(logic))
        using (var notificationIcon = new NotifyIcon {
          Icon = mainForm.Icon,
          Text = mainForm.Text,
        }) {

          // ReSharper disable once AccessToDisposedClosure
          notificationIcon.DoubleClick += (_, __) => mainForm.Show();
          notificationIcon.ContextMenuStrip = mainForm.cmsTrayMenu;
          notificationIcon.Visible = true;

          Application.Run();
          notificationIcon.Visible = false;

        }

        logic.SaveConfiguration();
      }
    }
  }
}
