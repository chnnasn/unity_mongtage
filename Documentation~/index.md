# Unity Montage

Unity Montage layers short-lived gameplay animation over a character's existing base animation. Locomotion can continue to use an Animator Controller while attacks, reloads, interactions, hit reactions, and abilities use Montage assets.

## Runtime graph

```text
AnimatorControllerPlayable ──────────────┐
UpperBody Slot Mixer ────────────────────┤
FullBody Slot Mixer ─────────────────────┼─ AnimationLayerMixerPlayable
Additive Slot Mixer ─────────────────────┘              │
                                                AnimationPlayableOutput
```

`MontagePlayer` owns the graph. Game code should not retain or modify its Playable nodes.

## Main types

- `MontageAsset`: serialized, reusable montage data.
- `MontagePlayer`: character component and public playback API.
- `MontageHandle`: control and query object for one playback.
- `MontagePlayOptions`: start section, play rate, priority, and blend-in options.
- `MontageNotifyAsset`: extension point for point and state notifications.
- `IMontageRootMotionReceiver`: optional integration with a character motor.

See [Getting Started](getting-started.md), [Montage Preview](montage-preview.md), and [Runtime API](runtime-api.md).
