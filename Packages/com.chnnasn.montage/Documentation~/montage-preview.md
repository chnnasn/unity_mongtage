# Montage preview

Open **Window > Animation > Unity Montage**, then assign a MontageAsset and a character Prefab or model containing an Animator.

The preview runs in an isolated Preview Scene and uses a manual PlayableGraph. It does not add objects to the active scene and does not execute Montage Notify assets.

## Playback controls

- Play and pause the selected range.
- Stop and return to the range start.
- Step backward or forward at 30 FPS.
- Loop playback.
- Select speeds from 0.25x to 2x.
- Preview the entire montage or one Section.
- Drag the time slider or timeline ruler to sample an exact pose.

Enable **Include Animator Controller base pose** to evaluate the preview character's controller below the Montage layers. It is disabled by default so controller behaviours cannot unexpectedly influence a montage-only preview.

## Camera controls

- Left mouse drag: orbit.
- Middle mouse drag: pan.
- Mouse wheel: zoom.
- F while the pointer is over the preview: frame the character.

Notifies are highlighted as the playhead crosses point events or remains inside state events. Preview intentionally does not invoke the notify callbacks.
