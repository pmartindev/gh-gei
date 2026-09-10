using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GitlabToGithub.Commands.DiagnoseGitlabExport;

public class DiagnoseGitlabExportCommandArgs : CommandArgs
{
    public string GitlabServerUrl { get; set; }
    public string GitlabGroup { get; set; }
    public string GitlabProject { get; set; }
    [Secret]
    public string GitlabPat { get; set; }
    public string Output { get; set; }
    public bool Overwrite { get; set; }
    public bool NoSslVerify { get; set; }
    public string SshHost { get; set; }
    public string SshUser { get; set; }
    public string SshKey { get; set; }
    public int SshPort { get; set; } = 22;
    public string GitlabContainer { get; set; }

    public override void Validate(OctoLogger log)
    {
        if (GitlabServerUrl.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("--gitlab-server-url must be provided.");
        }

        if (GitlabGroup.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("--gitlab-group must be provided.");
        }

        if (GitlabProject.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("--gitlab-project must be provided.");
        }

        if (Output.HasValue() && Directory.Exists(Output))
        {
            throw new OctoshiftCliException("--output must be a file path, not a directory.");
        }

        if (SshHost is null && SshUser is null && SshKey is null && GitlabContainer is null && SshPort == 22)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SshHost) || string.IsNullOrWhiteSpace(SshUser) || string.IsNullOrWhiteSpace(SshKey))
        {
            throw new OctoshiftCliException("--ssh-host, --ssh-user and --ssh-key must be provided together.");
        }

        if ((!IPAddress.TryParse(SshHost, out _) && !Regex.IsMatch(SshHost, @"\A[a-zA-Z0-9][a-zA-Z0-9._-]*\z")) ||
            !Regex.IsMatch(SshUser, @"\A[a-zA-Z0-9_][a-zA-Z0-9_.-]*\z"))
        {
            throw new OctoshiftCliException("--ssh-host must be a hostname or IP address, and --ssh-user must be an OS account name.");
        }

        if (SshPort is < 1 or > 65535)
        {
            throw new OctoshiftCliException("--ssh-port must be between 1 and 65535.");
        }

        if (!File.Exists(SshKey))
        {
            throw new OctoshiftCliException("--ssh-key must point to an existing private key file.");
        }

        if (GitlabContainer is not null && !Regex.IsMatch(GitlabContainer, @"\A[a-zA-Z0-9][a-zA-Z0-9_.-]*\z"))
        {
            throw new OctoshiftCliException("--gitlab-container must be a Docker container name or ID.");
        }
    }
}
