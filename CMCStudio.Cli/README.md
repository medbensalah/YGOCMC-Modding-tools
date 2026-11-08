# CMC Studio CLI

This CLI references CMCStudio.csproj and uses the same Workspace loader, ModSession name editor, portrait importer, project saver and ISO exporter as the desktop app. It does not edit project JSON or image payloads independently.

## Import a complete roster

```powershell
& .\CMCStudio\cli\CMCStudio.Cli.exe import-roster --source "path\og.iso" --project "path\Revolution.cmcproject" --manifest "path\roster.json" --output "path\Revolution.cmcproject"
```

The UTF-8 manifest is an array of exactly 200 entries:

```json
[
  { "id": 0, "name": "Left Arm of the Forbidden One", "portrait": "Art/left-arm.jpg" }
]
```

The example shows one entry; supply all 200. IDs must be distinct, within 0-276. Portrait paths can be absolute or relative to the manifest. Supplied IDs become enabled; other slots become disabled. Names and all six linked portrait variants are updated. Combat stats, attributes, model assignments, campaign changes, SPA assignments and transformation rules are preserved.

Portrait import matches desktop Full colour + Keep imported resolution + Update detail/shop/selection portraits. It applies the shared 512-pixel cap, image alpha handling, native menu dimensions, atlas packing and original-resolution detail flags. Batch preparation validates the same combined assets once rather than rebuilding them after each image. The desktop single-image import calls this same batch method.

The output project is backed up before replacement. Sidecar assets and the human-readable .changes.md report are saved by the normal app saver. No ISO is exported by import-roster.

## Export an ISO

```powershell
& .\CMCStudio\cli\CMCStudio.Cli.exe export-iso --source "path\og.iso" --project "path\Revolution.cmcproject" --output "path\new.iso"
```

Uses Workspace.Export, including shop name sprites, linked portraits, other editors, enabled-count requirement and Markdown change report. Existing ISOs cannot be overwritten.

## This import

The reviewed manifest for monsters.xlsx is at ../investigation/roster_import/roster.json. Spreadsheet rows 2-201 map to IDs 0-199. Abbreviations and spelling are expanded to full monster names. Robolady, Harpie Lady Sisters and Gogiga Gagagigo use the user-approved supplied alternative images (Super Robolady, Harpie Lady Elegance and Gagagigo the Risen). Serpent Night Dragon uses the supplied Wicked Serpent Night Dragon artwork. Attribute/pow atk/PowerLv columns are intentionally not applied, per user instruction.

## Export readable game data

```powershell
.\CMCStudio.Cli.exe export-report --source og.iso --project Revolution.cmcproject --output Revolution
```

Writes `Revolution.changes.md` with complete current tables: monsters, enemy order and armies, fusions, evolutions, shops and special rewards, starter kits, growth equations and rates, and attribute buffs/debuffs. Includes unchanged rows. App project saves and ISO exports produce the same report automatically.
