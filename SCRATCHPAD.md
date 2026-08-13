# Enable podman.socket because kind communicates over that
systemctl --user enable --now podman.socket

# Download the latest kind binary for Linux AMD64
curl -Lo ./kind https://kind.sigs.k8s.io/dl/latest/kind-linux-amd64

# Make the binary executable
chmod +x ./kind

# Move it to a directory in your PATH
sudo mv ./kind /usr/local/bin/kind

# -U (Universal: dauerhaft speichern) 
# -x (Export: an Programme wie 'kind' weitergeben)
set -Ux KIND_EXPERIMENTAL_PROVIDER podman

sudo mkdir -p /etc/systemd/system/user@.service.d
echo -e "[Service]\nDelegate=yes" | sudo tee /etc/systemd/system/user@.service.d/delegate.conf
sudo systemctl daemon-reload


# Create a new cluster named 'kind'
kind create cluster

# Verify the setup by listing the nodes
kubectl get nodes


podman build -t some-app:latest .

podman save localhost/some-app:latest -o some-app.tar

kind load image-archive some-app.tar


# Webspace Publish Workflow

- Receive request from client with updated webspace data (ViewModel)
- Read related DesiredState
- Validate updated webspace data
- Convert `password`s to `securePasswordToken`s
- Apply ViewModel to DesiredState
- Save updated DesiredState (with new version)
- Send webspace DesiredState to Backend (TechMW)
- Apply synchronous response data to DesiredState and force save (no new version)
- Return updated DesiredState as ViewModel to client
- Update Webshield DesiredState with updated mappings from updated webspace DesiredState (if necessary)
  - Wait for completion for all Webshield nodes to sent ACK notification asynchronously
- Update Product DNS with ManagedDomainBindings from updated webspace DesiredState synchronously (if necessary)
- Wait for final ACK notification from TechMW to complete webspace update
- Send final notification to client when all operations completed successfully