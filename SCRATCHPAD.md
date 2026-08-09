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