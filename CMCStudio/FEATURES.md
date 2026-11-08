# CMC Studio features

Native Windows modding tool for Yu-Gi-Oh! Capsule Monster Coliseum.

## Projects and ISO files

- Open a supported game ISO.
- Save and load mod projects.
- Save only changed settings and asset references.
- Keep imported assets in a companion project folder.
- Open older project files.
- Export edits into a new ISO.
- Show export progress.
- Write a human-readable change report when saving or exporting.
- Require exactly 200 enabled monsters only when exporting an ISO.

## Navigation and appearance

- Switch between Obsidian, Amethyst, and Paper themes.
- Open multiple workspace tabs.
- Open individual monsters in their own tabs.
- Use compact forms and editable tables.
- Search, sort, and filter monster lists.
- Reuse recent monster editors and model previews.
- Load monster portraits and models in the background.

## Monster roster and stats

- Edit monster names.
- Enable or disable monsters directly in the list.
- Show disabled monster names in red.
- Filter all, enabled, or disabled monsters.
- Show enabled and disabled counts.
- Edit type and attribute.
- Edit PP, AT, DF, AP, pAP, and MP.
- Edit symbol, terrain, and map affinities.
- Edit experience and comments.
- Edit raw advanced monster fields.
- Restore original stats and names.

## Monster grid

- Edit monsters in one spreadsheet-style table.
- Show portrait thumbnails.
- Show movement and attack patterns.
- Edit stats, names, enabled state, patterns, and SPA assignments.
- Filter individual columns.
- Use numeric filters such as >=, <=, and =.
- Sort columns.
- Open a monster's detailed editor.

## Movement, attacks, and special abilities

- Preview movement and attack ranges.
- Select patterns with previous and next buttons.
- Assign existing special abilities (SPAs) to monsters.
- Select SPAs by ability name.
- Search SPA names and descriptions.
- Read SPA descriptions and calculation explanations.
- Preview SPA range patterns.
- Restore original SPA assignments.

## Monster portraits

- Preview, import, export, and restore portraits.
- Update linked detail, shop, and selection portraits together.
- Import full-colour images with alpha transparency.
- Keep imported detail-portrait resolution.
- Remember the resolution checkbox setting.
- Automatically fit oversized imports into a 512 x 512 canvas.
- Keep menu copies at their supported native dimensions.

## Monster models

- Select the monster's Map Unit or Battle Unit model.
- Preview decoded models and their textures.
- Rotate and zoom previews with the mouse.
- Adjust preview rotation and zoom with sliders.
- View individual model parts or wireframes.
- Export OBJ models with MTL files and textures.
- Import OBJ geometry, UVs, normals, and supported diffuse materials.
- Restore original models.
- Replace all Map Unit models with one OBJ and MTL.
- Report bulk-import failures without staging a partial replacement.

## Evolution and fusion

- Edit evolution source monsters and results.
- Edit required levels and elemental thresholds.
- Add, delete, and reorder evolution rules.
- Edit fusion results and ingredients.
- Edit ingredient minimum levels.
- Create recipes with 2, 3, or 5 units.
- Add and delete fusion recipes.
- Show outgoing and incoming evolutions in monster details.
- Show fusions using or producing the selected monster.
- Open linked monsters from relationship rows.

## Campaign and enemies

- Reorder opponents between campaign slots.
- Assign maps to opponents.
- Edit map-effect assignments.
- Synchronize enemy artwork, names, and map thumbnails on export.
- Edit enemy monster armies and their levels.
- Edit enemy monster limits, AP, and MP.
- Edit enemy Symbol stats, attribute, and effects.

## Character portraits

- Edit campaign and loading-screen character artwork.
- Edit dialogue poses and expression sheets.
- Preview, import, export, and restore character images.
- Fit imports to original dimensions without stretching.
- Show encounters affected by shared images.

## Shops and starter kits

- Edit normal shop stock in its saved order.
- Edit starter monster rosters.
- Edit starter PP, DF, and AP.
- Keep original stock and starter entry counts.

## Special shops and rewards

- Edit temporary Area 2-5 shop batches.
- Edit the eight individually unlocked shop monsters.
- Edit decoded unlock requirements.
- Set required deck monsters and copy counts.
- Set required collection monsters and copy counts.
- Set collection totals by attribute.
- Set play-time requirements and comparison operators.
- Read explanations of stock and unlock triggers.
- Edit the eight-slot end-game reward pool.
- Read how ending rewards are selected.
- Edit the shop eligibility catalog.
- Fill the catalog from the enabled monster roster.
- Restore individual special-shop sections.

## Growth and combat

- Edit per-element growth rates by stat and level transition.
- Edit supported growth equation scales and rounding adjustments.
- Edit recovered combat equation settings.
- Edit the elemental attack and defence effect matrix.
- Calculate monster stats at a chosen level.
- Estimate combat damage with supported modifiers.
- Read simplified equations and worked examples.

## Maps and tiles

- Select and preview maps.
- View textured 3D maps.
- Edit individual tiles from a top-down view.
- Highlight the active tile.
- Edit recovered tile and map-effect fields.
- Edit player deployment zones.
- Edit starting Symbol positions for both players.
- Export map models with their textures and materials.

## Map texture enhancement

- Apply AI enhancement to all textures in a map.
- Choose whether to upscale by 2x.
- Choose detail enhancement.
- Export enhanced textures.
- Embed enhanced textures into the ISO.
- Combine export, embedding, and detail enhancement options.

## Dialogue and audio

- Edit encounter dialogue text.
- Preserve text IDs and required control codes.
- Search spoken dialogue grouped by encounter and line.
- List recordings whose dialogue links are unknown.
- Show shared uses of recordings.
- Play and stop voice recordings and sound effects.
- Export audio as WAV.
- Import supported PCM 16-bit WAV files.
- Convert imported audio to the game's native format.
- Restore original recordings and effects.

## Event triggers

- Reroute existing encounter entries to existing handlers.
- Select return or no-action handlers where available.
- Protect battle-initialization entries.

## Asset browser

- Browse assets by category, name, and resource ID.
- Preview supported portraits and models.
- Export raw game resources.
- Import and export supported editable assets.

## Command-line tool

- Import a roster from a JSON manifest.
- Apply monster names, portraits, and enabled states.
- Use the same portrait importer and project saver as the app.
- Back up an existing project before roster replacement.
- Export a project to ISO from the command line.

## Current limits

- High-resolution portrait loading remains experimental.
- Imported models and materials may need in-game adjustments.
- OBJ import does not preserve animations.
- Material import supports a diffuse texture or colour; advanced MTL settings are ignored.
- Unresolved event flags remain fixed and are labeled in the editor.
- Trigger editing reroutes existing handlers; it does not author new scripts.
- Audio replacements must fit the original recording duration.
- Background music replacement is not included.
- Raw advanced monster fields are only partly understood.

- Capsule starting positions: select the red or blue enemy army, assign monsters to slots, and click the top-down map to move capsules and Symbols.

- Map assignments carry the map's deployment grid and capsule/Symbol positions; opponents sharing a map share its editable starting layout.
