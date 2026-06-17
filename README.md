# Grid Level Editor

A grid-based level editor for Unity, built with UI Toolkit. It lets you author, manage and preview tile-and-block levels stored as ScriptableObjects — directly in the editor and at runtime.

## Overview

The tool is split into two editor windows and a runtime generator:

- **Block Library** — define reusable block types (id, icon, prefab, footprint).
- **Level Editor** — paint levels on a grid using drag-and-drop, with selection, rotation, copy/paste and undo/redo.
- **Runtime Generator** — spawns the authored level in the scene with staggered DOTween animations and optional auto-switching between levels.

Levels and blocks are plain ScriptableObjects under `Assets/Resources/StaticData`, so they are versionable and load through the standard static-data pipeline.

## Features

### Block Library
- Dedicated window (`Tools → Grid Level Editor → Block Window`).
- Create, rename and delete block types; assign id, icon and prefab.
- Define multi-cell footprints (rectangle, L, T, cross, …) on a paint grid.
- Search and sort by id, prefab or icon name.

![Block Library](https://github.com/SinlessDevil/Grid_Level_Editor/blob/main/Images/1.png)

### Level Editor
- Visual grid editor (`Tools → Grid Level Editor → Level Window`).
- Create, rename, resize and delete levels.
- Drag blocks from the palette onto the grid; multi-cell objects render as a single merged plate that follows their exact footprint.
- Range selection, rotation, copy/paste and a per-level undo/redo history.
- Context menu (right click) for block selection, rotation, copy/paste and clearing.
- Built-in log panel and a controls overlay.

![Level Editor](https://github.com/SinlessDevil/Grid_Level_Editor/blob/main/Images/2.png)
![Context Popup](https://github.com/SinlessDevil/Grid_Level_Editor/blob/main/Images/3.png)

### Runtime Generator
- Spawns floor tiles and blocks from the authored level data through injected factories.
- Staggered show/hide animations via DOTween.
- One block per object is spawned at the object's geometric center.
- Optional auto-switching to the next level on a configurable interval.

<table><tr>
  <td><img src="https://github.com/SinlessDevil/Grid_Level_Editor/blob/main/Images/4.png" width="300"/></td>
  <td><img src="https://github.com/SinlessDevil/Grid_Level_Editor/blob/main/Images/5.png" width="300"/></td>
  <td><img src="https://github.com/SinlessDevil/Grid_Level_Editor/blob/main/Images/6.png" width="300"/></td>
</tr></table>

## Controls & Shortcuts

| Action | Input |
| --- | --- |
| Place a block | Drag a tile from the palette onto the grid |
| Move a block / object | Left-click drag |
| Select cells (range) | Ctrl + Left-click |
| Block context menu | Right-click |
| Copy / paste selection | Ctrl + C / Ctrl + V |
| Undo / redo | Ctrl + Z / Ctrl + Y (or Ctrl + Shift + Z) |
| Clear selection | Backspace / Delete |

Undo/redo is window-local and scoped to the current level: the history resets when you switch levels and when the window is closed.

## Getting Started

1. Open the project in Unity 2023.2 or newer.
2. Open `Tools → Grid Level Editor → Block Window` and create a few blocks (id + icon required; prefab optional).
3. Open `Tools → Grid Level Editor → Level Window`, create a level, then drag blocks onto the grid.
4. Enter Play mode to see the level spawned by the runtime generator.

Data locations:
- Levels — `Assets/Resources/StaticData/LevelsData`
- Blocks & library — `Assets/Resources/StaticData/BlocksData`

## Project Structure

| Path | Responsibility |
| --- | --- |
| `Assets/Code/LevelEditor` | Runtime data model (`LevelMatrixEditor`, `LevelCell`, `BlockDataEditor`, `BlockLibrary`, `LevelDataDTO`) |
| `Assets/Code/LevelEditor/Editor` | UI Toolkit editor windows, grid view, palette, popups and undo history |
| `Assets/Code/Infrastructure` | Runtime generator, factories, static-data and save/load services |

The grid is stored as a flat, row-major `LevelCell[]` (Unity cannot serialize 2D arrays) and addressed through `Index(x, y)`. Multi-cell blocks share a positive `InstanceId`; standalone cells use `0`.

## Tech Stack

- Unity 2023.2
- UI Toolkit (editor UI)
- Zenject (dependency injection)
- DOTween (runtime animation)
- Odin Inspector (serialization in `SaveLoadService`)

## License

See [LICENSE](LICENSE).
