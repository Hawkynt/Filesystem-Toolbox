using System;
using System.Threading;
using System.Windows.Forms;
using Filesystem_Toolbox.Core;

namespace Filesystem_Toolbox {
  static class Program {

    private const string _MUTEX_NAME = "Filesystem-Toolbox.SingleInstance";
    private const string _SHOW_SIGNAL_NAME = "Filesystem-Toolbox.ShowWindow";

    /// <summary>
    /// Der Haupteinstiegspunkt für die Anwendung.
    /// </summary>
    [STAThread]
    static void Main() {
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);

      // single instance per session: a second start pops the existing window instead
      using (var mutex = new Mutex(true, _MUTEX_NAME, out var isFirstInstance))
      using (var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, _SHOW_SIGNAL_NAME)) {
        if (!isFirstInstance) {
          showSignal.Set();
          return;
        }

        using (var logic = new ToolboxService()) {
          logic.LoadConfiguration();

          using (var notificationIcon = new NotifyIcon())
          using (var mainForm = new MainForm(logic, new NotificationService(notificationIcon))) {
            notificationIcon.Icon = mainForm.Icon;
            notificationIcon.Text = mainForm.Text;

            // ReSharper disable once AccessToDisposedClosure
            notificationIcon.DoubleClick += (_, __) => mainForm.Show();
            notificationIcon.ContextMenuStrip = mainForm.cmsTrayMenu;
            notificationIcon.Visible = true;

            var showWaitHandle = ThreadPool.RegisterWaitForSingleObject(
              showSignal,
              // ReSharper disable once AccessToDisposedClosure
              (_, __) => mainForm.SafelyInvoke(() => {
                mainForm.Show();
                mainForm.Activate();
              }),
              null,
              Timeout.Infinite,
              false
            );

            Application.Run();
            showWaitHandle.Unregister(null);
            notificationIcon.Visible = false;

          }

          logic.SaveConfiguration();
        }

        GC.KeepAlive(mutex);
      }
    }
  }
}
