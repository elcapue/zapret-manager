# Third-party notices

Zapret Manager is an independent, unofficial project. It is not affiliated with or endorsed by Flowseal, the zapret authors, the WinDivert authors, or Microsoft.

The Zapret Manager source code and branding in this repository are licensed separately under the repository's [MIT license](LICENSE). The manager does not contain the Flowseal logo.

## What the release build contains

The self-contained release executable contains:

- Zapret Manager code and original branding from this repository;
- Microsoft .NET runtime components required to run the application.

The build is a single `.exe` without accompanying files. License texts for the bundled components are listed in this file and linked below.

The executable does **not** contain the Flowseal ZIP, zapret binaries or scripts, strategy files, or WinDivert binaries. Runtime is downloaded separately and directly from the official Flowseal GitHub Release during installation.

## Microsoft .NET

- Project: [.NET Runtime](https://github.com/dotnet/runtime)
- License: [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT)
- Copyright: .NET Foundation and contributors
- Full bundled notices: [.NET third-party notices](https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT)

## Flowseal zapret-discord-youtube

- Project: [Flowseal/zapret-discord-youtube](https://github.com/Flowseal/zapret-discord-youtube)
- License: [MIT with upstream notices](https://github.com/Flowseal/zapret-discord-youtube/blob/main/LICENSE.txt)
- Copyright notices published by the project: bol-van, 2016–2026; Flowseal, 2024–2026

Zapret Manager calls the GitHub Releases API and downloads the selected release from the official Flowseal repository. Flowseal files remain governed by Flowseal's license and included upstream notices.

## zapret

- Project: [bol-van/zapret](https://github.com/bol-van/zapret)
- License: [MIT](https://github.com/bol-van/zapret/blob/master/docs/LICENSE.txt)
- Copyright notice published by the project: bol-van, 2016–2024

zapret is an upstream component of the separately downloaded Flowseal runtime. It is not compiled into or redistributed with the Zapret Manager executable.

## WinDivert

- Project: [basil00/WinDivert](https://github.com/basil00/WinDivert)
- License: the recipient may choose [LGPL version 3 or GPL version 2](https://github.com/basil00/WinDivert/blob/master/LICENSE)
- Author: basil

WinDivert is an upstream component of the separately downloaded Flowseal runtime. It is not compiled into or redistributed with the Zapret Manager executable.

If a future Zapret Manager distribution starts bundling WinDivert, the release policy is to use the LGPLv3 option. That distribution must include the complete GPLv3 and LGPLv3 license texts and satisfy the LGPLv3 notice, source-code, and relinking requirements that apply to the package. The GPLv2 option must not be used without a separate license review of the entire product. The current release process intentionally avoids bundling WinDivert.

## Redistribution policy

Do not attach the Flowseal ZIP, zapret files, strategy files, or WinDivert binaries to a Zapret Manager release. Publish only the manager executable. Users receive runtime from the official Flowseal release source.

If upstream files are bundled in the future, retain their original copyright notices and license texts and perform a new license review before publishing.
