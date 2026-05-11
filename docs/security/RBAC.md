# Role-Based Access Control (RBAC)

**Status**: ✅ Implemented | **Design**: Role-based with document scoping

## Role Model

Four application roles are defined and seeded:

| Role | Purpose | Tier |
|------|---------|------|
| **Admin** | System administration, user/role management, backup operations | Administrative |
| **Manager** | Department/team leadership, document approval, reporting | Leadership |
| **Publisher** | Document publication and metadata management | Content |
| **Staff** | Document creation and viewing | User |

---

## Permission Matrix

The table below shows what each role can do:

| Function | Admin | Manager | Publisher | Staff |
|----------|-------|---------|-----------|-------|
| **Authentication** |
| Login | ✅ | ✅ | ✅ | ✅ |
| Change own password | ✅* | ✅* | ✅* | ✅* |
| Reset another user's password | ✅ | ❌ | ❌ | ❌ |
| **Documents** |
| View all active documents | ✅ | Dept/Assigned† | Dept/Assigned† | Dept/Assigned† |
| Create document | ✅ | ✅ | ✅ | ✅ |
| Update/modify document | ✅ | Own dept only | Own dept only | ❌ |
| Archive document | ✅ | Own dept only | Own dept only | ❌ |
| Restore from archive | ✅ | Own dept only | Own dept only | ❌ |
| Delete document | ✅ | ❌ | ❌ | ❌ |
| Move to another department | ✅ | ❌ | ❌ | ❌ |
| Extract PDF text | ✅ | ✅ | ✅ | ✅ |
| **Dashboard & Reporting** |
| View dashboard | ✅ All | Dept/Assigned† | Dept/Assigned† | Dept/Assigned† |
| Generate reports | ✅ | ✅ | ✅ | ❌ |
| **Catalog Management** |
| Read categories/statuses | ✅ | ✅ | ✅ | ✅ |
| Create/modify categories | ✅ | ✅ | ❌ | ❌ |
| Create/modify statuses | ✅ | ✅ | ❌ | ❌ |
| **System Administration** |
| User management (create/edit/delete) | ✅ | ❌ | ❌ | ❌ |
| View audit logs | ✅ | ❌ | ❌ | ❌ |
| Backup download | ✅ | ❌ | ❌ | ❌ |
| Backup restore | ✅** | ❌ | ❌ | ❌ |
| Health check | ✅ | ❌ | ❌ | ❌ |

**Legend**:
- ✅ = Allowed
- ❌ = Denied
- *️⃣ = Old password verification required
- †️ = Scoped by `ProcessingDepartment` claim or document assignment
- ** = Default-disabled in Production; operationally controlled

---

## Document Scoping Rules

The system enforces scoped document access based on user role and claims:

### Admin Users
- Can view **all active documents**
- Can view **archived documents** (soft-deleted)
- Full CRUD operations on all documents

### Non-Admin Users (Manager, Publisher, Staff)
- Can view documents where:
  - `ProcessingDepartment` matches their `Department` claim, **OR**
  - Document is explicitly **assigned to their username**, **OR**
  - Document's `VisibleTo` list includes their username (if implemented)
- Cannot see documents outside their scope
- Cannot see archived documents unless explicitly assigned

### Department-Based Scoping
- User claims include a `Department` claim (e.g., "IT", "HR")
- Document `ProcessingDepartment` is matched against user `Department` claim
- Case-sensitive comparison

---

## Authorization Checks

### How Authorization Works

1. **Route Handlers**: All API routes except `/api/auth/login` require `[Authorize]` attribute
2. **Role Checks**: Controller actions perform role-specific checks:
   ```csharp
   if (User.IsAdmin())  // Checks ClaimTypes.Role
   if (User.IsManager())
   if (User.IsPublisher())
   ```
3. **Document Scope Checks**: Document endpoints apply read/write scoping:
   ```csharp
   if (!CanViewDocument(user, document))
       return Forbidden();
   ```
