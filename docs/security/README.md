# Security Documentation

This section documents the security posture, architecture, and controls for the DocumentManagement system.

## Quick Reference

- **[Security Baseline](SECURITY.md)** - Current security posture and known issues
- **[RBAC & Authorization](RBAC.md)** - Role-based access control model and permission matrix
- **[Risk Matrix](RISK_MATRIX.md)** - Known risks, likelihood, and impact
- **[Archive: Full Audit Reports](../archive/GENERATED_REPORTS/)** - Original detailed audit reports

## Current Status

| Aspect | Status | Details |
|--------|--------|---------|
| JWT Configuration | ⚠️ Hardened | HTTPS metadata validation enabled in Production; placeholder secrets must be rotated |
| Upload Protection | ✅ Hardened | PDF validation, MIME type checks, file signature validation implemented |
| Backup/Restore | ⚠️ Hardened | API restore default-disabled in Production; SQLite header validation added |
| Authorization | ⚠️ Manual | RBAC working but manually implemented; centralization recommended |
| Rate Limiting | ⚠️ Missing | Login endpoints need rate limiting; audit logging needed |
| Password Validation | ✅ Hardened | Old password verification required for self-service changes |
| Audit Trail | ✅ Implemented | User management and destructive actions logged |

## Pre-Pilot Security Gates (CRITICAL)

Before pilot deployment, complete ALL P0 gates:

- [ ] Generate unique production `Jwt__Secret` (do not use placeholders)
- [ ] Rotate all default seeded passwords (`admin123`, `manager123`, etc.)
- [ ] Disable all unused seeded accounts
- [ ] Enforce HTTPS through IIS/reverse proxy or formally document internal-LAN exception
- [ ] Confirm backup/restore procedures tested on isolated database copy
- [ ] Enable audit logging for all destructive operations

**Note**: Pilot is conditional on completing these gates. See [Deployment Checklist](../deployment/CHECKLIST.md).

## Security Philosophy

- Baseline security posture: controls in place, but designed for **pilot only**
- Not internet-safe without additional hardening
- Designed for trusted internal environments
- Pilot will serve as validation for broader hardening

## Key Hardening Areas

1. **JWT & Authentication**
   - HTTPS metadata validation enforced in Production
   - Login rate limiting recommended (not implemented)
   - Password change requires old password
   - Token version/security stamp not yet implemented

2. **Upload & File Handling**
   - PDF uploads validated by content type and file signature
   - PDF parsing sandboxing recommended (not implemented)
   - Temp storage cleanup policy recommended

3. **Backup & Restore**
   - API restore default-disabled in Production
   - SQLite header validation implemented
   - Offline restore recommended for production data

4. **Authorization & RBAC**
   - Role-based access control fully implemented
   - Document scoping by department and assignment
   - Manual implementation; centralized policies recommended

5. **Audit & Logging**
   - User management operations logged
   - Backup operations logged
   - Destructive document actions logged

## Next Steps

**For Pilot**:
- Complete P0 security gates (see above)
- Daily security log review
- Daily backup verification
- Immediate rollback capability

**For Broader Production**:
- Implement centralized authorization policies
- Add login rate limiting and account lockout
- Implement token revocation/security stamp
- Add PDF parsing sandboxing
- Add comprehensive monitoring and alerting
- Consider external secret management

---

**Last Updated**: 2026-05-11
**Status**: Baseline hardened for pilot; additional hardening required for production
