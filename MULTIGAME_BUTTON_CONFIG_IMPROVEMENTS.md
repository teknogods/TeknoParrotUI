# MultiGameButtonConfig Improvement Document

**Date:** 2026-07-05  
**Status:** ✅ IMPLEMENTED (2026-07-05) — see Implementation Notes below  
**Priority:** High - Blocks proper multi-game setup workflow

---

## Implementation Notes (2026-07-05)

All critical fixes have been implemented and the solution builds clean:

| Change | Status |
|--------|--------|
| `CopyAllBindings()` helper — all sync ops (Apply/Copy From Game/Load Profile) now copy ALL input APIs | ✅ Done |
| `ResetToDefault_Click` resets ALL APIs to a clean state | ✅ Done |
| MergedInput added to InputApiSelector dropdown (default selection) | ✅ Done |
| `BuildMergedBindName()` + `UpdateBindNameForCurrentApi()` — combined `XI: … \| DI: … \| RI: …` display | ✅ Done |
| `StartListening()` MergedInput case — XInput + DirectInput (XInput GUIDs excluded) + RawInput listen simultaneously | ✅ Done |
| `ConfigTextBox_LostFocus` MergedInput case — rebuilds merged display after capture | ✅ Done |
| **Bonus fix:** dropdown parsing now uses `Tag` (enum name) instead of localized `Content` — fixes pre-existing bug where selecting "Raw Input" (localized with space) never matched `case "RawInput"` | ✅ Done |
| Availability column enabled in XAML (was commented out; data was already computed) | ✅ Done |
| Game search box enabled (was `Visibility="Hidden"`; logic already existed) | ✅ Done |
| New resource string `MultiGameButtonConfigMergedInput` ("Merged Input (All APIs)") | ✅ Done |

Not implemented (deemed unnecessary after the all-API sync fix): "Sync All APIs" toggle checkbox (Solution 6) and copy-from-game warnings (Solution 5) — copying all APIs is now the only behavior, which eliminates the destructive partial-copy scenario the warning was meant to guard.

---

## Executive Summary

The `MultiGameButtonConfig` control has critical issues preventing proper button synchronization across multiple games:

1. **Single-API Limitation**: Only copies bindings for the currently selected input API (DirectInput/XInput/RawInput)
2. **No MergedInput Support**: Missing UI support for MergedInput, which should be the default for one-time setup
3. **Incomplete Syncing**: User bindings are lost when switching between input APIs during or after setup
4. **Profile System Incomplete**: Profile load/save doesn't preserve all input API bindings simultaneously
5. **Display Logic Gap**: Doesn't show combined bindings like the main JoystickControl does

---

## Current Problems in Detail

### Problem 1: Limited Scope Button Syncing

**Location:** `MultiGameButtonConfig.xaml.cs`, `ApplyChangesToSelectedGames()` method (lines ~1070-1137)

**Issue:**
```csharp
switch (_currentInputApi)
{
    case InputApi.DirectInput:
        if (gameButton.DirectInputButton != sourceButton.DirectInputButton || 
            gameButton.BindNameDi != sourceButton.BindNameDi)
        {
            gameButton.DirectInputButton = sourceButton.DirectInputButton;
            gameButton.BindNameDi = sourceButton.BindNameDi;
            gameButton.BindName = sourceButton.BindNameDi;  // ← OVERWRITES general BindName
            gameChanges++;
        }
        break;
    // Only XInput OR RawInput copied, NOT BOTH
}
```

**Problem:**
- Only one input API binding is copied per apply operation
- If user switches APIs (DI → XI → DI), earlier bindings are overwritten
- `BindName` property is overwritten with API-specific value, losing history
- Games end up with incomplete bindings

**Impact:**
- Users doing one-time setup across 5-10 games must repeat setup for each input API
- Switching APIs during setup causes data loss
- Profile system becomes unreliable

---

### Problem 2: Missing MergedInput UI Support

