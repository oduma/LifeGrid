# LifeGrid Constitution: Architecture & Domain-Driven Design (DDD)

## 1. Architectural Foundation
LifeGrid is built on a strict implementation of **Clean Architecture**.
* **Template Reference:** The architecture is modeled after the [Xivotec CleanArchitecture.Maui](https://github.com/XivotecGmbH/CleanArchitecture.Maui) template.
* **Modernization:** The stack is strictly upgraded and adapted for **.NET 10** and **C# 14**.
* **Layering Rules (The Dependency Rule):** Dependencies must point INWARD only.
    * `Presentation (MAUI UI)` -> `Application (Use Cases/MediatR)` -> `Domain (Core Logic)`
    * `Infrastructure (SQLite/External APIs)` -> `Application`

## 2. Strict Domain-Driven Design (DDD) Guardrails
The gamification engine is deterministic and mathematically rigid. The Domain layer is sacred.

* **Bounded Contexts:** Maintain strict separation between systems.
    * `GoalManagement`: Handles overarching objectives, structural calendar timelines, and WeekItem generation.
    * `BehavioralEconomy`: Handles all deterministic gamification math, scoring (XP/SP/GP), and the Procrastination Escalator penalty states.
    * `InteractionLog`: Handles raw data entry, enforcing absolute timestamping and mutually exclusive Proof of Completion paths.
* **The Zero-Dependency Rule:** The `Domain` project must contain pure C# only. It is strictly forbidden to add MAUI, SQLite, EF Core, or any infrastructure-specific NuGet packages to the Domain.
* **Entities vs. Value Objects:**
    * **Entities:** Use for objects with distinct identity and continuity (e.g., `Goal`, `User_Profile`).
    * **Value Objects:** Use for descriptive, point-in-time measurements. They **must be immutable** (e.g., a `CompletedValueSet` containing value, unit, and timestamp).
* **Aggregates & Roots:** * Ensure data consistency is maintained exclusively through Aggregate Roots. 
    * For example, the `WeekItem` is the temporal root aggregate; penalty states (Level 1 Warnings) live strictly on the `WeekItem` and infect underlying habits.