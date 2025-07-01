# ![MaryS Game Engine Logo](docs/images/marys_logo_placeholder.png)

# MaryS Game Engine

Welcome to **MaryS Game Engine**!

This is the source for MaryS Game Engine, a modular C# game engine built on top of MonoGame framework, designed for rapid prototyping and robust 2D game development with a focus on modular architecture.

---

## 🚧👷 Warning
**Proceed at your own risk.** This engine is under active development, which means things will change frequently. We do our best to keep release branches stable, but expect breaking changes and features that are not perfect (yet!).

---

## MaryS Game Engine at a Glance

- **Modular Architecture**: Built with a flexible module system for easy extensibility.
- **MonoGame Foundation**: Leverages the power and reliability of MonoGame framework.
- **Cross-platform**: Develop and run on Windows, Linux, and MacOS.
- **Content Management**: Integrated content pipeline with MonoGame Content Builder.
- **Font and Asset Management**: Comprehensive asset handling system.
- **No external dependencies**: You have full control over your project structure.

---

## 📦 Project Structure

```
MarySGameEngine/
├── docs/
│   └── images/
│       └── marys_logo_placeholder.png
├── Content/                # Game content and assets
│   ├── Desktop/           # Desktop-specific content
│   ├── Fonts/             # Font assets
│   ├── Logos/             # Logo assets
│   ├── Modules/           # Game modules
│   └── Content.mgcb       # MonoGame Content Builder file
├── Main.cs                # Main game engine class
├── Program.cs             # Application entry point
├── IModule.cs             # Module interface
├── MarySGameEngine.csproj # Project file
├── .vscode/               # VS Code configuration
├── app.manifest           # Application manifest
└── README.md
```

---

## ![Screenshot Placeholder](docs/images/editor_screenshot_placeholder.png)

---

## How to Build MaryS Game Engine

1. **Clone the repository**:
   ```sh
   git clone https://github.com/MarySueXLsD/MaryS-Game-Engine.git
   ```
2. **Install .NET 6.0 SDK** (or later).
3. **Open the solution** in Visual Studio, VS Code, or your favorite IDE.
4. **Restore NuGet packages** and build the project.

The MaryS Game Engine provides a modular architecture where game functionality is organized into modules. Each module implements the `IModule` interface and can be loaded dynamically.

---

## 🏗️ Architecture Overview

MaryS Game Engine is organized for maximum flexibility and modularity:

- **Main Engine**: Core engine functionality in `Main.cs`.
- **Module System**: Extensible architecture through `IModule` interface.
- **Content Pipeline**: Integrated MonoGame Content Builder for asset management.
- **Cross-platform Support**: Built on MonoGame for wide platform compatibility.

### Module System
MaryS Game Engine uses a modular architecture where each feature is implemented as a separate module. This allows for:
- Easy feature addition and removal
- Clean separation of concerns
- Flexible game development workflow

---

## ![Architecture Diagram Placeholder](docs/images/architecture_diagram_placeholder.png)

---

## Requirements

- .NET 6.0 SDK or later
- MonoGame framework
- Visual Studio 2019/2022 or VS Code

---

## Supported Platforms

- **Windows**
- **Linux**
- **MacOS**

> **Note:** The engine is built on MonoGame, which provides excellent cross-platform support.

---

## Contributing

This is still super early, but feedback and contributions are welcome! Feel free to contact [MarySueXLsD](mailto:abbigeilwf@outlook.com) with suggestions or questions.

- Open issues for bugs or feature requests
- Submit pull requests for improvements
- Share your feedback and ideas!

---

## License

This project is open source and available under the MIT License. See [LICENSE](LICENSE) for details.

---

## MaryS Game Engine Logos

![MaryS logo](docs/images/marys_logo_placeholder.png) ![MaryS logo](docs/images/marys_logo_placeholder.png) ![MaryS logo](docs/images/marys_logo_placeholder.png)

---

> _Happy game development!_ 