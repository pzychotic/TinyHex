# TinyHex

[![GitHub License](https://badgen.net/github/license/pzychotic/TinyHex)](https://github.com/pzychotic/TinyHex/blob/main/LICENSE)
[![CI](https://github.com/pzychotic/TinyHex/actions/workflows/ci.yml/badge.svg)](https://github.com/pzychotic/TinyHex/actions/workflows/ci.yml)

A very fast hex viewer for big files.

It uses ```MemoryMappedFile```s together with a virtualizing ```ListView``` (```VirtualizingStackPanel.IsVirtualizing="True"```) for blazing fast file access and display.

## Features

- Read-only hex and ASCII view of files of any size, mapped into memory in chunks
- Open files via the toolbar button, `Ctrl+O` or drag-and-drop
- Adjustable bytes per line: 8, 16, 32 or 64
- Go to a file offset, entered as decimal or hex (`0x` prefix)
- Remembers window placement and bytes per line between runs

## Build
### From the command line

Prerequisites:
- .NET SDK 10.0 - install from https://dotnet.microsoft.com/

Build and run:
1. Build:
   ```
   dotnet build
   ```
2. Test (optional):
   ```
   dotnet test
   ```
3. Run:
   ```
   dotnet run --project Source\TinyHex\TinyHex.csproj
   ```

### From Visual Studio 2026

Prerequisites:
- .Net Desktop development workload
- .Net 10.0 Runtime
- .Net SDK

Just open ```TinyHex.slnx``` build and run.

## Dependencies

- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)
- [Microsoft.Xaml.Behaviors.Wpf](https://github.com/microsoft/XamlBehaviorsWpf)
