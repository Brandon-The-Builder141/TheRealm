# World asset pass four

Implemented 2026-09-12 local time. Scope: natural borderland cliffs and modular stone ruins, integrated into the existing environment. Character/enemy work remains a subsequent pass.

## Ownership and shared rules

- Nature agent owns `ArtSource/World/Pass4/Nature` and `Assets/Realm/Art/World/Pass4/Nature`: Cliff_A/B/C, RockShelf, ScreeCluster.
- Ruins agent owns corresponding `Pass4/Ruins` folders: RuinWall_A/B, RuinArch, RuinPillar, RuinStairs, RuinRubble.
- Main agent owns shared Unity source, placement, colliders, integration, validation and documentation.
- Both kits reuse the existing W_Rock/W_Rubble textures and world-scale UV conventions. Meter scale, ground origins, Blender Z up, FBX -Z forward / Y up. No new dependencies or downloaded assets.
- The original RealmWorld.blend and RealmCharacters.blend are preserved. Each new kit has its own blend, external deterministic builder, asset report and inspected contact sheet.

## Integration

`RealmBuilder.LoadWorldKit` resolves the original folder and both new folders. `RealmWorldImport` remaps the existing HDRP materials for nested FBXs. `RealmArt` replaces the borderland primitive escarpments and places eight off-road ruin clusters. Existing village, forest random sequence, road, quests and seal locations are retained.

Natural cliffs use conservative box colliders. Ruins use static mesh colliders to preserve arch openings. Rubble is decorative without collision. Stairs are scenery outside the traversable field because current player movement fixes height to the ground plane; this pass does not add stair traversal. Cliffs start at least 28m from the world centre to keep ruin passages clear.

`The Realm > Apply World Pass Four` updates only the environment in the saved gameplay scene, preserving other scene objects and the existing prefab GUID. It requires all eleven new models and an unmodified scene. The regular Build Adventure Scene also resolves the new kit, but is not needed for this pass.

## Rebuild and review

Run Blender 5.2 with `--background --disable-autoexec --python` and the relevant `build_nature.py` or `build_ruins.py` path. Each script locates the original source and exports only its owned FBXs. Refresh Unity, then apply the pass.

- `The Realm > Validate World Pass Four`: model/material checks, continuous road lane, all authored quest points, arch centres, missing scripts and no remaining primitive escarpments.
- `The Realm > Render Pass Four Preview`: eight views in `Logs/Realm/pass4-preview.png`.
- `The Realm > Render World Preview`: original comparison views in `Logs/Realm/world-preview.png`.
- `The Realm > Run Rules and Persistence Tests`: existing EditMode suite.

## Validation

Unity imported and compiled the integration. All eleven FBXs resolve to shared HDRP/Lit materials. Clearance/reference checks finished with zero failures. All 35 existing EditMode tests passed. Both Blender contact sheets and the integrated Unity previews were visually inspected.

Environment-only Play Mode smoke check passed with 147 cliff instances and eight arches, including the same collision/clearance checks. No host session or world save was opened; Play Mode was exited afterwards. Evidence: `Logs/Realm/pass4-runtime.txt`.

The first clearance check identified existing seal pillars on the exact road centre; the final check verifies a continuous lane 2m to its side. One new cliff overlapped an arch and was moved out with the cliff boundary. An existing preview cleanup error was corrected by detaching the camera target before releasing the render texture.

Baseline backups of changed environment source, scene, prefab and original preview are in `Logs/Realm/Pass4Baseline`. No player build, multiplayer session, or performance benchmark is claimed for this art pass. The previously observed RealmSession.Send exception is unrelated to these asset changes.
