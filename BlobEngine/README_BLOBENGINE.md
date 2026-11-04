# BlobEngine 7.5

This repository stores BlobEngine customization files and a Windows x64 GitHub Actions build workflow. The workflow obtains the upstream Cheat Engine 7.5 source at build time, applies the BlobEngine name and Lua tools, then compiles the Lazarus project.

The named search searches existing Address List records, including records loaded from compatible .CT tables. It does not identify arbitrary unknown memory addresses. The game selector is contextual only until a verified game-specific profile is supplied.

## Build
Open Actions → Build BlobEngine for Windows → Run workflow. On success, download the `BlobEngine-Windows-x64` artifact.

Upstream build requirements are Lazarus 2.2.2 / Free Pascal 3.2.2. Some optional CE helper projects are not built by this workflow.

## Use the dashboard
Launch BlobEngine, attach to a permitted offline process, load a compatible .CT table, open the Lua Engine and execute `BlobEngine/BlobEngineDashboard.lua`.

Cheat Engine is GPL-licensed. Preserve upstream notices and comply with the applicable GPL and third-party licenses when redistributing.
