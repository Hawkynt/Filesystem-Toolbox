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
        // TODO: systray icon
        // TODO: allow configuring folders from within gui
        // TODO: force running check from gui
        // TODO: allow configuring automatic checks from gui
        using (var mainForm = new MainForm()) {
          using (var timer = new Timer(TimeSpan.FromSeconds(2).TotalMilliseconds)) {
            timer.Elapsed += (_, __) => {
              // ReSharper disable AccessToDisposedClosure
              try {
                timer.Stop();
                mainForm.MarkVerificationRunning = true;
                logic.RunChecks(mainForm.MarkFileChecksumFailed, mainForm.MarkFileException);
              } finally {
                mainForm.MarkVerificationRunning = false;
                timer.Start();
              }
              // ReSharper restore AccessToDisposedClosure
            };
            timer.Start();
            Application.Run(mainForm);
          }
        }

        logic.SaveConfiguration();
      }
    }
  }
}
