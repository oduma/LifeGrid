# LifeGrid - Phase 29 Vertical Slice Requirements
## Notification Inbox: Centralized Log & System Messaging

This document defines the strict requirements for Phase 29. The objective is to implement a centralized "Notification Inbox" screen (accessible via the Global HUD), providing a historical log of push and system messages with support for active deep-linking to domain-specific entities (Goals/Habits).

---

## 1. External Reference Mapping
Claude Code must parse structural rules and data schemas directly from the master repository:
* **Functional Logic:** `docs\specs\functional-requirements.md` (Point 7).
* **UI Trigger:** `docs\specs\navigation-architecture.md` Global HUD (Component-HUD).
* **Design System:** `style-guide.md` (Alert colors, spacing, and typography).

---

## 2. Domain Layer (Notification Management)

### 2.1 Notification Entity Definition
Implement the `Notification` entity within the Domain layer:
* `Id` (Guid)
* `Title` (String)
* `Message` (String)
* `Type` (Enum: Quest, Warning, Shield_Update, Weekly_Recap)
* `DeepLinkUrl` (String - A structured URI string for routing, e.g., `lifegrid://goal/{goalId}` or `lifegrid://habit/{habitId}`)
* `IsRead` (Boolean)
* `Timestamp` (DateTime)

### 2.2 Notification Service Architecture
* Implement `INotificationService`:
    * `GetHistoryAsync()`: Retrieves all stored notifications ordered by `Timestamp` (descending).
    * `MarkAsReadAsync(Guid id)`: Updates `IsRead` status in SQLite.
    * `PushNotification(Notification n)`: Intercepts system/AI events and persists them to SQLite.
    * `GetUnreadCount()`: Returns integer count for HUD badge display.

---

## 3. Presentation Layer (UI & Interaction)

### 3.1 Routing & Global HUD
* **HUD Integration:** The Global HUD Notification button must observe `INotificationService.GetUnreadCount()`. If count > 0, render a small, high-contrast notification badge (using the `Error` or `Primary` color token).
* **View Launch:** Tapping the HUD notification icon executes a navigation command to push the `NotificationInboxView` onto the navigation stack.

### 3.2 Notification Inbox UI Layout
Construct the `NotificationInboxView` list:
* **Inbox Items:** Each notification must be rendered as a list item card (2px corner radius).
* **Unread State:** Apply a clear visual differentiator (e.g., a leading dot using the `Primary` neon color or bold text styling) for items where `IsRead == false`.
* **Deep-Linking:**
    * Intercept tap gestures on any list item.
    * Execute `MarkAsReadAsync` immediately upon selection.
    * Parse the `DeepLinkUrl` string and trigger the standard MAUI/Shell routing service to navigate the user directly to the referenced `Goal` or `Habit` detail view.

---

## 4. Test-Driven Development (TDD) Invariants

* **Persistence Verification:** Write a unit test asserting that a `Notification` object pushed to the service is correctly saved to the SQLite `Notifications` table with the correct `Timestamp`.
* **Deep-Link Routing Test:** Write a view model test that mocks a tap on a notification with a valid `DeepLinkUrl`. Assert that the `NavigationService` is called with the corresponding URI string.
* **Badge Count Logic:** Assert that calling `MarkAsReadAsync` successfully decrements the value returned by `GetUnreadCount()`.
* **Integrity Test:** Verify that marking a notification as read updates the database record state without modifying the notification's original `Message` or `Timestamp`.