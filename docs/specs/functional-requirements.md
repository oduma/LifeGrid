# Functional Requirements Specification: Core Application Engine (LifeGrid)

This document serves as the comprehensive, production-ready Functional Requirements Specification for the application's core engine. It outlines the deterministic rules, mathematical algorithms, behavioral loops, and data-state transitions required for implementation by development agents (e.g., Claude Code). 

---

## 1. User Setup & Onboarding Workflow (DONE)

### 1.1 The Settled Blueprint: Onboarding & Setup
The setup architecture is designed to be mathematically clean, psychologically motivating, and highly resistant to system-gaming. Any user interaction recorded in the system will have to bear a timestamp.

    [1. Stated Goals] ─────────> [2. AI Habits Pool] ──────> [3. Weekly Plan & Score]
    • Multiple allowed           • AI generated    • Week-by-week math
    • Descriptions + Deadlines                     • Contextual scaling curves

### 1.2 Goal-Driven Onboarding
* **Multi-Goal Support:** Users can input multiple goals right from day one (e.g., "Run a Marathon in 6 months", "Learn Spanish in 1 year").
* **User Entry:** each Goal will be entered in **free text input**.
* **Required Attributes:** Each goal will be analised by an AI and will result in being transformed in structured data with a **Clear Description** and an absolute **Deadline**.
* **Ambient Tagging:** The AI automatically handles high-level categorization (e.g., Physical, Intellectual) as background tags without forcing the user to navigate a rigid menu.
* **Prompt used by the AI:** <file path=".\assets\prompts\prompt1.txt">
* **Wireframe:** <file path=".\assets\wireframes\wf1.png">

### 1.3 Goal Context Interrogation (Inline Onboarding)
* **Workflow Placement:** Immediately after the user inputs their high-level goal and deadlines, but *before* the AI generates the pool of habits.
* **Contextual Trigger:** The AI analyzes the text of the goal and surfaces a maximum of **3 highly targeted, goal-specific baseline questions**. This is handled dynamically via chat or a minimal form interface.

    [User Inputs Goal] ──> [AI Generates 2-3 Specific Baseline Questions] ──> [AI Generates Habits]

### 1.4 Goal-Specific Baseline Question Blueprints
The system uses the category tag and goal keyword to pull the precise variables required to calibrate the pacing difficulty:

* **Intellectual / Skills Goals (e.g., "Learn Spanish")**
    * *Current State:* "What is your current level? (Absolute Beginner, Conversational, Advanced)"
    * *Resource Access:* "Do you have any active tools or subscriptions you want to use? (Duolingo, local classes, physical textbooks, a native speaker friend)"
    * *Immersion Availability:* "How much time do you realistically spend commuting or relaxing where you could listen to passive audio (podcasts, music)?"
* **Physical / Fitness Goals (e.g., "Run a Marathon")**
    * *Biometrics:* "To calibrate a safe recovery pacing, what is your age and gender?"
    * *Current Athletic Baseline:* "What is your current running baseline? (Can comfortably run a 5k, regular gym-goer but don't run, couch baseline)"
    * *Injury / Constraints:* "Do you have any physical joint or health limitations the engine should account for to prevent overtraining?"
* **Prompt used by AI to generate the three questions:** <file path=".\assets\prompts\prompt2.txt">
* **wireframe for the three questions:** <file path=".\assets\wireframes\wf2.png">

### 1.5 Impact on the Success Score & Habit Calibration