4. **Active User Middleware**: On each request, verifies user is still active in database

### JWT Claims

Each JWT token includes:
- **Standard Claims**: `sub` (user ID), `iss` (issuer), `aud` (audience), `exp` (expiration)
- **Role Claims**: Both `http://schemas.microsoft.com/ws/2008/06/identity/claims/role` and `role`
- **Custom Claims**: `Department` (for document scoping)

---

## Known Issues & Recommendations

### 1. Manual Authorization Logic (Medium Priority)

**Current State**: Authorization checks are manually coded in each controller

**Risk**: Future endpoints may drift or omit checks

**Recommendation**: Before broader production rollout, refactor to centralized policies:
```csharp
// Recommended pattern for future
[Authorize(Policy = "AdminOnly")]
[Authorize(Policy = "DocumentScope")]
```

### 2. PermissionService Diverges from API Behavior (Low Priority)

**Current State**: 
- `PermissionService.cs` defines permission codes
- API controllers enforce different hard-coded rules
- Example: Permission suggests Staff can edit; controller denies it

**Recommendation**: 
- Treat API controller checks as authoritative for pilot
- Align or remove stale permission definitions in future cleanup

### 3. Authorization Scope Consistency (Low Priority)

**Current State**: 
- Document list/search apply scoping
- Dashboard applies scoping
- Reports apply scoping
- Audit logs do not (Admin-only)

**Recommendation**: 
- Maintain consistency across all endpoints
- Ensure new endpoints follow established patterns

---

## Testing RBAC

Integration tests cover:
- ✅ Login flow (anonymous access)
- ✅ Authenticated request with valid JWT
- ✅ Document list scoping by department
- ✅ Document view scoping by assignment
- ✅ Dashboard scoping to readable documents
- ✅ Report access by role
- ✅ Backup access (Admin-only)
- ✅ Audit log access (Admin-only)
- ✅ User management (Admin-only)

**Test File**: [DocumentManagement.Tests/Api/ControllerSurfaceIntegrationTests.cs](../../DocumentManagement.Tests/Api/ControllerSurfaceIntegrationTests.cs)

---

## Operational Guidelines

### For System Administrators

1. **User Creation**
   - Assign appropriate role (Admin, Manager, Publisher, Staff)
   - Assign department claim (e.g., "IT", "HR")
   - Enforce strong password on first login
   - Document purpose and ownership of each user

2. **Audit Monitoring**
   - Daily review of audit logs for unusual activity
   - Alert on repeated failed login attempts
   - Alert on password resets or role changes

3. **Backup & Recovery**
   - Only Admin can download/restore backups
   - Backup restore is disabled by default in Production
   - Keep offline backup verification script

### For Department Managers

1. **Document Assignment**
   - Assign documents to staff as needed
   - Review document status and approvals
   - Ensure documents are properly archived when lifecycle ends

2. **Visibility Boundaries**
   - Managers can only see their own department documents
   - Cross-department document sharing must be explicit assignment

### For End Users

1. **Password Management**
   - Change password regularly
   - Use strong passwords (recommend 12+ characters)
   - Old password required to change own password

2. **Document Privacy**
   - Respect department boundaries
   - Do not screenshot/export sensitive data
   - Report suspicious access patterns

---

## Migration / Role Changes

To change a user's role:

1. Admin navigates to System → Users
2. Select user and change role
3. Change takes effect on next login (existing JWT will expire in 60 minutes)
4. Audit log records role change

---

## Future Enhancements

Recommended for production:

- [ ] Dynamic role creation (beyond seeded roles)
- [ ] Fine-grained permission matrix (beyond role-based)
- [ ] Attribute-based access control (ABAC)
- [ ] Centralized policy engine
- [ ] Audit event tracking for all permission checks
- [ ] Role analytics dashboard

---

**Last Updated**: 2026-05-11  
**Archive**: [RBAC_REVIEW.md](../archive/GENERATED_REPORTS/RBAC_REVIEW.md)
