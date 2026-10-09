# TaoKaeNoi Seaweed Slayer

A local multiplayer party game made for **TaoKaeNoi**, Thailand’s well-known crispy seaweed snack. Players collect seaweed packs, stun rivals, and shoot pests on a shared arena. The big screen runs in Unity. Each phone is a controller: scan the on-screen QR code, pick a name and color, and play.

Developed by the **College of Creative Design and Entertainment Technology, Dhurakij Pundit University**, as an original TaoKaeNoi activation for **gamescom asia x Thailand Game Show 2026** (29 October – 1 November 2026, Queen Sirikit National Convention Center, Bangkok).

**Project & Lead Developer:** Asst. Prof. Banyapon Poolsawas

![Lobby with the join QR code](images/intro.png)

![Top-down arena](images/gameplay.png)

## How it plays

One scene, `Assets/Scenes/Game.unity`, serves the Windows build, the WebGL arena, and the WebGL phone controller. The arena PC is the authority: it simulates movement, throws, collisions, and scores. Phones only send input.

Rooms use **Photon PUN** in the `asia` region, with game version `seaweed-slayer-v1`. A Photon App ID must be set in Photon Server Settings, and both the PC and the phones need internet.

1. The arena opens a room and shows a QR code for the controller URL.
2. Each phone scans it, chooses a name and a color, then taps **Ready**.
3. When everyone in the room is ready, the QR hides and a 3-second countdown starts. Cancelling Ready during the countdown returns the room to the lobby.
4. A round lasts 90 seconds (editable in Settings). Results stay up for 10 seconds, then the same room returns to the lobby for another Ready check.

| Action | Result |
| --- | --- |
| Collect a seaweed pack | +25 points. Packs respawn during the round. |
| Hit an enemy | +50 points. Up to 3 enemies. A new one appears 7–11 seconds after one is removed. |
| Throw | A cube flies in the facing direction. Cooldown is 0.65 seconds. |
| Get hit | The character flashes and cannot move, throw, or collect for 2 seconds. Extra hits during the stun do not extend it. |
| Touch an enemy | The player is stunned. |

- 1–20 players. The Photon room has 21 slots because the arena PC uses one. The cap is set in `Assets/SeaweedSlayer/SeaweedSettings.asset`.
- Scores sort from highest to lowest. Ties keep a stable order by Photon actor number.
- The live rank card shows 1st, 2nd, 3rd, and 4th–20th, and only lists players who are in the room.
- Trees are shuffled at the start of each round. A center path and the border stay walkable. Water and fences block movement.
- The camera is a straight orthographic view.
- With no phones joined, the PC can press **เล่นคนเดียว (WASD + SPACE)** and use WASD or the arrow keys to move, and Space to throw.

On a phone, drag the on-screen joystick to walk and press **THROW** to fire. The controller layout respects the device safe area.

## Run the project

Open the project in **Unity 6000.4.10f1**.

| Menu | Output |
| --- | --- |
| **Seaweed Slayer → Prepare Game Scene** | Refresh generated assets when the scene needs them. |
| **Seaweed Slayer → Build Windows PC** | `Builds/Windows/SeaweedSlayer.exe` |
| **Seaweed Slayer → Build WebGL Site (Arena + Controller)** | Arena at `Builds/WebGL/index.html`, controller at `Builds/WebGL/Controller/index.html` |
| **Seaweed Slayer → Build WebGL Arena + Controller + Windows** | All three builds |
| **Seaweed Slayer → Validate** | Checks assets and the player cap before a platform build. |

Host everything inside `Builds/WebGL` on HTTPS. The arena is `/` and the controller is `/Controller/`. Each page runs one Unity instance. WebGL is Gzip-compressed with decompression fallback, so a static host does not need a custom `Content-Encoding`. See the [Unity WebGL decompression fallback](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PlayerSettings.WebGL-decompressionFallback.html) setting.

On the arena PC, paste the public controller URL into the lobby field and press **Create QR** (labeled “สร้าง QR / เข้าห้องด้วยมือถือ”), or set `controllerUrl` on `SeaweedSettings.asset` before building. A URL entered on the PC is remembered for the next launch. The booth build uses `https://taokaenoiarena.vercel.app/Controller` and appends `?room=...&uid=...&host=...&mode=controller`. The `uid` in that URL is the room host. Each phone keeps its own player UID in browser storage.

`localhost` on the PC is not an address a phone can open. Use HTTPS, or the PC’s LAN address, on a network the phones can reach.

The arena page fills the browser window. **Fullscreen** needs one click, which browsers require.

## Reconnect

The controller stores the Photon UserId, room code, host UID, name, and color in WebGL PlayerPrefs. After a drop it rejoins with the same UID. The room keeps that slot and score for 60 seconds, and a character stops if no input arrives for 0.35 seconds. After the slot expires, or if the arena PC closes, the phone must scan a new QR code. Clearing site data deletes the saved UID.

To test a controller inside the Unity Editor, enable `editorController` on SeaweedSession and fill in `editorRoom`. Leave that off for the WebGL arena. The arena build can also open as a controller with `?mode=controller`. The dedicated controller build is always a controller.

## Scene edits

UI and gameplay objects live in `Assets/Scenes/Game.unity`: `Arena`, `PC UI`, `Controller UI`, and prefab slots for characters, snacks, enemies, and projectiles. Play mode reuses those objects and toggles them with the game state. It does not overwrite the Main Camera or UI RectTransforms.

See [Editing the Game scene](Assets/SeaweedSlayer/EDITING.md) for the canvases, prefabs, layout groups, and how to turn off tree shuffling. If Unity still has an older copy of the scene open, reopen `Game` to load the current hierarchy.

Generated URP prefabs and materials are under `Assets/SeaweedSlayer/Generated` and do not modify the original forest materials. Player clips are `Idle_Cute`, `Run_Cute`, and `Throw_Left`. **Seaweed Slayer → Upgrade Player Animations** updates those clips on an older generated prefab without rewriting the scene.

## Checks

**Seaweed Slayer → Validate** runs before each platform build.

The Windows player can run a scripted Photon room with two simulated controllers:

```powershell
./Builds/Windows/SeaweedSlayer.exe -batchmode -nographics -seaweed-smoke -logFile ./Builds/network-smoke.log
```

Look for `SEAWEED_NETWORK_SMOKE_SUCCESS`. The smoke test covers the PC slot, Ready, a cancelled countdown, movement, input timeout, throws and the 2-second stun, snack and enemy scoring, the enemy cap, rejoin with the same UID, and the results-to-lobby loop. It does not load 20 real phones.

`-seaweed-preview` writes `Preview.png` next to the executable and then quits. Test flags do nothing in a normal launch.

Photon references: [custom properties and state](https://doc.photonengine.com/pun/current/gameplay/synchronization-and-state), [rejoin and UserId](https://doc-api.photonengine.com/en/pun/current/class_photon_1_1_pun_1_1_photon_network.html).
