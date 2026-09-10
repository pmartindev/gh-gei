require 'base64'
require 'json'
require 'time'
require 'socket'

project_path = Base64.decode64('__PROJECT_PATH_BASE64__')
project = Project.find_by_full_path(project_path)
abort 'The project was not found on the SSH GitLab installation.' unless project

warnings = []
jids = []
jobs = []
if project.respond_to?(:export_jobs)
  project.export_jobs.order(created_at: :desc).limit(10).each do |job|
    details = job.attributes.slice('id', 'jid', 'status', 'user_id', 'created_at', 'updated_at')
    jids << job.jid
    if job.respond_to?(:relation_exports)
      relations = job.relation_exports.order(id: :desc).limit(101).to_a
      warnings << "Export job #{job.id}: only the latest 100 relations were collected." if relations.length > 100
      details['relations'] = relations.first(100).map do |relation|
        jids << relation.jid
        relation.attributes.slice('relation', 'jid', 'status', 'export_error')
      end
    else
      warnings << "Export job #{job.id}: relation exports are unavailable on this GitLab version."
    end
    jobs << details
  end
  warnings << 'No retained export jobs were found. Older attempts may have expired.' if jobs.empty?
else
  warnings << 'Export job records are unavailable on this GitLab version; inspect the matching logs.'
end
jids = jids.compact.reject(&:empty?)

# Only current logs are scanned, with a bounded tail and a project/job allowlist.
paths = [
  '/var/log/gitlab/gitlab-rails/exporter.log',
  '/var/log/gitlab/sidekiq/current',
  '/var/log/gitlab/gitlab-rails/exceptions_json.log',
  '/var/log/gitlab/gitlab-rails/api_json.log'
]
fields = %w[time severity message project_id project_path meta.project jid class
            job_status retry_count correlation_id relation project_export_job_id
            export_error exception.class exception.message exception.backtrace
            error_class error_message error_backtrace]
logs = paths.map do |path|
  entry = { path: path, matches: [], tail_truncated: false, matches_truncated: false, invalid_lines: 0 }
  begin
    File.open(path, 'rb') do |file|
      size = file.stat.size
      offset = [size - 8 * 1024 * 1024, 0].max
      file.seek(offset)
      file.gets if offset > 0
      entry[:tail_truncated] = offset > 0
      # A snapshot avoids following a busy log forever.
      content = file.read([size - file.pos, 0].max)
      content.each_line do |line|
        begin
          row = JSON.parse(line)
        rescue JSON::ParserError
          entry[:invalid_lines] += 1
          next
        end
        next unless row.is_a?(Hash)
        next unless row['project_id'].to_s == project.id.to_s ||
          row['project_path'] == project.full_path || row['meta.project'] == project.full_path ||
          jids.include?(row['jid'])

        selected = row.slice(*fields).transform_values do |value|
          if value.is_a?(Array)
            value.first(20).map { |frame| frame.to_s[0, 256] }
          else
            value.to_s[0, 1024]
          end
        end
        entry[:matches] << selected
        if entry[:matches].length > 100
          entry[:matches].shift
          entry[:matches_truncated] = true
        end
      end
    end
  rescue Errno::ENOENT, Errno::EACCES => error
    entry[:error] = error.message
    warnings << "#{path}: #{error.message}"
  end
  warnings << "#{path}: only the last 8 MiB were scanned." if entry[:tail_truncated]
  warnings << "#{path}: only the last 100 matching entries were retained." if entry[:matches_truncated]
  warnings << "#{path}: #{entry[:invalid_lines]} non-JSON lines could not be correlated." if entry[:invalid_lines] > 0
  entry
end
warnings << 'No matching log entries were found. Check the worker node and rotated or centralized logs.' if logs.all? { |log| log[:matches].empty? }

puts JSON.generate({
  collected_at: Time.now.utc.iso8601,
  hostname: Socket.gethostname,
  gitlab_version: Gitlab::VERSION,
  project_id: project.id,
  project_path: project.full_path,
  scope: 'Latest 10 export jobs (all users); latest 100 relations per job; last 8 MiB and 100 matches per current log. Rotated logs are not included. Log strings/backtraces are abbreviated.',
  warnings: warnings,
  export_jobs: jobs,
  logs: logs
})
