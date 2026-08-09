# Phase 3 — Lunar visuals & terrain (progress notes)

Goal: replace RA's terrestrial terrain with a cohesive lunar look — regolith
plains, impact craters, basalt flats, and ice deposits as the harvestable resource.

## Done

- **`tools/gen_lunar_terrain.py`** — production terrain generator. Produces a cohesive,
  **seamlessly tileable** set (torus-periodic value noise, so tiles wrap with no seam)
  in a unified lunar palette. Terrain vocabulary, 1:1 with `tilesets/lunar.yaml`/
  `tilesets/mars.yaml`: `Regolith` (×3 variants), `Basalt`, `IceDeposit`, `CraterFloor`,
  `CraterWall` (cliff). Runs for both `--palette moon` and `--palette mars`.
- **Outputs** (regenerate any time; not committed as game assets yet): per-tile PNGs,
  a packed `lunar_sheet.png`, `lunar_manifest.txt` (index→terrain→sheet xy), a labelled
  `lunar_montage.png`, and a `lunar_palette.png` swatch, under `artwork/lunar/`.
- **Verified visually**: `artwork/lunar_battlefield.png` composes the real tiles into a
  map region (regolith base + basalt patches + crater clusters + ice) and
  `artwork/regolith_tiling_proof.png` shows a single tile ×9 tiling without seams.
  Engine-side variant rotation/flip breaks grid repetition (as OpenRA does at runtime).
- **Tileset import.** The generated tiles ship as real OpenRA tilesets:
  `tilesets/lunar.yaml` and `tilesets/mars.yaml`, both registered in `mod.yaml`
  `TileSets:`. The shipped approach differs from what this note originally proposed
  (Asset Browser / `.tmp` sheet authoring): instead, every `Template` block from RA's own
  `temperat.yaml` was cloned 1:1 (same `Id`/`Size`/per-cell terrain type), with `Images:`
  repointed at a generated same-shaped PNG per template — so existing `.oramap` binary
  tile grids (which only reference template `Id`/index, never pixels) stay valid unchanged.
  `ysmir-lunar.oramap`/`ysmir-mars.oramap` (clones of `ysmir.oramap` with only the
  `Tileset:` line swapped) use this for the in-game `?terrain=` picker.

## Remaining (needs the engine / editor — can't be done headless)

1. **Transition / cliff-edge tiles.** The current set is base tiles; crater rims and
   regolith→basalt/ice borders want authored edge pieces for clean in-game blending.
2. **Resource = ice.** Point the `ResourceLayer`/`ResourceRenderer` at the `IceDeposit`
   sprites and rename ore→ice in tooltips (YAML-only, since the tileset already loads).
3. **Palette & lighting.** Apply a cold, high-contrast lunar palette and a black-sky
   backdrop; wire `CraterWall` as impassable in the ground `Locomotor` `TerrainSpeeds`.

## How to regenerate

```bash
cd mods/spaceage/tools
python3 gen_lunar_terrain.py --palette moon --tile 48 --out ../artwork/lunar
python3 gen_lunar_terrain.py --palette mars --tile 48 --out ../artwork/mars
```
Deps: `numpy`, `Pillow`.
