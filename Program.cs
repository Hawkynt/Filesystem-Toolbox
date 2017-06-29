using System;
using System.Windows.Forms;
using Timer = System.Timers.Timer;

namespace Filesystem_Toolbox {
  static class Program {
    /// <summary>
    /// Der Haupteinstiegspunkt für die Anwendung.
    /// </summary>
    [STAThread]
    static void Main() {
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);

      using (var logic = new MainLogic()) {
        logic.LoadConfiguration();
        // TODO: allow configuring folders from within gui
        // TODO: allow configuring automatic checks from gui

        using (var mainForm = new MainForm())
        using (var notificationIcon = new NotifyIcon {
          Icon = mainForm.Icon,
          Text = mainForm.Text,
        }) {

          // ReSharper disable once AccessToDisposedClosure
          notificationIcon.DoubleClick += (_, __) => mainForm.Show();
          notificationIcon.ContextMenuStrip = mainForm.cmsTrayMenu;
          notificationIcon.Visible = true;

          using (var timer = new Timer(TimeSpan.FromSeconds(2).TotalMilliseconds)) {

            // ReSharper disable AccessToDisposedClosure

            Action verificationAction = () => {
              try {
                timer.Stop();
                if (!mainForm.VerificationRunning) {
                  mainForm.VerificationRunning = true;
                  logic.RunChecks(mainForm.MarkFileChecksumFailed, mainForm.MarkFileException);
                }
              } finally {
                mainForm.VerificationRunning = false;
                timer.Start();
              }
            };

            mainForm.tsmiVerifyFolders.Click += (_, __) => verificationAction.BeginInvoke(verificationAction.EndInvoke, null);

            Action rebuildAction = () => {
              try {
                logic.RebuildDatabases();
              } finally {
                mainForm.SafelyInvoke(form => form.tsmiRebuildDatabase.Enabled = true);
              }
            };
            mainForm.tsmiRebuildDatabase.Click += (_, __) => {
              mainForm.tsmiRebuildDatabase.Enabled = false;
              rebuildAction.BeginInvoke(rebuildAction.EndInvoke, null);
            };

            // ReSharper restore AccessToDisposedClosure

            timer.Elapsed += (_, __) => verificationAction();
            timer.Start();

            Application.Run();
          }

          notificationIcon.Visible = false;
        }

        logic.SaveConfiguration();
      }
    }
  }
}