**Location:** `MultiGameButtonConfig.xaml` (lines 37-48)

**Current Code:**
```xml
<ComboBox 
    x:Name="InputApiSelector"
    Grid.Column="1"
    Width="150"
    Margin="10,0"
    SelectionChanged="InputApiSelector_SelectionChanged">
    <ComboBoxItem Content="DirectInput" />
    <ComboBoxItem Content="XInput" />
    <ComboBoxItem Content="RawInput" />
    <ComboBoxItem Content="RawInputTrackball" />
</ComboBox>
```

**Problem:**
- `MergedInput` enum value exists in `InputApi` but NOT in the UI dropdown
- Users cannot select MergedInput during one-time setup
- Forces users to pick a single API instead of using all available inputs
- Inconsistent with GameSettingsControl which adds MergedInput dynamically (line 46)

---

### Problem 3: Input API Switching Logic Incomplete

**Location:** `InputApiSelector_SelectionChanged()` method (lines ~582-645)

**Current Code:**
```csharp
private void InputApiSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    // ...
    string apiString = ((ComboBoxItem)InputApiSelector.SelectedItem).Content.ToString();
    switch (apiString)
    {
        case "DirectInput":
            _currentInputApi = InputApi.DirectInput;
            break;
        case "XInput":
            _currentInputApi = InputApi.XInput;
            break;
        // NO CASE FOR "MergedInput"!
    }
    
    // Updates only BindName, loses API-specific data
    foreach (var game in selectedGames)
    {
        foreach (var button in game.Profile.JoystickButtons)
        {
            switch (_currentInputApi)
            {
                case InputApi.DirectInput:
                    button.BindName = button.BindNameDi;
                    break;
                // ...
            }
        }
    }
}
```

**Problem:**
- Doesn't handle `InputApi.MergedInput` case
- Only displays one binding (BindName = BindNameDi/Xi/Ri)
- Should display combined format like JoystickControl does: `"XI: Button A | DI: Button 0"`
- When switching from DI to XI, user sees only XInput bindings, making it appear DI bindings are gone

---

### Problem 4: Profile System Loses Bindings

**Location:** `LoadProfileButton_Click()` method (lines ~488-525)

**Current Code (Good):**
```csharp
// Copy all input types regardless of current input API ✓ CORRECT
gameButton.DirectInputButton = savedButton.DirectInputButton;
gameButton.XInputButton = savedButton.XInputButton;
gameButton.RawInputButton = savedButton.RawInputButton;
gameButton.BindNameDi = savedButton.BindNameDi;
gameButton.BindNameXi = savedButton.BindNameXi;
gameButton.BindNameRi = savedButton.BindNameRi;
```

**Related Issue:** `ApplyChangesToSelectedGames()` (lines ~1088-1137)
```csharp
// BUG: Only copies ONE input API binding per call
case InputApi.DirectInput:
    gameButton.DirectInputButton = sourceButton.DirectInputButton;
    gameButton.BindNameDi = sourceButton.BindNameDi;
    gameButton.BindName = sourceButton.BindNameDi;
    break;
// MISSING: Copy ALL APIs regardless of current selection
```

**Problem:**
- `LoadProfileButton` does it RIGHT (copies all APIs)
- `ApplyChangesToSelectedGames` does it WRONG (copies only current API)
- Inconsistent behavior between "Apply to Selected Games" and "Load Profile" buttons
- Users report bindings disappearing after apply

---

### Problem 5: Copy From Game Only Syncs Current API

**Location:** `CopyFromGame_Click()` method (lines ~850-920)

**Current Code:**
```csharp
switch (_currentInputApi)
{
    case InputApi.DirectInput:
        targetButton.DirectInputButton = sourceButton.DirectInputButton;
        targetButton.BindNameDi = sourceButton.BindNameDi;
        // ✗ XInput and RawInput NOT copied!
        break;
    case InputApi.XInput:
        targetButton.XInputButton = sourceButton.XInputButton;
        targetButton.BindNameXi = sourceButton.BindNameXi;
        // ✗ DirectInput and RawInput NOT copied!
        break;
}
```

