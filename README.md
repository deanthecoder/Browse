[![Twitter URL](https://img.shields.io/twitter/url/https/twitter.com/deanthecoder.svg?style=social&label=Follow%20%40deanthecoder)](https://twitter.com/deanthecoder)
[![GitHub Repo stars](https://img.shields.io/github/stars/deanthecoder/Browse?style=social&label=Star)](https://github.com/deanthecoder/Browse/stargazers)

# Browse

Browse is a fast, dark, column-based file browser for Windows and macOS. It takes the navigation model of macOS Finder and combines it with familiar Windows behaviors such as F2 rename, Explorer drag-and-drop, UNC paths, and Windows context-menu integration.

## Current MVP

- Favorites and drives in a fixed sidebar.
- Finder-style folder columns with horizontal scrolling and alphabetical file/folder sorting.
- Fixed Info panel with bounded image previews (including TIFF), text and ZIP-content previews, plus a larger preview window.
- Markdown rendering in the larger preview window and syntax highlighting for common source-file formats.
- `Ctrl+G` path navigation (`Command+G` on macOS), including UNC locations.
- Separate settings for hidden/system items and dot-prefixed folders.
- Multi-selection, copy, cut, paste, F2 rename, and command-line-friendly path copying.
- Drag files and folders within Browse and to or from Explorer/Finder.
- Type-specific vector icons for folders, archives, images, documents, source files, media, and applications.
- Live folder watching so changes made outside Browse appear without a manual refresh.
- Open files with the system viewer and open a terminal from the context menu.
- On-demand folder-size calculation with reparse-point protection.
- ZIP creation with automatic numbered names when an archive already exists.
- Multiple windows, a task-tray menu, and restoration of the last closed location.
- Windows global launcher registered as `Ctrl+Alt+B`.
- Inno Setup packaging with optional Windows startup and Explorer “Browse...” integration.
- GitHub Actions packaging and GitHub Release creation via DTC.Installer.

Directory listings are cached for five seconds to make backtracking immediate. Active columns also have file-system watchers which invalidate that cache as soon as a supported local or network file system reports a change.

## Development

```text
git clone --recurse-submodules https://github.com/deanthecoder/Browse.git
dotnet build Browse.slnx
dotnet test Browse.slnx
dotnet run --project Browse/Browse.csproj
```

## Packaging

Run `python Installer/pack.py` to create local installers. The Installers workflow can build Windows and macOS artifacts and attach them to a GitHub Release.

The Windows installer can optionally launch Browse at login. It also offers Explorer context-menu commands for folders, folder backgrounds, and drive roots. On Windows 11 these registry-based commands appear under **Show more options**.

## Roadmap

- Add cancellable operation progress and undo for supported file operations.
- Once Browse is in regular use, update G33kSeek so file and folder results can be revealed in Browse when it is installed.

## License

Licensed under the MIT License. See [LICENSE](LICENSE) for details.
