# BrowserFork

[![CI](https://github.com/AlxndrJhn/BrowserFork/actions/workflows/ci.yml/badge.svg)](https://github.com/AlxndrJhn/BrowserFork/actions/workflows/ci.yml)

A tiny browser picker for Windows 11. Set it as your default browser, and every link you open
pops up a small topmost window with your installed browsers, plus **Copy link**.

![BrowserFork picker](docs/screenshot.png)

- Lists every browser registered with Windows (Edge, Chrome, Firefox, Brave, …) with its own icon.
- Preselects the browser you're most likely to want, and says why: *Last used from Outlook*,
  *Last used for github.com*, *Used last time*.
- Forwards the original arguments to the chosen browser exactly as received.
- Keyboard first: the preselected tile has focus, so Enter just goes.

## Install

Requires Windows 10/11 and the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0).

**From a release:** download `BrowserFork.exe` from
[Releases](https://github.com/AlxndrJhn/BrowserFork/releases), put it somewhere permanent, then run:

```powershell
.\BrowserFork.exe --register-machine   # as administrator, once
.\BrowserFork.exe --register
```

**From source** (needs the .NET 10 SDK):

```powershell
.\install.ps1
```

This builds to `%LOCALAPPDATA%\Programs\BrowserFork`, asks for admin once, and registers the app.

Then choose **BrowserFork** in *Settings › Apps › Default apps*. The installer offers to open
that page. Windows deliberately doesn't let apps make themselves the default browser.

> **Why admin?** Windows accepts a per-user (HKCU) browser registration, but *Settings › Default
> apps* only lists the app once it's also registered machine-wide (HKLM), like regular browser
> installs.

To uninstall, run `BrowserFork.exe --unregister` as administrator and delete the folder.

## Use

| Key | Action |
| --- | --- |
| Enter / Space / click | open in the focused browser |
| Shift + Enter / click | open in a private window |
| 1 – 9 | pick a tile directly |
| ← → | move focus |
| Ctrl + C | copy the link instead of opening it |
| Esc | cancel |

## How the preselection works

BrowserFork works out which app opened the link from its parent process. If the launch came
through a broker such as explorer or svchost, it uses the window that was in the foreground instead.
The focused tile is the last choice made:

1. **from the same app**, otherwise
2. **for the same host** (`www.` ignored), otherwise
3. **overall**.

History is stored in `%APPDATA%\BrowserFork\history.json`.

## Limitations

- Windows sends some links straight to Edge, such as Start menu search results and Widgets
  (`microsoft-edge:` links). Those never reach the default browser, so BrowserFork can't catch them.
- It shows one tile per browser, not one per browser profile.

## Development

```powershell
dotnet build BrowserFork.slnx
dotnet test BrowserFork.slnx
```

CI builds and tests every push and pull request. Pushing a `v*` tag publishes a release with
`BrowserFork.exe` attached.

## License

[MIT](LICENSE)
