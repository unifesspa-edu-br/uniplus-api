{{- define "verificador.name" -}}
{{ .Release.Name }}-verificador
{{- end -}}

{{- define "verificador.minioSecretName" -}}
{{ default (printf "%s-minio" (include "verificador.name" .)) .Values.minio.secretName }}
{{- end -}}

{{/* As NetworkPolicies do uniplus-infra selecionam o pod por este label. */}}
{{- define "verificador.selectorLabels" -}}
app.kubernetes.io/name: uniplus-verificador
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end -}}

{{- define "verificador.labels" -}}
{{ include "verificador.selectorLabels" . }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end -}}
