# LifeGrid - Phase 4 Core Infrastructure & Domain Baseline Requirements
## Production Schema & Domain Aggregate Initializer

This document defines the strict architectural and structural specifications for Phase 4. The objective is to build out the core Domain Entities and map them directly to the official SQLite persistence layer using Entity Framework Core, establishing a solid, test-verified structural foundation.

---

## 1. External Project Reference Matrix
Claude Code must parse property names, data type distributions, and relational constraints directly from the repository’s master definitions. Do not duplicate or redefine field configurations within code comments or logic:
* Entity Layouts, Tables & Properties: `docs\specs\data-structure.json` (and matching structural sections in `Refining LifeGrid Data Structure`)
* Architecture Isolation & Core Code Invariants: `docs\specs\TECHNICAL_STANDARDS.md`

---

## 2. Domain Layer Architecture (Pure DDD & TDD)

### 2.1 Domain Entity Realization
* Create the pure C# entity models within the `LifeGrid.Domain` project workspace:
  * `UserProfile` Aggregate Root
  * `Goal` Aggregate Root
* **Strict DDD Isolation Constraints:** * These classes must remain entirely decoupled from and ignorant of Entity Framework Core, SQLite, attributes, or any persistence framework mechanics.
  * Internal states must be properly encapsulated; use domain methods or constructor patterns to enforce initialization rules.

---

## 3. Infrastructure & Persistence Layer (SQLite / EF Core)

### 3.1 Entity Framework Core DbContext Expansion
* Integrate the new domain aggregates into the main database context infrastructure as mapped data collection sets (`DbSet<UserProfile>` and `DbSet<Goal>`).

### 3.2 Fluent API Mapping Configurations
* To maintain clean architectural boundaries, all table names, primary keys, relationships, index placements, and column parameter limits must be defined explicitly using Fluent API configurations inside the `LifeGrid.Infrastructure` layer. 
* **UserProfile Schema Binding:** Map fields and tracker profiles matching the data matrix in `data-structure.json`.
* **Goal Schema Binding:** Map identification structures, descriptions, and temporal configurations following `data-structure.json`.

### 3.3 Schema Evolution & Migration Engineering
* Generate the sequential EF Core migration scripts required to introduce the production `UserProfile` and `Goal` structures into the database file.
* **Staging Data Preservation:** The execution of these migrations must safely execute without dropping, altering, or losing any records contained within the temporary onboarding table (`OnboardingProgressCache`) built in Phase 2.

---

## 4. Test-Driven Development (TDD) Invariants

### 4.1 Domain Unit Testing Suite
* Implement rigorous unit tests using `xUnit` to verify that `UserProfile` and `Goal` classes initialize correctly in code with expected default states as mandated by the business logic.

### 4.2 Infrastructure Integration Testing Suite
* Build schema validation integration tests utilizing an isolated, in-memory SQLite database connection.
* **Strict Verification Asserts:**
  * Assert that the DbContext builds the database schema completely without error, validating that column dimensions, nullable constraints, and primary/foreign keys precisely mirror the rules in `docs\specs\data-structure.json`.
  * Assert that records for both `UserProfile` and `Goal` can be successfully written to and safely read back from the SQLite schema without truncation or type mismatch exceptions.