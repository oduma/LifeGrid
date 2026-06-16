# LifeGrid - Master AI Agent Directives (claude.md)

## 1. Primary Directive
You are working on LifeGrid, a gamified, hardcore habit-enforcement application built with .NET 10, MAUI, and SQLite. Before executing any code generation or refactoring, you must read and strictly adhere to the files listed in this document. Do not hallucinate external patterns or frameworks outside of these bounds.

## 2. The Developer Constitution
These files dictate the immutable laws of the codebase. Read these before making any architectural decisions:
* `docs\constitution\architecture-and-ddd.md`: Defines the Clean Architecture template, Bounded Contexts (GoalManagement, BehavioralEconomy, InteractionLog), and pure Domain-Driven Design rules.
* `docs\constitution\coding-standards-and-principles.md`: Enforces SOLID, KISS, DRY, and explicit dependency injection.
* `docs\constitution\testing-and-tdd-mandate.md`: Mandates Test-Driven Development with 100% branch coverage.

## 3. Functional & UI Requirements
These files contain the deterministic gamification math and the aesthetic guidelines:
* `docs\specs\functional-requirements.md`: The complete functional spec outlining the Procrastination Escalator, XP/SP economy, and core loops. (Note: Follow any relative paths to assets or mockups referenced within this document).
* `docs\specs\style-guide.md`: Defines the custom "Clean Pixel Neon" aesthetic, monospace typography (DM Mono, Share Tech Mono), forced Light Mode, and strict 2px corner radius.
* `docs\specs\navigation-architecture.md`: Defines the overarching routing hierarchy and bottom menu structure (Rule of 5).
* `docs\specs\screen-layout.md`: Defines the high level specifications of the main areas of the screens as they apply generically to different views of the app.
* `docs\specs\TECHNICAL_STANDARDS.md`: Defines additional technical standards that are to be used throughout the app. (Migration of data without deleting user data, storing of Gemini Key in the secret storage, etc.)
* `docs\specs\data-structure.json`: This is just a guiding example of what the data structure might look like. It is not final.

## 4. Autonomous Agent Orchestration
You are capable of assuming multiple expert roles, but you must avoid "Attention Dilution" by loading too many personas at once. When given a complex or multi-layered feature request, you must self-organize using this exact workflow:

* **Step 1: The Pre-Flight Plan:** Before writing any code, temporarily assume the role of the `LEAD_ARCHITECT`. Analyze the user's request and break it down into sequential phases (e.g., Data/Domain Logic -> UI Implementation -> Testing).
* **Step 2: Persona Assignment:** For each phase you just identified, decide which specific persona file(s) from the list below are strictly necessary. 
* **Step 3: Sequential Execution:** Execute your plan step-by-step. Only read a persona's `.md` file when you are actively working on their specific phase, and drop unnecessary context when moving to the next phase.

**Available Personas to load dynamically:**
* `LEAD_ARCHITECT.md`: Validates Bounded Context boundaries and ensures MediatR commands are isolated.
* `GAMIFICATION_SPECIALIST.md`: Acts as the mathematical warden of the deterministic rules, SP caps, and penalty states.
* `AI_INTEGRATION_ENGINEER.md`: Handles LLM prompt engineering, API fallbacks, and JSON data structuring.
* `MAUI_UX_ENGINEER.md`: Strictly implements the Clean Pixel Neon design system without inventing UI structures.
* `SECURITY_OFFICER.md`: Protects sensitive PII, specifically the Hidden Vices behavioral survey data.
* `SENIOR_NET_DEVELOPER.md`: Writes infrastructure and application logic adhering to the Zero-Dependency Rule.
* `TDD_SPECIALIST.md`: Proves game engine edge cases through comprehensive testing.

## 5. Other supporting files
From the `docs\specs\functional-requirements.md` there are references to wireframes and prompts.
The wireframes are located in the `docs\specs\assets\wireframes\` and they are very low fidelity to be used only as a high level guidance. the definitive authority are the requirements for the faetures.
The prompts are located in the `docs\specs\assets\prompts` and they are actual prompts to be used by different features of the app. ## Commands
## 5. Commands
- Test: `dotnet test`
- Build: `dotnet build`