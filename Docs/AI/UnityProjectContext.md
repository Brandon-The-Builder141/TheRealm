# The Realm — project context

Updated 2026-09-12. Project root is the nested `The Realm` directory containing Assets and ProjectSettings.

## Confirmed environment

- Unity 6000.4.1f1, HDRP 17.4.0, Input System 1.19.0.
- Netcode for GameObjects 2.13.2 was installed through Unity Package Manager for the approved four-player implementation.
- Original scene: `Assets/OutdoorsScene.unity`. Gameplay scene: `Assets/Realm/Scenes/TheRealm.unity`.
- Runtime, Editor and EditMode test assemblies are separated under Assets/Realm.

## Architecture

RealmSimulation owns gameplay decisions. RealmSession maps network identities to characters, receives commands, runs a 30 Hz host simulation, and sends state snapshots. Clients cannot supply damage, rolls, loot or resurrection outcomes. RealmSaveStore journals complete state before atomic snapshot replacement. RealmPresentation reads snapshots, maps existing Input System actions, and draws actors. Players use the animated Adventurer prefab (`Resources/Characters/Adventurer`) via RealmCharacterActor, which infers one-shot animations (attack, cast, dodge, drink, throw, hit) from replicated state changes, so no animation data crosses the network; enemies and NPCs are still procedural figures. RealmInterface presents the menu, creation, HUD, dialogue and journal. RealmArt and RealmBuilder generate original modular environment assets; the starter scene is retained.

RealmAbilities is the single source of truth for the twelve discipline abilities: name, energy, cooldown, range, cone, damage and effect shape. RealmSimulation resolves every cast from that table and RealmInterface reads its names and costs, so advertised abilities cannot drift from implemented behaviour again.

Campaign content is authored in FrontierCampaign.asset. Each quest carries settlementShift and wildernessShift describing how choosing its first option moves the two factions; the second option applies the opposite. Runtime state uses serializable plain data, not asset references. Identifiers are persisted strings. Immutable character discipline commitments and selected combat loadouts are distinct.

## Constraints

- Four players, trusted host, direct address/UDP 7777. No paid relay, matchmaking, dedicated server or host migration.
- Death erases character progress. One Spirit ally may intervene within three seconds, once per expedition. No inheritance or corpse recovery.
- Persistent world, fresh replacement characters. No live AI model. No new asset purchases.
- Keep original source/art provenance. Enemy and NPC figures are still prototype art.
- World kit pass 4: eleven new cliff and ruin meshes in `Assets/Realm/Art/World/Pass4/{Nature,Ruins}`, with separate Blender sources and reproducible scripts under `ArtSource/World/Pass4`. Replaces primitive borderland escarpments and adds eight ruin clusters. Existing W_Rock/W_Rubble materials are reused. `RealmBuilder.LoadWorldKit` resolves nested assets. `RealmWorldPass4.Apply` updates only the environment; `Validate` checks materials, references and clearance. Stairs are decorative outside the ground-plane movement area. See `Docs/AI/WorldPass4.md` for ownership, rebuild instructions and validation evidence.
- World kit pass 3 (2026-09-13): added `landmarks.py` (StoneGate, SealPillar, SealDais + SealDais_Detail). RealmArt's gate/seal loop now spawns these via Kit(); the gate needs two explicit BoxColliders (one per post) because a single bounds collider would seal the walkable gap, so RealmBuilder.Kit()'s usual auto-collider is bypassed there (solid=false) and RealmArt adds them by hand from the authored Blender dimensions. New `SwapGlow()` helper replaces the repeated "find the W_Glow material and recolour it" loop (lantern, gate, pillar, dais all share it).
- World kit pass 2 (2026-09-13): added `tex_recipes2.py` (W_Bark/W_ForestFloor/W_Rock + T_Foliage cutout atlas: broadleaf/fir/grass/fern quadrants), `tree.py` (oak/fir/birch builders + Kit.quad for explicit-UV cards), `undergrowth.py` (bush/fern clump/grass clump/boulder/log/stump). RealmArt's forest loop (z<150) spawns real trees + light undergrowth via Kit(); borderland escarpments deferred in this pass were replaced in pass 4.
- World kit: `ArtSource/World/RealmWorld.blend` (text blocks tex_lib/tex_recipes for tileable W_* textures, world_lib/cottage/props for meshes, world_export) → `Assets/Realm/Art/World/*.fbx` + `Textures`. `RealmWorldImport` makes HDRP materials; RealmBuilder passes KitProvider/MaterialProvider to RealmArt, which falls back to primitives when a kit piece is missing. `The Realm > Render World Preview` renders fixed viewpoints to Logs/Realm/world-preview.png for before/after comparison.
- Character pipeline: Blender source `ArtSource/Characters/RealmCharacters.blend` → `Assets/Realm/Art/Characters/Adventurer.fbx` (+ `_Animations.fbx`, Generic rig, shared skeleton). `RealmCharacterImport` configures imports/materials; `The Realm > Build Character Prefabs` builds the animator controller and prefab; `Render Character Preview` renders every state to Logs/Realm. Never delete-and-recreate the controller asset in a live editor session (references resolve to the destroyed object).
- A 6–10 hour polished campaign is the intended product; an authored quest sequence alone is not evidence that this duration or production quality is achieved.

## Validation and tools

Use `The Realm > Run Rules and Persistence Tests` and `The Realm > Build Windows Player`. Test results go to Logs/Realm; builds go to Builds/Windows. Local development commands are consumed once from Temp/realm-command.json by RealmAutomation. This bridge exists because the configured Unity MCP connection intermittently disappears after assembly reloads.

Unity AI account/discovery warnings predate implementation. Generation catalog access was not established. Do not claim AI-generated production models are available.
