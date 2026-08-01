using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;

namespace SimpleNvidiaUndervolt;

/// <summary>Puts the install directory on the system PATH, so any new terminal can run
/// <c>simple-nvidia-undervolt</c> by name. Machine-wide (HKLM) like the install itself, which is
/// also why the elevation the install already has suffices. The registry is edited directly instead
/// of through <see cref="Environment.SetEnvironmentVariable(string,string?,EnvironmentVariableTarget)"/>,
/// which rewrites the value as plain REG_SZ: the machine PATH is REG_EXPAND_SZ full of
/// <c>%SystemRoot%</c>-style entries that would then stop expanding.</summary>
internal static class SystemPath
{
    private const string EnvironmentKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment";
    private const string PathValueName = "Path";

    /// <summary>Adds the directory to the machine PATH when it isn't already listed, returning
    /// whether it added. Membership is decided on a read-only open — the already-listed case then
    /// succeeds without write access, like <see cref="Persistence.InstallApp"/>'s read-only
    /// already-installed check on a <c>--no-elevate</c> re-run. Running processes keep the PATH they
    /// started with; the settings-change broadcast makes Explorer re-read it, so terminals launched
    /// after the install see the entry.</summary>
    public static bool Add(string dir)
    {
        try
        {
            string current = ReadPathValue();
            if (ContainsDir(current, dir))
            {
                return false;
            }

            using RegistryKey key = OpenEnvironmentKey(writable: true);
            key.SetValue(PathValueName, Append(current, dir), RegistryValueKind.ExpandString);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            // Typically the environment refusing the write - a non-elevated run (--no-elevate), or a
            // policy/security product guarding this key - so report the message, not a stack (see
            // ErrorReporter.Describe).
            throw new CliError($"Adding {dir} to the system PATH failed: {ex.Message}");
        }

        BroadcastSettingsChange();
        return true;
    }

    /// <summary>The machine PATH's raw (unexpanded) text. A missing or non-string value is refused
    /// rather than read as empty: appending to a misread would replace the whole machine PATH with
    /// just the install directory, and on a real machine the value is never actually empty.</summary>
    private static string ReadPathValue()
    {
        using RegistryKey key = OpenEnvironmentKey(writable: false);
        return key.GetValue(PathValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames)
                is string { Length: > 0 } value
            ? value
            : throw new CliError(
                $@"The machine PATH value (HKLM\{EnvironmentKey}\{PathValueName}) is missing or not text.");
    }

    private static RegistryKey OpenEnvironmentKey(bool writable)
        => Registry.LocalMachine.OpenSubKey(EnvironmentKey, writable)
           ?? throw new CliError($@"The registry key HKLM\{EnvironmentKey} is missing.");

    /// <summary>Whether a PATH value already lists the directory: entries compare expanded (an
    /// entry written as <c>%ProgramFiles%\...</c> still counts), case-insensitively, and ignoring
    /// surrounding whitespace and a trailing backslash.</summary>
    internal static bool ContainsDir(string pathValue, string dir)
        => pathValue.Split(';').Any(entry =>
            Normalize(Environment.ExpandEnvironmentVariables(entry))
                .Equals(Normalize(dir), StringComparison.OrdinalIgnoreCase));

    /// <summary>The PATH value with the directory appended: the existing text stays byte-identical
    /// (its entries are other software's configuration) - a separator is added only when the value
    /// doesn't already end with one.</summary>
    internal static string Append(string pathValue, string dir)
        => pathValue.Length == 0 ? dir
            : pathValue.EndsWith(';') ? pathValue + dir
            : pathValue + ';' + dir;

    private static string Normalize(string path) => path.Trim().TrimEnd('\\');

    /// <summary>Tells running windows the environment changed — the same broadcast the system
    /// environment-variables dialog sends. Fire-and-forget with a short per-window timeout, so one
    /// hung window can't stall the install; a missed broadcast only means the new PATH waits for
    /// the next logon.</summary>
    private static void BroadcastSettingsChange()
        => SendMessageTimeoutW(HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, "Environment",
            SMTO_ABORTIFHUNG, 5000, out _);

    private static readonly IntPtr HWND_BROADCAST = new(0xFFFF);
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    /// <summary>For WM_SETTINGCHANGE, lParam names the changed setting as a wide string.</summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeoutW(IntPtr hWnd, uint msg, IntPtr wParam,
        string lParam, uint flags, uint timeoutMs, out IntPtr result);
}
