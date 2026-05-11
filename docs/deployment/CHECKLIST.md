# Deployment Checklist

**Target**: Pilot or Production deployment  
**Last Updated**: 2026-05-11

---

## Phase 1: Release Preparation

Complete these steps before beginning deployment:

- [ ] Confirm release version/tag in source control
- [ ] Confirm deployment target environment (pilot vs. production)
- [ ] Confirm database provider:
  - Pilot: SQLite acceptable for ≤50 users
  - Broader rollout: SQL Server required
- [ ] Confirm target server hostname/IP address
- [ ] Confirm API port (default: 5033 for dev, 5034 for staging)
- [ ] Confirm public/internal API URL that clients will connect to
- [ ] Confirm WPF client will be configured with correct API URL
- [ ] Confirm rollback artifact from previous release is available and tested
- [ ] Schedule deployment window (recommend off-hours for pilot)
- [ ] Notify stakeholders of deployment window

---

## Phase 2: Pre-Deployment Build Validation

Run from repository root to ensure clean builds:

```powershell
# Restore NuGet packages
dotnet restore .\DocumentManagement.sln

# Build solution
dotnet build .\DocumentManagement.sln --no-restore

# Run integration tests
dotnet test .\DocumentManagement.Tests\DocumentManagement.Tests.csproj --no-build
```

- [ ] **Restore passed** (all packages acquired)
- [ ] **Build passed** (0 errors)
- [ ] **Tests passed** (all 44+ tests passing)
- [ ] **No critical warnings** (warnings are acceptable if not blocking)
- [ ] **API starts locally** with development config:
  ```powershell
  dotnet run --project .\DocumentManagement.Api\DocumentManagement.Api.csproj
  # Verify http://localhost:5033 responds with /health endpoint
  ```

---

## Phase 3: Security Gates (CRITICAL - DO NOT SKIP)

**These gates are mandatory. Pilot deployment is blocked without completing them.**

### 3a. JWT Secret Management

- [ ] **Generate unique production `Jwt__Secret`** (minimum 32 characters)
  ```powershell
  # Example: Generate random secret
  -join((0..31) | ForEach-Object { [char](Get-Random -InputObject (33..126)) })
  ```
- [ ] **Do NOT use placeholder** `CHANGE_THIS_JWT_SECRET_IN_PRODUCTION`
- [ ] **Store secret securely**:
  - Option A: Environment variable `Jwt__Secret` at deployment time
  - Option B: Windows Service registry/config (preferred for service hosting)
  - Option C: IIS app settings (preferred for IIS hosting)
  - Option D: External secret store (recommended for multi-server)
- [ ] **Verify** production config does not contain secret in plain text
- [ ] **Rotate** if previously deployed with same secret

### 3b. Default Credential Rotation

- [ ] **All default-seeded accounts rotated** (change passwords):
  - [ ] `admin` (change from `admin123`)
  - [ ] `manager` (change from `manager123`)
  - [ ] `publisher` (change from `publisher123`)
  - [ ] `staff` (change from `staff123`)
- [ ] **Unused default accounts disabled**:
  - Navigate to System → Users
  - Deactivate seeded test accounts if not needed
- [ ] **First pilot admin account** has unique strong password
- [ ] **Password policy documented** for pilot users

### 3c. Transport Security

- [ ] **HTTPS enforced** or **documented exception**:
  - [ ] Kestrel HTTPS binding configured, OR
  - [ ] IIS/reverse proxy terminates TLS and forwards HTTP internally, OR
  - [ ] Formal exception signed: "API internal-LAN only until [DATE]"
- [ ] **Certificate valid** and properly installed (if using Kestrel HTTPS)
- [ ] **HSTS headers** configured (if applicable)
- [ ] **AllowedHosts** restricted to target hostnames

### 3d. Backup & Restore Policy

- [ ] **Confirm backup script** runs successfully on deployment server
- [ ] **Confirm restore tested** on isolated database copy (not production)
- [ ] **Decide restore API policy**:
  - [ ] Disabled in production (recommended)
  - [ ] Admin-only (requires authentication)
  - [ ] Operationally restricted (cannot be called from API)
- [ ] **Configure** `Backup:AllowApiRestore` setting

---

## Phase 4: Server Preparation

### 4a. Server Environment

- [ ] Server has **static IP** or **stable DNS** entry
- [ ] **Windows updates** applied; reboot if necessary
- [ ] **.NET Runtime** verified (check `dotnet --version`):
  - [ ] For self-contained deployment: not required
  - [ ] For framework-dependent: confirm runtime is installed
- [ ] **Antivirus** excludes deployment and database folders (performance)

### 4b. Deployment Folders

Create deployment structure:

```
C:\QuanLyVanBan\          # Root deployment folder
├── Api\                  # API binaries and config
├── Backups\              # Backup storage (not in Api folder)
├── Database\             # SQLite database (if using SQLite)
└── Logs\                 # Serilog rolling file logs
```

