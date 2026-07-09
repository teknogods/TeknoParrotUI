# TeknoParrot UI Theme Modernization Plan (2026)

## Current State Assessment
- **Framework**: Avalonia with FluentTheme base
- **Theme System**: Minimal — flat colors and inline styles in App.axaml
- **Visual Style**: Dated (described as "old XP" by users)
- **Issues**:
  - Harsh semi-transparent backgrounds (#10808080, #15808080, #12808080)
  - Emoji icons (unprofessional, inconsistent across platforms)
  - Minimal contrast hierarchy
  - No separation between light/dark themes
  - Flat design lacking modern depth perception

## Modernization Goals
1. **Professional appearance** aligned with 2026 UI standards
2. **Lightweight theme system** supporting light and dark variants
3. **GPU-light design** — no blur, animations, or heavy effects
4. **Fluent Design 2 principles** — Microsoft's modern system design
5. **Better typography & spacing** for visual hierarchy
6. **Improved accessibility** — better contrast ratios
7. **Cross-platform consistency** — works on Windows, Linux, macOS, and Android
8. **DPI-aware rendering** — proper scaling on mobile and high-DPI displays

---

## Phase 1: Design System Foundation

### 1.1 Color Palette (Light Theme)
```
Primary Accent:     #6C5CE7 (Modern purple, less harsh than current #7C4DFF)
Surface Primary:    #FFFFFF (Window backgrounds)
Surface Secondary:  #F5F5F5 (Cards, panels)
Surface Tertiary:   #EEEEEE (Subtle containers)
Text Primary:       #212121 (Main content)
Text Secondary:     #757575 (Secondary/muted)
Text Tertiary:      #BDBDBD (Disabled/hints)
Divider:            #E0E0E0 (Subtle borders)
Status Success:     #4CAF50
Status Warning:     #FF9800
Status Error:       #F44336
Hover/Focus:        #F0F0F0 (Subtle background lift)
```

### 1.2 Color Palette (Dark Theme)
```
Primary Accent:     #8B78FF (Lighter purple for visibility on dark)
Surface Primary:    #1E1E1E (Window backgrounds)
Surface Secondary:  #2D2D2D (Cards, panels)
Surface Tertiary:   #3A3A3A (Subtle containers)
Text Primary:       #E8E8E8
Text Secondary:     #B0B0B0
Text Tertiary:      #757575
Divider:            #404040
Status Success:     #66BB6A
Status Warning:     #FFA726
Status Error:       #EF5350
Hover/Focus:        #383838
```

### 1.3 Typography
- **Font Family**: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif (system default for Avalonia)
- **Heading 1**: 20px, SemiBold (600), line-height 1.4
- **Heading 2**: 16px, SemiBold (600), line-height 1.4
- **Heading 3**: 14px, SemiBold (600), line-height 1.4
- **Body Regular**: 13px, Regular (400), line-height 1.5
- **Body Small**: 12px, Regular (400), line-height 1.4
- **Caption**: 11px, Regular (400), line-height 1.3

### 1.4 Spacing Scale
```
xs: 4px    (close, grouped elements)
sm: 8px    (standard gaps)
md: 12px   (comfortable spacing)
lg: 16px   (large sections)
xl: 24px   (major sections)
xxl: 32px  (page-level spacing)
```

### 1.5 Corner Radius
- **sm**: 4px (small controls, inputs)
- **md**: 8px (cards, buttons, larger controls)
- **lg**: 12px (modal dialogs, prominent surfaces)

---

## Phase 2: Theme System Architecture

### 2.1 Folder Structure
```
TeknoParrotUi.Avalonia/
├── Themes/
│   ├── ThemeManager.cs           (runtime theme switching)
│   ├── IThemeProvider.cs         (abstraction for theme data)
│   ├── ThemeColors.cs            (color definitions)
│   ├── ThemeTypography.cs        (font/size definitions)
│   └── Themes/
│       ├── Light.axaml           (light theme resources)
│       ├── Dark.axaml            (dark theme resources)
│       └── Shared.axaml          (common styles, component library)
├── Controls/
│   ├── ModernButton.axaml        (button component)
│   ├── ModernCard.axaml          (card component)
│   └── [other components]
├── App.axaml                     (minimal, delegates to theme system)
└── [existing Views structure]
```

### 2.2 Theme Manager (C# Code-Behind)
```csharp
// Features:
// - Load light/dark theme at startup
// - Subscribe to system theme changes (Windows light/dark preference)
// - Persist user choice to settings
// - Reload theme dynamically without restart
// - Fallback to light theme if error
```

### 2.3 Dynamic Resource Keys (XAML)
Instead of hardcoded colors, define resource keys in each theme:
```
SystemBackgroundColorPrimary
SystemBackgroundColorSecondary
SystemTextColorPrimary
SystemTextColorSecondary
SystemAccentColor
SystemDividerColor
[etc for all 20+ colors]
```

---

## Phase 3: Component Library

Replace emoji icons with a proper icon approach:

### 3.1 Icon Strategy
- **Option A** (Recommended): Use Segoe MDL2 Assets font (Windows/Linux) + fallback
  - Built-in on Windows, available on Linux via fontconfig
  - Replace ☰ with ☰ (segoe style)
  - Replace 🎮 with  (game icon)
  - Replace ⚙ with  (settings icon)
  - One font = consistent, no emoji inconsistencies
  - **Fallback for Android/unsupported**: Font stack with system icon fonts or embedded TTF

- **Option B** (Alternative): Embed icon font as TTF resource
  - Material Design Icons (MDI) TTF embedded in app
  - Works identically on all platforms (Windows, Linux, macOS, Android)
  - Slightly larger app size (~200KB) but guaranteed consistency
  - Best for cross-platform parity

- **Option C**: Use simple SVG icons (lightweight, scalable)
  - Small SVG files for each icon
  - Scales perfectly to any DPI on all platforms
  - Slightly more rendering overhead but negligible

### 3.2 Component Styles
Rewrite the following as proper component classes in `Shared.axaml`:

**Button Component** (replaces current hardcoded button styles)
```
Classes:
- nav-primary    (sidebar/navigation, filled accent)
- nav-ghost      (minimal, transparent)
- action-primary (primary action, filled accent)
- action-ghost   (secondary action, bordered)
Variants:
- sizes: small, medium (default), large
- states: default, hover, pressed, disabled, loading
```

**Card Component** (replaces Border.card)
```
Elevation levels: none, 1 (subtle shadow), 2 (medium)
Padding: compact, normal, spacious
Border variants: none, divider, full
```

**Text Components**
```
TextBlock classes:
- .heading-1, .heading-2, .heading-3
- .body-large, .body, .body-small
- .caption, .overline
With color variants: primary, secondary, tertiary, accent
```

**Input Components**
```
TextBox, ComboBox, CheckBox redrawn with:
- Modern rounded borders
- Better focus states
- Clear error/success states
- Proper label integration
```

### 3.3 Layout Components
```
SidePanel   (sidebar with proper spacing, no semi-transparent hack)
HeaderBar   (clean header with subtitle text, proper badge)
StatusBar   (subtle footer)
ContentCard (padded content area with optional title)
FormGroup   (label + input + error message grouped)
```

---

## Phase 4: Migration Strategy

### 4.1 Phase 4a: Foundation (Week 1)
1. Create `Themes/` folder structure
2. Implement `ThemeManager.cs` with light/dark switching
3. Create color/typography definitions in C#
4. Move existing accent colors to resource keys

### 4.2 Phase 4b: Shared Theme (Week 1-2)
1. Create `Themes/Shared.axaml` with:
   - Component styles (Button, Card, TextBlock, Input)
   - Layout utilities
   - Icon font setup (Segoe MDL2)
2. Replace emoji icons throughout the app with proper icon approach
3. Update MainView to use new components

### 4.3 Phase 4c: Theme Variants (Week 2)
1. Create `Themes/Light.axaml` with light color palette
2. Create `Themes/Dark.axaml` with dark color palette
3. Update App.axaml to load theme dynamically
4. Test light/dark switching

### 4.4 Phase 4d: View Migration (Week 2-3)
Migrate views one-by-one, starting with most visible:
1. MainView (header, sidebar, navigation)
2. LibraryView (card-based game list)
3. SettingsView (form-based)
4. GameRunningView (minimal, mostly black)
5. [Other views]

Each view:
- Replace inline styles with component classes
- Update colors to use resource keys
- Improve spacing and typography
- Remove hardcoded semi-transparent backgrounds

### 4.5 Phase 4e: Polishing (Week 3)
1. Add focus/keyboard navigation visual feedback
2. Ensure proper contrast ratios (WCAG AA minimum)
3. **Cross-platform testing**:
   - Windows (desktop, multiple DPI settings, dark mode toggle)
   - Linux (Wayland & X11, system theme variations)
   - macOS (light/dark mode, Retina displays)
   - Android (portrait/landscape, various screen sizes, system dark mode)
4. Verify icon font renders correctly on all platforms
5. Performance review (no GPU-heavy effects, mobile-optimized)

---

## Phase 5: Feature Additions (Post-MVP)

### 5.1 User Settings
Add to `UiOptionsView`:
```
Theme Selection:
  - [ ] Light (default)
  - [ ] Dark
  - [ ] System (follow Windows preference)
  
Font Size:
  - [ ] Smaller
  - [ ] Normal (default)
  - [ ] Larger
  
Compact Mode:
  - [ ] On/Off (reduce spacing for 640x480 screens)
```

### 5.2 Accent Color Customization (Optional)
```
Primary Accent: [color picker]
(Auto-calculates derived colors: dark1, dark2, dark3, light1, light2)
```

### 5.3 Animation Foundation (GPU-Light, Optional)
For future use, define simple CPU-bound animations:
```
Opacity fade: 200ms
Slide (small): 150ms
(No blur, shadow, or complex transforms)
```

---
Icon Font Strategy** — Embedded TTF (Option B) ensures cross-platform consistency on Windows, Linux, macOS, and Android

### Avalonia-Specific Notes
- Use `{DynamicResource}` binding for colors (enables runtime switching)
- Define styles in `Application.Styles` in priority order
- Use `Classes` attribute for component variants (cleaner than multiple style selectors)
- FluentTheme provides baseline; we override/extend
- **Cross-Platform**: Avalonia's unified XAML works identically on Windows, Linux, macOS, and Android — no platform-specific rendering code needed

### Cross-Platform Compatibility
- **Colors**: DynamicResource ensures correct theme on all platforms (respects system dark mode on Linux/Android)
- **Typography**: System default font stack works on all platforms
- **Icon Font**: Embedded Material Design Icons TTF guarantees consistency across Windows, Linux, macOS, and Android
- **Spacing/Layout**: Relative sizing (sp/dp) scales correctly on mobile and high-DPI monitors
- **No Windows-Only APIs**: All C# code uses cross-platform .NET (avoid Windows.Foundation, etc.)

### Performance
- No theme-switching animation (instant apply)
- Minimal asset overhead (embedded icon font only)
- CSS-like styling (lightweight compared to runtime drawing)
- No layouting changes per theme (colors only)
- Mobile-optimized: lightweight theme system suitable for Android battery/CPU constraintsenables runtime switching)
- Define styles in `Application.Styles` in priority order
- Use `Classes` attribute for component variants (cleaner than multiple style selectors)
- FluentTheme provides baseline; we override/extend

