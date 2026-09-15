# Getting started

## Character setup

Add an Animator and MontagePlayer to the same GameObject. The player captures the Animator's RuntimeAnimatorController as its base playable when enabled.

## Asset setup

Create a Montage Asset from **Assets > Create > Montage > Montage Asset**. Add a slot track, then add one or more animation segments. Segment `Duration` may be zero to use the remaining source clip duration automatically.

Sections use absolute montage time. `Next Section` may be empty to end playback or name another section to create a chain or loop.

## Partial-body playback

Assign an AvatarMask to an `UpperBody` slot track. Leave the mask empty for a full-body override. Mark a track Additive only when its source animations were authored for additive playback.

The mask and additive mode of a named slot must remain consistent across montage assets used by one player.

## Notifies

Create a class derived from `MontageNotifyAsset`. A zero-duration entry invokes `OnNotify`. A duration entry invokes `OnBegin`, `OnTick`, and `OnEnd`. Active state notifies are closed immediately when playback is interrupted or stopped.
