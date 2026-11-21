# API contracts

`v1.json` is **generated** from the API's endpoints and **committed**. Do not
edit it by hand; your change will be overwritten on the next export and the
CI check will fail.

```bash
make openapi      # regenerate from the code
```

## Why it is committed

The contract is the interface between the backend, which deploys hourly, and
the iOS app, which cannot be force-updated once it is on a phone. Committing
the generated document means a breaking change shows up as a reviewable diff
in a pull request, instead of as a decoding crash for users three versions
behind.

CI fails if the committed file differs from what the code produces, so an API
change and its contract update land in the same commit.

## Generating clients

```bash
# Swift (the planned path - see ADR 0008)
swift-openapi-generator generate contracts/openapi/v1.json --mode types --mode client

# Anything else
npx @openapitools/openapi-generator-cli generate \
  -i contracts/openapi/v1.json -g typescript-fetch -o ./generated
```

The Swift models in `AtlasFeatureUsers` are currently hand-written, with the
reasoning — and the known weakness — in
[ADR 0008](../../docs/adr/0008-api-contract-sharing.md).
