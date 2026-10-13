# OverlayDesk - borderless overlays with snapping and locking

OverlayDesk is a lightweight C#/.NET desktop application for displaying and arranging images, videos, and other files in customisable, borderless overlay windows.

## Features

### Window Management

- **Borderless Windows:** Display files without traditional window borders, title bars, or unnecessary UI elements.
- **Multiple Files per Window:** Display and arrange multiple files within a single overlay window.
- **Independent Windows:** Overlays remain active without requiring the main launcher to be focused.
- **Always on Top:** Keep overlays above other application windows.
- **Window Repositioning:** Move entire overlay windows anywhere across your desktop.
- **Multi-Monitor Support:** Position overlays across multiple connected displays.
- **Kill All:** Close every active overlay from the launcher.
- **Exit Confirmation:** Confirmation dialogs prevent accidentally closing active overlays.

### File Display

- **Image Support:** Display common image formats, including PNG, JPG, BMP, and GIF.
- **Animated GIFs:** Play animated GIFs directly within overlays.
- **Video Playback:** Play supported video formats using LibVLC.
- **Audio Playback:** Play supported audio files.
- **Text File Viewing:** Display text, code, and configuration files.
- **Other File Types:** Open unsupported formats using their associated applications.
- **Original Image Dimensions:** Automatically detect and use an image's native resolution.
- **Custom Dimensions:** Manually configure the displayed width and height of individual files.

### Arrangement and Positioning

- **Free Positioning:** Drag files freely within overlays.
- **Individual Resizing:** Resize each displayed file independently.
- **Aspect Ratio Locking:** Preserve an image's proportions when resizing.
- **Modifier-Key Resizing:** Hold Shift + Alt while resizing to maintain the original aspect ratio.
- **File-to-File Snapping:** Automatically align files with nearby file edges.
- **Snap to Grid:** Align files to a configurable positioning grid.
- **Half-Grid Snapping:** Position files at half-cell increments for finer alignment.
- **Configurable Grid Size:** Adjust grid spacing to suit different layouts.
- **Visible Grid Overlay:** Display alignment guides across connected monitors while arranging files.
- **Optional Snapping:** Independently enable or disable file snapping and grid snapping.
- **Snap Priority:** prioritise alignment with nearby files over grid positions.
- **Arrange Mode:** Enable dedicated controls for moving and resizing overlay contents.
- **Overlay Locking:** Lock positioned overlays to prevent accidental movement, resizing, or editing.

### Layout Management

- **Saved Layouts:** Store file arrangements for future use.
- **Custom Layout Names:** Give each saved layout a recognisable name.
- **Layout Editing:** Modify files, dimensions, positions, and arrangement settings.
- **Layout Duplication:** Create copies of existing layouts.
- **Layout Deletion:** Remove layouts that are no longer needed.
- **Pinned Layouts:** Keep frequently used layouts at the top of the launcher.
- **Persistent Positions:** Remember file locations and dimensions between sessions.
- **Automatic Saving:** Save arrangement changes without manually recreating layouts.
- **Per-Layout Settings:** Configure snapping, grid spacing, aspect ratios, locking, and always-on-top behaviour independently.
- **Per-File Aspect Ratio Settings:** Override the layout's aspect-ratio behaviour for individual files.
- **JSON Storage:** Store layouts and settings locally for persistence.

### Main Launcher

- **Minimal Interface:** A lightweight central window for managing and launching overlays.
- **Quick Launch:** Open saved layouts without manually selecting their files again.
- **Layout Library:** Browse and manage all saved layouts.
- **Settings Menu:** Configure application preferences and default behaviors.
- **Overlay Controls:** Access arrangement, locking, and window-management actions.
- **Independent Operation:** Continue using overlays while the launcher is minimised or inactive.

## Controls

| Action | Input |
| --- | --- |
| Move an image | Left-drag |
| Resize and keep ratio | Shift+Alt+left-drag |
| Resize without ratio constraint | Alt+right-drag |
| Move entire layout | Shift+left-drag |
| Toggle Arrange mode | Launcher button or Ctrl+E |
| Toggle file snapping | Ctrl+Shift+S |
| Toggle grid snapping | Ctrl+Shift+G |
| Toggle always on top | Ctrl+Space |
| Close overlay (asks first) | Esc |
| Lock/unlock overlay | Launcher Lock / Unlock button |

When locked, only closing the overlay (with confirmation) remains allowed. To unlock, return to the launcher.

The optional grid is screen-aligned, including displays with negative coordinates. A 64px grid, for example, has full grid lines every 64px and subtle half-lines every 32px; dragging/snapping works at 32px steps. If snapping to both the grid and another file, the neighbouring file wins when within 12px.

## Media support

- **Images:** GIF, PNG, JPG/JPEG, BMP, GIF, TIFF, ICO, WEBP, SVG, AVIF,
- **Video:** MP4, M4V, MOV, MKV, AVI, WEBM, WMV, MPEG, MPG, TS (Using LibVLCSharp)
- **Audio:** MP3, OGG, OPUS, WAV, FLAC, M4A, AAC (Using LibVLCSharp)
- **Text:** TXT, MD, JSON, XML, YAML, LOG, CSV, INI, CS, JS, HTML, CSS, etc.
- **PDFs,** Office documents, and other unknown types are shown as external-open placeholders.

VLC video rendering uses a native child HWND; media-specific hardware/driver behaviour can affect responsiveness during resize. For native video use the Move and Size grips in Arrange mode, because playback handles its own mouse input.

## Build

1. Install the [**.NET 8 SDK**](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) on Windows
2. Go to the project's root directory
3. Run `build.bat` or `build.ps1`. The project retrieves `LibVLCSharp.WinForms` and `VideoLAN.LibVLC.Windows` from NuGet. Built files will be located in `OverlayDesk\bin\Release\net8.0-windows\`
4. Launch using `OverlayDesk\bin\Release\net8.0-windows\OverlayDesk.exe`.
5. (Optional) Move the built files to `C:\Program FIles (x86)\OverlayDesk`.

## Testing

- Run `test.bat` / `test.ps1` to execute geometry, grid and persistence checks.
- Run `publish.bat` / `publish.ps1` to build a self-contained `win-x64` package.
- The launcher stores data in `%LOCALAPPDATA%\OverlayDesk\data.json`
- The first save makes a `data.backup.json` backup of an existing data file (if none exists already).
  - Back up your layout data manually before migrating as well.
