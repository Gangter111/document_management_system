# Operational Guide

**Target Audience**: System administrators, operations staff  
**Last Updated**: 2026-05-11

---

## Quick Reference

| Task | Command/Location | Frequency |
|------|---|---|
| Check API health | `http://localhost:5033/health` | Daily |
| View logs | `C:\QuanLyVanBan\Logs\` | Daily |
| Verify backup | Check `C:\QuanLyVanBan\Backups\` | Daily |
| Review audit log | System → Audit Logs in app | Daily |
| Restart service | `Restart-Service DocumentManagementApi` | As needed |
| Restore database | Run `.\tools\restore-sqlite.ps1` (staging) | Per incident |

---

## System Overview

The QuanLyVanBan document management system consists of:

1. **API Server** - Windows Service running ASP.NET Core
2. **Database** - SQLite (pilot) or SQL Server (production)
3. **File Storage** - Local attachment/document storage
4. **Client Applications** - WPF desktop client on user machines
5. **Backups** - Daily backup files stored separately

---

## Pre-Operations Checklist

Before system goes operational, record:

```
SERVER DETAILS
├─ Server name: ________________
├─ IP address: ________________
├─ API port: ________________
├─ API URL (internal): ________________
├─ Technical owner: ________________
└─ Support contact: ________________

INSTALLATION PATHS
├─ API folder: ________________
├─ Database folder: ________________
├─ Backup folder: ________________
├─ Logs folder: ________________
└─ Storage folder: ________________

DATABASE
├─ Provider (SQLite/SQL Server): ________________
├─ Database name: ________________
├─ Connection string: ________________
└─ Backup type: ________________

CREDENTIALS
├─ Admin account: ________________
├─ Service account: ________________
└─ Backup schedule: ________________
```

---

## Daily Operations

### Morning Check (Start of Shift)

1. **API Health**
   ```powershell
   Invoke-RestMethod http://localhost:5033/health
   ```
   - Expected result: HTTP 200 with "Healthy" status
   - If down: See [Troubleshooting](#troubleshooting)

2. **Windows Service Status**
   ```powershell
   Get-Service DocumentManagementApi | Select Status, StartType
   ```
   - Expected: Status = `Running`, StartType = `Automatic`
   - If stopped: `Start-Service DocumentManagementApi`

3. **Disk Space**
   ```powershell
   Get-Volume | Where-Object { $_.Drive -eq "C:" }
   ```
   - Expected: Free space > 20% of total
   - If low: Archive old backups or expand disk

4. **Latest Backup**
   ```powershell
   Get-Item C:\QuanLyVanBan\Backups\* | Sort-Object LastWriteTime -Descending | Select-Object -First 1
   ```
   - Expected: File exists and was created in last 24 hours
   - If missing: Check backup job or run manually

5. **API Log Review**
   ```powershell
   Get-Content -Tail 50 C:\QuanLyVanBan\Logs\*.log | Select-String -Pattern "ERROR|FATAL"
   ```
   - Check for unexpected errors
   - If errors: Review logs and contact technical owner

---

### During Operations (Hourly)

Monitor for user issues:

- [ ] Users can login
- [ ] Users can create/view documents
- [ ] No repeated timeout messages
- [ ] No security warnings in logs

---

### End of Shift Check (End of Day)

1. **Document Count** (sanity check)
   - Login to application
   - Check dashboard document count
   - Compare with yesterday (should not drop suddenly)

2. **Backup Verification** (already done in morning)
   - Confirm latest backup exists

3. **Disk Space** (already done in morning)
   - Confirm disk still healthy

4. **Shutdown Readiness**
   - If shutdown planned: Notify users in advance
   - Confirm all documents saved
   - Create final backup before shutdown

---

## Weekly Operations

### Monday: Deep Audit Review

```powershell
# Export audit logs (last 7 days)
# (Via admin UI: System → Audit Logs)
```

Checklist:
- [ ] No unauthorized access attempts
- [ ] All destructive operations logged (delete, archive, user changes)
- [ ] No locked/deactivated accounts trying to login repeatedly
- [ ] No unexpected admin operations

---

### Wednesday: Backup Restore Drill

**Staging/Non-Production Only**:

1. Take copy of latest production backup
2. Restore to a non-production database (see [Backup & Restore](#backup--restore))
3. Verify:
   - [ ] Database opens without corruption
   - [ ] All tables present
   - [ ] Sample documents readable
   - [ ] User accounts still valid
4. Document results
5. Delete test database

---

### Friday: Performance Check

1. **API Response Times**
   - Spot-check via application UI
   - Look for any slowness
   - Review logs for long-running queries

2. **Database Size**
   - SQLite: Check file size: `C:\QuanLyVanBan\Database\app.db`
   - SQL Server: Run size query
   - If growing rapidly: Investigate

3. **Log File Retention**
   - Check log folder size
   - Confirm old logs being rolled/archived
   - Delete files older than 90 days if manual cleanup needed

---

## Backup & Restore

### Automated Backup

The system runs automated backups (if scheduled):

**Verify Scheduled Job**:

```powershell
Get-ScheduledTask -TaskName "*DocumentManagement*" -TaskPath "\*"
```

Expected: Task exists and is enabled.

---

### Manual Backup (SQLite)

```powershell
# Copy database file to backup folder
Copy-Item `
  "C:\QuanLyVanBan\Database\app.db" `
  "C:\QuanLyVanBan\Backups\app.db.$(Get-Date -Format yyyyMMdd-HHmmss).bak"
