# OverlayDesk — native overlays (performance update)

OverlayDesk is a Windows 10/11 (x64), .NET 8 Windows Forms application. Its compact launcher manages named layouts, one **borderless native overlay window per layout**, multiple independently arranged files, image/GIF playback, and LibVLC-based video/audio playback.

## What's new

- **Aspect-ratio constraints.** In **Edit layout**, check **Force-keep aspect ratio when resizing** to constrain all items in that layout. In the selected file's detail panel, check **Force ratio when dragging** to constrain only that file. Or hold **Shift+Alt while resizing** for a temporary constraint (including **Shift+Alt+left-drag** directly on an image). The original image dimensions supply the ratio, when available; otherwise the file's original displayed ratio is used.
- **Lock layouts.** Select the layout in the launcher and click **Lock**. While locked, movement, resizing, Arrange, hotkey changes to snapping/topmost, and editing in the layout editor are blocked. Click **Unlock** to modify it again. Locked state is saved and restored. You can also set the initial lock in Edit layout before saving.
- **Live file-to-file snapping.** File boundaries and matching edges snap within 12 pixels **during movement and resize**, not only when releasing the mouse. Snapping can be switched off in the editor or with Ctrl+Shift+S. File edges take priority over the grid when in range.
- **Optional screen grid.** In Edit layout, enable **Snap to screen grid** and choose an **even grid cell size** from 8–512 pixels. Item positions and resize corners snap at **half-cell intervals**. The visible major-cell and half-cell grid lines appear across all monitors *only in Arrange mode*; grid guides are non-activating and mouse-transparent. Ctrl+Shift+G toggles grid snapping for an unlocked active overlay.
- **Compact window rendering.** The native host now uses a bounding rectangle around the files instead of a full-virtual-desktop HWND. It grows when necessary while dragging and compacts again after release. Only the file rectangles are visible and receive mouse events.
- **Lower redraw overhead.** The parent form no longer allocates a giant double buffer. Region tightening is coalesced at 30fps during movement, with an exact region on release; no update timer runs when idle. Animated GIF invalidations are capped at 30fps and batched to prevent UI message flooding.
- **Preserved positioning.** Rebased child HWNDs retain their desktop coordinates when the containing host grows or shrinks. Snapping and screen grid coordinates continue to use global desktop coordinates, including negative monitors.

## Controls

| Action | Input |
| --- | --- |
| Move an image | Left-drag |
| Resize and keep ratio | Shift+Alt+left-drag an image, or hold Shift+Alt while using a Size grip |
| Resize without ratio constraint | Alt+right-drag, or drag the Size grip |
| Move entire layout | Shift+left-drag, or ALL grip in Arrange |
| Toggle Arrange mode | Launcher button or Ctrl+E |
| Toggle file snapping | Ctrl+Shift+S |
| Toggle grid snapping | Ctrl+Shift+G |
| Toggle always on top | Ctrl+Space |
| Close overlay (asks first) | Esc |
| Lock/unlock overlay | Launcher **Lock / Unlock** button |

When locked, only closing the overlay (with confirmation) remains allowed. To unlock, return to the launcher.

The optional grid is **screen-aligned**, including displays with negative coordinates. A 64px grid, for example, has full grid lines every 64px and subtle half-lines every 32px; dragging/snapping works at 32px steps. If snapping to both the grid and another file, the neighboring file wins when within 12px.

## Media support

- Native still images and animated GIF: PNG, JPG/JPEG, BMP, GIF, TIFF, ICO.
- Video: MP4, M4V, MOV, MKV, AVI, WEBM, WMV, MPEG, MPG, TS using LibVLCSharp.
- Audio: MP3, OGG, OPUS, WAV, FLAC, M4A, AAC using LibVLCSharp.
- Text: TXT, MD, JSON, XML, YAML, LOG, CSV, INI, CS, JS, HTML, CSS, PY.
- PDFs, Office documents, WEBP, SVG, AVIF, and unknown types are shown as external-open placeholders.

VLC video rendering uses a native child HWND; media-specific hardware/driver behavior can affect responsiveness during resize. For native video use the Move and Size grips in Arrange mode, because playback handles its own mouse input.

## Build and test

Install the **.NET 8 SDK** on Windows, extract the ZIP and run `build.bat` (or `build.ps1`). The project retrieves `LibVLCSharp.WinForms` and `VideoLAN.LibVLC.Windows` from NuGet. Launch `OverlayDesk\bin\Release\net8.0-windows\OverlayDesk.exe`.

Run `test.bat` / `test.ps1` to execute geometry, grid and persistence checks. `publish.bat` / `publish.ps1` builds a self-contained win-x64 package. The included GitHub Actions Windows workflow builds and runs tests. The launcher stores data in `%LOCALAPPDATA%\OverlayDesk\data.json`; the first save makes a `data.backup.json` backup of an existing data file (if none exists already). Back up your layout data manually before migrating as well.

**Testing status:** No Windows SDK/GUI is available in this environment. Source was checked structurally but a full C# compilation and interactive Windows test still require Windows with the SDK installed. This is a source distribution, not a prebuilt executable.

**Architecture:** The window's bounds fit the actual content (and can expand as you drag). Its WinForms `Region` is the union of content rectangles; there are no browser layout metrics, visible window border, title bars, scrollbars or gaps in normal display. The temporary grid is drawn by noninteractive helper forms on each monitor during Arrange mode. Monitor topology or resolution changes while running require relaunching active overlays.

## Performance troubleshooting

If performance is still poor after updating, open Task Manager and compare CPU/GPU usage with (1) a layout containing just one static PNG, (2) one GIF, and (3) one video. If the PNG is smooth but a GIF or video causes lag, decoding or resampling is likely the dominant cost. Very large still images or layouts whose items span widely separated monitors can still require significant memory and compositor work. In Arrange mode, temporarily switch off **Snap to screen grid** to test the additional grid-guide windows.

This revision preserves existing layouts and configuration; exit the old application before running the updated executable.