- [ ] **Create folders** with appropriate permissions
- [ ] **Set folder ACLs** (restrict to service account + admins):
  ```powershell
  # Restrict to service account
  icacls "C:\QuanLyVanBan\Api" /grant "NT Service\DocumentManagementApi:(OI)(CI)F"
  ```

### 4c. Service Account

- [ ] **Dedicated service account** created (e.g., `NT Service\DocumentManagementApi`)
- [ ] **Account has permissions**:
  - [ ] Read/write on API folder
  - [ ] Read/write on database folder
  - [ ] Read/write on backup folder
  - [ ] Read/write on log folder
  - [ ] Read on attachment storage folder
  - [ ] Database permissions (for SQL Server)
- [ ] **Account is NOT admin** (principle of least privilege)

---

## Phase 5: Database Preparation

### 5a. SQLite Deployment (Pilot Only)

- [ ] **Confirm pilot size** is appropriate (≤50 users)
- [ ] **Database path decided**: `C:\QuanLyVanBan\Database\app.db`
- [ ] **Database folder ACLs** set appropriately
- [ ] **Backup location** is separate: `C:\QuanLyVanBan\Backups\`
- [ ] **Backup script** will run daily (automated or manual)
- [ ] **Restore procedure** documented for emergency

### 5b. SQL Server Deployment (Broader Rollout)

- [ ] **SQL Server instance** confirmed accessible
- [ ] **Database name** decided (e.g., `DocumentManagementDb`)
- [ ] **Connection string** prepared:
  ```
  Server=DBSERVER\SQLEXPRESS;Database=DocumentManagementDb;Trusted_Connection=true;
  ```
- [ ] **Application login** created and permissions granted:
  - [ ] CREATE TABLE
  - [ ] ALTER TABLE
  - [ ] SELECT, INSERT, UPDATE, DELETE
  - [ ] EXECUTE (for stored procedures, if used)
- [ ] **Service account** has database login permissions
- [ ] **Database backup policy** established (separate from SQLite)

---

## Phase 6: Configuration & Deployment

### 6a. Generate Deployment Artifact

```powershell
# Publish API with Release configuration
dotnet publish .\DocumentManagement.Api\DocumentManagement.Api.csproj `
  -c Release `
  -o .\publish\Api `
  --self-contained
```

- [ ] **Publish completed** without errors
- [ ] **Publish output** contains:
  - [ ] `DocumentManagement.Api.exe` (or `.dll` for self-hosted)
  - [ ] `appsettings.json` (development defaults)
  - [ ] `appsettings.Production.template.json` (reference)
  - [ ] All dependency assemblies

### 6b. Prepare Production Configuration

Create `appsettings.Production.json` from template:

```bash
# Copy template
Copy-Item appsettings.Production.template.json appsettings.Production.json

# Edit with production values
notepad appsettings.Production.json
```

**Configuration Values to Set**:

- [ ] `Jwt:Secret` → unique value (from Phase 3a)
- [ ] `Jwt:Issuer` → your issuer (e.g., "documentmanagement-pilot")
- [ ] `Jwt:Audience` → your audience (e.g., "documentmanagement-client")
- [ ] `ConnectionStrings:DefaultConnection` → database connection
- [ ] `Database:Provider` → "SQLite" or "SqlServer"
- [ ] `Database:SqlitePath` → `./database/app.db` (if SQLite)
- [ ] `Logging:LogLevel` → "Information" (production)
- [ ] `AllowedHosts` → approved hostnames
- [ ] `Backup:AllowApiRestore` → false (production default)

- [ ] **Production config created** and reviewed
- [ ] **No secrets in ZIP/artifact** - inject at deployment time
- [ ] **Config file secured** (ACLs restrict to service account)

### 6c. Deploy to Server

Transfer published artifact to target server:

```powershell
# From deployment machine:
# Copy publish folder to deployment server
robocopy .\publish\Api \\TARGETSERVER\c$\QuanLyVanBan\Api /MIR /R:3

# Or use Azure Blob, S3, artifact repo, etc.
```

- [ ] **Files transferred** successfully
- [ ] **File permissions** set (ACLs for service account)
- [ ] **Configuration file** (`appsettings.Production.json`) placed in API root

---

## Phase 7: Windows Service Setup (If Using Service Hosting)

If deploying as Windows Service:

- [ ] **Service script** generated:
  ```powershell
  # From tools/ folder
  .\publish-api.ps1 -TargetPath "C:\QuanLyVanBan\Api" `
    -ServiceName "DocumentManagementApi" `
    -ServiceUser "NT Service\DocumentManagementApi"
  ```