**Problem:**
- Only copies the currently selected API's binding
- If copying DI settings from one game to 5 others, all 5 games will lose their XI and RI bindings
- Destructive operation with no undo

---

### Problem 6: Button Availability Logic Missing

**Location:** `UpdateButtonConfiguration()` method (lines ~190-210)

**Current Code:**
```csharp
private List<ButtonViewModel> GenerateButtonViewModels(List<GameProfile> selectedProfiles)
{
    if (!selectedProfiles.Any())
        return new List<ButtonViewModel>();

    var uniqueButtons = GetAllUniqueButtons(selectedProfiles);
    var buttonViewModels = new List<ButtonViewModel>();
    
    // ✗ NO logic to track which buttons exist in which games
    foreach (var button in uniqueButtons)
    {
        buttonViewModels.Add(new ButtonViewModel { 
            Button = button,
            Availability = null  // ← Always null, no tracking
        });
    }
    
    return buttonViewModels;
}
```

**Problem:**
- Does NOT track if a button is available in all selected games or only some
- XAML has commented-out `Availability` column (line 56)
- Users don't know if they're applying a binding to all games or only some
- Could lead to partial configuration and confusion

---

### Problem 7: MergedInput Display Not Implemented

**Location:** `InputApiSelector_SelectionChanged()` (lines ~608-645)

**Missing Implementation:**
```csharp
case InputApi.MergedInput:
    // ✗ NO CASE - display logic not implemented
    // Should build combined display like JoystickControl.BuildMergedBindName()
    // Example: "XI: Button A | DI: Button 0 | RI: F"
    break;
```

**Contrast with JoystickControl.xaml.cs (lines 250-263):**
```csharp
private static string BuildMergedBindName(string xiName, string diName, string riName = null)
{
    bool hasXi = !string.IsNullOrEmpty(xiName);
    bool hasDi = !string.IsNullOrEmpty(diName);
    bool hasRi = !string.IsNullOrEmpty(riName);
    var parts = new System.Collections.Generic.List<string>();
    if (hasXi) parts.Add($"XI: {xiName}");
    if (hasDi) parts.Add($"DI: {diName}");
    if (hasRi) parts.Add($"RI: {riName}");
    return string.Join(" | ", parts);
}
```

**Problem:**
- MultiGameButtonConfig doesn't use this display format for MergedInput
- Users can't see all input bindings at once
- Inconsistent UI behavior between setup and game configuration screens

---

### Problem 8: Reset to Default Only Applies Current API

**Location:** `ResetToDefault_Click()` method (lines ~920-950)

**Current Code:**
```csharp
foreach (var game in selectedGames)
{
    foreach (var button in game.Profile.JoystickButtons)
    {
        // ✗ Only resets current API
        switch (_currentInputApi)
        {
            case InputApi.DirectInput:
                button.DirectInputButton = defaultProfile.JoystickButtons[i].DirectInputButton;
                button.BindNameDi = "";
                break;
        }
    }
}
```

**Problem:**
- Reset only clears the current input API's bindings
- Other APIs' bindings remain, causing confusion
- Should reset ALL input APIs to clean state

---

## Root Cause Analysis

The fundamental issue: **MultiGameButtonConfig was designed for single-API configuration, not unified MergedInput setup.**

The design assumes:
- User picks ONE input API at the start
- All operations (apply, copy, load, reset) work only on that API
- No need to handle multiple simultaneous input APIs

But the correct design should be:
- MergedInput is the primary mode for setup
- ALL operations work on ALL input APIs simultaneously
- Individual API selection is for editing one API in isolation (legacy support)

---

## Recommended Solutions

### Solution 1: Always Copy All Input APIs (**Critical**)

**Change:** Modify all button sync operations to copy all input API bindings at once.

