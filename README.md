# OpenPhotoSort

A cross-platform desktop app for reading and organizing photos based on their EXIF metadata. Built with .NET 10 and .NET MAUI, targeting Windows and macOS.

## What it does

Select a folder and OpenPhotoSort scans it for image files (JPG, JPEG, PNG, BMP, GIF, HEIC), reads each file's embedded EXIF data using Magick.NET, and displays the metadata — camera make/model, date taken, GPS coordinates, and more. The intent is to eventually sort and copy photos into organized folder structures based on that metadata.

## Requirements

- .NET 10 SDK
- `maui-windows` workload (`dotnet workload install maui-windows`) on Windows
- `maui-maccatalyst` workload on macOS

## Build

```bash
# Windows
dotnet build OpenPhotoSort.UI/OpenPhotoSort.UI.csproj -f net10.0-windows10.0.19041.0

# macOS
dotnet build OpenPhotoSort.UI/OpenPhotoSort.UI.csproj -f net10.0-maccatalyst
```

## Project structure

```
OpenPhotoSort.Core/   — Class library: EXIF extraction via Magick.NET
OpenPhotoSort.UI/     — MAUI app: folder picker UI, Windows + macOS targets
```

## Status

Photo scanning and sorting are implemented. Core features:
- Scans a folder (optionally recursive) for JPG/JPEG/PNG/BMP/GIF/HEIC files
- Reads EXIF to detect date and camera model
- Copies or moves files into configurable date-based folder structures (10 patterns)
- Handles file conflicts: skip / rename / overwrite / route to duplicates folder
- Routes no-EXIF files to a dump folder or uses file date as fallback
- Two-phase UX: scan first to see stats, then copy or move with live progress
- Cancel button stops an in-progress operation
- All settings persist across sessions
