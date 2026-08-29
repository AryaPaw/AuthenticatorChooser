<img src="AuthenticatorChooser/YubiKey.ico" height="24" alt="" /> AuthenticatorChooser

[![Build](https://img.shields.io/github/actions/workflow/status/AryaPaw/AuthenticatorChooser/dotnet.yml?branch=master&logo=github)](https://github.com/AryaPaw/AuthenticatorChooser/actions/workflows/dotnet.yml)
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

1. Get the exe from [Releases](https://github.com/AryaPaw/AuthenticatorChooser/releases/latest): `win-x64` on a normal PC, `win-arm64` on ARM (some Surfaces).
2. Put it somewhere it can stay, e.g. `C:\Program Files\AuthenticatorChooser\`.
3. Run it (UAC is required so it can click the Windows Security dialog). It hides in the tray — double-click the key icon for the window.
4. Leave **Start when I sign in** on if you want it after logon.

Try it on [webauthn.io](https://webauthn.io) → **Authenticate**.

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

Output: `AuthenticatorChooser\bin\Release\net8.0-windows\win-x64\publish\AuthenticatorChooser.exe`.

Original program © Ben Hutchison. This continuation © AryaPaw.
