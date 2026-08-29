# <img src="AuthenticatorChooser/YubiKey.ico" height="28" alt="" /> AuthenticatorChooser

[![Build](https://img.shields.io/github/actions/workflow/status/AryaPaw/AuthenticatorChooser/dotnet.yml?branch=main&logo=github)](https://github.com/AryaPaw/AuthenticatorChooser/actions/workflows/dotnet.yml)
[![Release](https://img.shields.io/github/v/release/AryaPaw/AuthenticatorChooser?logo=github)](https://github.com/AryaPaw/AuthenticatorChooser/releases/latest)
[![.NET 8](https://img.shields.io/badge/.NET-8-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Windows 11](https://img.shields.io/badge/Windows-11-0078D4?logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE.txt)

Windows 11 asks “phone or security key?” every time you use a USB key. This program sits in the tray and clicks **Security key** for you. You can pause it, autostart it, and optionally autosubmit the key PIN by length (not Windows Hello).

<p align="center"><img src=".github/images/authenticator-prompt.png" alt="Windows asking to choose a phone or a security key" width="420" /></p>
<p align="center"><img src=".github/images/demo.gif" alt="The phone screen disappearing as Security key is chosen" width="420" /></p>
<p align="center"><img src=".github/images/status-window.png" alt="Status window" width="520" /></p>

## Install

Needs **Windows 11** (22H2 Moment 4 or newer) and the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime). On Remote Desktop, run it on the **client** PC, not the remote one.

1. Download **AuthenticatorChooser-Setup-win-x64.exe** from [Releases](https://github.com/AryaPaw/AuthenticatorChooser/releases/latest) (or `win-arm64` on ARM PCs).
2. Run the setup (UAC). It puts the program in Program Files and adds a Start Menu shortcut.
3. It stays in the tray — double-click the key icon for the window. Leave **Start when I sign in** on if you want it after logon.

Uninstall from **Settings → Apps**. That stops the program and removes Program Files, the shortcut, the logon task, and `%AppData%\AuthenticatorChooser` (settings and logs).

Try it on [webauthn.io](https://webauthn.io) → **Authenticate**.

## Updates

Installed from Setup, it checks GitHub for a newer installer when you **sign in to Windows**. If the PC is online and a new Setup exists, it installs in the background and restarts itself. The window and tray do not mention updates. If there is no internet yet, it waits until a connection appears, then updates.

## Using it

Closing the window does **not** quit; use **Exit**. A second launch opens the same window.

| | |
| --- | --- |
| **Pause** | Stops auto-clicks until you resume. Hold <kbd>Shift</kbd> to skip one click without pausing. |
| **Always choose the USB security key** | Also skip Windows Hello and an already-paired phone. Off = only skip “pair a new phone”. |
| **Autosubmit PIN** | Type a PIN of the length you use → **Turn on**. Only the length is stored. USB-key PIN only. |
| **Start when I sign in** | Starts with Windows. |

If it still highlights Security key but does not click Next, press <kbd>Enter</kbd>.

## Build from source

```ps1
git clone https://github.com/AryaPaw/AuthenticatorChooser.git
cd AuthenticatorChooser
dotnet publish AuthenticatorChooser -c Release --runtime win-x64 --no-self-contained -p:PublishSingleFile=true
```

Output: `AuthenticatorChooser\bin\Release\net8.0-windows\win-x64\publish\AuthenticatorChooser.exe`. Installer script: `installer\AuthenticatorChooser.iss`.

```ps1
dotnet test /p:CollectCoverage=true
```

Original program © Ben Hutchison. This repository is an independent fork.

## Related

### Creating new passkeys

When you try to create a passkey in your browser, the website may force it to be stored only in the TPM or only on a security key, rather than letting you freely choose between the two destinations. To override the site's mandate and put yourself back in control of where your new passkey will be saved, you can install [**Create Passkeys Anywhere**](https://github.com/Aldaviva/userscripts/raw/master/create-passkeys-anywhere/create-passkeys-anywhere.user.js) (requires [Tampermonkey](https://tampermonkey.net/) or a similar browser extension).
        ↓
  if you also pushed a tag v*  →  GitHub Release with two named exe files
```

The workflow file is [`.github/workflows/dotnet.yml`](.github/workflows/dotnet.yml). It does **not** publish a self-contained runtime: users still need the .NET 8 Desktop Runtime.

Creating a Release from the GitHub UI **without** a `v*` tag will not attach these binaries. Either push a tag (`git tag v0.7.0` then `git push origin v0.7.0`) or attach files by hand.

## Building

1. Install the [latest stable .NET SDK](https://dotnet.microsoft.com/en-us/download) (8 or later).
1. Clone **this** repository (not an old upstream tag if you want the GUI).
    ```ps1
    git clone "https://github.com/AryaPaw/AuthenticatorChooser.git"
    cd AuthenticatorChooser
    ```
1. Publish a single-file executable (example: x64).
    ```ps1
    dotnet publish AuthenticatorChooser -c Release --runtime win-x64 --no-self-contained -p:PublishSingleFile=true
    ```

The program will be compiled to:

```text
AuthenticatorChooser\bin\Release\net8.0-windows\win-x64\publish\AuthenticatorChooser.exe
```

You can also use an IDE like [Visual Studio](https://visualstudio.microsoft.com/vs/) Community 2022 or 2026.

## Testing

```ps1
dotnet test /p:CollectCoverage=true
```

Unit tests cover settings, skip policy, title/caption mapping, autostart helpers, and the status presenter. UI Automation against live Windows Security dialogs is not part of CI. Tests run under `testhost.exe` and do not show the "still running in the notification area" balloon.

Original program © Ben Hutchison. This repository is an independent fork.

## Related

### Creating new passkeys
When you try to create a passkey in your browser, the website may force it to be stored only in the TPM or only on a security key, rather than letting you freely choose between the two destinations. To override the site's mandate and put yourself back in control of where your new passkey will be saved, you can install my [**Create Passkeys Anywhere** user script](https://github.com/Aldaviva/userscripts/raw/master/create-passkeys-anywhere/create-passkeys-anywhere.user.js) (requires [Tampermonkey](https://tampermonkey.net/) or a similar browser extension). It doesn't only run on Windows, for example it also works on Firefox for Android.

With this script installed, you will by default always be asked whether to save each new passkey on a security key or in the TPM. If you want to override this behavior, you can also configure the user script by editing the `options.allowedPasskeyCreationStorage` value in the script source. If you change it from `anywhere` to `securityKey`, it will only allow you to save new passkeys on security keys, and if you change it to `tpm`, it will only allow them to be saved in the TPM.
