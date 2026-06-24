# LifeGrid - Phase 25 Vertical Slice Requirements
## The Vault: Badge & Achievement Showcase

This document specifies the structural and technical requirements for Phase 25. The objective is to introduce "The Vault," a dedicated root-level view accessed via the bottom navigation menu. This screen acts as the user's trophy room, displaying earned gamification badges using a responsive grid layout.

---

## 1. External Reference Mapping
Claude Code must parse visual and layout bounds directly from the master repository definitions:
* **Navigation Architecture:** `navigation-architecture.md` (Vault routing node)
* **Design System & Icons:** `style-guide.md` (Typography, colors, and Google Material Symbol integration)
* **Data Schema:** `data-structure.json` (Targeting the `UserProfile` achievement/badge arrays)

---

## 2. Domain & Application Layer (Query Architecture)

### 2.1 Badge Aggregation Service
* Implement a query handler (e.g., `GetUserBadgesQuery`) responsible for retrieving the list of achievements earned by the user.
* **Domain Entity Mapping:** Ensure the `Badge` or `Achievement` value object contains at minimum:
  * `Icon_Name` (String - Maps directly to the Google Material Symbol identifier, e.g., `"workspace_premium"` or `"event_available"`).
  * `Title` / `Description` (String).
  * `Earned_Date` (DateTime).

---

## 3. Presentation Layer (MAUI UI Injection)

### 3.1 Routing & Navigation Binding
* Activate the `"Vault"` node in the global bottom navigation menu.
* Tapping this node must safely route the Main Interaction Area to the new `VaultView` without stacking over the `HomeView` (maintain root-level tab switching behavior).

### 3.2 Vault Grid Layout Architecture
Within the Main Interaction Area, construct a responsive grid to showcase the badges.

* **Grid Container:** Implement a `CollectionView` configured with a `GridItemsLayout`.
  * **Span Constraint:** Set the grid `Span` to `3` or `4` columns (dynamically calculating based on device width to ensure comfortable spacing).
  * **Spacing:** Apply standard padding between rows and columns as defined in `screen-layout-specifications.md`.

### 3.3 The Badge Item Template
For each badge in the collection, render a distinct UI component adhering to the following top-to-bottom vertical stack:

* **Top Element (The Icon):** * Render a Google Material Symbol using the string provided by `Icon_Name`.
  * Size the icon prominently (e.g., 48dp or 64dp).
  * Apply the `Primary` or `Secondary` accent color token to highlight earned badges, while unearned/locked badges (if displayed) should use a muted, low-opacity `On-Surface` color.
* **Bottom Element (The Description):**
  * Render the text description directly beneath the icon.
  * **Alignment:** Strictly `Center` aligned.
  * **Typography:** Utilize the secondary monospace font (`Share Tech Mono`) with a small label size to prevent text wrapping issues within the tight grid columns.

### 3.4 The Empty State
* If the `GetUserBadgesQuery` returns an empty collection, render a centered empty-state message.
* **Text:** `"The Vault is empty. Stick to your grid to earn your first badge."` (Utilizing standard body typography).

---

## 4. Test-Driven Development (TDD) Invariants

* **Navigation State Verification:** Assert that tapping the Vault bottom menu icon successfully executes the routing command to load `VaultView` into the main viewport, and highlights the Vault icon as the active state.
* **Grid Data Binding Assertions:** Write a view model test verifying that the badge collection successfully populates the `ObservableCollection<Badge>` bound to the view.
* **Empty State UI Binding:** Assert that the `IsEmptyStateVisible` flag evaluates to `true` and hides the `CollectionView` when the user has zero earned badges.