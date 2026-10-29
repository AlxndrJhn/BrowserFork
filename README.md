# BrowserFork

A tiny browser picker for Windows 11. Set it as the default browser and every link you open
pops up a topmost tool window with your installed browsers (Edge, Chrome, Firefox, … — read from
the registry) plus **Copy link**.

## Install

```powershell
.\install.ps1
```

The installer asks for admin once: Settings › Default apps only lists BrowserFork when it is also
registered machine-wide (`--register-machine`, HKLM), like regular browser installs. A per-user
(HKCU) registration alone is accepted by Windows but never shown in that list.

Then choose **BrowserFork** for HTTP and HTTPS in *Settings › Apps › Default apps* (the installer
offers to open that page). Windows deliberately doesn't let apps set this themselves.
Remove with `BrowserFork.exe --unregister`, run as admin to also drop the machine-wide entries.

## Use

| Key | Action |
| --- | --- |
| Enter / Space / click | open in the focused browser — all arguments are forwarded verbatim |
| Shift + Enter / click | open in a private window |
| 1 – 9 | pick a tile directly |
| ← → | move focus |
| Ctrl + C | copy the link instead of opening it |
| Esc | cancel |

## Preselection

The calling app is detected from the parent process, falling back to the foreground window when the
launch came through a broker (explorer, svchost, …). The focused tile is the last choice made
**from the same app**, else **for the same host**, else the **last choice overall**; tiles show
why ("Last used from Outlook", "Last used for github.com"). History lives in
`%APPDATA%\BrowserFork\history.json`.
