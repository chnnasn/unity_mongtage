# Runtime API

## Playback

```csharp
// Uses the asset's Default Play Rate.
MontageHandle handle = player.Play(montage);

MontageHandle combo = player.Play(
    montage,
    MontagePlayOptions.Default
        .WithSection("Attack01")
        .WithPlayRate(1.1f)
        .WithPriority(100)
        .WithBlendIn(0.1f));

// WithStartTime clears StartSection. WithSection takes precedence if set later.
MontageHandle positioned = player.Play(montage,
    MontagePlayOptions.Default.WithStartTime(0.6f));
```

Validation failures, missing start sections, disabled players and higher-priority slot/group owners return an invalid handle. Explicit options use their positive PlayRate, with non-positive values falling back to the asset rate.

## Playback control

```csharp
handle.Pause();
handle.Resume();
handle.SetPosition(0.75f);
handle.SetNextSection("Attack01", "Attack02");
handle.SetNextSection("ReloadLoop", "ReloadLoop");
handle.SetNextSection("ReloadLoop", "ReloadEnd");
handle.ClearNextSection("ReloadLoop");
handle.JumpToSection("Finisher");
handle.SetPlayRate(1.25f);
handle.Stop(0.15f);
player.StopGroup("Combat", 0.15f);
float damageScale = handle.GetCurveValue("DamageScale");
```

Pause freezes time, blend progress and notify ticks. SetPosition clamps to the montage duration, updates the current section, closes active states and enters states covering the destination. Points at the destination fire once. Points skipped by the seek are omitted. JumpToSection applies the same state handling at the section start. Seeking and jumping require an instance that has not begun fading out.

SetNextSection stores an instance-local override. An empty destination ends that section. ClearNextSection restores the authored link. A linked section can loop until gameplay changes its destination. Positive montage rates advance the timeline, while negative segment rates reverse the source clip within its trimmed range.

## Slots and groups

Montages with the same non-empty Slot Group compete even when their Slot names differ. Shared Slots also compete. A higher-priority active instance rejects the incoming request before existing instances are interrupted. Empty groups retain the earlier slot-based arbitration. Masks and additive settings are shared by each Slot on a player.

## Blending and completion

Blend In Curve and Blend Out Curve map normalized blend progress to a value from zero to one. Blend Out uses that value as the amount removed from the starting weight. Curves are clamped and finish at their terminal weight.

Auto Blend Out starts on the final section. A negative Blend Out Trigger Time uses the blend duration adjusted for the current play rate. A non-negative value specifies lead time in montage seconds. Zero begins fading at the section end. Disabling Auto Blend Out holds the terminal pose until Stop or interruption. Linked sections continue without starting automatic blend out.

The negative default trigger begins fading before the section end. Set the trigger to zero to reproduce the earlier end-then-fade timing.

## Events

- Started fires before starting-position notifies.
- SectionChanged reports the previous and destination section names.
- NotifyTriggered delivers point events, including named entries with no asset payload.
- NotifyStateBegin, NotifyStateTick and NotifyStateEnd expose state windows.
- BlendingOut reports the reason when fade-out starts.
- Interrupted fires when another montage interrupts the instance.
- Ended reports Completed, Interrupted, Stopped or PlayerDisabled after removal.
- Completed follows Ended for natural completion.

Notify callbacks execute synchronously at timeline boundaries in chronological order. The context and handle both expose the boundary position. A callback may change the next section, seek, jump, stop or start another montage. Remaining frame time follows the resulting playback state. Adjacent sections share boundary points once and preserve spanning states. Discontinuous jumps and section loops close old states and enter destination states. State tick deltas cover the active portion of the frame.

Auto blend-out continues terminal-section notifications while playback advances. Explicit stops and interruptions close active states immediately. Cleanup removes each state before calling user code to support callbacks that reenter the player.

## Curves and clip timing

Named float curves use montage time in seconds. Evaluate them through `MontageAsset.EvaluateCurve(name, time)` or `handle.GetCurveValue(name)`. Missing names return zero. Gameplay can apply the resulting value to damage, IK, materials or morph weights.

Clip Out equal to zero uses the source clip end. Segment Duration equal to zero derives duration from Clip In, Clip Out, the absolute Play Rate and Loop Count. A positive Duration retains explicit timeline length. Runtime and editor preview share the same trimmed, reversed and looping clip-time calculation.

## Lifetime

A handle remains valid through its blend-out interval and becomes invalid before Ended. IsPaused and IsBlendingOut expose the current playback state. Stop with zero duration sets zero weight immediately, and the player releases the instance on its next update. Runtime section overrides belong to that handle and leave the asset reusable by other characters.
