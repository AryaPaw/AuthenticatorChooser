<img src="AuthenticatorChooser/YubiKey.ico" height="24" alt="YubiKey 5 NFC USB-A" /> AuthenticatorChooser
===

[![Build status](https://img.shields.io/github/actions/workflow/status/AryaPaw/AuthenticatorChooser/dotnet.yml?branch=master&logo=github)](https://github.com/AryaPaw/AuthenticatorChooser/actions/workflows/dotnet.yml)
[![Latest release](https://img.shields.io/github/v/release/AryaPaw/AuthenticatorChooser?include_prereleases&logo=github)](https://github.com/AryaPaw/AuthenticatorChooser/releases)
[![.NET 8](https://img.shields.io/badge/.NET-8-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Windows 11](https://img.shields.io/badge/Windows-11-0078D4?logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE.txt)
[![Upstream](https://img.shields.io/badge/fork_of-Aldaviva-555)](https://github.com/Aldaviva/AuthenticatorChooser)

This repository is an **independent continuation** of [Aldaviva/AuthenticatorChooser](https://github.com/Aldaviva/AuthenticatorChooser). The original project still works, but it has not been updated in a long time. This fork keeps the same idea — automatically choosing a USB security key in Windows FIDO/WebAuthn prompts — and adds a status window, tray icon, settings, and autostart.

Use **this** repository's [Releases](https://github.com/AryaPaw/AuthenticatorChooser/releases) (or [Actions](#download-from-github-actions) artifacts) for binaries. Do not download the old Aldaviva tags if you want the tray UI and PIN length autosubmit.

<!-- MarkdownTOC autolink="true" bracket="round" autoanchor="false" levels="1,2" -->

- [Problem](#problem)
- [Solution](#solution)
- [Requirements](#requirements)
- [Installation](#installation)
- [Downloading builds](#downloading-builds)
- [Status window and tray](#status-window-and-tray)
- [Demo](#demo)
- [How GitHub builds work](#how-github-builds-work)
- [Building](#building)
- [Testing](#testing)
- [Related](#related)

<!-- /MarkdownTOC -->

## Problem

Windows can display a Windows Security credential prompt when requested by a program, such as a browser with WebAuthn. This allows you to authenticate using a FIDO authenticator, such as a USB security key or a passkey in your computer's TPM protected by a Windows Hello PIN or biometrics, like a fingerprint.

In Windows 10 and 11 prior to 22H2 Moment 4 (September 2023), if the TPM contains the private key needed to authenticate to the relying party (like a website), Windows will prioritize prompting for the user's challenge (like a PIN or fingerprint) for this TPM authenticator first. Windows will still provide an option to choose a different authenticator (like a USB security key) with an additional click. Otherwise, if the TPM does not contain the required secret, Windows will immediately prompt you to insert a USB security key.

<p align="center"><img src=".github/images/usb-prompt.png" alt="usb security key prompt" width="456" /></p>

In Windows 11 [22H2 Moment 4](https://www.bleepingcomputer.com/news/microsoft/windows-11-moment-4-update-released-here-are-the-many-new-features/) (September 2023) and later (including [23H2](https://www.bleepingcomputer.com/news/microsoft/windows-11-23h2-new-features-in-the-windows-11-2023-update/)), this behavior changed to include the ability to pair with Android and iOS devices over Bluetooth to use their passkeys, which somewhat ameliorates the problem of passkeys not being portable outside their TPM. The behavior is unchanged if the Windows TPM contains the passkey. However, if the local TPM does not contain the passkey, an additional "Sign in with your passkey"/"Choose a passkey" step was added before you can use your USB security key.

Now it says "Choose a passkey," and you have to indicate whether you want to use an "iPhone, iPad, or Android device" or a "Security key." Choosing the USB security key requires one additional click or three additional keystrokes. It is impossible to opt out of this new prompt, even if you turn off Bluetooth, don't have an Android or iOS device, or never want to use it for FIDO authentication on your Windows computer. Windows does not remember the most recently used choice, either. You could disable your Bluetooth device in Device Manager, but this will also prevent you from using any other Bluetooth peripherals with your computer, such as Bluetooth mice, keyboards, headphones, and speakers.

<p align="center"><img src=".github/images/authenticator-prompt.png" alt="authenticator prompt" width="456" /></p>

## Solution

This is a background program that runs in your Windows user session. It waits for Windows FIDO credential provider prompts to appear, then chooses the Security Key option for you automatically. This fork shows a **status window** and a **notification-area icon** so you can see that it is running, pause it, and change options without Task Manager.

From the user's perspective, the Bluetooth screen barely even appears before it's replaced with the prompt to plug in your USB security key.

<p align="center"><img src=".github/images/demo.gif" alt="demo" width="465" /></p>

<p align="center"><img src=".github/images/status-window.png" alt="AuthenticatorChooser status window" width="640" /></p>

Internally, this program uses [Microsoft UI Automation](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-uiautomationoverview) to read and interact with the dialog boxes. Settings are stored in `%AppData%\AuthenticatorChooser\settings.json`.

## Requirements

- Windows 11 25H2, 24H2, 23H2, or [22H2 Moment 4](https://support.microsoft.com/en-us/topic/september-26-2023-kb5030310-os-build-22621-2361-preview-363ac1ae-6ea8-41b3-b3cc-22a2a5682faf)
- [.NET Desktop Runtime 8](https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime) or later, either x64 or arm64 (the published `exe` is **not** self-contained)
- When using Windows over Remote Desktop Connection, this program must run on the client, not the server, because FIDO prompts are forwarded and displayed by the client outside of the `mstsc` window

## Installation

1. Download `AuthenticatorChooser-win-x64.exe` or `AuthenticatorChooser-win-arm64.exe` from [Releases](https://github.com/AryaPaw/AuthenticatorChooser/releases) (see [Downloading builds](#downloading-builds) if a release does not exist yet).
1. Copy it to a directory of your choice, like `C:\Program Files\AuthenticatorChooser\`, and rename it to `AuthenticatorChooser.exe` if you want.
1. Run the program by double-clicking it. Windows will ask for administrator approval (needed so the program can click the elevated Windows Security prompt). It stays in the tray. Double-click the key icon to open the status window.
1. Optionally leave **Start when I sign in** enabled. That creates an elevated scheduled task so the program starts after logon without a second UAC prompt.

## Downloading builds

### GitHub Releases (stable downloads)

When a git tag matching `v*` is pushed (for example `v0.7.0`), GitHub Actions builds both runtimes and attaches:

- `AuthenticatorChooser-win-x64.exe`
- `AuthenticatorChooser-win-arm64.exe`

to a [GitHub Release](https://github.com/AryaPaw/AuthenticatorChooser/releases). Those files stay until someone deletes the release. This is the page you should link for "download the exe".

Until the first tag is published, that page will be empty. Use Actions artifacts below, or build locally.

### GitHub Actions artifacts (CI output)

Every push to `master`, every pull request, and every manual **Run workflow** run publishes zip artifacts named `AuthenticatorChooser-win-x64` and `AuthenticatorChooser-win-arm64`.

1. Open [Actions → .NET](https://github.com/AryaPaw/AuthenticatorChooser/actions/workflows/dotnet.yml).
1. Open a green run on `master`.
1. Download the artifact for your CPU at the bottom of the run page.
1. Unzip it. Inside is `AuthenticatorChooser.exe`.

**Retention:** artifacts expire after **90 days** (GitHub's default maximum for `actions/upload-artifact`, unless the repository owner shortens it in Settings → Actions). After that the zip disappears; you need a new successful run, a Release, or a local build.

Artifacts are also not a great public download page: GitHub often asks you to sign in, and you get a zip rather than a named installer.

## Status window and tray

The program starts in the **notification area** (tray). The status window is hidden until you double-click the key icon or choose Open.

- **Pause / Resume** — same effect as holding Shift, but until you turn it back on
- **Always choose the USB security key** — same as `--skip-all-non-security-key-options`
- **Start when I sign in** — on by default for new settings; creates or removes the elevated scheduled task
- **Autosubmit PIN** — type a sample PIN of the length you use, press **Turn on** (only the **length** is saved, never the digits). **Turn off** clears that. This is for the security-key PIN field, not Windows Hello.
- **Debug log** — write to a file under `%AppData%\AuthenticatorChooser` and open it
- Closing the window keeps the tray icon. **Exit** quits the process.
- Footer credits **Ben Hutchison** as the original author and **AryaPaw** as the fork maintainer, with links to both repositories.
- A second copy of the program asks the running instance to show the window instead of starting twice.

### Overriding the automatic next behavior

By default, this program does not interfere with local TPM passkey prompts (like requesting your Windows Hello PIN or biometrics). It also does not automatically submit FIDO prompts that contain additional options besides a USB security key and pairing a new Bluetooth smartphone, such as the cases when you already have a paired phone, or you previously declined a Windows Hello factor like a PIN but want to try a PIN again from the authenticator choice dialog. However, you may override this behavior if you wish and force it to **_always_** choose the USB security key in all cases, even if there are other valid options like Windows Hello PIN/biometrics, by passing the command-line argument `--skip-all-non-security-key-options` when starting this program, or by enabling the matching checkbox.

If a paired phone option appears in the dialog box and you want to remove it, [you can edit the registry to unpair an existing phone](https://github.com/Aldaviva/AuthenticatorChooser/wiki/Unpairing-Bluetooth-smartphone). This is useful if your old phone [bricked itself](https://en.wikipedia.org/wiki/Pixel_5a#Known_issues), or if you just upgraded to a new phone.

If this program skips the authenticator choice dialog when you don't want it to, for example, if you want to use a smartphone Bluetooth passkey only once or infrequently, you can hold <kbd>Shift</kbd> when the dialogs appear to temporarily suppress this program from automatically submitting the security key choice once.

Even if this program doesn't click the Next button (because an extra choice was present, or you were holding <kbd>Shift</kbd>), it will still highlight the Security Key option and focus the Next button for you, so you can just press <kbd>Enter</kbd> or <kbd>Space</kbd> to choose the Security Key anyway.

## Demo

To test with a sample FIDO authentication prompt, visit [WebAuthn.io](https://webauthn.io) and click the **Authenticate** button.

## How GitHub builds work

```text
push master / open PR / click "Run workflow"
        ↓
  restore + test (x64 job) + publish single-file exe (x64 and arm64)
        ↓
  upload-artifact  (zip, ~90 days)
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
When you try to create a passkey in your browser, the website may force it to be stored only in the TPM or only on a security key, rather than letting you freely choose between the two destinations. To override the site's mandate and put yourself back in control of where your new passkey will be saved, you can install my [**Create Passkeys Anywhere** user script](https://github.com/Aldaviva/userscripts/raw/master/create-passkeys-anywhere.user.js) (requires [Tampermonkey](https://tampermonkey.net/) or a similar browser extension). It doesn't only run on Windows, for example it also works on Firefox for Android.

With this script installed, you will by default always be asked whether to save each new passkey on a security key or in the TPM. If you want to override this behavior, you can also configure the user script by editing the `options.allowedPasskeyCreationStorage` value in the script source. If you change it from `anywhere` to `securityKey`, it will only allow you to save new passkeys on security keys, and if you change it to `tpm`, it will only allow them to be saved in the TPM.
