# Quick Kubernetes Operations Guide

## Common Commands for Preview Environments

### Check Deployment Status
```bash
# Overall status
kubectl get all -n ns-webhooks-env

# Database (StatefulSet)
kubectl get statefulset defaultconnection -n ns-webhooks-env
kubectl get pvc -n ns-webhooks-env

# Migration jobs (latest 5)
kubectl get jobs -n ns-webhooks-env -l app=storagemigrations --sort-by=.metadata.creationTimestamp | tail -5

# API and Web services
kubectl get deployment apiservice webfrontend -n ns-webhooks-env
```

### View Logs
```bash
# Database logs
kubectl logs statefulset/defaultconnection -n ns-webhooks-env --tail=50

# Latest migration job
LATEST_JOB=$(kubectl get jobs -n ns-webhooks-env -l app=storagemigrations --sort-by=.metadata.creationTimestamp -o jsonpath='{.items[-1].metadata.name}')
kubectl logs job/$LATEST_JOB -n ns-webhooks-env

# API service logs
kubectl logs deployment/apiservice -n ns-webhooks-env --tail=50 -f

# Web frontend logs
kubectl logs deployment/webfrontend -n ns-webhooks-env --tail=50 -f
```

### Troubleshooting

#### Database Issues
```bash
# Check if database is ready
kubectl get statefulset defaultconnection -n ns-webhooks-env

# Check PVC status
kubectl describe pvc mssql-data-defaultconnection-0 -n ns-webhooks-env

# Restart database (will reattach to same PVC)
kubectl rollout restart statefulset/defaultconnection -n ns-webhooks-env

# Connect to database for manual inspection
kubectl exec -it defaultconnection-0 -n ns-webhooks-env -- /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P '<password>'
```

#### Migration Job Failures
```bash
# Check failed jobs
kubectl get jobs -n ns-webhooks-env -l app=storagemigrations --field-selector status.successful!=1

# View specific job details
kubectl describe job <job-name> -n ns-webhooks-env

# Check pod events
kubectl get events -n ns-webhooks-env --sort-by='.lastTimestamp' | grep storagemigrations

# Delete failed job to retry (Flux will recreate)
kubectl delete job <job-name> -n ns-webhooks-env
```

#### Flux Reconciliation
```bash
# Check Flux kustomization status
flux get kustomizations

# Force reconcile
flux reconcile kustomization <kustomization-name> --with-source

# Check for errors
flux logs --level=error
```

### Cleanup Operations

#### Reset Database (DESTRUCTIVE!)
```bash
# Scale down everything that uses the database
kubectl scale statefulset defaultconnection --replicas=0 -n ns-webhooks-env
kubectl scale deployment apiservice --replicas=0 -n ns-webhooks-env

# Delete PVC (THIS DELETES ALL DATA!)
kubectl delete pvc mssql-data-defaultconnection-0 -n ns-webhooks-env

# Scale back up (will create fresh database)
kubectl scale statefulset defaultconnection --replicas=1 -n ns-webhooks-env
kubectl scale deployment apiservice --replicas=1 -n ns-webhooks-env
```

#### Clean Up Old Jobs
```bash
# Manual cleanup (jobs with TTL clean themselves)
kubectl delete jobs -n ns-webhooks-env -l app=storagemigrations --field-selector status.successful=1

# Keep only last 3 jobs
kubectl get jobs -n ns-webhooks-env -l app=storagemigrations --sort-by=.metadata.creationTimestamp -o name | head -n -3 | xargs kubectl delete -n ns-webhooks-env
```

#### Full Environment Teardown
```bash
# Delete entire namespace (including PVCs!)
kubectl delete namespace ns-webhooks-env

# Or use Flux to remove
flux delete kustomization <kustomization-name>
```

### Performance Monitoring

#### Resource Usage
```bash
# Check resource usage
kubectl top pods -n ns-webhooks-env

# Check PVC disk usage
kubectl exec -it defaultconnection-0 -n ns-webhooks-env -- df -h /var/opt/mssql
```

#### Service Health
```bash
# Port forward to access services locally
kubectl port-forward -n ns-webhooks-env svc/apiservice 8080:8080
kubectl port-forward -n ns-webhooks-env svc/webfrontend 8081:8080

# Check endpoints
curl http://localhost:8080/health
curl http://localhost:8081/
```

### Development Workflows

