# Montage preview

Open **Window > Animation > Unity Montage**, then assign a MontageAsset and a character Prefab or model containing an Animator.

The preview runs in an isolated Preview Scene and uses a manual PlayableGraph. It does not add objects to the active scene and does not execute Montage Notify assets.

The character preview includes a perspective ground grid with highlighted world axes. The timeline also draws vertical time-grid lines through every lane.

## Playback controls

- Play and pause the selected range.
- Stop and return to the range start.
- Step backward or forward at the frame rate configured beside Snap.
- Loop playback.
- Select speeds from 0.25x to 2x.
- Preview the entire montage or one Section.
- Drag the time slider or timeline ruler to sample an exact pose.

Enable **Include Animator Controller base pose** to evaluate the preview character's controller below the Montage layers. It is disabled by default so controller behaviours cannot unexpectedly influence a montage-only preview.

## Camera controls

- Right mouse drag: orbit.
- Middle mouse drag: pan.
- Mouse wheel: zoom.
- F while the pointer is over the preview: frame the character.

## Audio and VFX lanes

The timeline contains dedicated **Audio** and **VFX** lanes. Add an event at the current playhead by assigning an AudioClip or Prefab in the event toolbar and pressing the matching add button. Assets can also be dragged directly from the Project window onto a lane; their horizontal drop position becomes the event time.

- Drag an event block horizontally to change its time.
- Left-click an event to edit timing and payload references in Details. Use Edit Notify Payload to open its asset parameters.
- Right-click an event to duplicate or remove it.
- Audio events expose clip, volume, pitch, spatial blend, attachment path, and local offset.
- VFX events expose prefab, attachment path, offsets, parenting, and lifetime.

Audio and VFX events use built-in Montage Notify assets and execute during runtime playback. The isolated editor preview intentionally visualizes but does not invoke notify callbacks, preventing sound or instantiated effects while scrubbing.

## Adjustable workspace

Drag the divider beneath the preview to resize its height. Drag the divider between the preview and Details to resize their widths. These sizes and the Details visibility survive editor window serialization. Use Details to collapse the property pane and Reset Layout to restore default sizes. Authoring actions and timeline options occupy separate toolbar rows to fit narrower windows.

## Animation editing

Add a Slot, then drag one or more AnimationClips from Project onto its lane. Clips use the first available gap at or after the drop position. The first clip creates a Default section, and appended content extends the final section.

Select a segment to edit its source clip, Clip In/Out, explicit Duration, Play Rate, Loop Count and IK flags. Drag its body to move it within a gap. Drop across a neighbouring segment's midpoint to reorder and pack the track. Drag either edge to trim it. The left edge advances the source boundary. Right-click to duplicate, open the source asset, add a section or delete the segment.

## Sections and notify windows

Add a Section at the playhead or from the timeline context menu. Splitting a section transfers its outgoing link to the new section and links the first part to the second. Section names and destination links are editable in Details. Rename updates incoming links. Drag a section boundary to resize adjacent ranges. The first boundary remains at zero.

Section Links provides a destination selector for every section. Select itself for a loop or End to finish. Enable Follow links during entire-montage preview to inspect authored playback order. The preview Loop button restarts the montage when the link chain ends.

Add Notify creates a named point event. Add State creates a named duration window. Set Track in Details to place an entry on another notify lane. Existing MontageNotifyAsset resources can be dropped onto a notify lane. Drag entries to move them and drag a state's edges to resize it. The Name is available to runtime event listeners even when the payload asset is empty.

Use Snap and the FPS field for frame alignment, Zoom for timeline magnification, and the horizontal scrollbar to reach the remaining duration. Undo/Redo supports authoring operations and groups each drag into one edit.

## Float curves

Add Curve creates a named curve track. Click its curve field to edit keys in Unity's curve editor. The time axis uses montage seconds. Select the track header to rename it in Details. Gameplay samples curves through GetCurveValue.
