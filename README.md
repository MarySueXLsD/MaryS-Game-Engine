<p align="center">
  <img width="256" height="256" alt="image" src="https://github.com/user-attachments/assets/e79fb357-53e8-4713-9627-10ad7eb72629" />
  <img src="https://github.com/user-attachments/assets/59a258d0-a6f5-4e59-9c82-bf46fb7c0481" alt="MaryS Engine Architecture Diagram" width="350">
</p>

# MaryS Game Engine

**MaryS** is a 2D game engine and editor built on **C#** and **MonoGame**, focused on **turn-based and 2D games**. It aims to let developers—including those with little or no coding experience—create games through a desktop-style editor, visual tools, and an AI assistant (MarySue), with minimal hand-written code.

The editor runs like a small operating system: a **desktop** with icons and files, a **task bar** for open tools, and a **top menu bar**. All editor features are provided by **modules** that can be opened as windows. Game projects live in a **Projects** workspace; you create and manage them from the **Game Manager**, then use tools such as **Character Creation** to define characters, traits, skills, and effects for the active project.

---

## What’s in the box (current implementation)

- **Desktop** – Start screen with draggable icons and files (backed by `Content/Desktop`). Double-click to open assets or modules.
- **Module system** – The engine discovers and loads modules from `Content/Modules` via `bridge.json` and the `IModule` interface. Modules are compiled with the engine (same solution); removing a module folder disables that feature.
- **Game Manager** – Central hub for game projects: create projects (with a name + genre wizard: Isometric, JRPG, Top Down, Card Based), list and open them, activate a single **workspace**, rename/delete projects. Projects are stored under `Projects/` with an active workspace stored in `Projects/active_workspace.json`.
- **Character Creation** – Character template editor for the active workspace. Define **Characters** (templates composing stats, traits, skills, tags) and supporting entities: **Traits**, **Skills**, **Effects**, **Stats**, **Tags**. Entity descriptions and structure are documented in `EntityDescriptions.json`. Includes an asset-style browser, inspector, and workspace-scoped create/delete for all entity types.
- **Task Bar** – Shows open modules as icons; click to focus or minimize.
- **Top Bar** – Menu bar with dropdowns; integrates with the notification center.
- **Asset Browser** & **Hierarchy Tree** – Window shells (movable, resizable) ready for deeper integration with scenes and assets.
- **Chat** – AI mascot chat panel (MarySue) for in-editor assistance.
- **Console** – In-engine console; directory navigation is restricted to `Content/Desktop`.
- **Support modules** – Window management (draggable/resizable windows), pop-ups, flash messages, notification center, module settings, shared UI elements and fonts (e.g. pixel, Roboto, Smooch Sans).

Rendering and input are handled by MonoGame; the UI is custom-drawn (no separate GUI framework). Logs are written under `logs/` with a daily file.

<p align="center">
  <img src="https://github.com/user-attachments/assets/fb599633-4a7c-4d73-abbf-d2d64f247435" alt="MaryS Engine Architecture Diagram">
</p>

---

## Project structure

- **`Content/Desktop`** – Files and shortcuts that appear on the engine desktop. Positions are stored in the Desktop module’s `desktop_positions.json`. The in-engine console is restricted to this directory.
- **`Content/Modules`** – One folder per module. Each module has:
  - **`bridge.json`** – Name, version, description, shortcut, visibility, dependencies. If `is_visible` is true (or the module is essential), the engine loads it and exposes it (e.g. in the task bar / top bar).
  - **`*.cs`** – C# type implementing `IModule` in namespace `MarySGameEngine.Modules.<FolderName>`. The engine resolves the type by convention from `bridge.json` and instantiates it with `GraphicsDevice`, `SpriteFont`, and window width (and optionally a second font for some modules).
- **`Content/Fonts`** – SpriteFonts used by the editor (e.g. pixel_font, Roboto, Smooch Sans).
- **`Projects`** – Created at runtime. Contains:
  - **`projects.json`** – List of game projects (name, path, genre, dates, etc.).
  - **`active_workspace.json`** – Path of the currently active project.
  - One folder per project (e.g. `Projects/MyGame/`) – game content and data for that project.

