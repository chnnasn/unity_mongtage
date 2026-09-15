# Runtime API

## Playback

```csharp
MontageHandle handle = player.Play(montage);

MontageHandle combo = player.Play(
    montage,
    MontagePlayOptions.Default
        .WithSection("Attack01")
        .WithPlayRate(1.1f)
        .WithPriority(100)
        .WithBlendIn(0.1f));
```

An invalid handle is returned when asset validation fails, a requested start section does not exist, or a higher-priority montage owns a shared slot.

## Runtime control

```csharp
handle.SetNextSection("Attack01", "Attack02");
handle.JumpToSection("Finisher");
handle.SetPlayRate(1.25f);
handle.Stop(0.15f);
```

## Events

`MontagePlayer` exposes `Started`, `SectionChanged`, `NotifyTriggered`, and `Ended`. `Ended` reports one of `Completed`, `Interrupted`, `Stopped`, or `PlayerDisabled`.

## Lifetime

A handle remains valid while its runtime instance exists, including its blend-out interval. It becomes invalid immediately before `Ended` is raised. Handles are value types and may be safely compared.
