# Database Persistent Storage Configuration

## Overview
The SQL Server database (`defaultconnection`) is deployed as a **StatefulSet** with persistent storage to ensure data survives pod restarts and recreations.

## What Changed
- **Before**: Deployment without persistent storage - data was lost on pod recreation
- **After**: StatefulSet with volumeClaimTemplates - data persists across pod lifecycles

## Files
- `statefulset.yaml` - StatefulSet configuration (replaces `deployment.yaml`)
- `pvc.yaml` - Standalone PVC example (optional, StatefulSet uses volumeClaimTemplates)

## Storage Configuration

### Storage Size
Default: **10Gi**

To change the storage size, edit `statefulset.yaml`:
```yaml
volumeClaimTemplates:
  - metadata:
      name: mssql-data
    spec:
      resources:
        requests:
          storage: 20Gi  # Change this value
```

### Storage Class
If your cluster has multiple storage classes, specify one:
```yaml
volumeClaimTemplates:
  - metadata:
      name: mssql-data
    spec:
      storageClassName: fast-ssd  # Add this line
      accessModes:
        - ReadWriteOnce
      resources:
        requests:
          storage: 10Gi
```

Common storage classes:
- `standard` - Default HDD storage
- `gp2` / `gp3` - AWS EBS volumes
- `premium-rwo` - GCP SSD persistent disks
- `managed-premium` - Azure Premium SSD

Check available storage classes:
```bash
kubectl get storageclass
```

## Data Persistence

### Where Data is Stored
SQL Server data is mounted at `/var/opt/mssql` inside the container, which includes:
- `/var/opt/mssql/data` - Database files (.mdf, .ldf)
- `/var/opt/mssql/log` - Transaction logs
- `/var/opt/mssql/secrets` - Certificates and keys

### Persistent Volume Claims
The StatefulSet automatically creates a PVC named `mssql-data-defaultconnection-0` in your namespace.

View the PVC:
```bash
kubectl get pvc -n ns-webhooks-env
```

View PVC details:
```bash
kubectl describe pvc mssql-data-defaultconnection-0 -n ns-webhooks-env
```

## Important Notes

### StatefulSet vs Deployment
StatefulSets are better for databases because:
- Stable pod identity (`defaultconnection-0`)
- Ordered, graceful deployment and scaling
- Stable persistent storage tied to pod identity
- Volumes persist even if StatefulSet is deleted (unless explicitly deleted)

### Data Lifecycle
- **Pod deletion**: Data persists, reattaches on pod recreation
- **StatefulSet deletion**: Data persists in PVC
- **PVC deletion**: Data is permanently lost (use with caution!)

### Scaling Considerations
This configuration is set to `replicas: 1` because SQL Server requires special configuration for high availability (Always On Availability Groups). For production:
- Consider Azure SQL Database or AWS RDS for managed HA
- Or implement SQL Server Always On with proper licensing
- Single replica is fine for dev/preview environments

## Preview Environment Deployments

When deploying preview environments, the database will now:
1. ✅ Retain data across pod restarts
2. ✅ Survive pod kills/crashes
3. ✅ Maintain data during rolling updates
4. ✅ Keep data if namespace is recreated (if PVC survives)

### Cleanup
To fully clean up a preview environment including data:
```bash
# Delete StatefulSet
kubectl delete statefulset defaultconnection -n ns-webhooks-env

# Delete PVC (this will delete the data!)
kubectl delete pvc mssql-data-defaultconnection-0 -n ns-webhooks-env
```

### Fresh Start
If you need to reset the database in a preview environment:
```bash
# Scale down to zero
kubectl scale statefulset defaultconnection --replicas=0 -n ns-webhooks-env

# Delete the PVC
kubectl delete pvc mssql-data-defaultconnection-0 -n ns-webhooks-env

# Scale back up (will create new PVC with fresh data)
kubectl scale statefulset defaultconnection --replicas=1 -n ns-webhooks-env
```

## Monitoring Storage

### Check disk usage
```bash
kubectl exec -it defaultconnection-0 -n ns-webhooks-env -- df -h /var/opt/mssql
```

### Check PVC size
```bash
kubectl get pvc mssql-data-defaultconnection-0 -n ns-webhooks-env -o jsonpath='{.spec.resources.requests.storage}'
```

## Troubleshooting

### Pod stuck in Pending
Check PVC status:
```bash
kubectl describe pvc mssql-data-defaultconnection-0 -n ns-webhooks-env
```

Common issues:
- No storage provisioner available
- Insufficient storage capacity
- Storage class not found

### Storage full
Increase PVC size (if storage class supports expansion):
```bash
kubectl patch pvc mssql-data-defaultconnection-0 -n ns-webhooks-env -p '{"spec":{"resources":{"requests":{"storage":"20Gi"}}}}'
```

### Migration from Deployment to StatefulSet
The StatefulSet uses a different naming pattern. If migrating from existing Deployment:
1. Backup your data
2. Delete old Deployment: `kubectl delete deployment defaultconnection -n ns-webhooks-env`
3. Apply new StatefulSet: `kubectl apply -k DefaultConnection/`
4. Restore data if needed
