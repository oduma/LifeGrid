# Implementation Plan: Bootstrap LifeGrid Solution Scaffolding

**Status:** DONE — completed 2026-06-16  
**Requirements Source:** `docs/requirements/FUNCTIONAL_REQUIREMENTS.md`  
**Target:** .NET 10 / MAUI Android — Clean Architecture (Xivotec-style)

---

## Phase 0 — Pre-Flight Checks

**Goal:** Confirm the environment is ready before issuing any `dotnet` commands.

| Step | Command | Purpose |
|---|---|---|
| 0.1 | `dotnet --version` | Confirm .NET 10 SDK is active |
| 0.2 | `dotnet workload list` | Confirm `maui` and `android` workloads are installed |
| 0.3 | Check current directory is `c:\Code\LifeGrid` | Ensure all paths are relative to the repo root |

**Exit condition:** All checks green. No existing `.sln` or `src\` folder at the repo root (clean slate).

---

## Phase 1 — Solution & Directory Structure

**Goal:** Create the bare bones skeleton before any project files exist.

| Step | Command | Notes |
|---|---|---|
| 1.1 | `dotnet new sln -n LifeGrid` | Creates `LifeGrid.slnx` at the repo root — .NET 10 SDK defaults to the new `.slnx` XML format, not `.sln` |
| 1.2 | `mkdir src tests` | Creates the two top-level subdirectory containers |

---

## Phase 2 — Source Projects

Execute steps in order (Domain first, then dependents).

### Step 2.1 — LifeGrid.Domain
```
dotnet new classlib -n LifeGrid.Domain -o src/LifeGrid.Domain -f net10.0
```
- Delete the auto-generated `Class1.cs` placeholder.
- Create skeleton folders: `Entities/`, `ValueObjects/`, `Interfaces/`, `BoundedContexts/GoalManagement/`, `BoundedContexts/BehavioralEconomy/`, `BoundedContexts/InteractionLog/`.
- **No NuGet packages added.**

### Step 2.2 — LifeGrid.Application
```
dotnet new classlib -n LifeGrid.Application -o src/LifeGrid.Application -f net10.0
```
- Delete `Class1.cs`.
- Create skeleton folders: `Commands/`, `Queries/`, `Handlers/`, `Interfaces/`, `Common/`.
- Add P2P reference: `dotnet add src/LifeGrid.Application reference src/LifeGrid.Domain`
- Add NuGet: `dotnet add src/LifeGrid.Application package MediatR`

### Step 2.3 — LifeGrid.Infrastructure
```
dotnet new classlib -n LifeGrid.Infrastructure -o src/LifeGrid.Infrastructure -f net10.0
```
- Delete `Class1.cs`.
- Create skeleton folders: `Persistence/`, `Persistence/Configurations/`, `Persistence/Migrations/`, `AI/`, `Repositories/`.
- Add P2P reference: `dotnet add src/LifeGrid.Infrastructure reference src/LifeGrid.Application`
- Add NuGet packages:
  - `dotnet add src/LifeGrid.Infrastructure package Microsoft.EntityFrameworkCore.Sqlite`
  - `dotnet add src/LifeGrid.Infrastructure package Microsoft.EntityFrameworkCore.Design`
  - `dotnet add src/LifeGrid.Infrastructure package Microsoft.Extensions.AI`

### Step 2.4 — LifeGrid.Presentation
```
dotnet new maui -n LifeGrid.Presentation -o src/LifeGrid.Presentation
```
- Trim the `.csproj` target frameworks to `net10.0-android` only (remove `net10.0-ios`, `net10.0-maccatalyst`, `net10.0-windows` if present in the generated file).
- Add P2P reference: `dotnet add src/LifeGrid.Presentation reference src/LifeGrid.Application`
- Add NuGet packages:
  - `dotnet add src/LifeGrid.Presentation package CommunityToolkit.Mvvm`
  - `dotnet add src/LifeGrid.Presentation package SkiaSharp.Views.Maui.Controls`
- Create skeleton folders: `Pages/`, `ViewModels/`, `Controls/`, `Resources/Styles/`.

---

## Phase 3 — Test Projects

### Step 3.1 — LifeGrid.Domain.Tests
```
dotnet new xunit -n LifeGrid.Domain.Tests -o tests/LifeGrid.Domain.Tests -f net10.0
```
- Add P2P reference: `dotnet add tests/LifeGrid.Domain.Tests reference src/LifeGrid.Domain`
- Add NuGet packages:
  - `dotnet add tests/LifeGrid.Domain.Tests package FluentAssertions`
  - `dotnet add tests/LifeGrid.Domain.Tests package NSubstitute`

### Step 3.2 — LifeGrid.Application.Tests
```
dotnet new xunit -n LifeGrid.Application.Tests -o tests/LifeGrid.Application.Tests -f net10.0
```
- Add P2P references:
  - `dotnet add tests/LifeGrid.Application.Tests reference src/LifeGrid.Application`
  - `dotnet add tests/LifeGrid.Application.Tests reference src/LifeGrid.Domain`
- Add NuGet packages:
  - `dotnet add tests/LifeGrid.Application.Tests package FluentAssertions`
  - `dotnet add tests/LifeGrid.Application.Tests package NSubstitute`
  - `dotnet add tests/LifeGrid.Application.Tests package MediatR` (for handler testing)

### Step 3.3 — LifeGrid.Infrastructure.Tests
```
dotnet new xunit -n LifeGrid.Infrastructure.Tests -o tests/LifeGrid.Infrastructure.Tests -f net10.0
```
- Add P2P references:
  - `dotnet add tests/LifeGrid.Infrastructure.Tests reference src/LifeGrid.Infrastructure`
  - `dotnet add tests/LifeGrid.Infrastructure.Tests reference src/LifeGrid.Application`
- Add NuGet packages:
  - `dotnet add tests/LifeGrid.Infrastructure.Tests package FluentAssertions`
  - `dotnet add tests/LifeGrid.Infrastructure.Tests package NSubstitute`
  - `dotnet add tests/LifeGrid.Infrastructure.Tests package Microsoft.EntityFrameworkCore.InMemory` (for DbContext integration tests)

---

## Phase 4 — Register All Projects in the Solution

```
dotnet sln add src/LifeGrid.Domain/LifeGrid.Domain.csproj
dotnet sln add src/LifeGrid.Application/LifeGrid.Application.csproj
dotnet sln add src/LifeGrid.Infrastructure/LifeGrid.Infrastructure.csproj
dotnet sln add src/LifeGrid.Presentation/LifeGrid.Presentation.csproj
dotnet sln add tests/LifeGrid.Domain.Tests/LifeGrid.Domain.Tests.csproj
dotnet sln add tests/LifeGrid.Application.Tests/LifeGrid.Application.Tests.csproj
dotnet sln add tests/LifeGrid.Infrastructure.Tests/LifeGrid.Infrastructure.Tests.csproj
```

Verify: `dotnet sln list` must show all 7 entries.

---

## Phase 5 — Verification Build

```
dotnet build LifeGrid.slnx
```

**Acceptance criteria:**
- Exit code 0.
- Zero errors.
- No P2P reference warnings.

If errors are found, fix them before proceeding (common issues: target framework mismatch, MAUI workload NuGet restore failure on Android targets).

**Result:** Build succeeded. 0 Error(s). 0 Warning(s). Time elapsed 00:01:43.

---

## Phase 6 — Report Folder Tree

Run a directory tree listing and report the full output back to the user, confirming the physical layout matches the requirements exactly.

---

## Dependency Graph (summary)

```
LifeGrid.Domain
    └── (no deps)

