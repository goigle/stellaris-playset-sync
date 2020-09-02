# Stellaris Playset Sync

This tool is designed to make it easier to share playsets with friends before a multiplayer game, especially playsets where load order is critical. The goal was to be as simple as possible to use. The project uses .NET Core 3.1, and theoretically could be expanded to be used on multiple platforms once .NET 5 comes out with the unified, multiplatform UI.

## Download
 * Go to the [releases section](https://github.com/goigle/stellaris-playset-sync/releases/)

## Supported Platforms
 * Windows (only tested on Windows 10)
 * Steam (Paradox and local mods are not currently supported)

 This was a quick-and-dirty project to make my weekend MP games with my friends go smoother - unfortunately, I'm most comfortable with C# and thus for now this tool is Windows-only as the Mac/Linux UI for .NET Core is still rather limited. I don't have a Mac to test or compile the software on anyway. If you're handy with code you could make your own tool that uses the same JSON format.

## Features
 * Exporting an existing playset (to share)
 * Importing a playset shared by this tool
 * Automatic backups of the launcher DB before changes

## Usage
 * Click Step 1 (it should auto-detect the launcher file location)
 * For exporting a playset, click the playset and then click export. It will save the file where you specify.
 * For importing a playset, either click import or drag the .json file for the mod onto the import button. **This tool will only work with .json files exported from this tool.**
 * If importing, you will need to click step 3 to save the changes.

## Building
 * Download the repository
 * In a terminal window, go to the repository on your disk
 * Run `dotnet restore`
 * Open the project in Visual Studio 2019
 * Build it
