# LifeGrid - Phase 10 Vertical Slice Requirements
## Temporal Alignment & Week Synchronization Rules

This document specifies the core domain rules and structural updates required for Phase 10. The objective is to enforce strict temporal alignment across the application, ensuring all goals synchronize to a unified, Monday-anchored global calendar without duplicating timeline entities.

---

## 1. External Reference Mapping
Claude Code must parse existing schemas and apply modifications directly to the established structural foundations:
* Target Entities: `Goal`, `Week`, and `WeekGoal_Item` (`docs\specs\data-structure.json`)
* Architecture Standards: `docs\specs\TECHNICAL_STANDARDS.md`

---

## 2. Domain Layer Architecture (Strict DDD Enforcement)

### 2.1 Goal Temporal Anchor (Rule 1)
* Modify the `Goal` aggregate root creation logic.
* **Invariant:** When a user finalizes a new Goal, the system must independently calculate and set the Goal's starting date to exactly the **first Monday following the current creation date** at 00:00:00 local time.

### 2.2 WeekGoal_Item Sequence Tracking (Rule 3)
* Expand the `WeekGoal_Item` domain entity.
* **New Property:** Add an integer property named `WeekGoalNumber`.
* **Behavior:** This property represents the relative week sequence for a specific goal (e.g., `1` for the first week of the goal, `2` for the second). It is strictly relative to the `GoalID` and operates independently of the global calendar dates.

---

## 3. Application & Infrastructure Layer (Synchronization)

### 3.1 Global Week Deduplication (Rule 2)
* Update the database commit transaction handler responsible for generating weeks and habits (the AI pipeline finalization from Phase 6).
* **Upsert Logic:** Before creating a new `Week` entity for a generated `WeekGoal_Item`, the handler must query the SQLite database for an existing `Week` that shares the exact same Monday starting date.
  * **If Not Found:** Create a new `Week` entity, persist it, and link the new `WeekGoal_Item` to its ID.
  * **If Found:** Do *not* create a new `Week`. Extract the `WeekID` of the existing record and bind the new `WeekGoal_Item` to it.

---

## 4. Test-Driven Development (TDD) Invariants

* **Temporal Math Verification:** Write a domain unit test explicitly verifying that if a Goal is created on a Wednesday, the `StartDate` mathematically evaluates to the immediately following Monday. Add an edge-case test asserting that if a goal is created *on* a Monday, it defers to the *next* Monday to ensure the user gets a full setup window.
* **Deduplication Integration Test:** Write an infrastructure integration test where two distinct `Goal` creation commands are executed on the same date. 
  * Assert that the SQLite context contains exactly *one* `Week` entity for that starting date.
  * Assert that both resulting `WeekGoal_Item` records possess the same Foreign Key linking to that single `WeekID`.
* **Sequence Verification:** Assert that the generated `WeekGoal_Item` objects for a new goal are properly stamped with sequential `WeekGoalNumber` values starting perfectly at `1`.