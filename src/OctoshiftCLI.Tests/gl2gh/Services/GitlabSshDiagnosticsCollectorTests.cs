using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OctoshiftCLI.GitlabToGithub.Commands.DiagnoseGitlabExport;
using OctoshiftCLI.GitlabToGithub.Services;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GitlabToGithub.Services;

public sealed class GitlabSshDiagnosticsCollectorTests : IDisposable
{
    private readonly string _key = Path.GetTempFileName();

    private DiagnoseGitlabExportCommandArgs Args() => new()
    {
        GitlabServerUrl = "http://gitlab",
        GitlabGroup = "parent/group",
        GitlabProject = "project",
        SshHost = "gitlab-admin",
        SshUser = "admin",
        SshKey = _key
    };

    [Theory]
    [InlineData("gitlab-admin")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public void Validates_Ssh_Options(string host)
    {
        var args = Args();
        args.SshHost = host;
        args.Validate(new OctoLogger());
    }

    [Theory]
    [InlineData("host")]
    [InlineData("user")]
    [InlineData("key")]
    public void Requires_All_Ssh_Inputs(string missing)
    {
        var args = Args();
        if (missing == "host")
        {
            args.SshHost = null;
        }

        if (missing == "user")
        {
            args.SshUser = null;
        }

        if (missing == "key")
        {
            args.SshKey = null;
        }

        Assert.Throws<OctoshiftCliException>(() => args.Validate(new OctoLogger()));
    }

    [Theory]
    [InlineData("-oProxyCommand=bad", "admin", "gitlab", 22)]
    [InlineData("host;bad", "admin", "gitlab", 22)]
    [InlineData("host\nbad", "admin", "gitlab", 22)]
    [InlineData("host", "root;bad", "gitlab", 22)]
    [InlineData("host", "admin", "gitlab';bad", 22)]
    [InlineData("host", "admin", "-bad", 22)]
    [InlineData("host", "admin", "", 22)]
    [InlineData("host", "admin", "gitlab", 0)]
    [InlineData("host", "admin", "gitlab", 65536)]
    public void Rejects_Unsafe_Or_Invalid_Inputs(string host, string user, string container, int port)
    {
        var args = Args();
        args.SshHost = host;
        args.SshUser = user;
        args.GitlabContainer = container;
        args.SshPort = port;
        Assert.Throws<OctoshiftCliException>(() => args.Validate(new OctoLogger()));
    }

    [Fact]
    public void Rejects_Missing_Key_File()
    {
        var args = Args();
        args.SshKey = Path.Combine(_key, "missing");
        Assert.Throws<OctoshiftCliException>(() => args.Validate(new OctoLogger()));
    }

    [Theory]
    [InlineData(null, "gitlab-rails runner -")]
    [InlineData("gitlab-ce", "docker exec -i -- 'gitlab-ce' gitlab-rails runner -")]
    public void Uses_Strict_OpenSsh_With_No_Local_Shell(string container, string remote)
    {
        var args = Args();
        args.GitlabContainer = container;
        args.SshPort = 2222;
        args.SshKey = "/path with spaces/key";
        var start = GitlabSshDiagnosticsCollector.BuildStartInfo(args);
        Assert.Equal("ssh", start.FileName);
        Assert.False(start.UseShellExecute);
        Assert.True(start.RedirectStandardInput);
        Assert.Contains("StrictHostKeyChecking=yes", start.ArgumentList);
        Assert.Contains("BatchMode=yes", start.ArgumentList);
        Assert.Contains("IdentitiesOnly=yes", start.ArgumentList);
        Assert.Contains("ForwardAgent=no", start.ArgumentList);
        Assert.Contains("/path with spaces/key", start.ArgumentList);
        Assert.Contains("2222", start.ArgumentList);
        Assert.Equal($"if [ \"$(id -u)\" -eq 0 ]; then {remote}; else sudo -n {remote}; fi", start.ArgumentList.Last());
    }

    [Fact]
    public void Encodes_Project_Path_As_Data_Not_Ruby_Code()
    {
        var args = Args();
        args.GitlabProject = "quotes'\"$()```";
        var encoded = GitlabSshDiagnosticsCollector.EncodeProjectPath(args);
        Assert.Equal($"{args.GitlabGroup}/{args.GitlabProject}", Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));
        Assert.DoesNotContain("'", encoded);
        Assert.DoesNotContain("$", encoded);
    }

    [Fact]
    public async Task Bounds_Ssh_Output()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(new string('x', (4 * 1024 * 1024) + 1)));
        using var reader = new StreamReader(stream);
        await Assert.ThrowsAsync<OctoshiftCliException>(() => GitlabSshDiagnosticsCollector.ReadBounded(reader, CancellationToken.None));
    }

    [Fact]
    public async Task Reads_Ssh_Output()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"logs\":[]}"));
        using var reader = new StreamReader(stream);
        Assert.Equal("{\"logs\":[]}", await GitlabSshDiagnosticsCollector.ReadBounded(reader, CancellationToken.None));
    }

    [Fact]
    public async Task Writes_Private_Report_Including_When_Overwriting()
    {
        var provider = new FileSystemProvider();
        await provider.WritePrivateTextAsync(_key, "diagnostics");
        Assert.Equal("diagnostics", await File.ReadAllTextAsync(_key));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(_key));
            File.SetUnixFileMode(_key, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead);
            await provider.WritePrivateTextAsync(_key, "updated");
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(_key));
        }
    }

    public void Dispose() => File.Delete(_key);
}
