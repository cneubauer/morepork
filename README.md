# WaaS Manager — Helm & Infrastructure Setup

WaaS (**Webhosting-as-a-Service**) Manager provides the core API service and Temporal background workers for managing webhosting infrastructure. 

> **Note**: A dedicated database system component will be integrated and implemented in a future iteration.

This repository contains the application code and deployment configurations in [`deploy/helm/waas`](deploy/helm/waas).

---

## 1. Host Prerequisites & Management Tools

Install `helm` and `kubectl` locally on your host machine. These tools are used to manage all deployments — from local Incus testing sandboxes to production clusters:

```bash
# openSUSE Tumbleweed:
sudo zypper install helm kubernetes-client

# Verify installation
helm version
kubectl version --client
```

---

## 2. Local Development & Testing Setup (Incus Sandbox)

For local development and testing, Kubernetes runs inside an isolated **Incus** system container. This keeps your host OS clean, avoids permission issues, and isolates the test cluster while allowing `helm` and `kubectl` on your host to manage it directly.

### Step 2.1: Install & Initialize Incus (openSUSE Tumbleweed)

```bash
# 1. Install Incus package
sudo zypper install incus incus-tools

# 2. Enable and start Incus daemon
sudo systemctl enable --now incus.socket incus.service

# 3. Add your user to the incus-admin group
sudo usermod -aG incus-admin $USER
newgrp incus-admin

# 4. Initialize Incus (press Enter to accept defaults)
incus admin init
```

### Step 2.2: Launch Isolated K8s Cluster inside Incus

```bash
# 1. Launch container with nesting enabled for inner container/K3s runtimes
incus launch images:ubuntu/24.04 k8s-sandbox -c security.nesting=true

# 2. Install K3s inside the Incus container
incus exec k8s-sandbox -- sh -c "curl -sfL https://get.k3s.io | sh -"

# 3. Wait for K3s to generate the cluster configuration file
incus exec k8s-sandbox -- sh -c "until [ -f /etc/rancher/k3s/k3s.yaml ]; do sleep 2; done"

# 4. Export KUBECONFIG to your host machine using incus file pull
mkdir -p ~/.kube
incus file pull k8s-sandbox/etc/rancher/k3s/k3s.yaml ~/.kube/config-incus

# 5. Update IP address in kubeconfig to point to the Incus container IP
INCUS_IP=$(incus list k8s-sandbox -c 4 --format csv | awk '{print $1}')
sed -i "s/127.0.0.1/$INCUS_IP/g" ~/.kube/config-incus
export KUBECONFIG=~/.kube/config-incus

# 6. Verify host connection to Incus Kubernetes cluster
kubectl get nodes
```

### Step 2.3: Lint & Deploy to Local Development Cluster

```bash
# 1. Lint chart syntax on host
helm lint deploy/helm/waas

# 2. Dry-run template rendering
helm template waas-test deploy/helm/waas --debug

# 3. Install dev release (with local mock secret creation enabled)
helm install waas-dev deploy/helm/waas \
  --set secret.create=true \
  --set secret.data.ConnectionStrings__WaaS="Server=localhost;Database=WaaS;"

# 4. Inspect deployed pods and services
kubectl get all -l app.kubernetes.io/instance=waas-dev

# 5. Run Helm test hook
helm test waas-dev
```

### Step 2.4: Clean Teardown of Local Sandbox

```bash
# Delete Incus container and all cluster state
incus delete --force k8s-sandbox
rm -f ~/.kube/config-incus
```

---

## 3. Production & Staging Deployment Setup

For staging and production environments, Kubernetes is hosted on dedicated cloud or bare-metal infrastructure (EKS, GKE, AKS, OpenShift, or RKE2). `helm` and `kubectl` on your host or CI/CD runner connect directly to the target cluster.

### Step 3.1: Configure Target Cluster Context

```bash
# Point KUBECONFIG to target environment credentials
export KUBECONFIG=/path/to/production-kubeconfig.yaml

# Or configure via cloud provider CLI:
# aws eks update-kubeconfig --name waas-prod-cluster --region us-east-1
```

### Step 3.2: Production Secrets Setup

In production, sensitive connection strings are managed out-of-band using **ExternalSecrets**, **SealedSecrets**, or HashiCorp Vault (`secret.create: false`).

Ensure the production secret is present in the target namespace before deploying:
```bash
kubectl create namespace waas-prod --dry-run=client -o yaml | kubectl apply -f -

kubectl create secret generic waas-secrets \
  --from-literal=ConnectionStrings__WaaS="Server=prod-db.example.com;Database=WaaS;" \
  --namespace waas-prod
```

### Step 3.3: Deploy or Upgrade Release in Production

Use `helm upgrade --install` with environment-specific overrides:

```bash
helm upgrade --install waas-prod deploy/helm/waas \
  --namespace waas-prod \
  --set image.registry="registry.example.com/waas" \
  --set image.tag="1.4.0" \
  --set api.replicas=3 \
  --set api.ingress.enabled=true \
  --set api.ingress.host="waas.example.com"
```

### Step 3.4: Verify Production Rollout & Rollback Strategy

```bash
# Check deployment rollout status
kubectl rollout status deployment/waas-prod-api -n waas-prod

# Check release revision history
helm history waas-prod -n waas-prod

# Perform instant rollback if an issue is detected
helm rollback waas-prod <revision-number> -n waas-prod
```

---

## Helm Chart Reference (`deploy/helm/waas`)

The chart structure:
* `Chart.yaml` — Chart metadata and appVersion.
* `values.yaml` — Default configuration values for API, Temporal worker pool, ingress, and secrets.
* `templates/api.yaml` — Deployment, Service, and Ingress for WaaS API.
* `templates/workers.yaml` — Deployment template looping over configured Temporal workers (`spaceClassic`, `webshield`).
* `templates/_helpers.tpl` — Standard labels, security context, and initContainer helpers.
* `templates/serviceaccount.yaml` — Dedicated ServiceAccount resource.
* `templates/secret.yaml` — Optional developer secret template (enabled via `secret.create: true`).
* `templates/tests/test-connection.yaml` — Connection test hook.
