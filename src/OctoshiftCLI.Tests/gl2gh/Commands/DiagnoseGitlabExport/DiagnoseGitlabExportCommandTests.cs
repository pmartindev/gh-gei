using System;
using FluentAssertions;
using Moq;
using OctoshiftCLI.GitlabToGithub.Commands.DiagnoseGitlabExport;
using OctoshiftCLI.GitlabToGithub.Factories;
using OctoshiftCLI.GitlabToGithub.Services;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GitlabToGithub.Commands.DiagnoseGitlabExport;

public class DiagnoseGitlabExportCommandTests
{
    private const string GITLAB_SERVER_URL = "https://gitlab.contoso.com";
    private const string GITLAB_PAT = "gitlab-pat";

    private readonly Mock<IServiceProvider> _mockServiceProvider = new();
    private readonly Mock<GitlabApiFactory> _mockGitlabApiFactory = TestHelpers.CreateMock<GitlabApiFactory>();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<FileSystemProvider> _mockFileSystemProvider = TestHelpers.CreateMock<FileSystemProvider>();

    private readonly DiagnoseGitlabExportCommand _command = [];

    public DiagnoseGitlabExportCommandTests()
    {
        _mockServiceProvider.Setup(m => m.GetService(typeof(OctoLogger))).Returns(_mockOctoLogger.Object);
        _mockServiceProvider.Setup(m => m.GetService(typeof(GitlabApiFactory))).Returns(_mockGitlabApiFactory.Object);
        _mockServiceProvider.Setup(m => m.GetService(typeof(FileSystemProvider))).Returns(_mockFileSystemProvider.Object);
        _mockServiceProvider.Setup(m => m.GetService(typeof(GitlabSshDiagnosticsCollector))).Returns(new GitlabSshDiagnosticsCollector());
    }

    [Fact]
    public void Should_Have_Options()
    {
        _command.Should().NotBeNull();
        _command.Name.Should().Be("diagnose-gitlab-export");
        _command.Options.Count.Should().Be(13);

        TestHelpers.VerifyCommandOption(_command.Options, "gitlab-server-url", false);
        TestHelpers.VerifyCommandOption(_command.Options, "gitlab-group", false);
        TestHelpers.VerifyCommandOption(_command.Options, "gitlab-project", false);
        TestHelpers.VerifyCommandOption(_command.Options, "gitlab-pat", false);
        TestHelpers.VerifyCommandOption(_command.Options, "output", false);
        TestHelpers.VerifyCommandOption(_command.Options, "overwrite", false);
        TestHelpers.VerifyCommandOption(_command.Options, "no-ssl-verify", false);
        TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        TestHelpers.VerifyCommandOption(_command.Options, "ssh-host", false);
        TestHelpers.VerifyCommandOption(_command.Options, "ssh-user", false);
        TestHelpers.VerifyCommandOption(_command.Options, "ssh-key", false);
        TestHelpers.VerifyCommandOption(_command.Options, "ssh-port", false);
        TestHelpers.VerifyCommandOption(_command.Options, "gitlab-container", false);
    }

    [Fact]
    public void It_Creates_The_GitlabApi_With_The_Provided_Server_Url_And_Pat()
    {
        var args = new DiagnoseGitlabExportCommandArgs
        {
            GitlabServerUrl = GITLAB_SERVER_URL,
            GitlabPat = GITLAB_PAT,
            NoSslVerify = true
        };

        _command.BuildHandler(args, _mockServiceProvider.Object);

        _mockGitlabApiFactory.Verify(m => m.Create(GITLAB_SERVER_URL, GITLAB_PAT, true));
    }
}
