using Filesystem_Toolbox.Core.Commands;

namespace Filesystem_Toolbox.Tests.Unit;

[TestFixture]
[Category("Unit")]
public class OnCorruptionCommandRunnerTests {

  [Test]
  public void Given_TemplateWithPlaceholders_When_Building_Then_BothAreSubstituted() {
    var file = new FileInfo(@"C:\data\broken.txt");
    var folder = new DirectoryInfo(@"C:\data");

    var result = OnCorruptionCommandRunner.BuildCommandLine("notify.exe --file \"{file}\" --root \"{folder}\"", file, folder);

    Assert.That(result, Is.EqualTo($"notify.exe --file \"{file.FullName}\" --root \"{folder.FullName}\""));
  }

  [Test]
  public void Given_TemplateWithRepeatedPlaceholder_When_Building_Then_AllOccurrencesAreSubstituted() {
    var file = new FileInfo(@"C:\a.txt");

    var result = OnCorruptionCommandRunner.BuildCommandLine("{file} {file}", file, null);

    Assert.That(result, Is.EqualTo($"{file.FullName} {file.FullName}"));
  }

  [Test]
  public void Given_NullTemplate_When_Building_Then_ArgumentNullExceptionIsThrown()
    => Assert.That(() => OnCorruptionCommandRunner.BuildCommandLine(null!, null, null), Throws.ArgumentNullException);

  [TestCase("tool.exe", "tool.exe", "")]
  [TestCase("tool.exe --arg", "tool.exe", "--arg")]
  [TestCase("\"C:\\Program Files\\tool.exe\" --arg x", "C:\\Program Files\\tool.exe", "--arg x")]
  [TestCase("\"C:\\tool.exe\"", "C:\\tool.exe", "")]
  [TestCase("  tool.exe  --arg ", "tool.exe", "--arg")]
  public void Given_CommandLine_When_Splitting_Then_ExecutableAndArgumentsSeparate(string commandLine, string expectedExecutable, string expectedArguments) {
    var (executable, arguments) = OnCorruptionCommandRunner.SplitCommandLine(commandLine);

    Assert.Multiple(() => {
      Assert.That(executable, Is.EqualTo(expectedExecutable));
      Assert.That(arguments.TrimEnd(), Is.EqualTo(expectedArguments));
    });
  }

  [Test]
  public void Given_EmptyCommandLine_When_Splitting_Then_ArgumentExceptionIsThrown()
    => Assert.That(() => OnCorruptionCommandRunner.SplitCommandLine("   "), Throws.ArgumentException);

  [TestCase(null)]
  [TestCase("")]
  [TestCase("  ")]
  public void Given_EmptyTemplate_When_Running_Then_FalseWithoutStartingAnything(string? template)
    => Assert.That(OnCorruptionCommandRunner.Run(template!, new FileInfo(@"C:\x.txt"), new DirectoryInfo(@"C:\")), Is.False);

  [Test]
  public void Given_NonExistingExecutable_When_Running_Then_FalseInsteadOfThrowing()
    => Assert.That(OnCorruptionCommandRunner.Run("this-tool-does-not-exist-anywhere.exe {file}", new FileInfo(@"C:\x.txt"), new DirectoryInfo(@"C:\")), Is.False);

  [Test]
  [Platform("Win")]
  public void Given_RealCommand_When_Running_Then_ItExecutesWithSubstitutedArguments() {
    var marker = new FileInfo(Path.Combine(Path.GetTempPath(), $"FstCmdTest_{Guid.NewGuid()}.txt"));
    try {
      var started = OnCorruptionCommandRunner.Run(
        $"cmd.exe /c echo rotten>\"{marker.FullName}\"",
        new FileInfo(@"C:\x.txt"),
        new DirectoryInfo(@"C:\")
      );

      Assert.Multiple(() => {
        Assert.That(started, Is.True);
        Assert.That(() => File.Exists(marker.FullName), Is.True.After(5000, 50), "the command should have produced its output file");
      });
    } finally {
      if (marker.Exists)
        marker.Delete();
    }
  }

}
