# Storage Migrations Job Configuration

## Overview
The storage migrations job runs database migrations on each deployment. Due to Kubernetes Job immutability, special handling is required to allow Flux to reconcile when image tags change.

## The Problem
Kubernetes Jobs cannot be modified after creation. When Flux tries to update a Job with a new image tag, it fails with:
```
Job.batch "storagemigrations" is invalid: spec.template: Invalid value: ... field is immutable
```

## The Solution
This configuration uses **hash suffixes** to create unique job names on each deployment:

1. **Hash-based naming**: Job names get a unique suffix based on configMap content (e.g., `storagemigrations-5b8c9d7f`)
2. **Version tracking**: A separate configMap (`storagemigrations-version`) includes the image tag
3. **Automatic cleanup**: Completed jobs are deleted after 5 minutes via `ttlSecondsAfterFinished`

### How It Works
```yaml
# kustomization.yaml
generatorOptions:
  disableNameSuffixHash: false  # Enables hash suffixes

configMapGenerator:
- name: storagemigrations-version
  literals:
    - IMAGE=ghcr.io/helix26xyz/webhooks-dotnet-storagemigrations:main
```

When the image tag changes:
1. The `storagemigrations-version` configMap content changes
2. Kustomize generates a new hash suffix
3. A new Job is created with the new name
4. Old completed jobs are cleaned up automatically

## Job Configuration

### Auto-cleanup
```yaml
spec:
  ttlSecondsAfterFinished: 300  # Delete 5 minutes after completion
```

Adjust this value based on your needs:
- **300** (5 min) - Quick cleanup, minimal clutter
- **3600** (1 hour) - Keep for debugging
- **86400** (24 hours) - Audit/compliance requirements
- Remove field entirely to keep jobs indefinitely

### Retry Behavior
```yaml
spec:
  backoffLimit: 0  # Do not retry on failure
  template:
    spec:
      restartPolicy: OnFailure
```

- `backoffLimit: 0` - Job fails immediately if container exits with error
- `restartPolicy: OnFailure` - Container restarts only on failure, not on success
- Rationale: Migrations are idempotent, manual intervention needed for failures

### Flux Integration
```yaml
metadata:
  annotations:
    kustomize.toolkit.fluxcd.io/force: "enabled"
```

This annotation tells Flux to force-apply changes, ensuring proper reconciliation.

## Manual Operations

### View all migration jobs
```bash
kubectl get jobs -n ns-webhooks-env -l app=storagemigrations
```

### Check job status
```bash
kubectl get job storagemigrations-<hash> -n ns-webhooks-env
```

### View migration logs
```bash
# Get the latest job
LATEST_JOB=$(kubectl get jobs -n ns-webhooks-env -l app=storagemigrations --sort-by=.metadata.creationTimestamp -o jsonpath='{.items[-1].metadata.name}')

# View logs
kubectl logs job/$LATEST_JOB -n ns-webhooks-env
```

### Manual cleanup of old jobs
```bash
# Delete completed jobs older than 1 hour
kubectl delete jobs -n ns-webhooks-env -l app=storagemigrations --field-selector status.successful=1
```

### Force re-run migrations
To re-run migrations without changing code:
1. Update the image tag in `kustomization.yaml`:
   ```yaml
   configMapGenerator:
   - name: storagemigrations-version
     literals:
       - IMAGE=ghcr.io/helix26xyz/webhooks-dotnet-storagemigrations:main-$(date +%s)
   ```
2. Commit and push
3. Flux will create a new job

Or manually trigger:
```bash
kubectl create job --from=job/storagemigrations-<hash> storagemigrations-manual -n ns-webhooks-env
```

## Troubleshooting

### Job fails immediately
Check logs:
```bash
kubectl logs -l app=storagemigrations -n ns-webhooks-env --tail=50
```

Common issues:
- Database not ready (check StatefulSet status)
- Connection string incorrect (check secrets)
- Migration script syntax error

### Multiple jobs running
This is expected during deployments. Each job should:
- Complete successfully
- Get cleaned up after TTL expires
- Not interfere with other jobs (migrations are idempotent)

### Job stuck in pending
Check pod status:
```bash
kubectl get pods -l app=storagemigrations -n ns-webhooks-env
kubectl describe pod <pod-name> -n ns-webhooks-env
```

Common causes:
- Image pull errors
- Resource limits exceeded
- Node scheduling issues

### Flux reports "field is immutable"
This shouldn't happen with the hash suffix approach, but if it does:
1. Verify `generatorOptions.disableNameSuffixHash: false` in kustomization.yaml
2. Check that `storagemigrations-version` configMap exists
3. Manually delete the old job:
   ```bash
   kubectl delete job storagemigrations -n ns-webhooks-env
   ```
4. Flux will recreate it on next reconciliation

## Preview Environment Considerations

### Fast iterations
For preview environments with frequent deployments:
- Reduce TTL: `ttlSecondsAfterFinished: 60` (1 minute)
- Or disable: Remove the field to keep completed jobs for manual inspection

### Cost optimization
Each job creates a pod. To minimize resource usage:
- Set appropriate resource limits in job.yaml
- Use shorter TTL for faster cleanup
- Configure Flux to clean up old preview environments

### Database state
Remember: Migration jobs are idempotent but cumulative:
- Job reruns won't duplicate data
- Rolling back code doesn't roll back migrations
- Use database backups for true rollbacks

## Integration with CI/CD

### Updating image tags
When your CI/CD pipeline builds new images:

```yaml
# In kustomization.yaml
configMapGenerator:
- name: storagemigrations-version
  literals:
    - IMAGE=ghcr.io/helix26xyz/webhooks-dotnet-storagemigrations:${GIT_SHA}
```

This ensures each commit triggers a new migration job.

### Flux image automation
If using Flux image automation, add:
```yaml
# Image automation marker
# {"$imagepolicy": "flux-system:storagemigrations"}
- IMAGE=ghcr.io/helix26xyz/webhooks-dotnet-storagemigrations:main
```

Flux will automatically update the tag, triggering new jobs.

## Best Practices

1. **Monitor job history**: Set up alerts for failed migration jobs
2. **Test migrations**: Always test migration scripts in dev before production
3. **Keep migrations idempotent**: Jobs should be safe to run multiple times
4. **Version migrations**: Use timestamps or version numbers in SQL filenames
5. **Backup before migrations**: Especially in production environments
6. **Set resource limits**: Prevent runaway migrations from consuming cluster resources

## Alternative Approaches

If this hash-suffix approach doesn't work for your needs, alternatives include:

### 1. Init Container
Move migrations to an init container in the API service:
```yaml
initContainers:
- name: migrations
  image: ghcr.io/helix26xyz/webhooks-dotnet-storagemigrations:main
```
**Pros**: No separate job, automatic ordering  
**Cons**: API pod restart triggers migrations

### 2. Helm Hooks
Use Helm pre-upgrade/pre-install hooks:
```yaml
annotations:
  "helm.sh/hook": pre-upgrade,pre-install
```
**Pros**: Clear lifecycle integration  
**Cons**: Requires Helm (this project uses Kustomize)

### 3. Flux Post-Build Substitution
Use Flux to inject unique suffixes:
```yaml
postBuild:
  substitute:
    TIMESTAMP: "${TIMESTAMP}"
```
**Pros**: Centralized configuration  
**Cons**: Requires Flux-specific setup

The current hash-suffix approach is chosen for simplicity and compatibility with Kustomize-only workflows.