* **Data Submission & AI Processing:** Once the user answers the maximum of 3 AI-generated environmental questions, they submit these text responses alongside the structured goal information (Name, Calculated Deadline, Duration). The AI engine processes this complete, bundled payload to dynamically formulate the tailored weekly habits and the initial schedule.
* **Tailored Difficulty:** The user's answers directly modify the *intensity value* of the recommended habits. (e.g., An absolute beginner runner gets "Walk 5km/week" in Week 1; an experienced runner gets "Run 15km/week" in Week 1).
* **Validating the Success Metric:** This context ensures the math remains honest. If a user lies about their baseline, the AI will generate habits that are too difficult, prematurely triggering the Procrastination Escalator.
* **Prompts for generaticng the habits:** <file path=".\assets\prompts\prompt2.1.txt"> <file path=".\assets\prompts\prompt2.2.txt">
* **Navigation after etsblishing the habits** app navigates back to the create goal screens but now the screen also has a list of one existing clickable goal and underneath there is an Add New Goal button that if clicked will launch again the creation of another goal.
* **Wireframe** <file path=".\assets\wireframes\wf1.png">

### 1.6 Contextual, Adaptive Weekly Scaling
The Goal Success Score is calculated strictly on a week-by-week basis to prevent timeline overwhelm. The progression curve adapts to the goal type:
* **Physical/Endurance Goals:** The weekly workload scales up noticeably to reflect actual physiological progression requirements.
* **Intellectual/Consistency Goals:** The workload remains relatively steady and flat, prioritizing long-term routine over sudden intensity spikes.

### 1.7 The Hidden Vices & Bad Habits Detector
This workflow functions as a preventative risk-assessment layer, operating alongside or immediately following the core setup to identify pre-existing behavioral patterns that threaten goal success.

* **Behavioral Survey Interrogation:** The user is presented with a concise, targeted survey designed to uncover default routines, stress-response behaviors, and daily time-sinks.
* **Prompt to generate the questions** <file path=".\assets\prompts\prompt3.1.txt">
* **wireframes** <file path=".\assets\wireframes\wf3.png"> <file path=".\assets\wireframes\wf4.png">
* **AI Analysis & Extraction:** The AI processes the survey responses to explicitly identify and define the user's "Hidden Vices" or bad habits. Using<file path=".\assets\prompts\prompt3.2.txt">
* **Goal Linkage:** The AI cross-references the extracted bad habits against the user's specific Stated Goals, drawing direct causal links where a vice directly impedes a goal.
* **Danger Level Quantification:** The AI assigns a quantified **Goal Danger Level (Scale: 1 to 5)** to each linked bad habit, assessing its potential impact on the timeline and success score of the goal attached to.

### 1.8 Optional Survey & Initial Shield Economy

The Hidden Vices and Bad Habits survey is presented to the user as an optional onboarding component. To incentivize deep engagement and upfront behavioral honesty, the engine integrates a specific, cap-bypassing reward mechanic during setup.

* **Standard Setup Baseline:** Upon finalizing the mandatory onboarding steps, the system automatically credits the user with **1 "Life Happens Shield"**.
* **The Survey Incentive:** If the user voluntarily completes the Hidden Vices survey, the system processes the data and instantly credits them with **1 additional "Life Happens Shield"**.
* **The Cap Bypass Rule:** The specific bonus shield earned via this survey explicitly overrides the standard application "Anti-Hoarding Cap" (which normally enforces a strict maximum inventory of 2 shields). Earning this initial bonus shield expands the user's maximum allowable inventory to **3 Shields**.
* **Starting State Outcomes:**
  * **Setup without Survey:** 1 Shield. Max inventory cap of 2 applies.
  * **Setup with Survey:** 2 Shields. Max inventory cap of 3 applies.
* **Wireframe:** <file path="wf5.png"> 

### 1.9 Post-Setup System State & Data Architecture
Upon successful completion of the onboarding, context interrogation, and vice detection workflows, the AI finalizes the initial system state. The application generates and stores the following relational data structure to power the engine:

* **User_Profile**
  * `UserID`: String
  * `Current_Level`: Integer
  * `Economy`: 
    * `Lifetime_GP_Average`: Float (Global average progression across all goals)
    * `Lifetime_XP`: Integer
    * `Current_SP`: Integer (0-29, tracks progress towards next Shield)
    * `Shields_Available`: Integer (0-3)
    * `Max_Shield_Cap`: Integer (2 or 3)
  * `Active_States`: `Double_XP_Mode` (Boolean), `Double_XP_Expiry` (DateTime)
  * `Trophy_Room_Badges`: List of earned badges (BadgeID, Type, Description, Date_Earned)

