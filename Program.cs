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

      using (var logic = new MainLogic()) {
        logic.LoadConfiguration();

        using (var mainForm = new MainForm())
          Application.Run(mainForm);

        logic.SaveConfiguration();
      }
    }
  }
}
