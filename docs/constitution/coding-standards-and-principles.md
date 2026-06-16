# LifeGrid Constitution: Coding Standards & Principles

## 1. SOLID Principles
All code generated for LifeGrid must strictly adhere to SOLID principles to ensure the complex XP/SP calculators and UI state management remain scalable.
* **Single Responsibility Principle (SRP):** Classes and Use Cases should do one thing. Use MediatR handlers to isolate individual application commands and queries.
* **Open/Closed Principle (OCP):** Core engine math should be open for extension but closed for modification.
* **Liskov Substitution Principle (LSP):** Derived classes must be substitutable for their base classes.
* **Interface Segregation Principle (ISP):** Abstract external services (e.g., AI LLM calls, SQLite Repositories) behind granular, specific interfaces to guarantee testability.
* **Dependency Inversion Principle (DIP):** High-level modules must not depend on low-level modules.

## 2. Design Patterns & Best Practices
* **Prefer Well-Known Patterns:** Do not invent custom patterns. Rely on industry standards (e.g., Repository Pattern, Unit of Work, Mediator Pattern).
* **Dependency Injection (DI) is Mandatory:** Use `Microsoft.Extensions.DependencyInjection` for all service lifetimes. The **Service Locator pattern is strictly forbidden** as it obscures dependencies and ruins testability.

## 3. Keep It Simple, Stupid (KISS)
* Do not over-engineer. If a CRUD operation is simple, keep the implementation simple.
* Avoid premature optimization. 
* Do not use complex AutoMapper profiles with conditional business logic. Mapping should be strictly 1:1. If mapping requires business rules, write an explicit manual factory method.

## 4. Don't Repeat Yourself (DRY)
* Centralize shared logic (like XP conversion rates or penalty math) within the Domain.
* However, do not blindly apply DRY if it violates KISS. Decoupling two inherently different processes is better than forcing them to share a complex, fragile abstraction.