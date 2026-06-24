# Design System & Style Guide

## 1. Core Directives
- **Role:** You are acting as the primary UI implementer for the LeanAI Android application.
- **Framework:** Adhere strictly to Material Design 3 (M3) guidelines, adapting them exclusively with the custom Design Tokens provided in this document.
- **Behavior:** Do not invent custom UI structures, shapes, or colors. Whenever generating or refactoring UI code, you must map standard M3 components to these specific tokens.
- **Theme Enforcement:** The application strictly enforces **Light Mode only**. Do not implement dynamic system-level Dark Mode color inversion. The custom color palette defined below must remain constant regardless of the user's OS settings.

## 2. Color Palette (Design Tokens)
Apply these exact hex codes to their respective Material semantic roles. Do not deviate or guess complementary shades.

- **Primary:** `#35f8db` (Use for prominent buttons, active states, FABs, and key highlights)
- **On-Primary:** `#58585a` (Use for text/icons sitting on top of the Primary color)
- **Secondary:** `#e5cde1` (Use for less prominent actions, subtle accents, and selection controls)
- **On-Secondary:** `#a20ba0` (Use for text/icons sitting on top of the Secondary color)
- **Background:** `#fbfbfe` (Use for the main underlying app background)
- **Surface:** `#ffffff` (Use for components layered above the background, such as cards, dialogs, and bottom sheets)
- **On-Background / On-Surface:** `#58585a` (Use for all standard body text, input text, and default icons)
- **Error:** `#FFFF1B77` (Use for failed validations, destructive actions, and error states)

### 2.1 Gamification Semantic Colors (Achievement Tiers)

In addition to the core functional palette, the application uses a strict trio of metallic tokens to represent gamification achievement tiers (Badges, Ranks, Streaks). Do not deviate from these hex codes when rendering tier-based UI.

* **Tier-Gold:** `#FFC300` (Use for highest-tier achievements, master level badges)
* **Tier-Silver:** `#9CA3AF` (Use for mid-tier achievements, intermediate badges)
* **Tier-Bronze:** `#D47A43` (Use for entry-tier achievements, beginner badges)

**Implementation Rule:** When rendering a tiered badge in MAUI, bind the `Color` property of the `<FontImageSource>` directly to these specific tier tokens based on the badge's data state.

## 3. Typography
The application relies entirely on a monospaced typographic hierarchy to maintain a technical, structured aesthetic. Both fonts are available via Google Fonts.

- **Primary Font:** `DM Mono`
  - *Usage:* Apply to all headings, screen titles, display text, and major emphasis (M3 `display`, `headline`, and `title` styles).
- **Secondary Font:** `Share Tech Mono`
  - *Usage:* Apply to all standard paragraph text, button labels, subtitles, and smaller UI elements (M3 `body` and `label` styles).

## 4. Component Shape Overrides
Material Design 3's default heavy rounding (e.g., pill-shaped buttons) must be overridden to match the technical typography.

- **Global Corner Radius:** `2px`
  - *Usage:* Apply a strict 2px slightly-rounded corner radius to all standard interactive and structural components. This includes Buttons, Cards, Dialog boxes, Text Fields, and Bottom Sheets.

## 5. Iconography & Visual States (MAUI Implementation Standard)

To guarantee maximum offline performance, crisp scaling, and seamless dynamic styling across both functional UI and gamified elements, the application relies entirely on font-based iconography.

* **Universal Icon Rendering Standard:** ALL iconography—including standard navigation icons, action controls, AND gamified assets (e.g., Vault Badges, Shields)—MUST be rendered using embedded Google Material Symbols font files via MAUI's `<FontImageSource>`.
* **Asset Prohibition:** Do not use static images (PNG/JPG), SVGs, network-fetched URL assets, or native Android `res/drawable` resources for any UI icons or gamified badges. 
* **Dynamic Styling & States:** Colors and opacity states (e.g., locked vs. unlocked badges, disabled vs. active buttons) must be handled dynamically and natively within XAML. 
  * Use **DataTriggers** bound to boolean states (e.g., `IsEarned`) to shift opacities or colors.
  * Use the MAUI **Visual State Manager (VSM)** to handle interaction animations (e.g., `PointerOver` for hover scaling or glow effects).

## 5.1 Vault Grid Layout Standards

To ensure consistent grid behavior across Android form factors, implement the following constraints for the Vault screen:

* **Badge Grid Configuration:**
  - **Column Count:** 3 columns (Optimized for both readability and visual impact).
  - **Icon Size (Glyph Size):** `64dp` (This ensures icon prominence while allowing for adequate padding).
  - **Padding:** Maintain a minimum of `16dp` padding between icon containers.
* **Text Styling for Grid Items:**
  - **Font:** `Share Tech Mono` (Secondary Font).
  - **Size:** `10sp` (Small enough to prevent truncation on 3-column layouts, but legible for badges).
  - **Truncation:** If the badge title exceeds 2 lines, implement `LineBreakMode="WordWrap"` or `Trimming="Tail"`.

## 6. Spacing & Margin System (The Grid)
Adhere strictly to the Google Material Design 8dp grid system to maintain visual rhythm.

- **Screen Margins:** Apply a standard `16dp` horizontal padding to all scrollable screen content so UI elements never touch the edge of the device screen.
- **Item Spacing:** Space related items within lists or adjacent elements by `8dp`.
- **Section Spacing:** Separate major vertical sections or distinct data groupings by `16dp` or `24dp`.

## 7. Animation & Transitions
Animations should enforce the gamified, technical aesthetic of the application. 

- **Data State Changes:** When dynamic text or numbers change (e.g., GP/SP values incrementing in the HUD), do not use soft crossfades. Implement a subtle "typewriter" effect (rapid deletion and re-typing of the new string) to simulate a terminal updating.
- **Screen Navigation:** All primary screen transitions and modal appearances must animate by sliding/popping up from the bottom of the screen. Do not use standard side-to-side swipe transitions.