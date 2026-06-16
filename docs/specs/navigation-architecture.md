# Navigation Architecture Specification

## 1. Core Routing Philosophy
- **Separation of Concerns:** The Bottom Navigation bar is strictly for destination routing between primary app views. The Global HUD is strictly for status tracking and must not serve as primary navigation (though it may contain shortcuts to contextual bottom sheets, like Shield inventory).
- **Navigation Framework:** Use standard Jetpack Compose Navigation. 

## 2. Main Navigation Loop (Bottom Bar)
The application utilizes a strict 4-item bottom navigation bar, mapped to Material Design 3 (M3) standards.

- **Route 1: Home (Current Week)**
  - *Label:* Home
  - *M3 Icon:* `Icons.Filled.Home`
  - *Behavior:* Resolves directly to the active week's Interaction Hub.
- **Route 2: Weekly Flow**
  - *Label:* Timeline
  - *M3 Icon:* `Icons.Filled.ViewTimeline`
  - *Behavior:* Displays the chronological calendar view of all weeks.
- **Route 3: Goals List**
  - *Label:* Goals
  - *M3 Icon:* `Icons.Filled.TrackChanges`
  - *Behavior:* Displays the master list of all active/inactive goals.
- **Route 4: Achievements**
  - *Label:* Vault
  - *M3 Icon:* `Icons.Filled.MilitaryTech`
  - *Behavior:* Displays the gamified badge repository and XP leveling path.

## 3. Utility Navigation (Top App Bar)
Utility destinations that fall outside the daily behavioral loop are housed in the Top App Bar, flanking the Global HUD.

- **Notifications:**
  - *Placement:* Top Right.
  - *M3 Icon:* `Icons.Filled.Notifications`
  - *Behavior:* Opens the dedicated push/system message inbox screen.
- **Profile / Setup:**
  - *Placement:* Top Left (or via standard Settings gear).
  - *M3 Icon:* `Icons.Filled.AccountCircle`
  - *Behavior:* Opens user configuration, onboarding recall