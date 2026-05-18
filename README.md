<p align="center">
  <img src="Documents/Tataru_img.png" width="112" alt="Tataru Helper app icon" />
</p>

<h1 align="center">Tataru Helper</h1>

<p align="center">Real-time translation overlay for Final Fantasy XIV in-game text.</p>
<p align="center">Maintained fork of the original project: <a href="https://github.com/NightlyRevenger/TataruHelper">NightlyRevenger/TataruHelper</a>.</p>

<p align="center">
  <a href="https://github.com/progneo/TataruHelper/actions/workflows/build.yml"><img src="https://github.com/progneo/TataruHelper/actions/workflows/build.yml/badge.svg" alt="Build" /></a>
  <a href="https://github.com/progneo/TataruHelper/releases/latest"><img src="https://img.shields.io/github/v/release/progneo/TataruHelper?label=Release" alt="Release" /></a>
  <a href="https://github.com/progneo/TataruHelper/releases"><img src="https://img.shields.io/github/downloads/progneo/TataruHelper/Setup.exe?label=Downloads&display_asset_name=false" alt="Setup.exe Downloads" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/progneo/TataruHelper" alt="License" /></a>
  <img src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white" alt=".NET 10" />
  <a href="https://crowdin.com/project/tataru-helper"><img src="https://badges.crowdin.net/tataru-helper/localized.svg" alt="Crowdin" /></a>
  <a href="https://discord.gg/bSrpbd9"><img src="https://img.shields.io/discord/592039000538349569?label=Discord&logo=discord" alt="Discord" /></a>
  <a href="https://github.com/progneo/TataruHelper/stargazers"><img src="https://img.shields.io/github/stars/progneo/TataruHelper?style=flat" alt="Stars" /></a>
  <a href="https://github.com/progneo/TataruHelper/commits"><img src="https://img.shields.io/github/commit-activity/m/progneo/TataruHelper?label=Activity" alt="Commit Activity" /></a>
</p>

<p align="center"><strong><a href="https://github.com/progneo/TataruHelper/releases/latest/download/Setup.exe">Download Setup.exe</a></strong> · <a href="Documents/Guide.MD">Guide</a> · <a href="https://discord.gg/bSrpbd9">Discord</a> · <a href="#support">Support</a></p>

<p align="center"><strong>Languages:</strong> <a href="README.md">EN</a> | <a href="Documents/README_ru_RU.md">RU</a> | <a href="Documents/README_ko_KR.md">KO</a> | <a href="Documents/README_es_ES.md">ES</a> | <a href="Documents/README_ca_ES.md">CA</a> | <a href="Documents/README_pl_PL.md">PL</a> | <a href="Documents/README_pt_BR.md">PT-BR</a> | <a href="Documents/README_uk_UA.md">UK</a> | <a href="Documents/README_zh_ZH.md">ZH</a> | <a href="Documents/README_ja_JP.md">JA</a></p>

## Features

- Translates in-game Japanese text (MSQ, cutscenes, quests, NPC lines, and chat).
- Supports selectable source and destination languages.
- Lets you switch translation engines and methods.
- Can target specific chat channels for translation.
- Includes automatic updates.

## Requirements

- Windows 7 x64 or newer (x64 only).
- [.NET 10 SDK/Runtime or newer](https://dotnet.microsoft.com/download).
- Final Fantasy XIV running with DirectX 11 (x64 client).

## Quick Install

1. Download the latest installer from [Releases](https://github.com/progneo/TataruHelper/releases/latest).
2. Run `Setup.exe`. If SmartScreen appears, select **More info** and then **Run anyway**.
3. Let Tataru Helper launch and complete the initial language/setup flow.
4. Close settings and place the floating overlay where you want it.
5. In FFXIV chat settings, enable the required message types shown below.

![In-game chat settings](Documents/InGameSettings.png)

## Usage

- Full usage walkthrough: [Guide](Documents/Guide.MD).
- After install, launch from the desktop/start-menu shortcut (not from `Setup.exe` again).

## Demo

- Video demo: [YouTube demonstration](https://youtu.be/7HiQXzmkQuw)
- Live preview gifs:

![Live Action](Documents/LiveAction.gif)
![Live Translation](Documents/2020-08-18_21-12-52.gif)

## Contributing / Translation

- Code contributions are welcome via pull requests.
- Help translate the app on [Crowdin](https://crowdin.com/project/tataru-helper).

## Credits

Thanks to all contributors and the projects that helped make Tataru Helper possible:

- [WPF Toolkit](https://github.com/xceedsoftware/wpftoolkit)
- [NHotKey.Wpf](https://github.com/thomaslevesque/NHotkey)
- [NotifyIcon WPF](https://bitbucket.org/hardcodet/notifyicon-wpf/)
- [Sharlayan](https://github.com/FFXIVAPP/sharlayan)
- [Tataru Art by Nezusagi](https://www.deviantart.com/nezusagi)
- [Velopack](https://github.com/velopack/velopack)

## Contacts

- Original community Discord: [discord.gg/bSrpbd9](https://discord.gg/bSrpbd9)
- Maintainer Discord: `progneo`
- Telegram: [t.me/progneo](https://t.me/progneo)
- Email: `prograneo@gmail.com`

## Support

- Boosty: [boosty.to/progneo](https://boosty.to/progneo)

## License

[MIT](LICENSE)
