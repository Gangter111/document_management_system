# DocumentManagement - Documentation Index

Welcome to the DocumentManagement system documentation. This is a .NET Core-based internal document management system with a Windows desktop client and centralized API server.

## 📋 Quick Navigation

### For New Team Members
- **[Getting Started](../README.md)** - Start here for project overview and setup
- **[Architecture Overview](architecture/ARCHITECTURE.md)** - System design and layers
- **[Deployment Guide](deployment/CHECKLIST.md)** - How to deploy the system

### For Operations & Support
- **[Operational Guide](operations/OPERATIONAL_GUIDE.md)** - Running and maintaining the system
- **[Operational Guide (Vietnamese)](operations/OPERATIONAL_GUIDE_VI.md)** - Hướng dẫn vận hành (Tiếng Việt)
- **[Stabilization Policy](operations/STABILIZATION_POLICY.md)** - Permitted work during stabilization phase

### For Security & Compliance
- **[Security Documentation](security/README.md)** - Security posture, threats, and hardening
- **[RBAC & Authorization](security/RBAC.md)** - Role-based access control model
- **[Risk Matrix](security/RISK_MATRIX.md)** - Known risks and tracking

### For Deployment & Readiness
- **[Deployment Checklist](deployment/CHECKLIST.md)** - Pre-deployment validation
- **[Readiness Assessment](deployment/READINESS.md)** - Production & operational readiness
- **[Staging Deployment](deployment/STAGING.md)** - Staging environment setup

### For Testing & Quality
- **[Test Strategy](testing/TEST_STRATEGY.md)** - Testing approach and coverage
- **[Integration Tests](testing/INTEGRATION_TESTS.md)** - Available test suites

### For Pilots & Trials
- **[5-User Pilot Plan](pilot/PLAN.md)** - Initial pilot deployment strategy
- **[Pre-Pilot Security Plan](pilot/SECURITY_PLAN.md)** - Security gates and requirements
- **[Pilot Evaluation](pilot/EVALUATION.md)** - Results and decisions from pilot trial

## 📚 Document Organization

```
docs/
├── README.md                    # This file
├── architecture/
│   └── ARCHITECTURE.md          # System design and architecture
├── security/
│   ├── README.md                # Security overview
│   ├── SECURITY.md              # Baseline security posture
│   ├── RBAC.md                  # Role-based access control
│   └── RISK_MATRIX.md           # Risk tracking
├── deployment/
│   ├── CHECKLIST.md             # Deployment validation steps
│   ├── READINESS.md             # Production/operational readiness
│   └── STAGING.md               # Staging deployment report
├── testing/
│   ├── TEST_STRATEGY.md         # Testing approach
│   └── INTEGRATION_TESTS.md     # Integration test documentation
├── operations/
│   ├── OPERATIONAL_GUIDE.md     # English operations guide
│   ├── OPERATIONAL_GUIDE_VI.md  # Vietnamese operations guide
│   └── STABILIZATION_POLICY.md  # Stabilization work policy
├── pilot/
│   ├── PLAN.md                  # 5-user pilot plan
│   ├── SECURITY_PLAN.md         # Pre-pilot security requirements
│   └── EVALUATION.md            # Pilot evaluation results
└── archive/                     # Historical reports and artifacts
    ├── README.md                # Archive explanation
    ├── GENERATED_REPORTS/       # Generated audit reports
    └── PILOT_REPORTS/           # Historical pilot reports
```

## 🔑 Key Information

### System Components
- **DocumentManagement.Api** - ASP.NET Core Web API (server)
- **DocumentManagement.Wpf** - Windows desktop client
- **DocumentManagement.Application** - Business logic layer
- **DocumentManagement.Infrastructure** - Data and persistence
- **DocumentManagement.Domain** - Domain models and entities
- **DocumentManagement.Contracts** - API contracts (DTOs)
- **DocumentManagement.Tests** - Integration test suite

### Current Readiness
- **Pilot Ready**: Yes (with mandatory gates completed)
- **Production Ready**: No (additional hardening required)
- **Test Coverage**: Comprehensive integration tests
- **Security Posture**: Baseline (see [Security Documentation](security/README.md))

## 🚀 Getting Started

1. Read the [main README](../README.md) for non-technical overview
2. Review [Architecture Overview](architecture/ARCHITECTURE.md) for system design
3. Check [Deployment Checklist](deployment/CHECKLIST.md) before deploying
4. Follow [Operational Guide](operations/OPERATIONAL_GUIDE.md) for day-to-day operations

## 📖 Archive

Historical audit reports, generated assessments, and previous pilot evaluations are preserved in [archive/](archive/README.md) for reference and compliance.

---

**Last Updated**: 2026-05-11
**Status**: Production maintenance phase
