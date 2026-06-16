# LifeGrid - Technical Requirements & Best Practices (TECHNICAL_STANDARDS.md)

## 1. Architecture
- **Base Template:** Inspired by the Clean Architecture structure matching `XivotecGmbH/CleanArchitecture.Maui`.
- **Framework:** .NET 10.0 targeting cross-platform mobile runtimes.
- **Boundary Rules:** Core Domain layers must have zero dependencies on external frameworks or UI libraries.

## 2. Security & Privacy
- **Gemini API Key:** MUST be securely stored using `Microsoft.Maui.Storage.SecureStorage`. Prompt the user once at runtime if the key is missing; never hardcode or log credentials.
- **PII Hardening:** Highly sensitive user data (including behavioral logs, comment entries, and Hidden Vices survey inputs) must be restricted to local-only storage. Encrypt files or tables during persistence if processing limits allow.

## 3. Data Persistence & Schema Evolution
- **Zero Data Loss Policy:** All historical user data, habit configurations, and gamification logs MUST seamlessly survive application updates.
- **ORM Strategy:** Use Entity Framework Core (EF Core) with SQLite. To preserve **Strict DDD**, do not pollute the Core Domain with database-specific data annotations. All database mappings must be configured via the Fluent API within the Infrastructure layer.
- **Migrations:** Every relational schema change must be tracked via an explicit EF Core migration script generated on the host machine using:
  `dotnet ef migrations add <MigrationName> --project src/LifeGrid.Infrastructure`
- **Design-Time Tooling:** Implement `IDesignTimeDbContextFactory` safely inside `LifeGrid.Infrastructure` to support migration generation during development.
- **Startup Execution:** Application startup must safely invoke `context.Database.Migrate()` inside `MauiProgram.cs` before completing initialization.

## 4. Architectural Patterns
- **MVVM Architecture:** Standardize presentation logic using `CommunityToolkit.Mvvm`, leveraging source generators for `@ObservableProperty` and `[RelayCommand]`.
- **Async Everywhere:** All I/O operations, local database queries, and AI engine evaluation tasks must execute asynchronously using `await` or `Task.Run()` barriers to keep the UI running at a fluid 60fps.
- **The Result Pattern:** Avoid utilizing native exceptions to handle expected business logic validation errors (such as failed habit check-ins or boundary rule violations). Implement a strict, expressive `Result<T>` or `Result` pattern across the Application and Domain boundaries.

## 5. Authorized External Libraries
The project context is restricted to well-known, high-quality open-source packages. Do not introduce alternative framework bloat:
- **Database Core:** `Microsoft.EntityFrameworkCore.Sqlite` (and associated EF Core tooling).
- **AI Integration Client:** `Microsoft.Extensions.AI` — all text-based semantic calls to the Gemini engine must utilize `IChatClient.GetResponseAsync` or standard provider abstractions.
- **MVVM Tooling:** `CommunityToolkit.Mvvm`.
- **Core TDD Infrastructure:** `xUnit` (Testing Runner), `NSubstitute` (Isolated Mocking & Stubbing Engine), and `FluentAssertions` (Readable, clean assertion checking).

## 6. Android Integration Mechanics
- **Share-Sheet Registration:** The Android target platform must register cleanly with the native Share-Sheet API to capture system-wide incoming intents (specific rules defined inside `functional-requirements.md`).
- **Intent Processing:** Handle extracted incoming extras safely outside of synchronous UI lifecycle hooks. Utilize the non-generic framework casting style:
  `Intent?.GetParcelableExtra(key) as Android.Net.Uri`
- **Warning Suppression:** Safely isolate required API level deviations or platform-specific deprecations by wrapping target segments within `#pragma warning disable CA1422` and `#pragma warning restore CA1422` blocks.

## 7. Graphics Rendering & Authorized Dependencies
To ensure absolute architectural consistency, maximum performance across cross-platform mobile runtimes, and strict adherence to the KISS principle, the graphics infrastructure is explicitly defined:

- **Authorized Graphics Library:** SkiaSharp (`SkiaSharp.Views.Maui.Controls`) is the single, universally approved 2D graphics engine for LifeGrid. All dynamic UI visualizations—including neon custom gauges, radial trackers, complex progress shapes, and data-driven charts—must be rendered utilizing SkiaSharp canvases.
- **Native XAML Path Restriction:** Embedding heavy or complex layout paths directly into standard XAML files via native `<Path Data="..." >` tags is strictly prohibited. Native XAML shapes may only be used for simple, predictable structural elements (e.g., basic borders, rects, lines).
- **Zero Dependency Bloat:** Do not introduce alternative graphics engines, wrapper utilities, or additional layout canvas controls. The codebase must remain clean, predictable, and single-framework unified.

## 8. Low-Level Implementation Guardrails
- **C# Language Standards:** Leverage modern language features including Primary Constructors for dependency insertion, `record` configurations for raw DTO transfers, and Raw String Literals (`"""`) to wrap complex prompt engineering layouts.
- **UI Non-Blocking Law:** Never execute direct disk transactions or semantic AI queries on the primary thread. Offload long-running calculations to worker allocations via explicit `Task.Run` operations on the Application/Infrastructure perimeter.