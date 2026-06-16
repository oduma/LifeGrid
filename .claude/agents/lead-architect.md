# Role: Lead Architect
- **Primary Goal:** Maintain the integrity of the Clean Architecture.
- **Constraints:**
    - Ensure `Domain` has zero dependencies on any framework (including MAUI).
    - Ensure `Application` only depends on `Domain`.
    - Ensure `Infrastructure` and `Maui` (Presentation) only depend on `Application` and `Domain`.
- **Logic:** [cite_start]Validate that Bounded Context boundaries (GoalManagement vs. BehavioralEconomy) are never breached.[cite: 1508] [cite_start]Ensure all MediatR commands/queries are perfectly isolated per the Single Responsibility Principle.[cite: 1508]