```

---

### Manual Restore (SQLite - STAGING ONLY)

**DANGER**: Restore will overwrite current database. Do not use in production without maintenance window.

```powershell
# Stop service
Stop-Service DocumentManagementApi

# Restore from backup
Copy-Item `
  "C:\QuanLyVanBan\Backups\app.db.20260510-140000.bak" `
  "C:\QuanLyVanBan\Database\app.db" `
  -Force

# Start service
Start-Service DocumentManagementApi

# Verify
Invoke-RestMethod http://localhost:5033/health
```

---

### SQL Server Backup

(See `tools/backup-sqlserver.ps1` for details)

---

### SQL Server Restore

(See `tools/restore-sqlserver.ps1` for details, **staging only**)

---

## Service Management

### Start Service

```powershell
Start-Service DocumentManagementApi
```

### Stop Service

```powershell
Stop-Service DocumentManagementApi
```

### Restart Service (Clear Memory/Connections)

```powershell
Restart-Service DocumentManagementApi
```

### Check Service Status

```powershell
Get-Service DocumentManagementApi | Format-List
```

---

## Log Files

### Location

```
C:\QuanLyVanBan\Logs\
```

### Log Format

- **Filename**: `DocumentManagement-{date}.log`
- **Rolling**: 30 files kept (oldest deleted when new file created)
- **Entry format**: `[Timestamp] [Level] [Source] Message`

### Log Levels

- **ERROR**: Problem requiring attention
- **WARN**: Potential issue (rate-limited login, authorization denial)
- **INFO**: Normal operations
- **DEBUG**: Detailed diagnostics (dev only)

### Viewing Logs

```powershell
# Last 50 lines
Get-Content -Tail 50 C:\QuanLyVanBan\Logs\*.log

# Search for errors
Get-Content C:\QuanLyVanBan\Logs\*.log | Select-String "ERROR"

# Real-time (follow)
Get-Content -Tail 20 C:\QuanLyVanBan\Logs\*.log -Wait
```

---

## Troubleshooting

### API Down (Health Check Fails)

**Check 1**: Windows Service Running?

```powershell
Get-Service DocumentManagementApi
```

- If `Stopped`: `Start-Service DocumentManagementApi`
- Check logs immediately after:
  ```powershell
  Get-Content -Tail 100 C:\QuanLyVanBan\Logs\*.log
  ```

**Check 2**: Port Conflict?

```powershell
netstat -ano | findstr :5033
```

- If another process uses port 5033, stop it or change API port

**Check 3**: Database Issue?

