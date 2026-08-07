# Rushhouse Unity Port

This Unity project is a direct migration target for the HTML Rushhouse prototype.

Open `Assets/Scenes/Main.unity` in Unity 6000.4.8f1 or newer.

Batch scene generation:
`Unity.exe -batchmode -quit -projectPath <this-folder> -executeMethod RushhouseSceneBuilder.BuildMainScene`

Windows playable build:
`Unity.exe -batchmode -quit -projectPath <this-folder> -executeMethod RushhouseSceneBuilder.BuildWindowsPlayer`

The Windows executable is written to `Builds/Windows/RushhouseUnity.exe`.

Current migrated systems:
- Portrait mobile/PC layout.
- Menu, studio shop, floorplan, recipe, play, and result screens.
- A true orthographic 3D room plane with textured floor slabs, volumetric walls/divider, soft shadows,
  and bounds-grounded FBX furniture/stations. The existing animated directional player, staff, and
  customers are preserved; carried food and dishes remain sprites for mobile readability.
- Grid restaurant with providers, tables, counters, hobs, prep, sink, trash, and workers.
- Service loop with orders, prep timing, cooking, plating, serving, complaints, queue pressure, staff automation, party-size tables, floorplan persistence, daily market choices, and save data.
- Tables can be configured for 1, 2, or 4 seats in floorplan mode; larger parties need multiple matching meals and drinks.
- Daily goals, rush hour, customer types, combo tips, star/reputation report, live service tickets, plate-building guidance, decor/marketing upgrades, and a zoomed portrait PC build.
- Four-direction rig-rendered character animation for walking, working, carrying, sitting, eating, standing, and leaving.
- Waiters now reserve a customer task, visibly travel with the order or dish, and complete the action only after reaching the table.

Character animation source files and the reproducible renderer are kept in `SourceArt/QuaterniusUltimateCharacters_CC0` and `Tools/render_quaternius_characters.py`; those source FBX files are not duplicated into the runtime build.

The original HTML build remains in `../rushhouse-grid` and a timestamped backup remains in `../rushhouse-grid-html-backup-*`.

## Audio

Four looping tracks (`Assets/Resources/Music`) rendered locally by
`Tools/music_gen.py` with Stable Audio 3 — menu, service, rush and day-end, each
at its own tempo so the crossfade reads as a gear change. Sound effects are
synthesised at runtime from sine tones; there are no sample libraries.

**Powered by Stability AI.** The music model is licensed under the Stability AI
Community License (Copyright (c) Stability AI Ltd. All Rights Reserved). See
[NOTICE](NOTICE) — commercial use is free under $1M annual revenue but must be
registered at <https://stability.ai/community-license>.

Regenerate with:

    python Tools/music_gen.py --steps 60
