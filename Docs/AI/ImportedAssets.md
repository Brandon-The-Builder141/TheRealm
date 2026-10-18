# Imported assets: 2026-10-11

## Saved and runtime verified: 2026-10-16

The distributed 12-site dressing was previously only in the unsaved Editor scene. It is now saved into `Assets/Realm/Scenes/TheRealm.unity` with user approval. Do not rebuild the scene to restore this pass. The existing redistribution menu remains available for deliberate layout revisions.

KayKit skeletons are now configured in `Assets/Realm/Resources/Characters/Skeletons.asset` and spawned by `RealmPresentation`: Minion in the forest, Rogue in the borderlands, Mage in the final region, and larger Warrior bosses. The Mage currently uses melee staff attacks under existing combat rules, not new spellcasting AI. `RealmEnemyActor` evaluates matching Medium rig clips for idle, running, windup, attack, hit and death. This presentation does not own damage or network authority. The source model imports and existing player prefab/controller remain intact. The old procedural enemy fallback has been removed; missing skeleton configuration produces an explicit error.

The user's saved campaign had all 49 enemies defeated, explaining the absence of visible enemies. At the user's request, hosting a completely cleared roster now restores all existing enemies at their home positions and full health, with a four-second attack delay. A partial clear remains unchanged. Character permadeath, character progression, campaign chapter and quest progress are not reset. This happens on hosting, not immediately after the last kill. Closing a session also clears its visual actors before reopening.

Actual Play Mode validation used isolated saves: all 12 sites and 48 standard regional skeletons were visible; real chase/windup/attack behavior and controlled hit/death transitions passed. A separate test hosted a copy of the user's cleared save: 49 skeletons restored, character/quest progress preserved, partial clear preserved on reopening, and actor cleanup passed. The user's original save was not modified by these tests. The restart rule applies to it on the next normal Host.

Evidence: `Logs/Realm/runtime-assets.png`, `runtime-assets.txt`, `enemy-restart-smoke.txt`, and the EditMode reports. The local bridge supports `inspect-art`, `asset-smoke`, and `enemy-restart-smoke`; both smoke actions require Play Mode at the title screen and isolate test saves under Temp. `save-verified-art` validates before saving and must only be used when scene saving is authorized.

Final October 16 checks: **52 passed, 0 failed, 0 skipped** after the restart and presentation-cleanup changes; no C# compilation errors. The saved scene was reloaded by the test runner and the runtime tour was repeated successfully against the saved layout.

## Current layout: distributed second pass

The initial adjacent camp/graveyard arrangement below was rejected during visual review and has been superseded. **The Realm > Imported Assets > Redistribute Dressing Across Map** now lays out 12 sites over the length of the map: village stores, forest camp, woodland graves, woodcutters' rest, border supplies, parish graves, caravan camp, roadside remembrance, abandoned supplies, sunless burial ground, last expedition camp and fallen travellers.

The new layout spans z=2 to z=391, with a measured closest site spacing of about 35.5 metres (23 metres is the enforced minimum). The sites contain 105 objects/114 renderers, including ground patches and vegetation that tie the imported props into the existing landscape. Camp supplies and gravestones have deterministic positional, scale and orientation variation; the two owned imported palette materials have subdued tints. Full footprints still respect existing world exclusions, and ground-cover patches avoid the existing Pass Five patches.

Redistribution preserves the imported root and modifies only its owned children through one Undo group. It constructs the replacement before removing old children and rolls back if validation fails. No gameplay object, source prefab or imported asset is rebuilt. Repeating the action produces identical positions; Undo/Redo, imported validation and existing Pass Four/Five checks passed. The scene is left unsaved. The 44/0 rules test result below is from the preceding pass; this layout-only follow-up was checked through Editor compilation, visual previews and the world validators rather than claiming another test-suite run.

## Inventory and first integration

Compared against the initial Git commit and the previous inspection, six new raw asset packs were added on October 11:

| Pack | Inspection / integration state |
| --- | --- |
| KayKit Adventurers 2.0 FREE | Six character FBXs plus equipment/alternate formats. Sample Knight imports with nine skinned renderers, no configured Avatar or embedded clips. Character replacement deferred to a focused rig/animation pass. |
| KayKit Skeletons 1.1 FREE | Four models and equipment now integrated as animated enemies; see October 16 verification above. |
| KayKit Character Animations 1.1 | Medium rig clips integrated and verified on skeletons. Large rigs and playable-character retargeting remain pending. |
| Kenney Survival Kit | Audited selected FBX geometry and embedded HDRP materials. Implemented a decorative wayfarer camp using seven placed props and a dedicated HDRP palette material. |
| Kenney Graveyard Kit 5.0 | Audited selected FBX geometry/materials. Implemented six graves and one damaged bench using a dedicated HDRP palette material. |
| forrest_ground_01_4k.blend | Blender sample imports as a two-metre cube, not terrain. Diffuse, displacement, OpenGL normal EXR and roughness textures exist. Not applied to the world; source attribution and proper HDRP texture packing remain pending. |

ArtisticMechanics, Furry Squirrel, RSG_DogsPack, TerrainSampleAssets and Wolf were already present during the October 10 material pass. They are untracked in Git but are not newly added since that inspection. Their complete gameplay integration is outside this first world-dressing pass. The sampled Wolf includes 27 clips and several material shader types; no live actor/controller was replaced.

The five Kenney/KayKit packs include CC0 license files. Keep those files with the packs. No new download or purchase was made.

## Authoring commands

- **The Realm > Imported Assets > Audit Camp and Graveyard Models** writes `Logs/Realm/imported-world-audit.txt`.
- **Apply Camp and Graveyard Dressing** creates only an `Imported world props — camps and graves` child beneath the existing environment. Repeated application validates/preserves it. Uses Undo and leaves the scene unsaved.
- **Validate Camp and Graveyard Dressing** checks expected counts, ground alignment, material bindings and clearance.
- **Render Camp and Graveyard Comparison** captures matching views with the new root temporarily hidden/visible, restoring its state in a finally block. Outputs `Logs/Realm/imported-clusters-before.png` and `imported-clusters-after.png`.

The implementation reuses `RealmWorldPolish.Placement` for named-surface sampling and full-footprint exclusions for roads, quests, gates, arches, seal arenas and existing colliders. It tries deterministic candidate positions for complete clusters and rejects a candidate if any prop violates clearance. The camp settled at (-18, 0, 78), and the graves at (-18, 0, 70). Props are aligned to the sampled surface by their actual renderer bounds. The tiny imported models are scaled only on instances; the canvas tent is 2.8 metres wide.

Dedicated materials are stored under `Assets/Realm/Art/World/Materials/Imported_Kenney_*.mat`, referencing each pack's FBX palette texture. Imported files, original material remaps, existing prefabs and player/AnimatorController assets are not modified. The 14 props add 17 renderers and no colliders or gameplay behaviours: these are visual locations, not functional camps/checkpoints or interactive graves.

## Evidence

Unity opened the correct saved scene with 741 renderers and no unsaved edits. The fresh standard Realm EditMode suite passed 44, failed 0, skipped 0 before placement. Imported-model audit, existing Pass Four and Pass Five validation, and the new dressing validation passed. Repeated Apply kept root identity; Undo removed the pass and Redo restored it. Close-up inspection caught the initially selected frame-only tent; the final pass uses the complete tent-canvas model.

Before/after world previews and matching close-up previews were rendered through HDRP. Final scene edits are intentionally unsaved for user review. This is the first scoped integration, not completion of character retargeting, wildlife AI, terrain replacement, performance benchmarking or the remaining packs.
