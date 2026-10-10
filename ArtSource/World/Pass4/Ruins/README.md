# Pass four: frontier ruins

Six standalone meshes authored procedurally in Blender 5.2. Source is `Ruins.blend`; regenerate using `build_ruins.py` through Blender's `--background --disable-autoexec --python` flags. The script loads `../../RealmWorld.blend`, explicitly evaluates only its `world_lib.py` helper, and reuses original W_Rubble and W_Rock materials and world-scale UV projections. It never saves the original world file or changes shared materials/textures. No external assets were downloaded.

Exports are in `Assets/Realm/Art/World/Pass4/Ruins`. Each FBX is a single mesh at ground origin, meter scale, -Z forward / Y up. The source blend keeps all meshes at their export origin; the contact sheet is staged only after saving and exporting. Hide other objects to inspect one source mesh.

RuinArch has an open 3.6m passage; use a non-convex static mesh collider or separate jamb/arch colliders. A convex collider would seal the opening. RuinStairs has seven 0.25m rises, 1.75m total walking rise; side remnants extend its bounding height to 2.114m. Use an invisible ramp collider for smooth character traversal. Wall faces run along Blender X and their depth is Y. All items can be rotated or repeated as static set dressing.

`asset_report.json` records dimensions and triangle counts. `Ruins_ContactSheet.png` was rendered and visually inspected. Unity material remapping, colliders and scene placement are owned by the parent integration task.
