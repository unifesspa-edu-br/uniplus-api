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

{{/*
Referência de uma imagem: repositório, tag (vazia usa v<appVersion>) e, com digest
preenchido, @sha256. Puxada pelo digest, a imagem não muda se a tag for movida no
registro. Recebe (dict "image" <values da imagem> "chave" <caminho nos values> "ctx" $).
*/}}
{{- define "verificador.imagem" -}}
{{- $ref := printf "%s:%s" .image.repository (default (printf "v%s" .ctx.Chart.AppVersion) .image.tag) -}}
{{- $digest := .image.digest -}}
{{- if not (or (kindIs "invalid" $digest) (eq (toString $digest) "")) -}}
{{- if not (and (kindIs "string" $digest) (regexMatch "^sha256:[0-9a-f]{64}$" $digest)) -}}
{{- fail (printf "%s.digest=%q não é um digest sha256 completo" .chave (toString $digest)) -}}
{{- end -}}
{{- $ref = printf "%s@%s" $ref $digest -}}
{{- end -}}
{{- $ref -}}
{{- end -}}
