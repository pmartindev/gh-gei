using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.GitlabToGithub.Services;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GitlabToGithub.Commands.DiagnoseGitlabExport;

public class DiagnoseGitlabExportCommandHandler : ICommandHandler<DiagnoseGitlabExportCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly GitlabApi _gitlabApi;
    private readonly FileSystemProvider _fileSystemProvider;
    private readonly GitlabSshDiagnosticsCollector _sshCollector;

    public DiagnoseGitlabExportCommandHandler(OctoLogger log, GitlabApi gitlabApi, FileSystemProvider fileSystemProvider, GitlabSshDiagnosticsCollector sshCollector)
    {
        _log = log;
        _gitlabApi = gitlabApi;
        _fileSystemProvider = fileSystemProvider;
        _sshCollector = sshCollector;
    }

    public async Task Handle(DiagnoseGitlabExportCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        var output = args.Output.HasValue()
            ? args.Output
            : $"gitlab-export-diagnostics-{SanitizeFileName(args.GitlabGroup)}-{SanitizeFileName(args.GitlabProject)}.md";

        if (_fileSystemProvider.FileExists(output) && !args.Overwrite)
        {
            throw new OctoshiftCliException($"File {output} already exists! Use --overwrite to overwrite this file.");
        }

        _log.LogInformation("Collecting GitLab export diagnostics...");

        var (version, enterprise) = await _gitlabApi.GetServerVersion();
        var projectDetails = await _gitlabApi.GetProjectDetails(args.GitlabGroup, args.GitlabProject);
        var exportDetails = await _gitlabApi.GetExportDetails(args.GitlabGroup, args.GitlabProject);

        var report = BuildReport(args, version, enterprise, projectDetails, exportDetails);
        await _fileSystemProvider.WritePrivateTextAsync(output, report);

        if (args.SshHost.HasValue())
        {
            var sshSummary = $"\n## Server-side diagnostics (SSH)\n\n- SSH host: {args.SshHost}:{args.SshPort}\n- SSH user: {args.SshUser}\n- GitLab container: {args.GitlabContainer ?? "(direct installation)"}\n";
            _log.LogWarning("Collecting administrator diagnostics over SSH. The report may contain sensitive project data, paths and error messages; review and redact it before sharing.");
            try
            {
                var diagnostics = await _sshCollector.Collect(args);
                var data = JObject.Parse(diagnostics);
                if ((long?)data["project_id"] != projectDetails.Id)
                {
                    throw new OctoshiftCliException("The SSH project ID does not match the GitLab API project ID. Verify that SSH reaches the intended GitLab installation.");
                }
                if (data["warnings"] is JArray { Count: > 0 })
                {
                    _log.LogWarning("Server-side diagnostics have collection warnings. See the warnings in the report for missing logs and collection limits.");
                }
                report += $"{sshSummary}\n```json\n{diagnostics.Replace("`", "\\u0060")}\n```\n";
                await _fileSystemProvider.WritePrivateTextAsync(output, report);
            }
            catch (OctoshiftCliException)
            {
                await _fileSystemProvider.WritePrivateTextAsync(output, report + sshSummary + "\nCollection failed. API-only evidence is preserved above; see the CLI error for details.\n");
                _log.LogWarning($"SSH collection failed. API-only diagnostics were preserved in {output}.");
                throw;
            }
        }

        if (string.Equals(exportDetails.ExportStatus, "failed", StringComparison.OrdinalIgnoreCase))
        {
            _log.LogWarning("GitLab reported the project export as failed before GitHub received an archive. Ask a GitLab administrator to inspect the project export job on the GitLab instance using the commands in the report.");
        }

        _log.LogSuccess($"Wrote GitLab export diagnostics to {output}.");
    }

    private static string BuildReport(
        DiagnoseGitlabExportCommandArgs args,
        string version,
        bool enterprise,
        GitlabProjectDetails projectDetails,
        GitlabExportDetails exportDetails)
    {
        var projectPath = $"{args.GitlabGroup}/{args.GitlabProject}";
        var encodedProjectPath = GitlabSshDiagnosticsCollector.EncodeProjectPath(args);
        var builder = new StringBuilder();

        builder.AppendLine("# GitLab export diagnostics");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine($"- GitLab server: {args.GitlabServerUrl.TrimEnd('/')}");
        builder.AppendLine($"- GitLab version: {ValueOrUnknown(version)} ({(enterprise ? "Enterprise" : "Community")} Edition)");
        builder.AppendLine($"- Project path: {projectPath}");
        builder.AppendLine($"- Project ID: {ValueOrUnknown(projectDetails.Id)}");
        builder.AppendLine($"- Export status: {ValueOrUnknown(exportDetails.ExportStatus)}");
        builder.AppendLine("- API export status is scoped to the authenticated GitLab user. Use the same user that initiated the export; another user may see `none`.");
        builder.AppendLine();
        builder.AppendLine("## Project details");
        builder.AppendLine();
        builder.AppendLine($"- Web URL: {ValueOrUnknown(projectDetails.WebUrl)}");
        builder.AppendLine($"- Visibility: {ValueOrUnknown(projectDetails.Visibility)}");
        builder.AppendLine($"- Archived: {ValueOrUnknown(projectDetails.Archived)}");
        builder.AppendLine($"- Repository size: {ValueOrUnknown(projectDetails.RepositorySize)} bytes");
        builder.AppendLine($"- Uploads size: {ValueOrUnknown(projectDetails.UploadsSize)} bytes");
        builder.AppendLine($"- Job artifacts size: {ValueOrUnknown(projectDetails.JobArtifactsSize)} bytes");
        builder.AppendLine();
        builder.AppendLine("## GitLab admin follow-up commands");
        builder.AppendLine();
        builder.AppendLine("Run these commands on the GitLab instance to retrieve the server-side export job error. GitLab does not expose these logs through the project export API.");
        builder.AppendLine();
        builder.AppendLine("```bash");
        builder.AppendLine($"sudo gitlab-rails runner \"p = Project.find_by_full_path(Base64.decode64('{encodedProjectPath}')); puts p.export_jobs.order(created_at: :desc).limit(10).map {{ |j| j.attributes.slice('id', 'jid', 'status', 'user_id', 'created_at', 'updated_at').merge('relations' => j.relation_exports.limit(100).map {{ |r| r.attributes.slice('relation', 'jid', 'status', 'export_error') }}) }}.to_json\"");
        builder.AppendLine("sudo grep -F '<EXPORT_OR_RELATION_JID>' /var/log/gitlab/sidekiq/current");
        builder.AppendLine("sudo tail -n 200 /var/log/gitlab/gitlab-rails/exporter.log");
        builder.AppendLine("sudo tail -n 200 /var/log/gitlab/gitlab-rails/exceptions_json.log");
        builder.AppendLine("```");
        builder.AppendLine("These Linux-package commands use export job records available on recent GitLab versions. For Docker, execute them inside the GitLab container. Correlate by project, export/child job IDs and attempt time; include rotated or centralized logs and other worker nodes when needed.");
        builder.AppendLine();
        builder.AppendLine("## Raw GitLab export API response");
        builder.AppendLine();
        builder.AppendLine("```json");
        builder.AppendLine(exportDetails.RawJson.Replace("`", "\\u0060"));
        builder.AppendLine("```");

        return builder.ToString();
    }

    private static string SanitizeFileName(string value) => Regex.Replace(value, "[^A-Za-z0-9_.-]+", "-").Trim('-');

    private static string ValueOrUnknown(object value) => value?.ToString() ?? "unknown";
}
