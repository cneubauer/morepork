{{/*
Expand the name of the chart.
*/}}
{{- define "waas.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{/*
Create a default fully qualified app name.
*/}}
{{- define "waas.fullname" -}}
{{- if .Values.fullnameOverride -}}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- $name := default .Chart.Name .Values.nameOverride -}}
{{- if contains $name .Release.Name -}}
{{- .Release.Name | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" -}}
{{- end -}}
{{- end -}}
{{- end -}}

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "waas.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{/*
Common labels
*/}}
{{- define "waas.labels" -}}
helm.sh/chart: {{ include "waas.chart" . }}
{{ include "waas.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end -}}

{{/*
Selector labels
*/}}
{{- define "waas.selectorLabels" -}}
app.kubernetes.io/name: {{ include "waas.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end -}}

{{/*
Service account name
*/}}
{{- define "waas.serviceAccountName" -}}
{{- if .Values.serviceAccount.create -}}
    {{- default (include "waas.fullname" .) .Values.serviceAccount.name -}}
{{- else -}}
    {{- default "default" .Values.serviceAccount.name -}}
{{- end -}}
{{- end -}}

{{- define "waas.temporalAddress" -}}
{{- if .Values.temporal.enabled -}}
{{- printf "%s-temporal-frontend:7233" .Release.Name -}}
{{- else -}}
{{- .Values.temporal.address -}}
{{- end -}}
{{- end -}}

{{- define "waas.imageTag" -}}
{{- .Values.image.tag | default .Chart.AppVersion -}}
{{- end -}}

{{- define "waas.securityContext" -}}
runAsNonRoot: true
runAsUser: 64198
allowPrivilegeEscalation: false
readOnlyRootFilesystem: true
capabilities:
  drop: ["ALL"]
seccompProfile:
  type: RuntimeDefault
{{- end -}}

{{/*
Blocks until the Temporal frontend answers. Both compose and K8s need this:
temporal's schema setup must finish before dependents connect, and K8s has no
equivalent of compose's depends_on/service_healthy.
*/}}
{{- define "waas.waitForTemporal" -}}
- name: wait-for-temporal
  image: temporalio/admin-tools:1.25.2
  command:
    - sh
    - -c
    - |
      until temporal operator cluster health --address "$TEMPORAL_ADDRESS"; do
        echo "waiting for temporal frontend at $TEMPORAL_ADDRESS"
        sleep 3
      done
  env:
    - name: TEMPORAL_ADDRESS
      value: {{ include "waas.temporalAddress" . }}
{{- end -}}

