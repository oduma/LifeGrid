# Implementation Plan: Phase 1 — UI Shell

**Status:** DONE — completed 2026-06-17  
**Requirements Source:** `docs/requirements/FUNCTIONAL_REQUIREMENTS.md` (Phase 1 section)  
**Target:** LifeGrid.Presentation — net10.0-android — MAUI Shell skeleton

---

## Key Decisions (from clarification)

| Decision | Choice |
|---|---|
| Launcher icon | Extract mipmap PNGs from zip → `Platforms/Android/Resources/` |
| Bottom navigation | MAUI Shell + TabBar |
| UI icons | Material Symbols Rounded .ttf + Unicode codepoints (FontImageSource) |
| Fonts | Download DM Mono + Share Tech Mono + Material Symbols from Google Fonts/GitHub |

---

## Material Symbols Codepoint Reference (As-Built)

| Icon | Material Name | Unicode | Used In |
|---|---|---|---|
| `&#xE88A;` | home | U+E88A | Home tab |
| `&#xF382;` | clock_arrow_down | U+F382 | Timeline tab *(changed from view_timeline U+E9A7)* |
| `&#xE8D0;` | track_changes | U+E8D0 | Goals tab |
| `&#xF6A0;` | social_leaderboard | U+F6A0 | Vault tab *(changed from military_tech U+EA0E)* |
| `&#xE7F4;` | notifications | U+E7F4 | Top HUD right |
| `&#xE853;` | account_circle | U+E853 | Top HUD left |

---

## Phase A — Asset Preparation

### Step A.1 — Launcher Icon (mipmap PNGs)
**Files touched:** `LifeGrid.Presentation.csproj`, `Platforms/Android/Resources/`, `Platforms/Android/AndroidManifest.xml`

1. Remove the `<MauiIcon>` item group entry from `LifeGrid.Presentation.csproj` (eliminates conflict with native mipmap resources).
2. Extract `z-ai-com/IconKitchen-Output.zip` in-memory via PowerShell.
3. Copy all `android/res/mipmap-*/` entries into `src/LifeGrid.Presentation/Platforms/Android/Resources/`, preserving density subfolder names:
   - `mipmap-mdpi/ic_launcher.png` (+ background, foreground, monochrome)
   - `mipmap-hdpi/...`
   - `mipmap-xhdpi/...`
   - `mipmap-xxhdpi/...`
   - `mipmap-xxxhdpi/...`
   - `mipmap-anydpi-v26/ic_launcher.xml`
4. Verify `Platforms/Android/AndroidManifest.xml` contains `android:icon="@mipmap/ic_launcher"`.

### Step A.2 — Typography Fonts
**Target dir:** `src/LifeGrid.Presentation/Resources/Fonts/`

1. Download **DM Mono** from Google Fonts API:
   - `DMM_Mono.zip` → extract `DMM Mono/static/DMMono-Regular.ttf`, `DMMono-Medium.ttf`, `DMMono-Italic.ttf`
2. Download **Share Tech Mono** from Google Fonts API:
   - `Share_Tech_Mono.zip` → extract `Share_Tech_Mono/ShareTechMono-Regular.ttf`
3. Download **Material Symbols Rounded** variable font from Google GitHub:
   - `MaterialSymbolsRounded[FILL,GRAD,opsz,wght].ttf`
4. Delete `OpenSans-Regular.ttf` and `OpenSans-Semibold.ttf` from `Resources/Fonts/`.
5. Confirm all `.ttf` files have `<MauiFont Include="Resources\Fonts\*" />` coverage (already in .csproj).

---

## Phase B — Global Theme

### Step B.1 — Color Tokens in Colors.xaml
**File:** `src/LifeGrid.Presentation/Resources/Styles/Colors.xaml`

Replace the default MAUI color stubs with the exact design token hex values from the style guide. Named resources:

```
Primary         #35f8db
OnPrimary       #58585a
Secondary       #e5cde1
OnSecondary     #a20ba0
Background      #fbfbfe
Surface         #ffffff
OnSurface       #58585a
Error           #FF1B77
```

Also define corner radius constant: `GlobalCornerRadius = 2`.

### Step B.2 — Typography Styles in Styles.xaml
**File:** `src/LifeGrid.Presentation/Resources/Styles/Styles.xaml`