**Affected Methods:**
- `ApplyChangesToSelectedGames()` 
- `CopyFromGame_Click()`
- `ResetToDefault_Click()`
- `LoadProfileButton_Click()`
- `SaveProfileButton_Click()`

**Implementation Pattern:**
```csharp
// BEFORE (current - BAD)
switch (_currentInputApi)
{
    case InputApi.DirectInput:
        gameButton.DirectInputButton = sourceButton.DirectInputButton;
        gameButton.BindNameDi = sourceButton.BindNameDi;
        break;
    case InputApi.XInput:
        gameButton.XInputButton = sourceButton.XInputButton;
        gameButton.BindNameXi = sourceButton.BindNameXi;
        break;
}

// AFTER (proposed - GOOD)
// Always copy ALL input APIs regardless of current selection
gameButton.DirectInputButton = sourceButton.DirectInputButton;
gameButton.XInputButton = sourceButton.XInputButton;
gameButton.RawInputButton = sourceButton.RawInputButton;
gameButton.BindNameDi = sourceButton.BindNameDi;
gameButton.BindNameXi = sourceButton.BindNameXi;
gameButton.BindNameRi = sourceButton.BindNameRi;

// Update display name based on current API
switch (_currentInputApi)
{
    case InputApi.DirectInput:
        gameButton.BindName = gameButton.BindNameDi;
        break;
    case InputApi.XInput:
        gameButton.BindName = gameButton.BindNameXi;
        break;
    case InputApi.MergedInput:
        gameButton.BindName = BuildMergedBindName(
            gameButton.BindNameXi, 
            gameButton.BindNameDi, 
            gameButton.BindNameRi);
        break;
    // ... etc
}
```

**Benefit:** Eliminates data loss when switching APIs. Users can configure DI, switch to XI, configure it, switch to RI, all without losing previous work.

---

### Solution 2: Add MergedInput to UI Dropdown (**Critical**)

**Change:** Add MergedInput as a ComboBoxItem in XAML.

**Implementation:**
```xml
<ComboBox 
    x:Name="InputApiSelector"
    Grid.Column="1"
    Width="150"
    Margin="10,0"
    SelectionChanged="InputApiSelector_SelectionChanged">
    <ComboBoxItem Content="MergedInput" />  <!-- ← ADD THIS (first/default) -->
    <ComboBoxItem Content="XInput" />
    <ComboBoxItem Content="DirectInput" />
    <ComboBoxItem Content="RawInput" />
    <ComboBoxItem Content="RawInputTrackball" />
</ComboBox>
```

**C# Handler Update:**
```csharp
private void InputApiSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (_isLoading) return;
    StopListening();
    
    string apiString = ((ComboBoxItem)InputApiSelector.SelectedItem).Content.ToString();
    _currentInputApi = (InputApi)Enum.Parse(typeof(InputApi), apiString);
    
    // Now handles MergedInput automatically
    UpdateButtonConfiguration();
}
```

**Benefit:** Users see MergedInput as the primary option. Cleaner UI and more intuitive for one-time setup.

---

### Solution 3: Implement MergedInput Display Logic (**High Priority**)

**Change:** Add MergedInput case to display combined bindings.

**Location:** `InputApiSelector_SelectionChanged()` method

**Implementation:**
```csharp
case InputApi.MergedInput:
    button.BindName = BuildMergedBindName(
        button.BindNameXi, 
        button.BindNameDi, 
        button.BindNameRi);
    break;
```

**Add Helper Method:**
```csharp
private static string BuildMergedBindName(string xiName, string diName, string riName = null)
{
    var parts = new List<string>();
    if (!string.IsNullOrEmpty(xiName)) parts.Add($"XI: {xiName}");
    if (!string.IsNullOrEmpty(diName)) parts.Add($"DI: {diName}");
    if (!string.IsNullOrEmpty(riName)) parts.Add($"RI: {riName}");
    return string.Join(" | ", parts) ?? "(not configured)";
}
```

