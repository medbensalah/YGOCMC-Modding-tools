# CMC Studio

Native Windows editor for Yu-Gi-Oh! Capsule Monster Coliseum.

All application code, engine code, catalog data, pattern images and bundled texture enhancement tools live in this folder.

## Build

Requires Windows and the .NET 10 SDK. SkiaSharp is restored from NuGet.

```powershell
dotnet restore CMCStudio.csproj --configfile NuGet.Config
dotnet publish CMCStudio.csproj -c Release --no-restore -o dist
```

Run `Launch CMC Studio.cmd`. Keep the entire `dist` folder together.

## Source

- Root: WPF UI and project/workspace handling.
- Engine: binary formats, ISO editing, data repositories and data models. Namespaces: `CMCStudio.Engine`, `CMCStudio.Data`, `CMCStudio.Models`.
- Patterns: movement and attack preview images.
- tools: bundled Real-ESRGAN engine, neural model and licenses.

Open a game ISO, edit, save a project and export a new ISO.

The editor uses compact inline property rows, 26-pixel table rows, tighter tabs/toolbars and smaller panel margins. Pattern previews and validation remain available alongside the fields.

Monster editing uses three fitted columns: stats/SPA, portrait, and model. All edit and import/export controls remain on the Stats tab with no editor scrolling. Panels scale down at smaller window sizes.

Monsters → Replace all models… accepts one OBJ + MTL and applies it to every existing Map Unit variant for monster IDs 0–276, including enemy and damage-state variants. The operation is all-or-nothing: every selected resource must import successfully before replacements are staged. Failures are listed and leave existing edits unchanged; cancellation stages none. Existing material importer restrictions apply, including an embedded indexed/RGB/RGBA texture and one opaque input material. Export ISO to write staged results.

Bulk material import now supports multiple native textures by using the first native allocation for the replacement diffuse material and redirecting existing stage-zero texture bindings. It preserves the native scene/shader graph and retains unused image allocations. Runtime testing is still required for imported assets.

The material importer preserves native indexed, RGB24 and RGBA32 storage. Bulk OBJ + MTL replacement is restricted to Map Units and never commits a partial result. Battle Units are excluded.

Campaign exports synchronize the selected enemy's character artwork and name labels, the stage-details character lookup, and each slot's map thumbnail. Stock maps use their original artwork; maps without stock thumbnails receive a textured geometry render at the native 200×152 size. D_SYS is rebuilt at a new ISO extent, preserving original banks as donors for later reorder/revert operations. Unlock/progression flags remain attached to their slots. Binary export/reopen checks pass; in-game verification is still required.

Campaign → Enemy portraits edits campaign artwork, loading screens and D_CHAR dialogue poses/expression sheets. Each view supports import, transparent PNG export and revert. Imports retain native dimensions, anchors, frame counts and indexed storage; expression frames are edited together as a horizontal strip to preserve their shared palette. Shared rematch images list their affected encounters. Custom artwork is saved in projects and embedded in exported ISOs, including later reordering of an exported ISO. Dialogue text/audio and trigger editing are not included in this portrait build.

Image import sizing: monster images exceeding either 512-pixel dimension are reduced to fit a 512×512 transparent canvas before staging. Character images downsize proportionally to fit their native canvas without stretching or enlarging smaller inputs; spare space is transparent.

### Dialogue, audio and event routing
Campaign now includes Dialogue text, Voice & sound, and Triggers (advanced).
- Dialogue text covers the 25 encounter banks, preserving all 1,025 IDs per bank and control/glyph bytes. Edit a line then Apply text. `{XX}` denotes a preserved byte; special spaces remain intact. Layout/line length still needs in-game review.
- Voice & sound previews and exports native D_SE samples as WAV, and imports PCM 16-bit mono/stereo WAV. Inputs are mixed to mono, resampled to the sample's native rate and encoded to PS ADPCM. Short inputs get silence padding; inputs exceeding the original duration are rejected. Bank metadata, playback flags and allocation remain unchanged. Samples are currently identified by bank/sample ID; character and dialogue associations are not yet recovered. D_AUDIO music replacement is not included.
- Triggers (advanced) reroutes an existing entry to another existing handler in the same encounter, including return/no-action handlers. Initial/rematch setup is protected. Other event names and conditions are unresolved. This is experimental routing, not a custom-condition or script-authoring tool; incompatible handler state can break progression.
All edits persist in projects and export inside the ISO. Source ISO is never overwritten.
Audio header interpretation was checked against vgmstream's primary format documentation/source: https://github.com/vgmstream/vgmstream/blob/master/src/meta/hd_bd.c . The app uses its own parser and codec; it has no runtime dependency on vgmstream.

### Categorized spoken dialogue
Voice & sound now opens Dialogue recordings, grouped by encounter with searchable line text. Sound effects remain in their own subtab. VOICE.BIN is separate from D_SE; its 3,328 mono recordings are now previewable/exportable/importable as WAV. Verified script-linked lines are grouped under all 25 encounters; remaining clips are explicitly shown under Other recordings (dialogue link unknown). Shared uses are listed before replacing a recording. Edits persist in projects and export inside VOICE.BIN without changing clip lengths or playback flags.

Recovery: EE 2b1fe0 validates voice groups; 2b2130 reads the 26-group boundary header at 40c810 and eight-byte clip records at 40c850 (24-bit byte length and sector offset). Script 20/0A and 20/0B pack IDs as group<<7 | sample; 2f7654/2f7774 expand these for the loader. Dialogue links accept consecutive known animation/text commands ending at 31/01, resource slot 10 with message index packed above its low six bits. A later voice cue ends an earlier candidate link. Unsupported sequences remain unlinked. Native voice pitch is 0xC99 (EE 302314, IOP SNDCTLTX 23D0), represented as nominal 37,800 Hz WAV. No script or trigger edits are made by categorization.

### Fast project saves
New saves use CMC Studio 2: a small `.cmcproject` manifest containing only changed values and paths to imported assets. Game-ready images/models/audio are written once to the adjacent `<project>.cmcproject.assets` folder and reused on subsequent saves. No Base64 assets or copies of untouched monsters, armies, shops, starter kits or transformation tables are embedded. Reverting an edit removes it from the next manifest. Save As creates references and an asset folder beside the new project.

Keep the project and its `.assets` folder together when moving/sharing them. These references preserve the prepared game resources; source OBJ/MTL conversion and image encoding are not repeated during save/load. Existing version 1 projects still open; saving them migrates to the referenced format. Missing/corrupt asset files report their path. ISO export retains full validation. Project saves and dirty checking no longer rebuild assets or prepare ISO patches; loading reads only the project's edited assets and applies changed rows on top of the source ISO.


The monster roster has one-click Enabled checkboxes, red names for disabled monsters, an enabled/disabled count, and All/Enabled/Disabled filters. Project save/load accepts any enabled count; the exactly-200 requirement is enforced only for ISO export.

SPA name/description search is available both in the monster roster (filters by assigned ability) and inside the SPA picker. Search is case-insensitive and matches all entered words across the name and description. Filtering the picker does not change the assigned SPA.


ISO export now prepares its patch plan once, writes fixed-size models as blocks, batches nearby patches, and merges archive edits so each archive is handled once. Archives stay in place when all replacements fit their existing slots; larger replacements relocate the archive once. Source hashing happens during the ISO copy instead of rereading the copy. The busy overlay reports the current export stage and source-copy percentage. ISO export still requires exactly 200 enabled monsters and a new output filename. Tests and consistency-check runs were skipped for this build as requested.
