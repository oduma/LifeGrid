# LifeGrid - Phase 11 Vertical Slice Requirements
## High Fidelity Goal Card UI Specification

This document provides the unambiguous visual architecture and high-fidelity specifications for the **Goal Card** component. This component is intended for deployment within a vertically scrolling list view on an Android mobile form factor, strictly adhering to the established design system tokens.

---

## 1. External Reference Mapping
Claude Code must derive all component dimensional limits, interactive states, color mappings, and typography behaviors from the master repository specifications. Do not define raw color values or fonts internally:
* Visual Tokens & Themes (forced light mode baseline): `style-guide.md`
* Layout Constraints (Android context): `screen-layout-specifications.md`
* Data Schema Bindings: `data-structure.json` (`Goal` aggregate fields)

---

## 2. Component Visual Architecture

### 2.1 Main Card Container
* **Layout Context:** Intended for a `weight(1f)` Main Interaction Area. It is one item in a vertically stacked list view (`RecyclerView` or similar Android standard).
* **Surface Styling:** Utilizes the `#ffffff` Surface color token.
* **Border/Radius:** Strictly constrained to the universal `2px` corner radius metric. Subtle surface elevation is applied for card context.
* **Padding:** Strict internal padding (e.g., 16dp) using defined layout spacing increments from `screen-layout-specifications.md`.

### 2.2 Functional Layout Stratification (Vertical Top-Down)

#### Zone A: Status & Category Header (Top Row)
* **Description:** A horizontally split layout for high-level technical grouping.
* **Element: Ambient_Tag (Left-Aligned):** Rendered as muted text (e.g., prefixed with `#`) using the `DM Mono` primary font token. Visual styling must be minimal (no background badge).
* **Element: Status (Right-Aligned):** Rendered as a critical visual indicator. This element uses high-saturation status-color mappings (mapped per `functional-requirements.md`) for maximum prominence. Utilizes `Share Tech Mono` secondary font for clarity.

#### Zone B: Core Description (Headline Row)
* **Description:** The dominant visual element of the card. Spans full width.
* **Typography:** Renders the "Description" string using the `DM Mono` primary font token. Size is significantly larger than all other text on the card, bolded, with generous line height for readability on mobile. Mapped to the default text color (`On-Surface`).

#### Zone C: Timeline & Progression Data (Bottom Zone)
* **Description:** Densely packed metadata arranged horizontally in distinct functional columns. Uses muted baseline styling with subtle highlights on weekly data.
* **Typography:** All elements in this zone utilize the `Share Tech Mono` secondary monospace font.

* **Col 1: Duration Metric:**
    * Label: Muted text label `"DURATION"` (Share Tech Mono, muted color).
    * Value: Bolded textual representation `"6 months"` (Default text color).

* **Col 2: Deadline Metric:**
    * Label: Muted text label `"DEADLINE"` (Share Tech Mono, muted color).
    * Value: Formatted `DateTime` value `"20 Dec 2026"` (Default text color).

* **Col 3: Total Progression Budget (Weekly):**
    * Label: Muted text label `"TOTAL WKS"` (Share Tech Mono, muted color).
    * Value: Highlighted digit `"26"` (Utilizes primary accent color `#35f8db`).

---

## 3. Interaction & States

### 3.1 Uninitialized/Loading State
* **Visual Behavioral:** While data is being queried from SQLite, the card must render utilizing the design system's typewriter loading animation, with Zone B and Zone C elements temporarily masked or rendered as shimmer placeholders before the definitive goal data is hydrated.

---

## 4. Test-Driven Development (TDD) Invariants

### 4.1 Layout Integrity Asserts
* **Visual Assertion Test:** Build visual snapshot tests verifying the specific top-to-bottom layout hierarchy. 
  * Assert that the **Status** ( saturates Status color) is the most visually prominent color block.
  * Assert that the **Description** (Large DM Mono) is the largest text element.
  * Assert that the **Total Weeks** budget is visually distinguishable via the primary accent highlight.