1. Remove all default OpenSans-based `Style` entries.
2. Add font aliases as `x:String` resources:
   - `FontDMMono` = `"DMMono-Regular"`
   - `FontShareTechMono` = `"ShareTechMono-Regular"`
   - `FontMaterialSymbols` = `"MaterialSymbolsRounded"`
3. Define implicit `Label` style: `FontFamily = ShareTechMono`, `TextColor = {StaticResource OnSurface}`.
4. Define named `HeadlineStyle`: `FontFamily = DMMono`, larger `FontSize`.
5. Define `IconStyle`: `FontFamily = MaterialSymbolsRounded`, `FontSize = 24`, `TextColor = {StaticResource OnSurface}`.

### Step B.3 — MauiProgram.cs Font Registration
**File:** `src/LifeGrid.Presentation/MauiProgram.cs`

Register all fonts in `ConfigureFonts`:
```csharp
fonts.AddFont("DMMono-Regular.ttf", "DMMono-Regular");
fonts.AddFont("DMMono-Medium.ttf", "DMMono-Medium");
fonts.AddFont("DMMono-Italic.ttf", "DMMono-Italic");
fonts.AddFont("ShareTechMono-Regular.ttf", "ShareTechMono-Regular");
fonts.AddFont("MaterialSymbolsRounded[FILL,GRAD,opsz,wght].ttf", "MaterialSymbolsRounded");
```

Remove any OpenSans registrations.

### Step B.4 — Light Mode Lock
**File:** `src/LifeGrid.Presentation/App.xaml.cs`

Set `UserAppTheme = AppTheme.Light` in the `App` constructor to enforce light mode unconditionally.

---

## Phase C — Core Layout Structure

### Step C.1 — 4 Placeholder ContentPages
**Target:** `src/LifeGrid.Presentation/Pages/`

Create 4 minimal XAML ContentPage files (XAML + code-behind):
- `HomePage.xaml`
- `TimelinePage.xaml`
- `GoalsPage.xaml`
- `VaultPage.xaml`

Each page layout (identical for Phase 1):
```
Grid (RowDefinitions: *, 50)
  ├── Row 0: ScrollView → Grid → Label "Placeholder Text" (HCenter, VCenter)
  └── Row 1: AdBannerView (50dp)
```

### Step C.2 — AdBannerView ContentView
**File:** `src/LifeGrid.Presentation/Controls/AdBannerView.xaml`

```
Border (HeightRequest=50, BackgroundColor=Surface, StrokeShape="RoundRect 2")
  └── Label "Ads Area" (HCenter, VCenter, FontFamily=ShareTechMono, TextColor=OnSurface)
```

### Step C.3 — HudView ContentView
**File:** `src/LifeGrid.Presentation/Controls/HudView.xaml`

```
Border (HeightRequest=85, BackgroundColor=Surface)
  └── Grid (ColumnDefinitions: Auto, *, Auto, Padding=16,0)
        ├── Col 0: Label "&#xE853;" (IconStyle, no-op TapGestureRecognizer)
        ├── Col 1: Empty ContentView (Phase 1 — uninitialized)
        └── Col 2: Label "&#xE7F4;" (IconStyle, no-op TapGestureRecognizer)
```

No-op handlers in code-behind: `void OnProfileTapped(...)` and `void OnNotificationsTapped(...)` — empty method bodies.

---

## Phase D — AppShell Rewrite

### Step D.1 — AppShell.xaml
**File:** `src/LifeGrid.Presentation/AppShell.xaml`

Full rewrite with:

