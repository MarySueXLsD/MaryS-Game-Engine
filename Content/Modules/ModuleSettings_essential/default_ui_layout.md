TABS {modules}

## General Settings
- [ ] Enable Grid {isGridEnabled}
- [x] Show FPS Counter {isFPSShowed}

Input: Enter something here {inputValue}
Color: #9370DB Background Color {backgroundColor}
Scale [min=1, max=100, step=1, value=35] UI {UIScale}

Button: Save Settings {saveSettings()}
Button: Reset to Defaults {resetToDefaults()}

---

## Display Settings
- [ ] Fullscreen Mode {isFullscreen}
- [x] VSync Enabled {isVSyncEnabled}

Scale [min=0, max=100, step=5, value=75] Brightness {brightness}
Scale [min=0, max=100, step=5, value=50] Contrast {contrast}

Button: Apply Display Settings {applyDisplaySettings()}