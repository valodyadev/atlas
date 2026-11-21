# Terraform

Placeholder. Deliberately empty, because the first resource you write depends
entirely on decisions this repository cannot make for you: cloud provider,
account and environment topology, networking, and how state is stored and
locked.

The Kustomize overlays in [`../k8s`](../k8s) assume a cluster already exists.
This is where that cluster, its database and its secret store would be defined.

## When you fill it in

```
terraform/
  modules/              reusable building blocks (network, cluster, database)
  environments/
    dev/                one root module per environment, one state file each
    prod/
```

A few things worth deciding before the first `terraform apply`:

- **Remote state with locking** from the very first commit (S3 + DynamoDB,
  GCS, or Terraform Cloud). Local state on a laptop ends with two engineers
  applying conflicting plans.
- **One state file per environment.** A single state file for dev and prod
  means a mistake in dev can destroy prod.
- **OIDC federation for CI**, not a long-lived cloud access key. See
  [`docs/security/secrets.md`](../../docs/security/secrets.md).
- **`terraform plan` on every pull request**, with the plan posted as a
  comment, and `apply` gated behind a GitHub Environment with required
  reviewers.
- **Pin the provider versions** and commit the lock file.
- Databases, buckets and anything else holding state get
  `prevent_destroy = true`.

## Alternatives

Pulumi, CDK and OpenTofu are all reasonable. Whichever you pick, record the
choice as an ADR — it is one of the hardest decisions in this repo to reverse.