**Benefit:** Users see all configured inputs at once in MergedInput mode. Matches behavior of main JoystickControl.

---

### Solution 4: Track Button Availability (**Medium Priority**)

**Change:** Implement availability tracking in `GenerateButtonViewModels()`.

**Current:**
```csharp
public class ButtonViewModel
{
    public JoystickButtons Button { get; set; }
    public string ButtonName { get => Button.ButtonName; }
    public string BindName { get => Button.BindName; set => Button.BindName = value; }
    public string Availability { get; set; }  // ← Always null
}
```

**Proposed:**
```csharp
public class ButtonViewModel
{
    public JoystickButtons Button { get; set; }
    public string ButtonName { get => Button.ButtonName; }
    public string BindName { get => Button.BindName; set => Button.BindName = value; }
    public int GameCount { get; set; }                    // ← How many games have this button
    public int SelectedGameCount { get; set; }            // ← Of selected games
    public bool IsInAllSelectedGames { get; set; }        // ← Availability flag
    public string Availability 
    { 
        get => GameCount == SelectedGameCount 
            ? "All games" 
            : $"{GameCount}/{SelectedGameCount} games"; 
    }
}
```

**Implementation:**
```csharp
private List<ButtonViewModel> GenerateButtonViewModels(List<GameProfile> selectedProfiles)
{
    if (!selectedProfiles.Any())
        return new List<ButtonViewModel>();

    var uniqueButtons = GetAllUniqueButtons(selectedProfiles);
    var buttonViewModels = new List<ButtonViewModel>();
    
    foreach (var button in uniqueButtons)
    {
        // Count how many selected games have this button
        int gameCountWithButton = selectedProfiles.Count(gp => 
            gp.JoystickButtons.Any(b => b.InputMapping == button.InputMapping));
        
        buttonViewModels.Add(new ButtonViewModel { 
            Button = button,
            GameCount = gameCountWithButton,
            SelectedGameCount = selectedProfiles.Count,
            IsInAllSelectedGames = gameCountWithButton == selectedProfiles.Count
        });
    }
    
    return buttonViewModels;
}
```

**Benefit:** Users know which buttons exist in all vs. some games. Can make informed decisions about which buttons to configure.

---

### Solution 5: Validation & Warnings (**Medium Priority**)

**Add:** Warn users when performing destructive operations.

**Example - CopyFromGame:**
```csharp
private void CopyFromGame_Click(object sender, RoutedEventArgs e)
{
    var selectedGames = _filteredGames.Where(g => g.IsSelected).ToList();
    if (selectedGames.Count == 1)
    {
        // ... game selection logic
        
        // Check if source has all APIs configured
        bool sourceHasAll = !string.IsNullOrEmpty(sourceProfile.JoystickButtons[0].BindNameDi) &&
                           !string.IsNullOrEmpty(sourceProfile.JoystickButtons[0].BindNameXi);
        
        if (!sourceHasAll && selectedGames.Count > 1)
        {
            var result = MessageBox.Show(
                "Source game doesn't have all input APIs configured.\n" +
                "This will overwrite existing bindings in target games.\n\n" +
                "Copy anyway?",
                "Warning", 
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            
            if (result != MessageBoxResult.Yes)
                return;
        }
    }
}
```

**Benefit:** Prevents accidental data loss from incomplete configurations.

---

### Solution 6: Add "Copy All APIs" Checkbox (**Low Priority**)

**Change:** Let users choose between syncing current API only vs. all APIs.

**UI Addition:**
```xml
<CheckBox
    x:Name="SyncAllApisCheckBox"
    Content="Sync All Input APIs (Recommended)"
    IsChecked="True"
    Margin="10,5"
    ToolTip="When checked, all input API bindings are copied. When unchecked, only the current API is copied." />
```

