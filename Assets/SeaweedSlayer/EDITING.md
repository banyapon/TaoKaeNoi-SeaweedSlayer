# Editing the Game scene

Open `Assets/Scenes/Game.unity`. The scene now contains its environment, both UI canvases, and all gameplay objects before entering Play Mode.

- **Main Camera**: edit Transform, projection, size/FOV, clipping and viewport normally. Gameplay never sets these values.
- **PC UI**: edit the lobby, logo, QR Image Sprite, buttons and score overlay using RectTransforms. Rank card list uses a standard Vertical Layout Group; each card has a Layout Element for its height.
- **Controller UI**: enable this canvas and disable PC UI temporarily to edit the profile, Ready, Joystick Pack and Throw. The session selects the appropriate canvas at startup. Button callbacks are persistent Inspector events. At runtime, SeaweedUI fits the Profile/Joystick panels to the screen safe area, lays out colors in five columns in portrait or one row in landscape, and sizes both circular controls to fit the available area. These responsive RectTransforms override their authored positions when the viewport changes. Edit the remaining children normally.
- **Arena / Environment**: edit the checker tiles, fences, border, water and grass directly. Grass uses a sparse scatter of `ForestGrass02` under `Environment / Grass`.
- **Arena / Trees**: edit tree models and colliders. Disable `Randomize Trees` on the Arena component to keep your placed positions between rounds.
- **Arena / Grass**: sparse `ForestGrass02` tufts. They do not block movement. Disable `Randomize Grass` to keep your placed positions between rounds.
- **Arena / Players, Snacks, Enemies, Cubes**: inactive prefab instances are reusable game slots. Expand them to edit their model, sprite, name canvas, outline and collider. Enable a slot temporarily to preview it. Gameplay switches slots on and off instead of creating or deleting objects.

Shared prefabs are in `Assets/SeaweedSlayer/Generated/Authoring`. Edit these prefabs to change every slot, or override individual scene instances. The snack sprite size and its base height are saved on its billboard Transform.

Normal build commands validate the existing scene; they do not reconstruct its UI, environment or camera. `Materialize Editable Hierarchy` is an idempotent one-time migration and leaves an already authored scene alone. Reopen Game if Unity had the previous version open when the scene file changed externally.

WebGL defaults to the same PC quality/pipeline as Windows. Keep Additional Light Shadows enabled on both URP assets: in this Unity/URP version, disabling it leaves a color fallback bound to a shadow sampler and WebGL rejects 3D draws. This setting also initializes a valid depth fallback when no additional light casts shadows.

Use `Seaweed Slayer > Build WebGL Site (Arena + Controller)` to build `Builds/WebGL/index.html` and `Builds/WebGL/Controller/index.html`. Upload the entire `Builds/WebGL` folder as one static site. Unity build files use content hashes so updated deployments request the new assets.
