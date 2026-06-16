# Screen Layout & Real Estate Specification

## 1. Core Layout Philosophy
- **Framework:** Jetpack Compose `Scaffold` or standard nested Column structure.
- **Behavior:** The Top HUD and Bottom Navigation are firmly pinned (static). Only the Main Interaction Area is vertically scrollable.
- **System UI:** Do not hide or override the Android System Status Bar (Battery, Time, WiFi). The application must render safely below it.

## 2. Vertical Real Estate Blueprint (Top to Bottom)

### A. Global HUD & Utility Bar (Pinned)
- **Height:** ~80dp to 90dp (Fixed or dynamically wrapped, but kept compact).
- **Placement:** Anchored to the very top of the app, immediately below the System Status Bar.
- **Structure:** - *Far Left:* Profile / Setup Navigation Icon.
  - *Center Content:* The Gamification Stats (GP, SP, Level). Utilize distinct colors from the Design Tokens to differentiate between Weekly (e.g., Primary) and Lifetime (e.g., On-Surface) metrics.
  - *Far Right:* Notification Navigation Icon.

### B. Main Interaction Area (Scrollable)
- **Height:** `weight(1f)` (Dynamically consumes all remaining vertical space).
- **Behavior:** Vertically scrollable. As the user scrolls through lists (like Goals or Weekly Flow), this content slides underneath the opaque, pinned Global HUD.

### C. Monetization / Ad Container (Conditional)
- **Height:** `50dp` (Strict standard banner height).
- **Placement:** Anchored immediately above the Bottom Navigation Bar.
- **Behavior:** If the user opts out of ads (Premium), this container must be completely destroyed/removed from the composition, allowing the Main Interaction Area to instantly reclaim the 50dp of space.

### D. Core Bottom Navigation (Pinned)
- **Height:** `80dp` (Strict Material Design 3 standard).
- **Placement:** Anchored to the absolute bottom of the screen.
- **Content:** Max 4 destinations (Home, Timeline, Goals, Vault) as defined in the Navigation Architecture spec.
-