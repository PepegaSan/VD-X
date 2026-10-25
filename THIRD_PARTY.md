# Third-party components

## VirtualDesktopAccessor (DLL)

This repository **includes** `VirtualDesktopAccessor.dll` in the **project root**. The build copies it next to `VD-X.exe`, and the installer ships it together with the upstream **MIT** license text.

| Item | Location |
|------|-----------|
| Binary | `VirtualDesktopAccessor.dll` |
| Upstream MIT license (verbatim) | [`third-party/VirtualDesktopAccessor/LICENSE.txt`](third-party/VirtualDesktopAccessor/LICENSE.txt) |
| Short notes | [`third-party/VirtualDesktopAccessor/README.md`](third-party/VirtualDesktopAccessor/README.md) |

- **Project:** [Ciantic / VirtualDesktopAccessor](https://github.com/Ciantic/VirtualDesktopAccessor)
- **License:** **MIT** — copyright Jari Otto Oskari Pennanen (see `LICENSE.txt` above; text copied from the upstream `rust` branch).

### Updating the DLL

To refresh to the **latest upstream release**, run `download-vda.ps1` from the project root (overwrites the root `VirtualDesktopAccessor.dll`). After upgrading, consider recommitting the new binary if you want the backup copy in git to stay current.

### Stability note

The DLL uses **undocumented** Windows virtual-desktop APIs. Major Windows updates can require a newer DLL build from the upstream project.