### Performance
- No theme-switching animation (instant apply)
- Minimal PNG assets (just icon font)
- CSS-like styling (lightweight compared to runtime drawing)
- No layouting changes per theme (colors only)

---

## Success Criteria

- ✓ App no longer looks like Windows XP (user feedback)
- ✓ **Works identically on Windows, Linux, macOS, and Android**
- ✓ Icons render correctly on all platforms (embedded font)
- ✓ Dark mode respects system preference on all platforms
- ✓ Text scales properly on mobile and high-DPI displayss professional and modern
- ✓ Dark theme is readable and consistent
- ✓ Theme switches instantly without restart
- ✓ All views updated with new components
- ✓ No emoji icons (all replaced with icon font or SVG)
- ✓ Proper spacing and typography throughout
- ✓ WCAG AA contrast compliance
- ✓ No GPU-heavy effects
- ✓ Works on 640x480 (compact mode)
- ✓ Works on Android

---

## Estimated Effort
- **Design system** (colors, typography): 2-3 days
- **Theme architecture** (ThemeManager, resource keys): 2-3 days
- **Component library** (Button, Card, inputs): 3-4 days
- **View migration** (10 views): 5-7 days
- **Polish & testing**: 2-3 days
- **Total: 2-3 weeks** (1 developer)

---

## Next Steps
1. Approve design direction (color palette, icon strategy)
2. Create `Themes/` folder and `ThemeManager.cs`
3. Design and implement `Shared.axaml` component styles
4. Migrate MainView as proof-of-concept
5. Migrate remaining views

