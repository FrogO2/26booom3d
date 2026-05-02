# Dynamic Blood Projector Notes

## Goal

Move from the current frozen attack-time visibility snapshot to a projector model that can refresh visibility over time, while keeping reveal and blood appearance as separate systems.

## Current Code Anchor

- `Assets/Scripts/SprayPaint.cs` is still the attack/input entry point.
- `Assets/Scripts/FrozenProjectorManager.cs` still owns projector geometry plus the visibility atlas snapshot.
- `Assets/Scripts/BloodRevealManager.cs` and `Assets/Scripts/BloodFxManager.cs` upload reveal and blood appearance data separately.
- `Assets/Scripts/BloodRevealPass.cs` and `Assets/Scripts/BloodFxPass.cs` are the fullscreen URP passes.
- `Assets/Arts/Shaders/BloodRevealMask.shader` and `Assets/Arts/Shaders/BloodProjectorFx.shader` currently consume the frozen visibility atlas.

## Migration Direction

1. Keep `SprayPaint` responsible for attack input and projector registration only.
2. Split projector ownership from visibility refresh so a projector can be updated after it is created.
3. Keep reveal and blood FX separate, but make them read the same projector-space visibility result.
4. Replace one-shot attack-time visibility with a refreshable projector depth model.

## Bridge Implementation Added In This Iteration

1. `BloodFxManager` now uploads the same depth slice index and depth bias layout that the blood shader already expects.
2. `FrozenProjectorManager` gains a projector refresh API so an existing projector can recapture its visibility data.
3. `SprayPaint` can opt into refreshing the most recent projector every frame as a transitional step toward a fully dynamic projector pipeline.

## Next Implementation Steps

1. Replace the CPU raycast atlas refresh in `FrozenProjectorManager` with a projector-owned depth render path.
2. Move both fullscreen shaders to projector-space depth comparison against dynamic render targets.
3. Keep reveal and blood appearance independently toggleable while sharing the same visibility test.
