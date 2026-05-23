# FolderComparer

FolderComparer is a Windows desktop app (WPF, .NET 10) for comparing two folders and showing file-level differences in a fast, filterable grid.

## Features

- Compare source and destination folders recursively.
- Show comparison status for each relative file path:
  - Match
  - Source only
  - Destination only
  - Size differs
  - Date differs
  - Size and date differ
- Display source/destination file sizes and last modified dates.
- Live filtering by text and by status category.
- Progress status updates and cancel support during comparison.
- Excludes hidden and read-only files and folders from comparison.

## Tech Stack

- .NET 10 (`net10.0-windows`)
- WPF
- MVVM-style ViewModels and commands

## Project Structure

- `FolderComparer.slnx`: solution file
- `FolderComparer/`: WPF project
- `FolderComparer/Services/FolderComparisonService.cs`: comparison engine
- `FolderComparer/ViewModels/MainViewModel.cs`: UI state, commands, filtering
- `FolderComparer/Models/FileComparisonResult.cs`: result/status models

## Requirements

- Windows
- .NET SDK 10.0+

## Build

From the repository root:

```powershell
dotnet build .\FolderComparer.slnx
```

## Run

From the repository root:

```powershell
dotnet run --project .\FolderComparer\FolderComparer.csproj
```

## How To Use

1. Launch the app.
2. Select a Source Folder.
3. Select a Destination Folder.
4. Click Compare.
5. Use status checkboxes and text filter to narrow results.

## Notes

- Date differences are evaluated with a small tolerance to account for filesystem timestamp precision differences.
- Comparison keys are case-insensitive by relative path.
