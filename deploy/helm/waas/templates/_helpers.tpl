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
