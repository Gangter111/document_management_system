# Risk Matrix

Generated: 2026-05-11

| ID | Risk | Likelihood | Impact | Severity | Status | Owner |
| --- | --- | --- | --- | --- | --- | --- |
| R-001 | Checked-in JWT/admin/default credentials reused in pilot | Medium | Critical | Critical | Open | DevOps/Security |
| R-002 | Password change does not verify old password | Medium | High | High | Open | Backend |
| R-003 | Upload endpoints accept files based mostly on extension | Medium | High | High | Open | Backend/Security |
| R-004 | Backup restore endpoint can overwrite live DB with weak validation | Low | Critical | High | Open | Backend/Operations |
| R-005 | Destructive user/catalog/backup actions lack complete audit trail | Medium | High | High | Open | Backend |
| R-006 | API may run over plaintext HTTP in pilot | Medium | High | High | Open | DevOps |
| R-007 | Dashboard/reports load all documents into memory | High | Medium | High | Open | Backend |
| R-008 | No transaction around document mutation + history + audit | Medium | Medium | Medium | Open | Backend/Data |
| R-009 | No optimistic concurrency for document updates | Medium | Medium | Medium | Open | Backend/API |
| R-010 | SQL Server indexes lag SQLite indexes | Medium | Medium | Medium | Open | Data |
| R-011 | Duplicate source tree causes wrong-tree patch/deploy mistakes | Medium | Medium | Medium | Open | Release Manager |
| R-012 | Minimal monitoring and alerting | High | Medium | Medium | Open | DevOps |

