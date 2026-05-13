# AGENTS.md

# PROJECT OVERVIEW

This repository is a REAL enterprise Document Management System.

Tech stack:

* WPF
* .NET 8
* MVVM
* Clean Architecture

Projects:

* DocumentManagement.Wpf
* DocumentManagement.Api
* DocumentManagement.Application
* DocumentManagement.Domain
* DocumentManagement.Infrastructure
* DocumentManagement.Contracts

IMPORTANT:

This is NOT:

* a prototype
* a demo shell
* a startup dashboard
* a UI concept
* a mock workflow system

This IS:

* a production enterprise desktop application

# CURRENT STATUS

The application already includes:

## Runtime/lifecycle hardening

* ViewModel deactivation/disposal safety
* stale async completion protection
* cancellation ownership
* preview synchronization safety
* keyboard popup/IME routing safety
* export cancellation guards
* DataGrid virtualization preservation

## Enterprise Fluent shell foundation

* MainWindow Fluent shell
* shared Fluent ResourceDictionaries
* typography token system
* spacing token system
* color token system
* shared DataGrid styles
* shared badge/button/panel styles

## DocumentList modernization pass 1

* Fluent visual refinement
* enterprise command/filter layout
* preview panel modernization
* lightweight DataGrid modernization
* frozen cached brush optimization in HexToBrushConverter

# CORE ENGINEERING RULES

DO NOT:

* rewrite backend architecture
* rewrite MVVM structure unnecessarily
* duplicate Application-layer orchestration
* move business logic into WPF
* create fake APIs
* create placeholder workflows
* create fake analytics
* create mock modules
* introduce CommandManager invalidation
* introduce animation-heavy UI
* introduce deep visual trees
* introduce converter-heavy styling

KEEP:

* existing runtime safety
* existing cancellation behavior
* existing keyboard routing safety
* existing preview synchronization safety
* existing virtualization behavior

# UI PHILOSOPHY

Target visual direction:

* Fluent 2
* Windows 11
* Outlook
* Teams
* enterprise operations software

Prioritize:

* low eye fatigue
* operational clarity
* dense scanability
* keyboard-first workflow
* restrained hierarchy
* calm visual rhythm

Avoid:

* startup dashboard feel
* dribbble gradients
* oversized spacing
* card-heavy layouts
* heavy shadows
* excessive animations
* visual clutter

# PERFORMANCE RULES

Preserve:

* DataGrid virtualization
* recycling virtualization
* lightweight row rendering
* async safety
* cancellation safety
* keyboard routing safety

Avoid:

* nested DataTemplates
* per-row visual complexity
* dynamic brush allocation
* excessive converters
* heavy triggers
* visual tree explosions

# IMPLEMENTATION RULES

Prefer:

* ResourceDictionary
* shared Fluent tokens
* reusable lightweight styles
* static/frozen brushes
* flattened visual structures
* lightweight DataGrid cells

Do NOT:

* redesign workflow logic without explicit instruction
* redesign backend orchestration
* create hidden application layers inside WPF

# CURRENT PHASE

Current focus:

DocumentList enterprise refinement only.

Do NOT expand:

* Dashboard
* Reports
* Archive
* Categories
* System modules

until explicitly requested.

# REQUIRED WORKFLOW

Before changes:

1. inspect existing implementation
2. inspect related ViewModels/services
3. inspect shared Fluent resources
4. preserve runtime safety
5. preserve virtualization

After changes:

1. run build
2. run tests
3. verify virtualization remains enabled
4. verify no lifecycle regressions
5. verify keyboard routing protections still work
6. verify no allocation-heavy rendering paths were introduced

# REQUIRED VERIFICATION

Always run:

dotnet build .\DocumentManagement.Wpf\DocumentManagement.Wpf.csproj

and when relevant:

dotnet test .\DocumentManagement.Tests\DocumentManagement.Tests.csproj

Never claim success without verification.

# IMPORTANT ENGINEERING PRINCIPLE

The goal is:

enterprise operational software,
NOT UI experimentation.

Optimize for:

* long-session stability
* operator efficiency
* calm enterprise UX
* predictable runtime behavior
* maintainable architecture

# COMPLETED ENTERPRISE WORKFLOW STABILIZATION PHASE

The application has completed:

* runtime/lifecycle hardening
* stale async protection
* cancellation ownership hardening
* keyboard popup/IME safety
* preview synchronization safety
* export cancellation guards
* enterprise Fluent shell modernization
* DocumentList modernization
* DocumentForm modernization
* DocumentDetail modernization
* workflow consistency refinement
* keyboard workflow hardening
* operational honesty cleanup

The application now has:

* restrained Fluent enterprise UX
* keyboard-first workflow behavior
* truthful operational surfaces
* lightweight visual trees
* virtualization-safe rendering
* coherent interaction grammar
* operationally predictable workflows

IMPORTANT:

Do NOT:

* re-modernize the same screens
* re-polish visuals endlessly
* add speculative UI refinements
* add placeholder workflows
* add fake interactive controls
* redesign keyboard routing architecture
* introduce animation-heavy UI
* introduce visual-tree complexity
* create dashboard-style UI clutter

The current state is intentionally restrained.

# CURRENT ENGINEERING PRIORITY

Future work should prioritize:

* real operator workflow feedback
* human-driven QA findings
* operational friction remediation
* workflow trust
* predictable keyboard behavior
* runtime stability

NOT:

* speculative modernization
* visual experimentation
* architecture rewrites
* feature creep

# IMPORTANT ENGINEERING PRINCIPLE

The software should evolve through:

small evidence-driven operational improvements

NOT:

continuous speculative polishing.