```powershell
# Check if database file exists
Test-Path C:\QuanLyVanBan\Database\app.db
```

- If missing: Restore from backup
- If corrupted: See [Backup & Restore](#backup--restore)

**Check 4**: Disk Full?

```powershell
Get-Volume | Where-Object { $_.Drive -eq "C:" }
```

- If free space < 5%: Emergency cleanup
  - Archive old backups
  - Delete old log files
  - Expand disk

**Check 5**: Review Recent Logs

```powershell
Get-Content -Tail 200 C:\QuanLyVanBan\Logs\*.log | Select-String "FATAL|EXCEPTION"
```

---

### Users Cannot Login

**Check 1**: API Responding?

```powershell
Invoke-RestMethod http://SERVERIP:5033/health
```

- If not responding: Solve API down first

**Check 2**: User Account Active?

- Admin → System → Users
- Check if user is active (not deactivated)
- Check if user has appropriate role

**Check 3**: Wrong Password?

- User should reset password
- Admin can reset in System → Users

**Check 4**: Client Configuration?

- Check WPF client points to correct server
- `C:\QuanLyVanBan\Client\appsettings.json`
- Verify `ApiUrl` setting

---

### Slow Performance

**Check 1**: Large Document Load?

- Reduce page size in UI
- Check if many users accessing simultaneously

**Check 2**: Disk Bottleneck?

```powershell
Get-Volume | Select-Object Drive, SizeRemaining, @{Name="PercentFree"; Expression={[math]::Round(($_.SizeRemaining / $_.Size) * 100, 2)}}
```

- If < 20% free: Cleanup/expand

**Check 3**: Database Size?

```powershell
# SQLite
(Get-Item C:\QuanLyVanBan\Database\app.db).Length / 1MB

# SQL Server
# Run DBCC SQLPERF in SQL Studio
```

- SQLite should be < 500MB for pilot
- If larger: Archive old documents

---

### Backup Job Not Running

**Check 1**: Scheduled Task Exists?

```powershell
Get-ScheduledTask -TaskName "*DocumentManagement*"
```

**Check 2**: Enable If Disabled

```powershell
Enable-ScheduledTask -TaskName "DocumentManagement-Backup"
```

**Check 3**: Run Manually

```powershell
Start-ScheduledTask -TaskName "DocumentManagement-Backup"
```

**Check 4**: Review Job Output

```powershell
# Check job history
Get-ScheduledTaskInfo -TaskName "DocumentManagement-Backup"
```

---

## User Management (Admin Only)

### Create User

- Admin → System → Users → Create
- Assign role (Admin, Manager, Publisher, Staff)
- Set temporary password (user changes on first login)
- Assign department

### Change User Password

- Admin → System → Users → Select user → Reset Password
- Provides temporary password; user changes on first login

### Disable User

- Admin → System → Users → Select user → Deactivate
- User cannot login
- Existing JWTs remain valid until expiration (1 hour)

### Delete User

- User is soft-deleted (marked inactive)
- Cannot permanently delete (for audit trail)

---

## Security Responsibilities

### Daily
- [ ] Review API logs for errors/warnings
- [ ] Check health endpoint responds
- [ ] Verify backup exists and is recent

### Weekly
- [ ] Review audit logs (System → Audit Logs)
- [ ] Check for unusual access patterns
- [ ] Verify no disabled users are repeatedly trying to login

### Monthly
- [ ] Review user list; disable/remove inactive accounts
- [ ] Verify all users have appropriate roles
- [ ] Test backup/restore procedure

### Annually
- [ ] Security audit review
- [ ] Access policy review
- [ ] Disaster recovery drill

---

## Escalation

**Contact technical owner for**:
- API won't start after restart
- Database corruption suspected
- Unusual performance degradation
- Security incident (unauthorized access attempt)
- Restore needed (production environment)

---

**Archive**: [OPERATIONAL_READINESS.md](../archive/GENERATED_REPORTS/OPERATIONAL_READINESS.md)

**Related**: [Deployment Checklist](../deployment/CHECKLIST.md) | [Security Guidelines](../security/SECURITY.md)