* **Goal**
  * `GoalID`: String
  * `Description`: String
  * `Duration`: String (Textual representation, e.g., '6 months'. Updates dynamically upon Overwhelmed timeline extension)
  * `Deadline_Date`: DateTime (Updates dynamically upon Overwhelmed timeline extension)
  * `Status`: String (Active, Overwhelmed, Abandoned, Completed)
  * `Ambient_Tag`: String
  * `Linked_Bad_Habits`: List of identified vices (BadHabitID, Description, Danger_Level 1-5)

* **Week (Master Timeline)**
  * `WeekID`: String
  * `Week_Number`: Integer
  * `Start_Date`: DateTime
  * `Status`: String (Active, Hibernated, Frozen)
  * `Global_Metrics`: `Total_Weekly_SP_Earned` (Integer)

* **Week_Goal_Items (Junction Object linking a Goal to a Week)**
  * `Week_GoalID`: String (Primary Key)
  * `GoalID`: String (Foreign Key to Goal)
  * `Penalty_State`: String (Clean, Level_1_Warning, Probation_Week_2, Reckoning_Week_3)
  * `Metrics`: 
    * `Goal_Weekly_GP`: Float (The strict evaluation metric for THIS specific goal)
    * `Goal_Weekly_XP_Earned`: Integer (Subject to 50% cut in Week 2 or 100% cut in Week 3)

