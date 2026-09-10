# GitHub Enterprise Importer CLI

[![Actions Status: CI](https://github.com/github/gh-gei/workflows/CI/badge.svg)](https://github.com/github/gh-gei/actions?query=workflow%3ACI)


The [GitHub Enterprise Importer](https://docs.github.com/en/migrations/using-github-enterprise-importer) (GEI, formerly Octoshift) is a highly customizable API-first migration offering designed to help you move your enterprise to GitHub Enterprise Cloud. The GEI-CLI wraps the GEI APIs as a cross-platform console application to simplify customizing your migration experience.

> GEI is generally available for repository migrations originating from Azure DevOps, GitHub, or GitLab that target GitHub Enterprise Cloud. It is in public beta for repository migrations from BitBucket Server and Data Center to GitHub Enterprise Cloud.

## Using the GEI CLI
There are 4 separate CLIs that we ship as extensions for the official [GitHub CLI](https://github.com/cli/cli#installation):
- `gh gei` - Run migrations between GitHub products
- `gh ado2gh` - Run migrations from Azure DevOps to GitHub
- `gh bbs2gh` - Run migrations from BitBucket Server or Data Center to GitHub
- `gh gl2gh` - Run migrations from GitLab to GitHub

To use `gh gei` first install the latest [GitHub CLI](https://github.com/cli/cli#installation), then run the command
>`gh extension install github/gh-gei`

To use `gh ado2gh` first install the latest [GitHub CLI](https://github.com/cli/cli#installation), then run the command
>`gh extension install github/gh-ado2gh`

To use `gh bbs2gh` first install the latest [GitHub CLI](https://github.com/cli/cli#installation), then run the command
>`gh extension install github/gh-bbs2gh`

To use `gh gl2gh` first install the latest [GitHub CLI](https://github.com/cli/cli#installation), then run the command
>`gh extension install github/gh-gl2gh`

We update the extensions frequently, so make sure you update them on a regular basis:
>`gh extension upgrade github/gh-gei`

To see the available commands and options run:

>`gh gei --help`

>`gh ado2gh --help`

>`gh bbs2gh --help`

>`gh gl2gh --help`

### GitHub to GitHub Usage (GitHub.com -> GitHub.com)
1. Create Personal Access Tokens with access to the source GitHub org, and the target GitHub org (for more details on scopes needed refer to our [official documentation](https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer)).

2. Set the GH_SOURCE_PAT and GH_PAT environment variables.

3. Run the `generate-script` command to generate a migration PowerShell script.
>`gh gei generate-script --github-source-org ORGNAME --github-target-org ORGNAME`

4. The previous command will have created a `migrate.ps1` script. Review the steps in the generated script and tweak if necessary.

5. The migrate.ps1 script requires PowerShell to run. If not already installed see the [install instructions](https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7.2) to install PowerShell on Windows, Linux, or Mac. Then run the script.

Refer to the [official documentation](https://docs.github.com/en/migrations/using-github-enterprise-importer) for more details, including differences when migrating from GitHub Enterprise Server.

#### Migrating from GitHub Enterprise Cloud with data residency (ghe.com)

`gh gei` also supports migrating from a GitHub Enterprise Cloud with data residency tenant. Pass `--github-source-api-url` with the API endpoint of your source tenant (or set the `GH_SOURCE_API_URL` environment variable):

>`gh gei migrate-repo --github-source-org SOURCE_ORG --source-repo SOURCE_REPO --github-source-api-url https://api.SUBDOMAIN.ghe.com --github-target-org TARGET_ORG --target-repo TARGET_REPO`

If the target is also a data residency tenant, add `--target-api-url` and `--target-uploads-url` as usual. `--github-source-api-url` and `--ghes-api-url` cannot be used together.

### Azure DevOps to GitHub Usage
1. Create Personal Access Tokens with access to the Azure DevOps org, and the GitHub org (for more details on scopes needed refer to our [official documentation](https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer)).

2. Set the `ADO_PAT` and `GH_PAT` environment variables.

3. Run the `generate-script` command to generate a migration script.
>`gh ado2gh generate-script --ado-org ORGNAME --github-org ORGNAME --all`

4. The previous command will have created a `migrate.ps1` PowerShell script. Review the steps in the generated script and tweak if necessary.

5. The `migrate.ps1` script requires PowerShell to run. If not already installed see the [install instructions](https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7.2) to install PowerShell on Windows, Linux, or Mac. Then run the script.

Refer to the [official documentation](https://docs.github.com/en/migrations/using-github-enterprise-importer/migrating-repositories-with-github-enterprise-importer/migrating-repositories-from-azure-devops-to-github-enterprise-cloud) for more details.

### Bitbucket Server and Data Center to GitHub Usage
1. Create Personal Access Token for the target GitHub org (for more details on scopes needed refer to our [official documentation](https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer)).

2. Set the `GH_PAT`, `BBS_USERNAME`, and `BBS_PASSWORD` environment variables.

3. If your Bitbucket Server or Data Center instance runs on Windows, set the `SMB_PASSWORD` environment variable.

4. Run the `generate-script` command to generate a migration script.
```
> gh bbs2gh generate-script --bbs-server-url BBS-SERVER-URL \
  --github-org DESTINATION \
  --output FILENAME \
  # Use the following options if your Bitbucket Server instance runs on Linux
  --ssh-user SSH-USER --ssh-private-key PATH-TO-KEY
  # Use the following options if your Bitbucket Server instance runs on Windows
  --smb-user SMB-USER
  # Use the following option if you are running a Bitbucket Data Center cluster or your Bitbucket Server is behind a load balancer
  --archive-download-host ARCHIVE-DOWNLOAD-HOST
```

5. The previous command will have created a `migrate.ps1` PowerShell script. Review the steps in the generated script and tweak if necessary.

6. The `migrate.ps1` script requires PowerShell to run. If not already installed see the [install instructions](https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7.2) to install PowerShell on Windows, Linux, or Mac. Then run the script.

Refer to the [official documentation](https://docs.github.com/en/migrations/using-github-enterprise-importer/migrating-repositories-with-github-enterprise-importer/migrating-repositories-from-bitbucket-server-to-github-enterprise-cloud) for more details.

### GitLab to GitHub Usage
1. Create a Personal Access Token for the source GitLab instance (with `api` and `read_repository` scopes) and one for the target GitHub org (for more details on scopes needed refer to our [official documentation](https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer)).

2. Set the `GITLAB_PAT` and `GH_PAT` environment variables.

3. Run the `generate-script` command to generate a migration script.
```
> gh gl2gh generate-script --gitlab-server-url GITLAB-SERVER-URL \
  --github-org DESTINATION \
  --output FILENAME
```

The `--gitlab-server-url` flag accepts both GitLab.com (`https://gitlab.com`) and self-hosted GitLab instances.

4. The previous command will have created a `migrate.ps1` PowerShell script. Review the steps in the generated script and tweak if necessary.

5. The `migrate.ps1` script requires PowerShell to run. If not already installed see the [install instructions](https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7.2) to install PowerShell on Windows, Linux, or Mac. Then run the script.

### GitLab export diagnostics

GitLab-to-GitHub migration does not require an administrator SSH key. If an export fails before a GitHub migration ID is created, collect API-visible diagnostics with your `GITLAB_PAT`:

```bash
gh gl2gh diagnose-gitlab-export \
  --gitlab-server-url https://gitlab.example.com \
  --gitlab-group parent/group --gitlab-project project \
  --output diagnostics.md
```

Use the same GitLab user that initiated the export: the export API status is user-specific. No GitHub PAT or migration ID is required for this command.

For self-managed GitLab, optionally add administrator SSH access to collect actual export job/child job IDs, retained errors and matching server log entries:

```bash
gh gl2gh diagnose-gitlab-export \
  --gitlab-server-url https://gitlab.example.com \
  --gitlab-group parent/group --gitlab-project project \
  --output diagnostics.md \
  --ssh-host gitlab-admin.example.com --ssh-user admin \
  --ssh-key /path/to/private-key --ssh-port 22
```

If GitLab runs in Docker on that SSH host, add `--gitlab-container gitlab`. Omit it if SSH already lands inside the GitLab container. SSH must reach the **OS administrator shell**, not GitLab's Git-over-SSH endpoint.

- Install the OpenSSH client (`ssh`) on the machine running the CLI. Verify the server's host key through a trusted channel and add it to your OpenSSH `known_hosts` before running the command. Unknown or changed keys are rejected; host verification is never disabled.
- Supply `--ssh-host`, `--ssh-user` and `--ssh-key` together. Encrypted keys must already be unlocked in `ssh-agent`; SSH password/passphrase prompts are disabled.
- The account must be root or have non-interactive `sudo` access to `gitlab-rails` (or `docker exec` for container installations). Rails runner executes an administrator script and Docker access is effectively root access; use an appropriately authorized account.
- Collection is read-only: it does not start/retry exports or change GitLab settings. It collects the latest 10 export jobs across users, up to 100 relations per job, and the last 8 MiB/100 matching entries of each current `exporter.log`, Sidekiq, `exceptions_json.log` and `api_json.log`. Strings and backtraces are abbreviated. Missing files, unavailable version-specific records and truncation appear as warnings in the report.
- Collection targets Linux-package GitLab installations, directly or inside Docker, with a five-minute SSH limit. Rotated logs, other worker nodes and centralized/Kubernetes logging are not collected automatically; use the administrator follow-up instructions when the report is incomplete.
- If SSH collection fails, the command exits with an error and preserves the API-only report. Use `--overwrite` to replace an existing report.

**Treat reports and verbose CLI logs as sensitive.** Server messages can include customer data, internal paths and credentials. Collection limits which log fields are retained, but does not guarantee secret redaction. Reports are written with owner-only permissions on Linux/macOS; on Windows, secure the output directory with appropriate ACLs. Store all files securely and review/redact them before sharing. The private SSH key stays on the client; only its file path is passed to OpenSSH.

### Skipping version checks

When the CLI is launched, it logs if a newer version of the CLI is available. You can skip this check by setting the `GEI_SKIP_VERSION_CHECK` environment variable to `true`. 

### Skipping GitHub status checks

When the CLI is launched, it logs a warning if there are any ongoing [GitHub incidents](https://www.githubstatus.com/) that might affect your use of the CLI. You can skip this check by setting the `GEI_SKIP_STATUS_CHECK` environment variable to `true`. 

### Configuring multipart upload chunk size

Set the `GITHUB_OWNED_STORAGE_MULTIPART_MEBIBYTES` environment variable to change the archive upload part size. Provide the value in mebibytes (MiB); For example:

```powershell
# Windows PowerShell
$env:GITHUB_OWNED_STORAGE_MULTIPART_MEBIBYTES = "10"
```

```bash
# macOS/Linux
export GITHUB_OWNED_STORAGE_MULTIPART_MEBIBYTES=10
```

This sets the chunk size to 10 MiB (10,485,760 bytes). The minimum supported value is 5 MiB, and the default remains 100 MiB.

This might be needed to improve upload reliability in environments with proxies or very slow connections.

## Contributions

See [Contributing](CONTRIBUTING.md) for more info on how to get involved.
