using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Mimi.Tests;

/// <summary>
/// The installer is the only artefact users run before the app itself, and it is
/// the one place where a mistake can destroy data that is not ours. These checks
/// encode the decisions D-02 was settled on, so a later edit cannot quietly undo
/// them:
///
/// * the AppId is permanent — changing it turns every existing install into a
///   second, parallel copy;
/// * the install is per-user and never owns %LOCALAPPDATA%\Mimi;
/// * the shortcut path and AppUserModelID match what the app itself creates, or
///   Windows shows two Mimis;
/// * uninstalling keeps the library unless the user says otherwise, twice.
/// </summary>
public sealed class InstallerTests
{
    private const string InstallerScript = @"installer\Mimi.iss";

    [Fact]
    public void TheAppIdIsAFixedGuidAndSaysWhyItMustNotChange()
    {
        var script = Read(InstallerScript);

        // Inno escapes a literal brace by doubling it.
        Assert.Matches(
            new Regex(
                @"^AppId=\{\{\{?[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12}\}\r?$",
                RegexOptions.Multiline),
            script);

        Assert.Contains("CHANGING IT MAKES EVERY EXISTING INSTALL A", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheInstallerNeedsNoAdministrator()
    {
        var script = Read(InstallerScript);

        Assert.Contains("PrivilegesRequired=lowest", script, StringComparison.Ordinal);
        Assert.DoesNotContain("PrivilegesRequired=admin", script, StringComparison.Ordinal);

        // Per-user program folder, separate from the data folder.
        Assert.Contains(
            @"DefaultDirName={localappdata}\Programs\{#AppName}",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheInstallerNeverOwnsTheLibraryFolder()
    {
        var script = Read(InstallerScript);

        // No [UninstallDelete] section at all: the uninstaller removes only what
        // it installed, and the data folder is a different path anyway.
        Assert.DoesNotContain("\n[UninstallDelete]", script, StringComparison.Ordinal);

        // Deleting data is a prompt, defaults to "keep", and is gated by a flag
        // the prompt sets - never by anything the installer decides on its own.
        Assert.Contains("Keep your Mimi library, settings and logs?", script, StringComparison.Ordinal);
        Assert.Contains("MB_YESNO or MB_DEFBUTTON1", script, StringComparison.Ordinal);
        Assert.Contains("RemoveUserData := (Answer = IDNO);", script, StringComparison.Ordinal);
        Assert.Contains("if RemoveUserData then", script, StringComparison.Ordinal);

        // A silent uninstall must not delete anything either.
        Assert.Contains("if not UninstallSilent then", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSilentInstallPathIsGuardedToo()
    {
        var script = Read(InstallerScript);

        // /SILENT and /VERYSILENT never show the directory page, so a guard that
        // only lives in NextButtonClick would protect nobody who scripts the
        // install - and it is exactly those runs that would empty a folder.
        Assert.Contains("function PrepareToInstall(var NeedsRestart: Boolean): String;", script, StringComparison.Ordinal);
        Assert.Contains("TargetProblem(ExpandConstant('{app}'))", script, StringComparison.Ordinal);
        Assert.Contains("Problem := TargetProblem(WizardDirValue);", script, StringComparison.Ordinal);
    }

    [Fact]
    public void EverySectionHeaderIsRealAndThePascalLivesInCode()
    {
        var script = Read(InstallerScript);

        // Inno treats everything after an unknown section header as an entry in
        // the previous one, so a missing [Code] line turns the whole script into
        // garbage that fails to compile - or worse, compiles into something else.
        var headers = Regex.Matches(script, @"^\[([A-Za-z]+)\]", RegexOptions.Multiline)
            .Cast<Match>()
            .Select(m => m.Groups[1].Value)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "Setup", "Languages", "Tasks", "Files", "InstallDelete",
                "Icons", "Run", "Code",
            },
            headers);

        // Every [Code] construct must sit after that header, not before it.
        var codeAt = script.IndexOf("\n[Code]", StringComparison.Ordinal);
        Assert.True(codeAt > 0);
        foreach (var marker in new[] { "function NextButtonClick", "function PrepareToInstall", "procedure CurUninstallStepChanged" })
        {
            Assert.True(
                script.IndexOf(marker, StringComparison.Ordinal) > codeAt,
                $"{marker} is outside [Code]");
        }
    }

    [Fact]
    public void PointingTheInstallerAtRealDataIsRefused()
    {
        var script = Read(InstallerScript);

        Assert.Contains("function IsInsideUserData", script, StringComparison.Ordinal);
        Assert.Contains("'Mimi cannot be installed inside its own data folder.'", script, StringComparison.Ordinal);

        // And a folder full of someone else's files is refused too, because
        // [InstallDelete] clears the folder it installs into.
        Assert.Contains("LooksLikeMimiInstall", script, StringComparison.Ordinal);
        Assert.Contains(
            "'That folder already contains something that is not a Mimi '",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheShortcutMatchesTheOneTheAppCreates()
    {
        var script = Read(InstallerScript);
        var identity = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "src", "Mimi.App", "Services", "WindowsAppIdentity.cs"));

        // The app writes {userprograms}\Mimi.lnk and stamps its AppUserModelID on
        // it. If the installer used a group folder or a different id, Windows
        // would list Mimi twice and group the windows separately.
        Assert.Contains(@"Name: ""{userprograms}\{#AppName}""", script, StringComparison.Ordinal);
        Assert.Contains(@"AppUserModelID: ""{#AppUserModelId}""", script, StringComparison.Ordinal);
        Assert.Contains(@"""Mimi.lnk""", identity, StringComparison.Ordinal);

        // One identity string, agreed by both sides.
        var declared = Regex.Match(script, @"#define AppUserModelId ""([^""]+)""").Groups[1].Value;
        var appConstant = Regex
            .Match(identity, @"AppUserModelId = ""([^""]+)""")
            .Groups[1].Value;

        Assert.False(string.IsNullOrWhiteSpace(declared));
        Assert.Equal(appConstant, declared);
    }

    [Fact]
    public void TheVersionComesFromTheBuildNotFromThisFile()
    {
        var script = Read(InstallerScript);

        // A version written here would drift from the executable.
        Assert.Contains("#ifndef AppVersion", script, StringComparison.Ordinal);
        Assert.Contains("#error AppVersion is required", script, StringComparison.Ordinal);
        Assert.DoesNotContain("AppVersion=0.", script, StringComparison.Ordinal);
        Assert.Contains(@"AppVersion={#AppVersion}", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLicenceAndTheCreditsTravelWithTheInstaller()
    {
        var script = Read(InstallerScript);

        Assert.Contains(@"LicenseFile=..\LICENSE", script, StringComparison.Ordinal);
        Assert.Contains("InfoBeforeFile=BEFORE-INSTALL.txt", script, StringComparison.Ordinal);

        Assert.True(File.Exists(Path.Combine(RepositoryRoot(), "LICENSE")));
        Assert.True(File.Exists(Path.Combine(RepositoryRoot(), "installer", "BEFORE-INSTALL.txt")));

        // The files the wizard shows are read by the compiler: keep them ASCII so
        // a non-English locale cannot turn them into mojibake on screen.
        AssertAsciiOnly(Path.Combine(RepositoryRoot(), InstallerScript));
        AssertAsciiOnly(Path.Combine(RepositoryRoot(), "installer", "BEFORE-INSTALL.txt"));
    }

    [Fact]
    public void UninstallingRemovesTheRegistrationTheAppMade()
    {
        var script = Read(InstallerScript);
        var code = Section(script, "[Code]", null);

        // Left behind, these advertise an executable that no longer exists.
        Assert.Contains(@"Software\Mimi'", code, StringComparison.Ordinal);
        Assert.Contains(@"Software\Classes\{#AppProgId}'", code, StringComparison.Ordinal);
        Assert.Contains("RegDeleteKeyIncludingSubkeys", code, StringComparison.Ordinal);

        // And nothing is written outside HKEY_CURRENT_USER: this is a per-user
        // install, so it must not need elevation for its own cleanup either.
        Assert.DoesNotContain("HKLM", code, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCleanupNamesTheSameKeysTheAppWrites()
    {
        var script = Read(InstallerScript);
        var service = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "src", "Mimi.App", "Services", "WindowsFileAssociationService.cs"));

        // Both sides must agree on the exact key and the exact ProgID. They did
        // not, once: the installer cleaned the RegisteredApplications key under
        // Software\Microsoft\Windows\CurrentVersion, while the app writes the
        // one directly under Software. The value would have survived every
        // uninstall, and nothing would have reported an error.
        var registeredApplications = Regex
            .Match(service, @"RegisteredApplicationsPath = @""([^""]+)""")
            .Groups[1].Value;
        var progId = Regex.Match(service, @"ProgId = ""([^""]+)""").Groups[1].Value;

        Assert.False(string.IsNullOrWhiteSpace(registeredApplications));
        Assert.False(string.IsNullOrWhiteSpace(progId));
        Assert.Contains(
            $"RegisteredApplicationsPath = '{registeredApplications}';",
            script,
            StringComparison.Ordinal);

        var declaredProgId = Regex
            .Match(script, @"#define AppProgId ""([^""]+)""")
            .Groups[1].Value;
        Assert.Equal(progId, declaredProgId);

        // The application name is the value name inside that key, and the name
        // the installer shows everywhere else. One name, both files.
        var applicationName = Regex
            .Match(service, @"ApplicationName = ""([^""]+)""")
            .Groups[1].Value;
        var declaredName = Regex
            .Match(script, @"#define AppName ""([^""]+)""")
            .Groups[1].Value;

        Assert.Equal(applicationName, declaredName);
        Assert.Contains("RegisteredApplicationsPath, '{#AppName}'", script, StringComparison.Ordinal);
    }

    [Fact]
    public void UninstallClearsOpenWithAliasesForExactlyTheSupportedExtensions()
    {
        var script = Read(InstallerScript);
        var extensions = File.ReadAllText(Path.Combine(
                RepositoryRoot(), "src", "Mimi.Core", "Services", "SupportedAudioFiles.cs"))
            .Split('\n')
            .Select(line => line.Trim().TrimEnd(','))
            .Where(line => Regex.IsMatch(line, @"^""\.[a-z0-9]+""$"))
            .Select(line => line.Trim('"'))
            .ToArray();

        Assert.Equal(8, extensions.Length);

        // The list Mimi can open is the list that has to be cleaned up. A new
        // format added to the app would otherwise leave "Open with Mimi" behind.
        foreach (var extension in extensions)
        {
            Assert.Contains(
                $"RemoveOpenWithProgid('{extension}');",
                script,
                StringComparison.Ordinal);
        }

        // And only Mimi's own value is removed: those keys belong to Windows and
        // hold other applications' entries too, so the uninstaller never deletes
        // one wholesale - it deletes a value inside it.
        var code = Section(script, "[Code]", null);
        Assert.Equal(
            4,
            Regex.Matches(code, "RegDeleteKeyIncludingSubkeys\\(").Count);

        foreach (var key in new[]
                 {
                     @"'Software\Mimi'",
                     @"'Software\Classes\{#AppProgId}'",
                     @"'Software\Classes\Applications\{#AppExe}'",
                     @"'Software\Microsoft\Windows\CurrentVersion\App Paths\{#AppExe}'",
                 })
        {
            Assert.Contains(key, code, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheReadmeSaysWhereToGetTheInstaller()
    {
        var readme = Read("README.md");

        // Shipping a file nobody is told about is the same as not shipping it.
        Assert.Contains("releases/latest", readme, StringComparison.Ordinal);
        Assert.Contains("Setup.exe", readme, StringComparison.Ordinal);
        Assert.Contains("%LOCALAPPDATA%\\Programs\\Mimi", readme, StringComparison.Ordinal);

        // And the promise the installer keeps has to match what the page says.
        Assert.Contains("does not touch your library", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void TheInstallerIsCompiledOnEveryBuildAndNotOnlyAtRelease()
    {
        var build = Read(Path.Combine(".github", "workflows", "windows-build.yml"));
        Assert.Contains("Compile the installer (dry run)", build, StringComparison.Ordinal);
        Assert.Contains("installer/Mimi.iss", build, StringComparison.Ordinal);

        // The runner image does not always ship Inno Setup, so the workflow has
        // to find it or install it rather than assume.
        Assert.Contains("choco install innosetup", build, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReleaseShipsTheInstallerAndAPublishedChecksum()
    {
        var release = Read(Path.Combine(".github", "workflows", "release.yml"));

        Assert.Contains("Build the installer", release, StringComparison.Ordinal);
        Assert.Contains("/DAppVersion=", release, StringComparison.Ordinal);

        // The zip and the installer must come from the same publish output.
        Assert.Contains("Rename-Item -LiteralPath publish/win-x64", release, StringComparison.Ordinal);

        Assert.Contains("SHA256SUMS.txt", release, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256", release, StringComparison.Ordinal);
        Assert.Contains("gh release upload $tag $asset $setup dist/SHA256SUMS.txt", release, StringComparison.Ordinal);

        // Set-Content writes CRLF, and "sha256sum -c" then hunts for a file whose
        // name ends in a carriage return. Only the two downloads are listed.
        Assert.Contains("[System.IO.File]::WriteAllText(", release, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Content -LiteralPath dist/SHA256SUMS.txt", release, StringComparison.Ordinal);
    }

    private static void AssertAsciiOnly(string path)
    {
        var text = File.ReadAllText(path);
        var offenders = text.Where(c => c > 127).Distinct().ToArray();
        Assert.True(
            offenders.Length == 0,
            $"{Path.GetFileName(path)} contains non-ASCII characters: " +
            string.Join(", ", offenders.Select(c => $"U+{(int)c:X4}")));
    }

    /// <summary>
    /// Finds a section by its header line. Anchored on purpose: the script talks
    /// about sections in comments, and a substring search happily matched a
    /// comment once - which is how a missing [Code] header went unnoticed.
    /// </summary>
    private static string Section(string text, string header, string? endHeader)
    {
        var from = HeaderMatch(text, header, 0);
        if (endHeader is null) return text[from..];

        var to = HeaderMatch(text, endHeader, from);
        return text[from..to];
    }

    private static int HeaderMatch(string text, string header, int startAt)
    {
        var match = Regex.Match(
            text[startAt..],
            "^" + Regex.Escape(header) + @"\s*\r?$",
            RegexOptions.Multiline);

        Assert.True(match.Success, $"Missing section header {header}");
        return startAt + match.Index;
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Mimi.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new Xunit.Sdk.XunitException(
            "Could not locate the repository root (Mimi.sln) above " +
            AppContext.BaseDirectory);
    }
}
