using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.GitlabToGithub.Commands.DiagnoseGitlabExport;
using OctoshiftCLI.GitlabToGithub.Services;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GitlabToGithub.Commands.DiagnoseGitlabExport;

public class DiagnoseGitlabExportCommandHandlerTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<GitlabApi> _mockGitlabApi = TestHelpers.CreateMock<GitlabApi>();
    private readonly Mock<FileSystemProvider> _mockFileSystemProvider = TestHelpers.CreateMock<FileSystemProvider>();
    private readonly Mock<GitlabSshDiagnosticsCollector> _mockSshCollector = new();

    private readonly DiagnoseGitlabExportCommandHandler _handler;

    public DiagnoseGitlabExportCommandHandlerTests()
    {
        _handler = new DiagnoseGitlabExportCommandHandler(_mockOctoLogger.Object, _mockGitlabApi.Object, _mockFileSystemProvider.Object, _mockSshCollector.Object);
    }

    [Fact]
    public async Task Handle_Writes_Report_With_Export_Status_And_Gitlab_Admin_Commands()
    {
        var args = new DiagnoseGitlabExportCommandArgs
        {
            GitlabServerUrl = "https://gitlab.contoso.com",
            GitlabGroup = "parent/group",
            GitlabProject = "project",
            Output = "diagnostics.md"
        };
        string report = null;

        _mockGitlabApi.Setup(m => m.GetServerVersion()).ReturnsAsync(("18.11.0-ee", true));
        _mockGitlabApi.Setup(m => m.GetProjectDetails("parent/group", "project"))
            .ReturnsAsync(new GitlabProjectDetails(123, "parent/group/project", "https://gitlab.contoso.com/parent/group/project", false, "private", 42, 43, 44));
        _mockGitlabApi.Setup(m => m.GetExportDetails("parent/group", "project"))
            .ReturnsAsync(new GitlabExportDetails(123, "failed", null, "{\"export_status\":\"failed\"}"));
        _mockFileSystemProvider.Setup(m => m.WritePrivateTextAsync("diagnostics.md", It.IsAny<string>()))
            .Callback<string, string>((_, contents) => report = contents)
            .Returns(Task.CompletedTask);

        await _handler.Handle(args);

        Assert.Contains("Export status: failed", report);
        Assert.Contains("p.export_jobs", report);
        Assert.DoesNotContain("p.import_state", report);
        Assert.DoesNotContain("Export ID:", report);
        Assert.Contains("same user that initiated", report);
        Assert.Contains("/var/log/gitlab/sidekiq/current", report);
        Assert.Contains("/var/log/gitlab/gitlab-rails/exporter.log", report);
        _mockSshCollector.Verify(m => m.Collect(It.IsAny<DiagnoseGitlabExportCommandArgs>()), Times.Never);
        _mockOctoLogger.Verify(m => m.LogWarning(It.Is<string>(s => s.Contains("GitLab reported the project export as failed"))), Times.Once);
        _mockOctoLogger.Verify(m => m.LogSuccess("Wrote GitLab export diagnostics to diagnostics.md."), Times.Once);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Handle_Collects_Ssh_Only_When_Requested_And_Preserves_Api_Report_On_Failure(bool fail, bool wrongProject)
    {
        var args = new DiagnoseGitlabExportCommandArgs
        {
            GitlabServerUrl = "http://gitlab",
            GitlabGroup = "group",
            GitlabProject = "project",
            Output = "diagnostics.md",
            SshHost = "admin-host"
        };
        _mockGitlabApi.Setup(m => m.GetServerVersion()).ReturnsAsync(("18.3.1", false));
        _mockGitlabApi.Setup(m => m.GetProjectDetails("group", "project"))
            .ReturnsAsync(new GitlabProjectDetails(1, "group/project", "http://gitlab/group/project", false, "private", 1, 0, 0));
        _mockGitlabApi.Setup(m => m.GetExportDetails("group", "project"))
            .ReturnsAsync(new GitlabExportDetails(1, "failed", null, "{\"export_status\":\"failed\"}"));
        string report = null;
        _mockFileSystemProvider.Setup(m => m.WritePrivateTextAsync(args.Output, It.IsAny<string>()))
            .Callback<string, string>((_, contents) => report = contents).Returns(Task.CompletedTask);
        if (fail)
        {
            _mockSshCollector.Setup(m => m.Collect(args)).ThrowsAsync(new OctoshiftCliException("SSH failed"));
        }
        else
        {
            _mockSshCollector.Setup(m => m.Collect(args))
                .ReturnsAsync($"{{\"project_id\":{(wrongProject ? 2 : 1)},\"warnings\":[\"log missing\"],\"error\":\"Permission denied ```\"}}");
        }
        if (fail || wrongProject)
        {
            await Assert.ThrowsAsync<OctoshiftCliException>(() => _handler.Handle(args));
            Assert.Contains("Collection failed", report);
            _mockOctoLogger.Verify(m => m.LogSuccess(It.IsAny<string>()), Times.Never);
        }
        else
        {
            await _handler.Handle(args);
            Assert.Contains("Permission denied \\u0060\\u0060\\u0060", report);
            _mockOctoLogger.Verify(m => m.LogWarning(It.Is<string>(s => s.Contains("collection warnings"))), Times.Once);
        }
        Assert.Contains("Export status: failed", report);
        Assert.Contains("## Server-side diagnostics (SSH)", report);
        _mockSshCollector.Verify(m => m.Collect(args), Times.Once);
        _mockFileSystemProvider.Verify(m => m.WritePrivateTextAsync(args.Output, It.IsAny<string>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_Throws_When_Output_Exists_Without_Overwrite()
    {
        var args = new DiagnoseGitlabExportCommandArgs
        {
            GitlabServerUrl = "https://gitlab.contoso.com",
            GitlabGroup = "parent/group",
            GitlabProject = "project",
            Output = "diagnostics.md"
        };

        _mockFileSystemProvider.Setup(m => m.FileExists("diagnostics.md")).Returns(true);

        var ex = await Assert.ThrowsAsync<OctoshiftCliException>(() => _handler.Handle(args));

        Assert.Equal("File diagnostics.md already exists! Use --overwrite to overwrite this file.", ex.Message);
    }
}
