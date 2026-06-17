# LifeGrid - Phase 1 UI Shell Execution Requirements

## 1. External Reference Mapping
This phase builds out the empty visual skeleton of the application. Claude Code must dynamically pull all dimensional budgets, color tokens, font configurations, and menu nodes directly from the existing repository guidelines. Do not repeat or redefine these specs internally:
- **Visual Aesthetics & Themes:** `docs\specs\style-guide.md`
- **Vertical Hierarchy & Heights:** `docs\specs\screen-layout-specification.md`
- **Navigation Items & Glyphs:** `docs\specs\navigation-architecture.md`

---

## 2. Phase 1 Blueprint Specifications

### 2.1 Application Launch Icon
- Map the application's compilation pipeline to target the launch icon asset defined and supplied in `C:\Code\LifeGrid\z-ai-com\IconKitchen-Output.zip`.

### 2.2 Global HUD Container (Top View)
- Allocate the top layout zone as defined in `docs\specs\screen-layout-specification.md`.
- **State Execution:** Entirely uninitialized. No metric digits, level badges, or progression bars should be computed or rendered.
- **Left Action Item:** Render the User Setup / Profile icon defined in `docs\specs\navigation-architecture.md`.
- **Right Action Item:** Render the Notifications icon defined in `docs\specs\navigation-architecture.md`.
- **Interaction Law:** Both icons are strictly hardcoded to a safe `no-op` state. Tapping them triggers no routing transitions or panel expansions.

### 2.3 Main Interaction Area (Variable Center View)
- Bind the placeholder layout to the central weighted canvas zone defined in `docs\specs\screen-layout-specification.md`.
- **Content Law:** Render exactly one literal text string centered vertically and horizontally on the canvas:
    ```text
    Placeholder Text
    ```

### 2.4 Advertising Banner Space (Lower Anchored View)
- Position the layout bounding container exactly as mapped out in `docs\specs\screen-layout-specification.md`.
- **Content Law:** Render exactly one center-aligned literal text string inside the bounding frame:
    ```text
    Ads Area
    ```

### 2.5 Bottom Navigation Menu (Absolute Bottom View)
- Build the bottom view layer adhering to the vertical constraints in `docs\specs\screen-layout-specification.md`.
- **Nodes Configuration:** Generate the exact four-node structural layout (Home, Timeline, Goals, Vault) utilizing the specific glyph mappings from `docs\specs\navigation-architecture.md`.
- **Interaction Law:** All menu handlers are forced to a safe `no-op` state. Selecting or tapping any button must not shift active screen frames or mutate application states.