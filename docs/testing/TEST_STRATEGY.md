# Testing

**Status**: 44+ integration tests | **Coverage**: API controllers, security, backup/restore

---

## Test Suite

The system includes comprehensive integration tests covering:

### Controllers Under Test

| Controller | Coverage | Tests |
|------------|----------|-------|
| **AuthController** | Login, password change | 2 |
| **DocumentsController** | Create, read, list, search, update, archive, restore, delete, PDF extraction | 8 |
| **BackupController** | Backup health, restore validation, authorization | 3 |
| **CatalogController** | Read, write, authorization | 3 |
| **DashboardController** | Read, scoping | 2 |
| **ReportsController** | Access, scoping | 2 |
| **SystemController** | User management, authorization | 3 |
| **AuditLogsController** | Read, authorization | 2 |
| **LookupsController** | Lookups | 1 |

### Security Tests

- ✅ Authentication required (401 for unauthenticated)
- ✅ Authorization checks (403 for unauthorized roles)
- ✅ PDF upload rejects non-PDFs
- ✅ Backup restore validates file format
- ✅ RBAC scoping (non-admins see only own documents)

### Running Tests

```powershell
# All tests
dotnet test .\DocumentManagement.Tests\DocumentManagement.Tests.csproj

# Specific test class
dotnet test .\DocumentManagement.Tests\DocumentManagement.Tests.csproj `
  --filter "ClassName=ControllerSurfaceIntegrationTests"

# Specific test method
dotnet test .\DocumentManagement.Tests\DocumentManagement.Tests.csproj `
  --filter "Name~LoginFlow"

# Verbose output
dotnet test .\DocumentManagement.Tests\DocumentManagement.Tests.csproj `
  --verbosity detailed
```

---

## Test Coverage

| Area | Coverage | Status |
|------|----------|--------|
| **Happy path** | Core workflows | ✅ Full |
| **Authorization** | Role-based access | ✅ Full |
| **Error handling** | Invalid input, not found | ✅ Partial |
| **Edge cases** | Boundary conditions | ⚠️ Partial |
| **Performance** | Load testing | ❌ Not included |
| **Integration** | End-to-end flows | ✅ Full |

---

## Adding New Tests

When adding features or fixing bugs, add corresponding tests:

1. Create test in `DocumentManagement.Tests/Api/` or appropriate folder
2. Follow existing test naming: `{Class}{Method}{Scenario}`
3. Use `[Fact]` for specific tests or `[Theory]` with `[InlineData]` for parameterized tests
4. Test both happy path and error cases

---

## Test Strategy Going Forward

**Current**: Integration tests covering API surface  
**Future recommendations**:
- Unit tests for business logic
- Performance/load testing
- UI automation tests for WPF

---

**Archive**: [TEST_REPORT.md](../archive/GENERATED_REPORTS/TEST_REPORT.md)