- [ ] **Service installed**:
  ```powershell
  New-Service -Name DocumentManagementApi `
    -BinaryPathName "C:\QuanLyVanBan\Api\DocumentManagement.Api.exe" `
    -Credential (Get-Credential) `
    -DisplayName "QuanLyVanBan API Service" `
    -StartupType Automatic
  ```

- [ ] **Service started**:
  ```powershell
  Start-Service DocumentManagementApi
  ```

- [ ] **Service verified running**:
  ```powershell
  Get-Service DocumentManagementApi
  ```

---

## Phase 8: Post-Deployment Validation

### 8a. API Health Check

- [ ] **API responds** to `/health` endpoint:
  ```powershell
  Invoke-RestMethod http://APISERVER:5034/health
  ```

- [ ] **Health status** shows all components OK

### 8b. Database Migration Check

- [ ] **Database migration** ran successfully (check logs)
- [ ] **Tables created** in database
- [ ] **Default users** seeded (verify admin account exists)

### 8c. Authentication Test

- [ ] **Login test**:
  ```powershell
  $body = @{
    username = "admin"
    password = "newpassword"  # rotated password
  } | ConvertTo-Json
  
  Invoke-RestMethod http://APISERVER:5034/api/auth/login `
    -Method Post `
    -Body $body `
    -ContentType "application/json"
  ```

- [ ] **JWT token** received
- [ ] **Token contains** expected claims (role, department)

### 8d. Backup Validation

- [ ] **Backup script** executed successfully
- [ ] **Backup file** created in backup directory
- [ ] **Backup size** is reasonable (not empty or corrupted)

---

## Phase 9: Client Configuration

### 9a. WPF Client Configuration

Configure each client machine:

- [ ] **API endpoint** set to production URL
- [ ] **SSL validation** appropriate for environment
- [ ] **Connection timeout** suitable for network
- [ ] **Client tested** against production API

### 9b. Network Configuration

- [ ] **Firewall rules** allow clients to reach API port
- [ ] **DNS** resolves API hostname correctly
- [ ] **TLS certificate** trusted by client machines (if applicable)

---

## Phase 10: Operational Handoff

### 10a. Documentation

- [ ] **Deployment artifacts archived** (for rollback)
- [ ] **Configuration documented** (without secrets)
- [ ] **Backup procedures** documented
- [ ] **Restore procedures** documented and practiced
- [ ] **Monitoring/alerting** configured

### 10b. Runbooks

- [ ] **Daily checks** defined:
  - [ ] Backup completion
  - [ ] Service health
  - [ ] Log file review
  - [ ] Security event review

- [ ] **Incident procedures** defined:
  - [ ] Service restart procedure
  - [ ] Rollback procedure
  - [ ] Escalation contacts

### 10c. Monitoring & Alerts (For Production)

- [ ] **Health endpoint** monitored (alerting if down)
- [ ] **Logs monitored** for errors/exceptions
- [ ] **Database space** monitored
- [ ] **Backup** monitored for success/failure

---

## Phase 11: Rollback Plan

If deployment fails or issues are discovered:

- [ ] **Previous release** artifact is available
- [ ] **Database backup** from pre-deployment is available
- [ ] **Rollback procedure** documented:
  1. Stop current service
  2. Restore previous API binaries
  3. Restore previous configuration
  4. Restart service
  5. Validate via `/health` endpoint
  6. Notify stakeholders

- [ ] **Rollback tested** in staging (before production deployment)
- [ ] **Rollback owner** assigned
- [ ] **Rollback time estimate** documented

---

## Phase 12: Post-Deployment Monitoring (Pilot Phase)

For first 5 business days:

- [ ] **Daily health check**: API responding, database accessible
- [ ] **Daily backup verification**: Backup runs and completes
- [ ] **Daily log review**: No unexpected errors or security events
- [ ] **User feedback**: Pilot users report no critical issues
- [ ] **Performance baseline**: Response times acceptable
- [ ] **Security logs reviewed**: No unauthorized access attempts

---

## Sign-Off

After completing all phases:

- [ ] **Release Engineer**: Deployment checklist complete
- [ ] **Operations**: Monitoring established
- [ ] **Security**: Security gates verified
- [ ] **Project Lead**: Approval to go live

| Role | Name | Date | Signature |
|------|------|------|-----------|
| Release Engineer | __________ | ________ | __________ |
| Operations | __________ | ________ | __________ |
| Security | __________ | ________ | __________ |
| Project Lead | __________ | ________ | __________ |

---

**Archive References**:
- [GO_LIVE_CHECKLIST.md](../archive/GENERATED_REPORTS/GO_LIVE_CHECKLIST.md)
- [DEPLOYMENT_CHECKLIST.md](../archive/GENERATED_REPORTS/DEPLOYMENT_CHECKLIST.md)

**Related Documents**:
- [Readiness Assessment](READINESS.md)
- [Security Guidelines](../security/SECURITY.md)
- [Operational Guide](../operations/OPERATIONAL_GUIDE.md)
