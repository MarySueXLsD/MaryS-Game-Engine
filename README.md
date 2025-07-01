# MaryS Game Engine

A C# game engine built with MonoGame framework, providing a modular architecture for game development.

## Features

- Modular game engine architecture
- Content management system
- Font and asset management
- Cross-platform support (Windows, macOS, Linux)
- Built with MonoGame framework

## Project Structure

```
MarySGameEngine/
├── Main.cs                 # Main game engine class
├── Program.cs              # Application entry point
├── IModule.cs              # Module interface
├── MarySGameEngine.csproj  # Project file
├── Content/                # Game content and assets
│   ├── Desktop/           # Desktop-specific content
│   ├── Fonts/             # Font assets
│   ├── Logos/             # Logo assets
│   ├── Modules/           # Game modules
│   └── Content.mgcb       # MonoGame Content Builder file
├── .vscode/               # VS Code configuration
└── app.manifest           # Application manifest
```

## Requirements

- .NET 6.0 or later
- MonoGame framework
- Visual Studio 2019/2022 or VS Code

## Building the Project

1. Clone the repository
2. Open the solution in Visual Studio or VS Code
3. Restore NuGet packages
4. Build the project

## Usage

The MaryS Game Engine provides a modular architecture where game functionality is organized into modules. Each module implements the `IModule` interface and can be loaded dynamically.

## License

This project is open source and available under the MIT License.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request. 