# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

A Unity VR dungeon-crawler prototype ("Jogo de VR") built in Unity **6000.5.7f1**, targeting Android XR / Meta Quest headsets via OpenXR (`com.unity.xr.androidxr-openxr`, `com.unity.xr.meta-openxr`, `com.unity.xr.hands`), using XR Interaction Toolkit 3.5.1, URP 17.5.0, and the new Input System. Core loop: enemies path toward an objective via NavMesh; the player defends using a hand-tracked slingshot that fires bombs.

There is no CLI build, lint, or test setup — this is developed entirely through the Unity Editor. To work on it, open the project in Unity 6000.5.7f1 and use Play mode to test changes; there is no automated test suite to run.

## Language convention

All code comments, `Debug.Log` messages, and in-Inspector `[Header]` labels are written in **Portuguese (pt-BR)**. Keep new comments/logs in Portuguese to stay consistent with the existing codebase.

## Architecture

### Scenes
`Assets/Scenes/Gameplay.unity` is the main playable scene. `LD.unity` and `BasicScene.unity` are level/layout work scenes; `SampleScene.unity` and `VFX.unity` are leftover template/test scenes.

### Enemy spawn & navigation
`SpawnManager` instantiates `enemyPrefab` (see `Assets/Prefabs/Inimigos/Enemy.prefab`) at random `spawnPoints` on a timer, for `gameDuration` seconds, and assigns each spawned `EnemyAI` an `objective` Transform. Enemies use `NavMeshAgent` to walk toward that objective; on arrival they self-destroy (reaching the dungeon currently has no other gameplay consequence, e.g. no player-health loss is wired up).

### Slingshot combat (`SlingshotController`)
Two-handed VR interaction driven by `InputActionReference`s (left/right trigger):
- **Left trigger held**: spawns a slingshot prefab attached to `leftHandTransform`; a `SlingshotZoneDetector` (added dynamically) watches for the object tagged `"RightHand"` entering its trigger zone, toggling `isRightHandInZone` and the slingshot's ready/idle color.
- **Right trigger pressed**, only while the right hand is in that zone: spawns a `Projectile_Bomb` (kinematic, collisions ignored against `playerColliders`) and begins drawing a simulated trajectory (`LineRenderer`, gravity-stepped with `Physics.Linecast` collision preview).
- **Right trigger released**: computes launch velocity from the pull vector between the two hand transforms (`pullForceCurve` over normalized pull distance), applies velocity + randomized spin proportional to launch force, and re-enables the bomb's collider.
- Overpulling past `breakThreshold` risks "arrebentando" (the slingshot breaking), rolling `maxBreakChance` and dropping the bomb harmlessly instead of launching it.
- There is a small (`giantBombChance`) chance any launched bomb becomes a "giant" bomb (scaled up, AoE explosion instead of direct-hit only).

### Damage: zone-based hit system (`EnemyAI` + `Projectile_Bomb`)
Each `EnemyAI` defines a `ZoneConfig[] zones` (Weak/Medium/Strong/Critical), each backed by a `BoxCollider` and a `damageMultiplier`. `EnemyAI.OnValidate` runs an editor-time auto-layout: changing one zone's `heightPercent` in the Inspector proportionally redistributes the remaining percentage across the other zones (keeping the total at 100%), then `RecalculateZoneLayout` repositions/resizes each zone's `BoxCollider` top-down along local Y, with the **last** array element rendered at the top (so `Critical` being last means it's automatically the head/top zone). This is non-obvious inspector-time logic — be careful when editing `zones` array order or `heightPercent` defaults.

On collision, `Projectile_Bomb` looks up which zone was hit via `EnemyAI.GetZoneByCollider(hitCollider)`, applies `zone.damageMultiplier` (and marks the hit as critical if `ZoneType.Critical`), and calls `EnemyAI.TakeDamage`. Giant bombs instead `Physics.OverlapSphere` at `giantExplosionRadius` and apply zone-based damage to every enemy caught in the blast.

### Feedback systems
- `DamageNumberManager` (singleton, `Instance`) spawns/stacks floating combat text (`DamageNumber`) per enemy; hits within `stackWindow` seconds accumulate into one number instead of spawning a new popup.
- `EnemyHealthDisplay` shows a TMP world-space health readout above each enemy, billboarded to the camera, color-coded by remaining health percentage, optionally hidden until the enemy takes its first hit.
- `EnemyVisualRandomizer.Initialize()` (called from `EnemyAI.Awake`) picks a random visual prefab + `Avatar` + walk animation clip (via `AnimatorOverrideController`) per spawned enemy, and returns the resulting renderers so `EnemyAI` can drive hit-flash feedback across the chosen model.

### Visual/atmosphere scripts
`TorchFlicker`, `ExplosionEffect`, and `ExplosionLightFlash` are small standalone effect scripts (flame flicker, explosion VFX lifecycle, explosion light pulse) with no cross-dependencies on the gameplay systems above.
