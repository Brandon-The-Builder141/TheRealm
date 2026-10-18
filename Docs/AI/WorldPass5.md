# Pass Five: automated Editor integration

## Grounding decision

The frontier renderer and BoxCollider end at world y=-0.10. This is the visible base ground. The road and village street are non-colliding surfaces above it. However, `RealmSession.ResolveMovement` explicitly forces the player's logical position to y=0; the player does not follow a downward ground raycast. This pass does not change movement.

Placement samples the highest named flat ground/road/street surface under the centre and corners of each rotated renderer footprint. It subtracts the model's actual lower bound, including the ground models' 1.5 cm pivot offset. Ground patches get a 2 mm separation from the sampled surface. Sloping surfaces, uncovered footprints, and footprints crossing height steps are rejected. This sampler is intentionally restricted to the current box surfaces; it does not pretend to support arbitrary terrain meshes.

## Editor menu sequence

1. Open `Assets/Realm/Scenes/TheRealm.unity`, outside Play Mode. Wait for compilation.
2. Run **The Realm > Pass Five > Render Before Preview**. Output: `Logs/Realm/pass5-before.png`.
3. Run **The Realm > Pass Five > Configure Ground Materials**. This duplicates `T_Foliage.mat` through AssetDatabase into `P5_ForestFloor.mat`, `P5_Gravel.mat`, and `P5_Moss.mat`, or reuses existing copies without changing their GUIDs. Each receives the matching ground BaseColor texture, unit tiling, zero offset and white tint; normal/mask maps are cleared. Every other source setting is copied rather than assigned invented defaults. HDRP refreshes derived map keywords. Only these three assets are saved, never the scene or source material. The action supports Undo/Redo and reports created/reused counts.
4. Run **The Realm > Pass Five > Apply World Polish (Undoable)**. Missing models/materials or mismatched cutout settings stop the action. Only a new child under the existing environment is added. No prefab is updated and the scene is not saved. Repeating the action validates and preserves the existing root rather than rebuilding it. Ctrl+Z removes the pass; Ctrl+Y restores it.
5. Run **The Realm > Pass Five > Validate World Polish**, then **Render After Preview**. Compare `Logs/Realm/pass5-before.png` and `pass5-after.png`. Validation report: `Logs/Realm/pass5-validation.txt`.
6. Review the previews and save the scene manually when satisfied. Do not Apply prefab overrides or run BuildAdventure. Slow camera movement is still needed to judge temporal shimmer; still images cannot prove its absence.

The standard **The Realm > Run Rules and Persistence Tests** suite includes 35 original tests and nine placement regressions. Run it on a clean scene before applying polish: Unity Test Runner asks to save dirty scenes. During automated verification, the new polish can be undone, the unchanged saved scene reopened, the suite run, and the menu action reapplied, leaving the final scene unsaved.

## Preservation and limitations

The seed and four placement groups remain intact. Candidates rejected for clearance still consume their original random draws. Road-shoulder ground patches move outward by their measured width to avoid crossing the road boundary. Full AABB footprints protect the five-metre quest interaction radius, the road/street, whole gate and arch bounds, seal arenas and existing colliders. Roots and moss may overlap tree trunks they dress, but never bypass the other exclusions. Conservative exclusion may produce sparse patches near landmarks; that is preferable to obstructing them. New dressing contains no colliders. Existing roots are never destroyed or silently refreshed.

The authoring action uses the saved campaign rather than hardcoded quest positions. The existing `RealmArt.BuildEnvironment` entry point remains in use for generation, with default campaign coordinates when no campaign is supplied. The Editor action restores both existing asset-provider delegates even if placement fails.

The automated material/integration sequence has now been executed in the main scene. Final visual approval and scene saving remain with the user. Compilation and automated placement tests alone do not establish performance or temporal shimmer quality.

## Temporary real-asset rehearsal (2026-10-10)

An isolated preview-scene copy of the current environment, using in-memory copies of T_Foliage for the three ground materials, produced 277 added renderers and zero colliders. Grounding and clearance checks reported zero failures. All nine models were represented: GrassTuft_A 32, GrassTuft_B 63, FernPatch 28, DryScrub 48, FallenBranch 12, RootCluster 27, ForestFloorPatch 15, GravelPatch 30, MossPatch 22. Repeated Build returned the same root; Undo removed it and Redo restored all 277 renderers. The temporary scene and materials were disposed, leaving the main scene clean with zero polish roots. These are rehearsal counts, not evidence of saved scene integration or performance approval.

Final Editor compilation completed and the Console error query returned zero errors. The standard Rules and Persistence menu suite ran all 44 tests (35 existing plus 9 placement regressions): 44 passed, 0 failed, 0 skipped. The existing pass-four model/material/road/quest/arch validator also passed. The main scene remained stopped and clean with its original 464 renderers. A fresh before preview was rendered to `Logs/Realm/pass5-before.png`; no after preview is claimed before manual integration. Existing Unity AI account/signature warnings were observed before this change.

## Automated main-scene integration (2026-10-10)

The new Configure Ground Materials menu action was executed: first run created 3/reused 0; the repeat reused 3/created 0 with identical GUIDs. Creation Undo removed the three materials and Redo restored them with valid settings. Full shader-property comparison found no changes outside the requested maps/tint and HDRP's derived `_MainTex` base-map alias. HDRP also updates map keywords to reflect cleared normal/mask maps. Source T_Foliage and its metadata are byte-for-byte unchanged.

Before > Configure > Apply > Validate > After completed. The main scene contains exactly one populated polish root, adding 277 renderers for 741 total, with zero added colliders and passing material/grounding/clearance checks. Both preview files exist. A fresh standard EditMode run passed 44/44 with zero failures/skips; polish was temporarily undone for the clean-scene test run and reapplied afterward. The final scene is dirty/unsaved and its disk file remains byte-for-byte unchanged, awaiting user review and save. The previews show visible ground dressing with open travel lanes; temporal shimmer and performance are not established by these still images.
