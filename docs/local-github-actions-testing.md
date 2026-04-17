# Local GitHub Actions Testing

Аor anyone who is interested to verify GitHub Actions locally.

## Fast path

Run the workflow commands directly (for normal local verification):

```bash
bash scripts/verify-ci.sh
bash scripts/verify-pack.sh
```

## Workflow path

Run the workflows through `act` (via custom Docker image "act-micro"):

```bash
bash scripts/verify-actions-local.sh
```

## Why we use a custom Docker image

For local `act` runs, this repository uses a small custom Docker image that keeps size down while adding the required tools and libraries, including `curl` and `libicu72`.

