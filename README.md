# The Realm

A Unity prototype for a hardcore fantasy RPG for one to four players, with mixed character disciplines, visible dice, consequential choices, and permanent character death.

## Open the project

1. Clone this repository.
2. Add the repository folder in Unity Hub and open it with **Unity 6000.4.1f1**.
3. Allow Unity to restore packages and import assets.
4. Open `Assets/Realm/Scenes/TheRealm.unity`.

The project uses HDRP 17.4.0, Input System 1.19.0, and Netcode for GameObjects 2.13.2. Multiplayer currently targets trusted friends using a direct connection; relay and matchmaking are not configured.

## Project layout

- `Assets/Realm`: gameplay code, scenes, prefabs, tests, and art.
- `ArtSource`: Blender sources, generation scripts, and asset reports.
- `Packages` and `ProjectSettings`: Unity dependencies and shared configuration.
- `Docs`: architecture notes and asset provenance.

## Status and validation

This is an in-development prototype, not a finished game. Pass Five vegetation and ground assets exist, but their integration into the main scene remains pending. Existing historical validation is documented in `Docs`; publishing this repository does not establish a fresh test or build result.

Run **The Realm > Run Rules and Persistence Tests** in the Unity Editor to validate rules and persistence. The previously recorded baseline is 35 passed, 0 failed. Generated results go to `Logs/Realm` and are intentionally excluded from Git.

Do not run `RealmBuilder.BuildAdventure` to update an existing scene: it rebuilds the scene. Preserve existing prefab GUIDs and use the focused editor workflows documented in `Docs`.

## Repository hygiene and assets

Unity caches, local editor settings, builds, logs, recovery files, local tool configuration, credentials, and player saves are excluded. Keep secrets in local configuration outside committed assets. All Unity source `.meta` files are versioned to preserve references.

See `Docs/ASSET_PROVENANCE.md` for original and third-party asset sources. Public repository visibility does not grant a blanket license to project code or art; third-party components retain their own terms.
