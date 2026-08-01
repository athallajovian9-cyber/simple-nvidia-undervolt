namespace SimpleNvidiaUndervolt.Tests;

/// <summary>Tests for the PATH edit the install command makes: how an existing PATH value is
/// searched for the install directory, and how the directory is appended to it.</summary>
public class SystemPathTests
{
    private const string Dir = @"C:\Program Files\simple-nvidia-undervolt";

    [Theory]
    [InlineData(@"C:\Windows;C:\Program Files\simple-nvidia-undervolt")]          // exact entry
    [InlineData(@"C:\PROGRAM FILES\SIMPLE-NVIDIA-UNDERVOLT;C:\Windows")]          // case-insensitive
    [InlineData(@"C:\Windows;C:\Program Files\simple-nvidia-undervolt\")]         // trailing backslash
    [InlineData(@"C:\Windows; C:\Program Files\simple-nvidia-undervolt ;C:\x")]   // padded entry
    public void ContainsDir_FindsTheListedDirectory(string pathValue)
    {
        Assert.True(SystemPath.ContainsDir(pathValue, Dir));
    }

    [Fact]
    public void ContainsDir_ExpandsEnvironmentReferencesInEntries()
    {
        // The machine PATH is REG_EXPAND_SZ, so an entry can spell the directory as
        // %ProgramFiles%\... - it still counts as listed.
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "simple-nvidia-undervolt");
        Assert.True(SystemPath.ContainsDir(@"%ProgramFiles%\simple-nvidia-undervolt", dir));
    }

    [Theory]
    [InlineData("")]                                                // empty PATH
    [InlineData(@"C:\Windows;C:\Program Files")]                    // a parent directory
    [InlineData(@"C:\Program Files\simple-nvidia-undervolt-old")]   // a longer name is not ours
    [InlineData(@"C:\Program Files\simple-nvidia-undervolt\data")]  // a subfolder
    public void ContainsDir_DoesNotMatchOtherEntries(string pathValue)
    {
        Assert.False(SystemPath.ContainsDir(pathValue, Dir));
    }

    [Fact]
    public void Append_KeepsTheExistingValueAndAddsOneEntry()
    {
        // The existing text stays byte-identical - unexpanded %refs included - with the new entry
        // after one separator.
        Assert.Equal(@"%SystemRoot%\system32;C:\Tools;" + Dir,
            SystemPath.Append(@"%SystemRoot%\system32;C:\Tools", Dir));
    }

    [Fact]
    public void Append_EmptyPath_IsJustTheDirectory()
    {
        Assert.Equal(Dir, SystemPath.Append(string.Empty, Dir));
    }

    [Fact]
    public void Append_DoesNotDoubleATrailingSeparator()
    {
        Assert.Equal(@"C:\Windows;" + Dir, SystemPath.Append(@"C:\Windows;", Dir));
    }

    [Fact]
    public void Append_LeavesRepeatedTrailingSeparatorsInPlace()
    {
        // Even odd existing text (a ";;" tail) is someone's configuration: it stays byte-identical,
        // with the new entry after it.
        Assert.Equal(@"C:\Windows;;" + Dir, SystemPath.Append(@"C:\Windows;;", Dir));
    }
}
