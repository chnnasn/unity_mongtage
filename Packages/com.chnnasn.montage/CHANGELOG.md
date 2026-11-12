# Changelog

## [Unreleased]

### Added

- Editable animation lanes with drag/drop, trim, reorder, duplication and frame snapping.
- Section splitting, link editing, loop preview and rename-safe section references.
- Named notify/state tracks and montage float curves.
- Resizable preview/Details/timeline panes, serialized layout sizes and a collapsible Details pane.
- Slot Group arbitration, group stop, pause/resume, seeking and start positions.
- Source clip trimming, reverse segment playback and finite loop counts.
- Blend curves, automatic blend-out lead time, final-pose hold and lifecycle/state callbacks.

- Perspective world grid in the character preview and vertical grid lines across timeline lanes.
- Visual Audio and VFX event lanes with object-picker and Project-window drag-and-drop authoring.
- Draggable event timing, event selection, and context-menu removal.
- Built-in runtime `MontageAudioNotifyAsset` and `MontageVfxNotifyAsset` implementations.
- Humanoid `IKRetargeterAsset` with semantic source/target chain mappings and Retarget Pose assets.
- Dual-character IK Retargeter preview with chain overlays and corrected right-mouse camera controls.
- Runtime `MontageIKRetargeter` pose correction and dependency-free hand/foot two-bone IK.
- Target-skeleton AnimationClip baker with root-translation scaling.

### Fixed

- Notify callbacks can redirect playback without stale time writes or collection mutation failures.
- State lifetimes and tick deltas respect jumps, loops, adjacent sections and interruption.
- Point notifications are ordered by time and dispatch once at shared section boundaries.
- Immediate stop during an existing fade remains at zero weight.
- Play without explicit options uses the asset default play rate.

- Preview orbit now uses the right mouse button and follows the expected drag direction.

## [0.1.0] - 2026-09-15

### Added

- UPM package structure and assembly definitions.
- PlayableGraph-based `MontagePlayer`.
- Montage assets with slots, segments, sections, and notifies.
- Cross-fade, interruption, priority, and parallel slot playback.
- Root motion driver and receiver interface.
- Asset inspector, timeline overview, and project validator.
- Interactive character preview with manual PlayableGraph evaluation.
- Playback, loop, speed, frame stepping, scrubbing, and section-range controls.
- Orbit, pan, zoom, and frame controls in an isolated preview scene.
- Basic sample and editor tests.
