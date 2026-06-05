using System;
using System.Diagnostics;
using System.IO;

namespace Filesystem_Toolbox.Core.Commands {

  /// <summary>
  /// Runs the user-configured command when corruption is found. The template may contain
  /// <c>{file}</c> and <c>{folder}</c> placeholders which are replaced by the affected
  /// file's full path and its watched root respectively.
  /// </summary>
  public static class OnCorruptionCommandRunner {

    public const string FILE_PLACEHOLDER = "{file}";
    public const string FOLDER_PLACEHOLDER = "{folder}";

    /// <summary>Substitutes the placeholders; exposed separately for testability.</summary>
    public static string BuildCommandLine(string commandTemplate, FileInfo file, DirectoryInfo folder) {
      if (commandTemplate == null) throw new ArgumentNullException(nameof(commandTemplate));

      return commandTemplate
        .Replace(FILE_PLACEHOLDER, file?.FullName ?? string.Empty)
        .Replace(FOLDER_PLACEHOLDER, folder?.FullName ?? string.Empty)
        ;
    }

    /// <summary>Splits a command line into executable and arguments, honoring a quoted executable path.</summary>
    public static (string executable, string arguments) SplitCommandLine(string commandLine) {
      if (commandLine == null) throw new ArgumentNullException(nameof(commandLine));

      var trimmed = commandLine.Trim();
      if (trimmed.Length == 0)
        throw new ArgumentException("Command line is empty", nameof(commandLine));

      if (trimmed[0] == '"') {
        var closing = trimmed.IndexOf('"', 1);
        if (closing < 0)
          return (trimmed.Trim('"'), string.Empty);

        return (trimmed.Substring(1, closing - 1), trimmed.Substring(closing + 1).TrimStart());
      }

      var space = trimmed.IndexOf(' ');
      return space < 0
        ? (trimmed, string.Empty)
        : (trimmed.Substring(0, space), trimmed.Substring(space + 1).TrimStart())
        ;
    }

    /// <summary>
    /// Runs the command for one file; <c>false</c> when the template is empty or the process could not start.
    /// </summary>
    public static bool Run(string commandTemplate, FileInfo file, DirectoryInfo folder) {
      if (string.IsNullOrWhiteSpace(commandTemplate))
        return false;

      var (executable, arguments) = SplitCommandLine(BuildCommandLine(commandTemplate, file, folder));

      try {
        using (Process.Start(new ProcessStartInfo {
          FileName = executable,
          Arguments = arguments,
          UseShellExecute = false,
          CreateNoWindow = true,
        })) { }
        return true;
      } catch (Exception) {
        return false;
      }
    }

  }
}