The engine entry point is `Program.cs` → `GameEngine` in `Main.cs`. Module loading runs in `LoadContent` after fonts and viewport are set up.

<p align="center">
  <img src="https://github.com/user-attachments/assets/3974eba4-8937-4718-8155-d73e015bdce0" alt="MaryS Engine Architecture Diagram" width="600">
</p>

---

## How to run

- **Requirements:** .NET 8, Windows (project uses `net8.0-windows` and `UseWindowsForms`).
- **Build:** Open the solution in Visual Studio or Rider, or run `dotnet build` in the repo root.
- **Run:** Run the project (e.g. F5 in Visual Studio) or `dotnet run` from the repo root. The window starts sized to the system work area (excluding taskbar).
- **First use:** Open **Game Manager** from the task bar or shortcut, create a project (name + genre), then activate it as workspace. Use **Character Creation** to add characters and related entities for that project.

---

## Module interface

Modules implement `IModule` and live in `Content/Modules/<ModuleFolder>/`:

```csharp
public interface IModule : IDisposable
{
    void Update();
    void Draw(SpriteBatch spriteBatch);
    void UpdateWindowWidth(int width);
    void LoadContent(ContentManager content);
}
```

Constructors receive `GraphicsDevice`, `SpriteFont`, and `int windowWidth` (and for some modules an extra font). The engine discovers the type as `MarySGameEngine.Modules.<ModuleFolder>.<ClassName>`, where the class name is taken from `bridge.json` (e.g. `"Name": "Game Manager"` → `GameManager`). Optional `SetTaskBar(TaskBar)` is invoked after load so modules can register with the task bar. Essential modules (Desktop, ModuleSettings, Console, GameManager, FlashMessage, NotificationCenter, PopUp) are always loaded even if not marked visible.

---

## Roadmap (plans, not commitments)

The following are target directions, not a fixed task list. Implementation order and scope may change.

- **Scene and level editing** – Scene editor with grid/tile placement, tilemaps (including autotiling), object layering, and linkage to a hierarchy and inspector.
- **Turn-based and game flow** – Predefined scene types (Menu, Base, Dialogue, Map, Battle), game state manager for scene/state transitions, turn manager (e.g. round-robin or initiative), grid utilities (movement, pathfinding, range).
- **Logic without code** – Event/trigger system and visual scripting (e.g. conditions and actions), so that common behaviors (e.g. “on button click → change scene”, “when turn ends → switch player”) can be configured in the editor.
- **Content and story** – Dialogue/cutscene editor, campaign/level flow, quest-style objectives, UI layout editor for in-game HUD and menus.
- **AI assistant (MarySue)** – Deeper LLM integration: context-aware help, code generation and edits, content generation (dialogue, descriptions), and optional function-calling so the AI can perform editor actions (e.g. “add NPC at (5,5)”) with user oversight.
- **Project wizard and UX** – Mascot-guided new-project flow, optional re-run for adding features (e.g. multiplayer), and templates (e.g. tactical RPG, JRPG) that scaffold project structure and default content.
- **Quality of life** – Global undo/redo, in-editor play/preview of the game, optional multiplayer hooks (e.g. hotseat, P2P, or client–server stubs), and documentation integrated into the editor and AI context.

The goal is to keep the engine modular so that new modules and community contributions can extend it without tying game content to a single monolithic toolset.

---

## References

- MonoGame community discussions on a plugin-based editor (e.g. minimal core, features as plugins).
- Turn-based design: state machines for game phases and triggers.
- [GDevelop](https://gdevelop.io) – no-code event system and WYSIWYG as inspiration.
- Experiments using LLMs (e.g. GPT-4) for story, dialogue, and in-game content placement (e.g. function calling) as inspiration for the AI assistant.

---

*MaryS Game Engine – a turn-based–focused 2D editor and engine on MonoGame, with a desktop metaphor and modular, extensible design.*