**Implementation:**
```csharp
private bool SyncAllApis { get => SyncAllApisCheckBox.IsChecked == true; }

private void ApplyChangesToSelectedGames()
{
    if (SyncAllApis)
    {
        // Copy all APIs (proposed default behavior)
        gameButton.DirectInputButton = sourceButton.DirectInputButton;
        gameButton.XInputButton = sourceButton.XInputButton;
        gameButton.RawInputButton = sourceButton.RawInputButton;
        // ... BindNames
    }
    else
    {
        // Copy only current API (legacy mode)
        switch (_currentInputApi) { /* ... */ }
    }
}
```

**Benefit:** Power users can choose legacy behavior if needed. Improves backward compatibility.

---

## Implementation Priority & Effort

| # | Solution | Priority | Effort | Time | Breaking | Value |
|---|----------|----------|--------|------|----------|-------|
| 1 | Always Copy All APIs | **CRITICAL** | High | 2-3h | Yes* | **CRITICAL** |
| 2 | Add MergedInput UI | **CRITICAL** | Low | 30min | No | **CRITICAL** |
| 3 | MergedInput Display | **HIGH** | Low | 1h | No | HIGH |
| 4 | Button Availability | Medium | Medium | 1h | No | MEDIUM |
| 5 | Validation Warnings | Medium | Low | 1h | No | MEDIUM |
| 6 | Sync Mode Toggle | Low | Low | 30min | No | LOW |

*Breaking change: Users will need to reconfigure games that were using single-API mode. This is recommended to fix current issues.

---

## Implementation Roadmap

### Phase 1: Fix Data Loss (Do First)
1. Modify `ApplyChangesToSelectedGames()` to copy ALL APIs
2. Modify `CopyFromGame_Click()` to copy ALL APIs
3. Modify `ResetToDefault_Click()` to reset ALL APIs
4. Test with 3-4 games to verify no data loss on API switch

### Phase 2: Improve UI (Do Second)
1. Add MergedInput to InputApiSelector dropdown
2. Implement BuildMergedBindName() helper
3. Update InputApiSelector_SelectionChanged() to handle MergedInput
4. Update UpdateButtonConfiguration() to show combined bindings

### Phase 3: Enhance UX (Do Third)
1. Implement button availability tracking
2. Enable Availability column in XAML (currently hidden line 56)
3. Add validation warnings to CopyFromGame
4. Add "Sync All APIs" toggle checkbox

---

## Testing Checklist

- [ ] Select 3 games, configure DI bindings, verify all saved
- [ ] Switch to XI, configure XI bindings, verify DI bindings still exist
- [ ] Switch to MergedInput, verify combined display `XI: X | DI: Button0`
- [ ] Use "Apply to Selected Games", verify ALL APIs copied
- [ ] Use "Copy From Game", verify ALL APIs copied
- [ ] Use "Load Profile", verify ALL APIs loaded
- [ ] Use "Reset to Default", verify ALL APIs reset
- [ ] Perform the above with 5+ games selected
- [ ] Save configuration, close app, reopen, verify all settings persisted

---

## Notes

- **GameSettingsControl Reference:** Already handles MergedInput correctly (line 46 adds it dynamically)
- **JoystickControl Reference:** Already has BuildMergedBindName() method that should be reused
- **GameRunning View:** Already properly handles MergedInput (line 70 detects it)
- **Compatibility:** MergedInput is an existing InputApi enum value - no schema changes needed
- **Documentation:** Add comments explaining the "always sync all APIs" principle

---

## Conclusion

The MultiGameButtonConfig needs a strategic redesign from single-API to multi-API-aware. The foundation exists (MergedInput enum, display logic in JoystickControl), but MultiGameButtonConfig doesn't use it.

**Recommended approach:**
1. Treat MergedInput as the primary mode (not a special case)
2. Always operate on all input APIs simultaneously
3. Use input API selector only to show/hide combined or individual bindings
4. Consistently use the BuildMergedBindName() pattern for display

This aligns MultiGameButtonConfig with the rest of the application's input handling architecture.
