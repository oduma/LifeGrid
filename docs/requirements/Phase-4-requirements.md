# LifeGrid - Phase 4 Infrastructure Requirements
## Production Schema Mappings (UserProfile & Goal Baseline)

This document defines the strict technological and structural specifications for Phase 4. The objective is to provision the database schema definitions for the core operational entities using Entity Framework Core and SQLite, establishing the official production storage layer.

---

## 1. External Project References
Claude Code must parse structural matrices, datatypes, fields, and constraints directly from the existing repository configuration tracking files. Do not rewrite or invent custom entity models:
* Schema Mappings & Entity Properties: `data-structure.json` (and tracking definitions in `Refining LifeGrid Data Structure`)
* Data Migration Invariants & Persistence Standards: `TECHNICAL_STANDARDS.md`

---

## 2. Infrastructure Layer Architecture (Strict DDD Isolation)

### 2.1 Entity Framework Core DbContext Expansion
* Integrate the official production aggregate roots into the main application database context library:
  * `UserProfile` Collection Set
  * `Goal` Collection Set

### 2.2 Fluent API Configuration Bindings
* To preserve clean Domain-Driven Design boundaries, all database definitions, keys, and explicit column behaviors must be declared via Fluent API mapping configurations inside the `LifeGrid.Infrastructure` layer. Do not dirty Domain entities with database annotations.
* **UserProfile Schema Mapping:** Map configurations strictly matching the constraints defined in `data-structure.json` (including core tracker segments for levels, progression XP, and baseline Shield Points).
* **Goal Schema Mapping:** Map configurations according to the specifications in `data-structure.json` (including unique tracking identifiers, description allocations, and absolute date parameters).

### 2.3 Schema Evolution & Migration Engineering
* Generate the sequential EF Core migration scripts required to build out the `UserProfile` and `Goal` tables on top of the existing schema baseline.
* **Data Mutation Safeguard:** The application's database initialization logic must execute migrations seamlessly without disrupting, dropping, or corrupting the temporary storage tables provisioned during Phase 2 (`OnboardingProgressCache`).

---

## 3. Test-Driven Development (TDD) Invariants

* **Infrastructure Context Testing:** Implement schema validation integration tests utilizing an isolated, in-memory SQLite database instance.
* **Strict Verification Asserts:**
  * Assert that the context successfully builds the schema model, proving that all foreign keys, constraints, and index relationships are mathematically sound.
  * Assert that column nullability, primitive field size restrictions, and unique constraints precisely match the parameters outlined in `data-structure.json`.