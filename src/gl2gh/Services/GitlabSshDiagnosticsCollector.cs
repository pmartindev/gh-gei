using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.GitlabToGithub.Commands.DiagnoseGitlabExport;

namespace OctoshiftCLI.GitlabToGithub.Services;

public class GitlabSshDiagnosticsCollector
{
    private const int MAX_OUTPUT_CHARACTERS = 4 * 1024 * 1024;

    public virtual async Task<string> Collect(DiagnoseGitlabExportCommandArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        using var resource = typeof(GitlabSshDiagnosticsCollector).Assembly.GetManifestResourceStream("GitlabExportDiagnostics.rb");
        using var reader = new StreamReader(resource);
        var script = (await reader.ReadToEndAsync()).Replace("__PROJECT_PATH_BASE64__", EncodeProjectPath(args));

        using var process = new Process { StartInfo = BuildStartInfo(args) };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new OctoshiftCliException($"Could not start OpenSSH (ssh). Install the OpenSSH client and ensure it is on PATH. {ex.Message}");
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var stdout = ReadBounded(process.StandardOutput, timeout.Token, timeout.Cancel);
        var stderr = ReadBounded(process.StandardError, timeout.Token, timeout.Cancel);
        try
        {
            var input = WriteScript(process, script, timeout.Token);
            await Task.WhenAll(input, stdout, stderr, process.WaitForExitAsync(timeout.Token));
            if (process.ExitCode != 0)
            {
                throw new OctoshiftCliException($"SSH diagnostics failed (exit {process.ExitCode}). Check the administrator key, trusted known_hosts entry, sudo permissions and container name. {await stderr}");
            }

            if (await input is IOException writeError)
            {
                throw new OctoshiftCliException($"Could not send the diagnostics script over SSH. {writeError.Message}");
            }

            JObject diagnostics;
            try
            {
                diagnostics = JObject.Parse(await stdout);
            }
            catch (JsonReaderException)
            {
                throw new OctoshiftCliException("SSH diagnostics did not return valid JSON. Verify the remote GitLab installation and ensure shell startup scripts do not write to stdout.");
            }
            if (diagnostics["project_path"]?.Value<string>() != $"{args.GitlabGroup}/{args.GitlabProject}" ||
                diagnostics["logs"] is not JArray || diagnostics["warnings"] is not JArray warnings)
            {
                throw new OctoshiftCliException("SSH diagnostics returned an unexpected response. Verify that SSH reaches the intended GitLab installation.");
            }
            if (!string.IsNullOrWhiteSpace(await stderr))
            {
                warnings.Add($"SSH stderr: {await stderr}");
            }
            return diagnostics.ToString();
        }
        catch (OperationCanceledException)
        {
            throw new OctoshiftCliException("SSH diagnostics exceeded the five-minute limit. Check GitLab server load and SSH connectivity.");
        }
        finally
        {
            timeout.Cancel();
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }

    internal static ProcessStartInfo BuildStartInfo(DiagnoseGitlabExportCommandArgs args)
    {
        var startInfo = new ProcessStartInfo("ssh")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in new[]
        {
            "-T", "-o", "BatchMode=yes", "-o", "StrictHostKeyChecking=yes",
            "-o", "IdentitiesOnly=yes", "-o", "ClearAllForwardings=yes", "-o", "ForwardAgent=no",
            "-o", "ConnectTimeout=15", "-o", "ServerAliveInterval=15", "-o", "ServerAliveCountMax=2",
            "-p", args.SshPort.ToString(CultureInfo.InvariantCulture), "-i", args.SshKey,
            "-l", args.SshUser, "--", args.SshHost
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        var command = args.GitlabContainer is null
            ? "gitlab-rails runner -"
            : $"docker exec -i -- '{args.GitlabContainer}' gitlab-rails runner -";
        startInfo.ArgumentList.Add($"if [ \"$(id -u)\" -eq 0 ]; then {command}; else sudo -n {command}; fi");
        return startInfo;
    }

    internal static string EncodeProjectPath(DiagnoseGitlabExportCommandArgs args) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{args.GitlabGroup}/{args.GitlabProject}"));

    private static async Task<IOException> WriteScript(Process process, string script, CancellationToken token)
    {
        try
        {
            await process.StandardInput.WriteAsync(script.AsMemory(), token);
            process.StandardInput.Close();
            return null;
        }
        catch (IOException ex)
        {
            // SSH may reject authentication before reading stdin; report its stderr first.
            return ex;
        }
    }

    internal static async Task<string> ReadBounded(StreamReader reader, CancellationToken token, Action cancel = null)
    {
        var result = new StringBuilder();
        var buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), token)) != 0)
        {
            if (result.Length + count > MAX_OUTPUT_CHARACTERS)
            {
                cancel?.Invoke();
                throw new OctoshiftCliException("SSH diagnostics exceeded the 4,194,304-character output limit. Collect a narrower set of logs directly on the server.");
            }
            result.Append(buffer, 0, count);
        }
        return result.ToString();
    }
}