```xml
<Shell
    Shell.NavBarIsVisible="False"
    Shell.TabBarBackgroundColor="{StaticResource Surface}"
    Shell.TabBarForegroundColor="{StaticResource Primary}"
    Shell.TabBarUnselectedColor="{StaticResource OnSurface}"
    BackgroundColor="{StaticResource Background}">

    <!-- Persistent top HUD injected as Shell NavBar content -->
    <Shell.TitleView>
        <controls:HudView />
    </Shell.TitleView>

    <TabBar>
        <Tab Title="Home">
            <Tab.Icon>
                <FontImageSource FontFamily="MaterialSymbolsRounded"
                                 Glyph="&#xE88A;"
                                 Color="{StaticResource OnSurface}" />
            </Tab.Icon>
            <ShellContent ContentTemplate="{DataTemplate pages:HomePage}" />
        </Tab>
        <Tab Title="Timeline">
            <Tab.Icon>
                <FontImageSource FontFamily="MaterialSymbolsRounded"
                                 Glyph="&#xE9A7;"
                                 Color="{StaticResource OnSurface}" />
            </Tab.Icon>
            <ShellContent ContentTemplate="{DataTemplate pages:TimelinePage}" />
        </Tab>
        <Tab Title="Goals">
            <Tab.Icon>
                <FontImageSource FontFamily="MaterialSymbolsRounded"
                                 Glyph="&#xE8D0;"
                                 Color="{StaticResource OnSurface}" />
            </Tab.Icon>
            <ShellContent ContentTemplate="{DataTemplate pages:GoalsPage}" />
        </Tab>
        <Tab Title="Vault">
            <Tab.Icon>
                <FontImageSource FontFamily="MaterialSymbolsRounded"
                                 Glyph="&#xEA0E;"
                                 Color="{StaticResource OnSurface}" />
            </Tab.Icon>
            <ShellContent ContentTemplate="{DataTemplate pages:VaultPage}" />
        </Tab>
    </TabBar>
</Shell>
```

**Note on HUD + Shell.NavBarIsVisible:** Setting `NavBarIsVisible="False"` at Shell level hides the built-in navigation bar. The HUD will instead be rendered as a fixed top row in each page layout OR as a persistent Shell header via a Shell `ControlTemplate`. The exact wiring (ShellContent vs. per-page) will be confirmed at build time — both paths produce the same visual result for Phase 1.

### Step D.2 — AppShell.xaml.cs
Remove all default route registrations (not needed for Phase 1 no-op shell).

---

## Phase E — Cleanup & Verification

### Step E.1 — csproj Cleanup
**File:** `src/LifeGrid.Presentation/LifeGrid.Presentation.csproj`

- Remove `<MauiIcon>` item (replaced by native mipmap PNGs).
- Remove `<MauiImage Update="Resources\Images\dotnet_bot.png" ...>` (default asset).
- Fonts ItemGroup already covered by `<MauiFont Include="Resources\Fonts\*" />`.

### Step E.2 — Remove Default Template Boilerplate
- Delete `src/LifeGrid.Presentation/MainPage.xaml` and `MainPage.xaml.cs` (replaced by HomePage).
- Delete `src/LifeGrid.Presentation/Resources/Images/dotnet_bot.png`.

### Step E.3 — Build Verification
```
dotnet build LifeGrid.slnx
```
Exit code 0, 0 errors, 0 warnings.

---

## Dependency Graph for This Phase

```
MauiProgram.cs
    ├── registers fonts: DMMono, ShareTechMono, MaterialSymbolsRounded
    └── sets App.UserAppTheme = Light

AppShell.xaml
    ├── Shell.TitleView → HudView (controls/)
    └── TabBar → 4 Tabs → 4 ShellContent → 4 Pages (Pages/)
                              Each page:
                              Grid (* | 50)
                                ├── ScrollView → "Placeholder Text"
                                └── AdBannerView (controls/)

Platforms/Android/Resources/
    └── mipmap-*/ic_launcher*.png  (from z-ai-com/IconKitchen-Output.zip)
```

---

## Risk Notes

| Risk | Mitigation |
|---|---|
| `MaterialSymbolsRounded` variable font: some older Android API levels may not render variable fonts | Target min API 21 (already set); variable fonts supported since API 26. Test on emulator API 26+. If issues arise, fall back to static MaterialIcons-Regular.ttf with adjusted codepoints. |
| `view_timeline` (U+E9A7) and `military_tech` (U+EA0E) not present in old Material Icons font | Using Material Symbols Rounded (newer), which covers both. Confirm codepoints render at build time. |
| `Shell.TitleView` persistence across tab switches | MAUI Shell re-renders `TitleView` per-tab by default. If the HUD flickers, wrap in a `ControlTemplate` or move HUD into each page's layout row instead. |
| `Shell.NavBarIsVisible="False"` may collapse tab bar title row | Verify tab labels still appear; if not, keep Shell default navbar and override its appearance. |
| Adaptive icon XML (mipmap-anydpi-v26/ic_launcher.xml) references foreground/background drawables | Ensure `ic_launcher_foreground.png` and `ic_launcher_background.png` are present in all mipmap density folders. |