* **Habit**
  * `HabitID`: String
  * `Week_GoalID`: String (Mandatory logical link: Identifies which goal's week state this habit serves)
  * `Habit_Type`: String (Planned, Moment Burst, Flash)
  * `Habit_Name`: String
  * `Habit_Description`: String
  * `Target`: 
    * `Target_Value`: Float
    * `Measurement_Unit`: String
    * `Deadline_DateTime`: DateTime
  * `Completed_Values_Log`: 
    * `Actual_Value`: Float
    * `Measurement_Unit`: String
    * `Proof_Text`: String (Nullable)
    * `Proof_Image_URL`: String (Nullable)
    * `Timestamp`: DateTime (Absolute timestamp of user interaction)

---

## 2. The Main Screen (Interaction Hub)

This interface is the primary operational dashboard of the application. Upon completing the onboarding and setup phases, the user is deposited here. It serves as the central command center for habit execution, logging, and gamified feedback. 
Main interaction screen wireframe:<file path=".\assets\wireframes\wf6.png">

### 2.1 The Week-Centric Timeline (Done)
* **Weekly Paradigm:** The application actively suppresses the traditional "daily to-do list" anxiety by anchoring navigation to the **Week** (`Week Number` from the data schema). 
* **Chronological Navigation:** The user navigates through a structured timeline focusing on "Current Week." 
* **Temporal Visibility:** Users can scroll backward to review past performance or scroll forward to preview upcoming AI-scaled workloads.
* **Wireframe** <file path=".\assets\wireframes\wf7.png"> 

### 2.2 The Global Goal Selector (Focus Filter) (done)
* **Aggregate View (Default):** The default state displays the combined Weekly Habit Roster across *all* active goals, providing a comprehensive overview of the week's total required effort.
* **Isolation Mode:** A prominent, frictionless Goal Selector allows the user to isolate a single Goal. 
* **Dynamic UI Updates:** Selecting a specific goal filters the screen to show only the `Week_Goal_Items` and `Habits` associated with that goal, alongside a persistent, ambient warning indicator for any `Linked Bad Habits` threatening that specific objective.

### 2.3 The Weekly Habit Roster (Action Layer) (Done)
* **Interactive Habit Modules:** For the actively viewed week and selected goal(s), the screen renders the necessary micro-actions as distinct UI cards.
* **Progress Visualization:** The core of the card is a visual comparison between the `Completed Values` (aggregated) and the `Target Value` for that week.
* **Zero-Friction Logging:** Each habit module must include an immediate, inline mechanism allowing the user to append new `Completed Values` to the log.
* **Wireframe** <file path=".\assets\wireframes\wf8.png">

---

## 3. Execution Workflow (Playing the Game)

### 3.1 The Habit Logging Trigger (Done)
* **Interaction:** The user initiates a logging event by tapping on an active Habit Card within the Weekly Habit Roster.
* **Temporal Constraints:**
    * *Current Week:* Fully active and tappable.
    * *Past Weeks:* Active and tappable (allows retroactive logging for missed data entry).
    * *Future Weeks:* Strictly locked and non-interactive.

### 3.2 The Logging Interface (Data Entry Form) (Done)
Upon tapping a valid Habit Card, a dedicated screen is presented.

#### 3.2.1 Informational Context (Read-Only) (Done)
* `Goal Name`, `Deadline Date`, `Week Number`, `Week Starting Date`, `Habit Name` & `Description`, `Target Value`.

#### 3.2.2 The Completed Set (Editable Fields) (Done)
The user inputs their actual progress through a specific data payload called the **Completed Set**. 
* `Completed Value`: The numeric amount achieved + measurement unit.
* `Proof of Completion`: Requires one of two mutually exclusive inputs:
    * *Option A:* Text field.
    * *Option B:* Image upload.
    * *Rule:* The UI must enforce this as an absolute XOR (exclusive OR) state.

### 3.3 Multi-Entry Support (Done)
* **Incremental Progress:** Users can submit multiple, distinct Completed Sets for the same habit over the course of the week.
* **Data Handling:** Each successful submission appends a new Completed Set object with a absolute `Timestamp` to the `Completed Values Log`. 

### 3.4 Proof of Work Verification Paths (Done)
The system treats both verification paths as equally prestigious. Providing Proof of Work awards Bonus XP.
* **Path A (Visual Proof):** Image upload.
* **Path B (Text Reflection):** Single-sentence reflection. The internal AI validates that the reflection is unique and genuine.

### 3.5 The Cheating & Integrity Deterrent  (Done)
* **The Trigger:** The internal AI flags blatant data fabrication or duplicate verification images.
* **The Consequence:** The user’s Shield Points counter instantly drops by a flat **-30 SP**.
* **The Deficit Dynamic:** If SP drops below zero, they enter a deep deficit and must log flawless activities just to reach a 0 SP baseline before earning new Shields.
* **Prompts** <file path=".\assets\prompts\prompt4.1.txt"> <file path=".\assets\prompts\prompt4.2.txt">

### 3.6 Weekly Tracking & Behavioral Economy (Done)
* **Granular Logging:** Each log directly updates their trajectory toward that week's macro-goal volume.
* **The Three-Tier Reward System:** Experience Points (XP) and Shield Points (SP) are allocated strictly based on effort and truthfulness:

| Activity Type | Verification Method | XP Reward | SP Reward (Shield Points) |
| :------------ | :-------------------| :-------- | :------------------------ |
| **Proven** | Path A (Visual Capture) or Path B (Text Reflection) covering between 75% and 100% of the target value | **20 XP** | **4 SP** |
| **Partially Proven** | Path A (Visual Capture) or Path B (Text Reflection) covering less than 75% of the target value | **10 XP** | **2 SP** |
| **Unproven** | Self-reported (no proof provided) | **3 XP** | **1 SP** |

### 3.7 Recording Non-Standard Types of Habits
* **Momentum Burst Quests (Overachiever Reward):** Successful completion generates and awards a personalized **Overachiever Badge**.
* **Flash Quests (Double XP State):** Successful, on-time completion instantly shifts the application into **Double XP Mode**. For the remainder of the current week, all standard XP earned is multiplied by two.

### 3.8 Real-Time Progression Math & Global HUD  (Done)
Every successful data entry triggers a recalculation of core metrics, which are broadcasted system-wide.

* **Goal Progression (GP) Calculation:**
    * *Definition:* A real-time metric representing the user's weekly success trajectory for a specific objective. 
    * *Formula:* GP is calculated per goal, per week (stored in `Week_Goal_Items`). It is the mathematical average of the completion percentages of all active habits tied to that goal for the current week. 
    * *Trigger Event:* The system recalculates the GP instantly upon the submission of any new `Completed Set`.

* **The Global HUD (Heads Up Display):**
    * *Persistent Visibility:* A floating banner anchored to the top of *every* screen.
    * *Data Surfaced:* The HUD continuously displays the user's current **GP**, total **XP**, and current **SP Balance**.

### 3.8.1 Metric Stratification: Weekly vs. Cumulative Tracking  (Done)
* **Dual-State Storage & Processing:** Every time a successful data entry triggers a recalculation, the database must update two separate values for each metric:
    * **Current Week Value:** Progress percentage and points earned within the currently active week. 
    * **Cumulative (Lifetime) Value:** An aggregated running total (XP/GP) or current progress-to-goal (SP).

* **HUD Display Requirements:**
    * The user must see their immediate weekly performance alongside their permanent lifetime growth/balances. 
    * *Display Example:* * `GP: 80% (Week) | 65% (Lifetime Avg)`
      * `XP: 120 (Week) | 1,450 (Lifetime)`
      * `SP: 12 (Earned this Week) | 24/30 (Current SP Balance)`

### 3.8.2 XP Accumulation & Progression Architecture  (Done)
* **The Baseline Matrix:** Levels are achieved strictly through linear accumulation of XP.
* **Display:** The current user Level is display on the HUD.

### 3.8.3 Milestone Unlocks ("The Goodies")
* **Level 10 & 20:** Unlocks a deep-dive, AI-driven Strategic Performance Audit (Macro-Trend Engine).
* **Level 25 & 50:** Grants a one-off Focus Token to temporarily suspend third-party advertisements on the free tier (24-hour and 48-hour respectively).

### 3.9 Alternative Logging Flow: External Media Sharing
* **The Trigger:** The user shares an image file to the application from an external source.
* **Cascading Selection Flow:** The user must define context sequentially: Goal (populates Deadline) -> Week Starting Date (populates Week Number) -> Habit (populates Target Value) -> Input Completed Value Set.

### 3.10 Daily Engagement Tracking & "Showing Up" Badges (Done)
The engine logically divides the calendar week into two distinct blocks: First Part (Monday-Wednesday) and Second Part (Thursday-Sunday). Requires daily login.
* **Mr. Consistency (Bronze):** Logged in every day, zero habit data recorded.
* **Mr. Consistency (Silver):** Logged in every day, recorded data *only* during the Second Part (Thu-Sun).
* **Mr. Consistency (Gold):** Logged in every day AND recorded at least one activity during the First Part (Mon-Wed).

---

## 4. Additional Interactions from the Interaction Hub

### 4.1 Main Screen Interactions
#### 4.1.1 Manually Launching the Bad Habits Survey (Done)
* Accessible from the top-level Interaction Hub for users who skipped it during onboarding. Completing it instantly credits the user with **1 Bonus "Life Happens Shield"** and applies the Cap Bypass Rule. Once processed, the action vanishes permanently.

### 4.2 Goal Level Interactions
#### 4.2.1 Reduce the Load or Give Up (DONE)
The user triggers an explicit "Overwhelmed" action for a specific goal.
* **Option A: Abandon Goal:** The target goal and all associated future habits are permanently cancelled. The engine revokes **100% of the XP** historically earned through that specific goal, **plus an additional flat penalty of -100 XP**. <file path="docs\specs\assets\wireframes\wf12.png">
* **Option B: Recalculate Schedule (Overwhelmed State) (Extend Deadline):** The user provides a text comment. The AI extends the goal's overall `Deadline Date` and `Duration` by exactly **25%** and recalculates the remaining schedule, by passing it through <file path="docs\specs\assets\prompts\prompt2.1.txt"> and <file path="docs\specs\assets\prompts\prompt2.2.txt">. Passing the existing information with the extended deadline. The user incurs a flat penalty of **-100 XP**.<file path="docs\specs\assets\wireframes\wf13.png">

#### 4.2.2 Automated Overwhelmed Trigger (Progress Degradation)
* **Trigger:** Goal Progression (GP) falls below **50% for two consecutive weeks**.
* **System Action:** The app bypasses the standard Interaction Hub and forces the user into the Overwhelmed resolution screen for the failing goal. They must choose Option A or Option B to continue using the app.

#### 4.2.3 Goal Selection & Habit Filtering (Done)
* Single-goal tap isolates automatically navigated to the Timeline view with filter on their week goal items only for the selected goal and at the bottom of the view an action See all Goals. If the user clicks on See all Goals. the filter is destroyed and all the weekgoal items are displayed;  
* Long-tapping allows multi-goal selection (Goals selected should be marked with a square checked box icon). As soon as one goal is selected at the bottom of the goals list an action named "View Filtererd Timeline" should appear if the user clicks on it they navigated to the Timeline view with filter on their week goal items only for the selected goal(s) and at the bottom of the view an action See all Goals. If the user clicks on See all Goals. the filter is destroyed and all the weekgoal items are displayed 

### 4.3 Week Level Interactions

#### 4.3.1 The "I Want More" Quest System (Moment Burst Quests) (Done)
* **Trigger:** Available only for the Current Week and only for the week goals with GP is at least 100%. 
* **Execution:** User inputs a text explaining what he wants. AI generates a habit flagged as `Moment Burst Quest`. This habit awards triple XP and triple SP but is explicitly excluded from the Goal Progression (GP) mathematical average.
* **Prompt:** <file path="docs\specs\assets\prompts\prompt5.txt">

#### 4.3.2 System Pause Mechanisms (Done)
* **Hibernate Action (Proactive Pause):** The target week has *not yet started*. User inputs a reason; week is safely suspended without penalty.
* **Emergency Freeze Action (Reactive Pause):** The target week has *already started* AND the day is strictly *before Friday*. User inputs an emergency reason; week is frozen, protecting metrics from end-of-week penalty degradation. Costs **1 Life Happens Shield** as a processing fee.
* **Re-Entry Week:** If a user freezes for 2+ consecutive weeks, the app automatically scales down measurement metrics by 30% for the returning week.

#### 4.3.3 The Shield Economy (SP to Shield Transition)
* **Earning:** The app tracks accumulated `Current_SP`. Every time the user reaches a milestone of **30 SP**, the counter resets to zero and grants **1 Life Happens Shield**.
* **Cap:** Strict maximum inventory cap of 2 Shields (3 if the Optional Survey was completed).

### 4.4 Habit Level Actions
#### 4.4.1 Flash Quests (Immediate Contextual Quests)
* **Trigger:** By Thursday/Friday, if a user lags significantly behind on a goal, the app deploys a high-impact Flash Quest targeting the lowest completion percentage habit.
* **Execution:** Features a dynamically calculated `Deadline DateTime` representing an immediate expiry window of **4 hours** from acceptance. Absolute hard cutoff at 20:00:00 on Sunday.
* **Reward:** Completing the quest shifts the app into **Double XP Mode** for the remainder of the current week.

---

## 5. End of the Week

### 5.1 Display the Week Summary
A read-only dashboard triggered strictly after the chronological end of a given week. 
* Displays Global Metrics Breakdown (XP/SP) and Goal-Specific GP vs Lifetime Average.

**The Procrastination & Underachievement Calculator:**
Upon loading the summary, the engine runs a silent calculation against every individual goal assigned to that week to evaluate completion deficits. Which means calculation against all the habits associated with each of the goal for that week. 
* *Clean State:* If a habit's target was met or fell within acceptable tolerances (no penalty triggered), the system remains silent and displays no penalty indicators.
* *Level 1 Warning State:* If a habit qualifies for a penalty, the UI flags it with a prominent **Level 1 Warning** directly adjacent to all the specific habits belonging to the goal that was underachieved for that week.

**Shield Mitigation (Fix with Shield):**
If a Level 1 Warning is triggered, the user can expend a "Life Happens Shield" via the **"Fix with Shield"** button to clear it. If not, the user enters the subsequent week carrying this specific warning level for all the habits belonging to that goal.

#### 5.1.1 The Procrastination & Under-Achievement Escalator
This system monitors user compliance week-by-week using the Goal's Weekly GP via a compounding 3-week probationary structure:
* **Week 1: The Warning Trigger**
    * *Condition:* At Sunday's rollover, if the goal's weekly GP falls **<= 80%**, a **Level 1 Warning** is issued and anchored to the goal's `Week_Goal_Items`.
    * *The Shadow:* Unmitigated, the user enters Week 2 with an active Warning shadow over that goal.
* **Week 2: The Probationary Squeeze**
    * *Clearance Target:* The user **must achieve a perfect 100% GP Score for that goal** to clear the Warning.
    * *Tier A (<=99%):* Partial success. All `Goal_Weekly_XP_Earned` during Week 2 is **cut in half** (unless rescued by a shield).
* **Week 3: The Ultimate Reckoning**
    * *Condition:* The user enters Week 3 carrying a warning and fails to hit 100% GP for that goal.
    * *Consequence:* **0 XP is awarded** for the entire week. The app automatically updates the parent Goal's `Status` to **Overwhelmed** and locks the app until that specific goal state is resolved.

#### 5.1.2 Automated Procrastination Evaluation (Passive Trigger)
If the user has not manually opened the Week Summary interface by **17:00 on the first Monday** following the week's chronological end, the system automatically executes the Procrastination Calculator in the background. If a Warning is detected, it dispatches a high-priority push notification forcing the user into the Week Summary interface.

### 5.2 The "Test me I'm being good" Action (Vice Check)
* **Trigger:** Available strictly on the Week Summary screen for exactly **72 hours** after the week ends.
* **Execution:** Initiating the audit grants a flat **+20 XP**. The AI selects one `Linked Bad Habit` associated with that week's active goals and asks 1 targeted, subtle question to see if the user indulged.
* **Penalty Math:** If the AI determines the user failed, it deducts exactly **1% per Danger Level** of the triggered bad habit retroactively from that week's Goal Progression (GP). 

### 5.3 Perseverance Badges (The "Execution" Engine)
Tracks 100% Weekly Goal Success Scores based on the absolute number of goals conquered. Includes Junior Tier (Bronze, Silver, Gold based on goal count) and Streak Multipliers (2-week and 3-week streaks).

---

## 6. The Trophy Room & Social Sharing

### 6.1 The Badge Gallery Interface (Done)
* Organizes and displays all accumulated visual rewards (`Trophy_Room_Badges` list), highlighting Showing Up Badges and Overachiever Badges. Contextual metadata shows when/how it was earned.

### 6.2 Social Export Mechanisms
* Prominent "Share" triggers dynamically generate export-ready graphic cards displaying the badge and Lifetime GP/XP. Includes native OS hooks for Instagram and direct sharing hooks for Blue Sky (pre-filled with AI-generated captions).

---

## 7. Notification Area
* **Screen Overview:** A dedicated inbox for the user displaying a historical log of Push Notifications (Momentum Burst Quests, penalty warnings) and System Messages (Shield inventory updates, Weekly Recap notices). Deep-links directly to relevant goals or habits.