#### Update Image Tags
When CI builds new images, update the image reference in:
- `storageMigrations/kustomization.yaml` - update IMAGE in `storagemigrations-version` configMap
- Other services - Flux image automation handles this automatically

#### Test Configuration Changes
```bash
# Build kustomization locally (from repo root)
kubectl kustomize webhooks.AppHost/aspirate-output/

# Apply to test namespace
kubectl apply -k webhooks.AppHost/aspirate-output/ --dry-run=client -o yaml

# Or use kustomize build
kustomize build webhooks.AppHost/aspirate-output/ > preview.yaml
```

#### Preview Branch Deployments
Each branch can have its own namespace:
```bash
# Create namespace
kubectl create namespace ns-webhooks-feature-xyz

# Deploy (adjust kustomization namespace in base)
kubectl apply -k webhooks.AppHost/aspirate-output/ -n ns-webhooks-feature-xyz
```

## Flux GitOps Patterns

### Kustomization Structure
```
webhooks.AppHost/aspirate-output/
├── kustomization.yaml          # Root - references all components
├── namespace.yaml              # Namespace definition
├── DefaultConnection/          # Database StatefulSet + PVC
│   ├── kustomization.yaml
│   ├── statefulset.yaml
│   └── service.yaml
├── storageMigrations/          # Migration Jobs (hash-based naming)
│   ├── kustomization.yaml
│   ├── job.yaml
│   └── service.yaml
├── apiservice/                 # API Deployment
├── webfrontend/                # Web Deployment
└── cache/                      # Redis
```

### Flux Reconciliation Order
Flux applies resources in this order:
1. Namespace
2. ConfigMaps and Secrets
3. StatefulSets and PVCs (database)
4. Jobs (migrations)
5. Deployments (API, Web)
6. Services and Ingress

Dependencies are handled by:
- Health checks on readiness probes
- Job dependencies in application code
- Flux kustomization dependencies (if configured)

### Image Update Automation
```yaml
# In Flux ImagePolicy
apiVersion: image.toolkit.fluxcd.io/v1beta1
kind: ImagePolicy
metadata:
  name: storagemigrations
spec:
  imageRepositoryRef:
    name: storagemigrations
  policy:
    semver:
      range: '>=1.0.0'
```

This automatically updates image tags in kustomization files.

## Security Notes

### Secrets Management
Secrets are referenced but not included in the repo:
```bash
# Create secrets manually or via Flux + SOPS
kubectl create secret generic defaultconnection-secrets \
  --from-literal=MSSQL_SA_PASSWORD='<password>' \
  -n ns-webhooks-env

kubectl create secret generic storagemigrations-secrets \
  --from-literal=ConnectionStrings__DefaultConnection='<connection-string>' \
  -n ns-webhooks-env
```

### RBAC
Ensure Flux has appropriate permissions:
```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: flux-reconciler
  namespace: ns-webhooks-env
subjects:
- kind: ServiceAccount
  name: flux
  namespace: flux-system
roleRef:
  kind: ClusterRole
  name: cluster-admin
  apiGroup: rbac.authorization.k8s.io
```

## Monitoring and Alerts

### Recommended Alerts
- StatefulSet not ready for > 5 minutes
- Migration job failed
- PVC storage > 80% full
- Multiple pods in CrashLoopBackOff

### Metrics to Track
- Migration job execution time
- Database storage growth rate
- API request latency
- Number of completed jobs (for cleanup monitoring)

## Common Issues

### "Field is immutable" errors
Fixed by hash-based job naming. If you still see this:
1. Check `generatorOptions.disableNameSuffixHash: false` 
2. Verify `storagemigrations-version` configMap exists
3. Delete the old job manually if needed

### Jobs not cleaning up
Check `ttlSecondsAfterFinished` setting. May need TTL controller enabled in cluster:
```bash
kubectl get deployment -n kube-system | grep ttl
```

### Database data lost
Ensure StatefulSet is used, not Deployment. Check PVC exists and is bound:
```bash
kubectl get pvc -n ns-webhooks-env
kubectl describe pvc mssql-data-defaultconnection-0 -n ns-webhooks-env
```

### Migrations run multiple times
This is OK - migrations are idempotent. Check `__MigrationHistory` table to see what ran:
```sql
SELECT * FROM __MigrationHistory ORDER BY AppliedAt DESC;
```