LifeGrid.Application
    └── LifeGrid.Domain

LifeGrid.Infrastructure
    └── LifeGrid.Application
        └── LifeGrid.Domain

LifeGrid.Presentation
    └── LifeGrid.Application
        └── LifeGrid.Domain

LifeGrid.Domain.Tests
    └── LifeGrid.Domain

LifeGrid.Application.Tests
    └── LifeGrid.Application → LifeGrid.Domain

LifeGrid.Infrastructure.Tests
    └── LifeGrid.Infrastructure → LifeGrid.Application → LifeGrid.Domain
```

---

## Risk Notes

| Risk | Mitigation | Outcome |
|---|---|---|
| `dotnet new maui` generates multi-platform TFMs | Explicitly edit `.csproj` to retain only `net10.0-android` after creation | Resolved — iOS/macCatalyst/Windows TFMs stripped |
| `Microsoft.Extensions.AI` package name drift | Verify exact package ID at restore time; currently stable as `Microsoft.Extensions.AI` | Resolved — installed as `Microsoft.Extensions.AI 10.7.0` |
| EF Core Design package version misalign | Pin `Microsoft.EntityFrameworkCore.Design` to same version as `Microsoft.EntityFrameworkCore.Sqlite` | Resolved — both at 10.0.9 |
| `dotnet build` on Android target may require Java/Android SDK | Workload check in Phase 0 confirms Android is installed; JDK 17 must be on PATH | No issue encountered |
| `.NET 10 generates .slnx not .sln` | **New:** Use `LifeGrid.slnx` in all build, test, and sln commands going forward | Documented |

## Resolved Package Versions

| Package | Version |
|---|---|
| MediatR | 14.1.0 |
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.9 |
| Microsoft.EntityFrameworkCore.Design | 10.0.9 |
| Microsoft.EntityFrameworkCore.InMemory | 10.0.9 |
| Microsoft.Extensions.AI | 10.7.0 |
| CommunityToolkit.Mvvm | 8.4.2 |
| SkiaSharp.Views.Maui.Controls | 3.119.4 |
| FluentAssertions | 8.10.0 |
| NSubstitute | 5.3.0 |
