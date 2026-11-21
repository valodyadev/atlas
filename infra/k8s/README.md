# Kubernetes manifests

Kustomize, with one base and one overlay per environment. No Helm: templating
a chart is worth it when you publish software for *other people* to configure.
For a single team deploying its own services, Kustomize's patches keep the
manifests readable as plain YAML, and `kubectl kustomize overlays/prod` shows
exactly what will be applied.

```
base/                  shared shape of every environment
overlays/dev/          development: 1 replica, debug logging
overlays/prod/         production: HPA-ready, strict resources
```

Preview and apply:

```bash
kubectl kustomize infra/k8s/overlays/dev          # render, change nothing
kubectl apply -k infra/k8s/overlays/dev
```

## What is deliberately not here

* **Secrets.** Nothing in this directory contains a credential. `secretRef`
  points at a Secret created by your secret manager's operator (External
  Secrets, the AWS/Azure CSI driver, Vault Agent). See
  [`docs/security/secrets.md`](../../docs/security/secrets.md).
* **Image tags.** The overlays carry a placeholder; CI sets the digest at
  deploy time. Never `:latest` — a pod that restarts must come back as the
  same build.
* **Ingress/TLS.** Depends on your cluster's controller and certificate
  issuer; add it in the overlay.
