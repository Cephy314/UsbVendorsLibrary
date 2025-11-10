{{- range .Versions }}
## {{ if .Tag }}{{ .Tag.Name }}{{ else }}Unreleased{{ end }}{{ if and .Tag .Tag.Date }} — {{ datetime "2006-01-02" .Tag.Date }}{{ end }}

{{- $hasChanges := false }}

{{- $printedNew := false }}
{{- range .Commits }}
  {{- if and (eq .Type "feat") (not .Merge) (not .Revert) }}
    {{- if not $printedNew }}
### What's New
      {{- $printedNew = true }}
    {{- end }}
    {{- $hasChanges = true }}
- {{ if .Subject }}{{ .Subject | trim }}{{ else }}{{ .Header | trim }}{{ end }}
  {{- end }}
{{- end }}

{{- $printedFix := false }}
{{- range .Commits }}
  {{- if and (eq .Type "fix") (not .Merge) (not .Revert) }}
    {{- if not $printedFix }}
### Fixed
      {{- $printedFix = true }}
    {{- end }}
    {{- $hasChanges = true }}
- {{ if .Subject }}{{ .Subject | trim }}{{ else }}{{ .Header | trim }}{{ end }}
  {{- end }}
{{- end }}

{{- $printedPerf := false }}
{{- range .Commits }}
  {{- if and (eq .Type "perf") (not .Merge) (not .Revert) }}
    {{- if not $printedPerf }}
### Performance
      {{- $printedPerf = true }}
    {{- end }}
    {{- $hasChanges = true }}
- {{ if .Subject }}{{ .Subject | trim }}{{ else }}{{ .Header | trim }}{{ end }}
  {{- end }}
{{- end }}

{{- $printedOther := false }}
{{- range .Commits }}
  {{- if and (ne .Type "feat") (ne .Type "fix") (ne .Type "perf") (not .Merge) (not .Revert) }}
    {{- if not $printedOther }}
### Other Updates
      {{- $printedOther = true }}
    {{- end }}
    {{- $hasChanges = true }}
- {{ if .Subject }}{{ .Subject | trim }}{{ else }}{{ .Header | trim }}{{ end }}
  {{- end }}
{{- end }}

{{- $printedBreaking := false }}
{{- range .NoteGroups }}
  {{- range .Notes }}
    {{- if not $printedBreaking }}
### Breaking Changes
      {{- $printedBreaking = true }}
    {{- end }}
    {{- $hasChanges = true }}
- {{ .Body | trim }}
  {{- end }}
{{- end }}

{{- if not $hasChanges }}
- No notable changes.
{{- end }}

{{ end -}}
