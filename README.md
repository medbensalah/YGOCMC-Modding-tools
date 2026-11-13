# CMC Studio

Native Windows modding tool for Yu-Gi-Oh! Capsule Monster Coliseum.

## Build and run

Requires Windows and the .NET 10 SDK.

```powershell
dotnet restore CMCStudio/CMCStudio.csproj
dotnet publish CMCStudio/CMCStudio.csproj -c Release -o CMCStudio/dist
.\"Launch CMC Studio.cmd"
```

Application and engine source: `CMCStudio/`. Command-line tools: `CMCStudio.Cli/`.
See their READMEs for details. Game ISOs, personal projects and generated builds are not included.

This revision replaces the previous tool with the current standalone CMC Studio source.

## Tutorial

### 1. Install and open a game

Download the Windows x64 ZIP from Releases, extract the entire folder, and run `CMCStudio.exe`. Keep the companion folders beside it. The self-contained release includes .NET.

Click **Open ISO** and select your original game ISO. Keep that original as the base for your projects. Edits are staged until you export a new ISO.

### 2. Edit monsters

- **Monsters** opens an individual monster's stats, name, SPA, portrait and model controls.
- **Monsters Grid** edits many monsters in a table. Filter by name or column values; use the Enabled checkbox to change the roster.
- Import a portrait from the monster editor. Linked imports update supported portrait variants. Higher-resolution/full-colour portrait support is experimental and may cause freezes in some screens.
- Choose **Map Unit** or **Battle Unit** before importing a model. OBJ + MTL imports need their referenced image files beside the material files. Map-unit replacement can include player, enemy and damaged variants; capsules and Symbols are separate resources.
- Evolution and fusion rules can be edited in the monster view or **Evolution & fusion**.

### 3. Change opponents and maps

Use **Campaign** to change encounter order, assigned maps and enemy armies. Red and Blue are alternative enemy setups, not the player's army. Some encounters share level values between these setups.

Joey's experimental expansion supports four or eight enemy capsules. **Extra X/Y** applies only to added units 5–8; it is a tile coordinate, not a positional offset.

In **Map editor → Capsules & Symbols**, select the opponent/setup, choose a capsule, then click a tile to place it. Capsule positions are saved per opponent, so opponents sharing a map can have different armies and placements. Terrain, deployment zones and Symbols remain shared by map. Edit the original four Joey positions here.

Map effects and script transfers are reverse-engineered. Map 24's incompatible experimental cube turn event is disabled to avoid a confirmed infinite wait. Test map swaps from an in-game memory-card save after exporting.

### 4. Edit other game data

- **Shops & starters:** edit shop inventories, special shop requirements, rewards and starter kits.
- **Growth & combat:** edit leveling equations and attribute effects.
- **UI:** replace supported title/startup artwork. Images fit the original canvas and indexed format.
- **Assets:** preview/export supported textures and models; maps can hide the sky sphere in the preview.

### 5. Save and export

Click **Save project** to write a `.cmcproject`. Keep its companion `.assets` folder beside it. Reopen the same base ISO, then load the project to continue editing. Project saving permits an unfinished roster; ISO export currently requires exactly 200 enabled monsters.

Click **Export ISO** and choose a new filename. The tool also writes a structured `.iso.changes.md` report containing game tables. Restart the game and load a memory-card save to test the new ISO; emulator save states retain old loaded code/data.

### 6. Export a PDF report

Open **Changes → Export report PDF**, select the `.iso.changes.md` file, and choose a PDF destination. The monster table keeps every column on a wide page with one line per monster. Zoom in to read it. Section links and a Contents link provide navigation.

### 7. Recover a project from an existing modded ISO

Open **ISO to Project**. Select the original **Reference ISO**, the modified **Target ISO**, and a new `.cmcproject` filename, then generate the project.

Load the recovered project against the reference ISO. Review its `.recovery.txt` report: supported editable data is recovered, but arbitrary executable/script patches and dialogue/audio are not reconstructed. Original image/OBJ filenames cannot be recovered. Monster images pass through the linked importer, so separate menu variants and exact image encoding are not preserved. Recognized Joey eight-unit scripts include extra units, levels and placement coordinates.

## Troubleshooting

- Keep `.cmcproject` and its `.assets` folder together; use the matching reference ISO.
- If a modified game freezes, first test native-size/indexed portraits and a memory-card save. High-resolution/full-colour portraits remain experimental.
- When reporting a map/script freeze, include the exported ISO path, opponent, assigned map, trigger and a `.p2s` captured while frozen. A state can be inspected offline.
- Builds are Windows x64. Source builds need the .NET 10 SDK and NuGet access; packaged self-contained releases include the runtime.
