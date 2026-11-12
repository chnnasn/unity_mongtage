using UnityEngine;

namespace ChnnAsn.Montage
{
    public sealed partial class MontagePlayer
    {
        private static bool CanDispatch(MontageRuntimeInstance instance) =>
            !instance.Removed && (!instance.FadingOut || instance.StopReason == MontageStopReason.Completed);

        private void UpdateInstance(MontageRuntimeInstance instance, float deltaTime)
        {
            if (instance.Paused)
                return;

            if (instance.HoldTime || !CanDispatch(instance))
            {
                if (!instance.HoldTime)
                    instance.Time = Mathf.Min(instance.Asset.Duration, instance.Time + deltaTime * instance.PlayRate);
                UpdateFade(instance, deltaTime);
            }
            else
                AdvanceTime(instance, deltaTime);

            if (!instance.Removed && initialized)
                UpdateTrackPlayables(instance);
        }

        private void AdvanceTime(MontageRuntimeInstance instance, float deltaTime)
        {
            var remaining = deltaTime;
            var transitions = 0;
            // Split at event boundaries so a notify can branch before later events fire.
            // The independent iteration cap also covers callbacks that repeatedly seek backwards.
            for (var guard = 0; guard < 4096 && remaining > 0f; guard++)
            {
                if (instance.Removed || instance.Paused)
                    return;
                if (instance.FadingOut && instance.Weight <= 0f)
                    return;
                if (instance.HoldTime || !CanDispatch(instance))
                {
                    UpdateFade(instance, remaining);
                    return;
                }

                var section = instance.Asset.FindSection(instance.CurrentSection);
                var limit = section != null ? section.EndTime : instance.Asset.Duration;
                var next = GetNextSection(instance, section);
                var trigger = limit;
                if (next == null && instance.Asset.AutoBlendOut)
                {
                    var lead = instance.Asset.BlendOutTriggerTime < 0f
                        ? instance.Asset.BlendOut * instance.PlayRate
                        : instance.Asset.BlendOutTriggerTime;
                    trigger = Mathf.Max(section != null ? section.StartTime : 0f, limit - lead);
                    if (!instance.FadingOut && instance.Time >= trigger)
                    {
                        var revision = instance.PositionRevision;
                        BeginFadeOut(instance, instance.Asset.BlendOut, MontageStopReason.Completed);
                        if (instance.Removed || instance.PositionRevision != revision || !CanDispatch(instance))
                            continue;
                    }
                }

                var target = Mathf.Min(limit, instance.Time + remaining * instance.PlayRate);
                if (instance.FadingOut)
                    target = Mathf.Min(target, instance.Time + Mathf.Max(0f, instance.FadeDuration - instance.FadeElapsed) * instance.PlayRate);
                if (!instance.FadingOut && trigger > instance.Time)
                    target = Mathf.Min(target, trigger);
                for (var i = 0; i < instance.Asset.Notifies.Count; i++)
                {
                    var notify = instance.Asset.Notifies[i];
                    if (notify == null)
                        continue;
                    if (notify.Time > instance.Time)
                        target = Mathf.Min(target, notify.Time);
                    if (notify.IsState && notify.EndTime > instance.Time)
                        target = Mathf.Min(target, notify.EndTime);
                }

                // A tiny remainder can round back to the same float timestamp.
                if (target <= instance.Time && instance.Time < limit)
                {
                    UpdateFade(instance, remaining);
                    return;
                }
                var elapsed = Mathf.Max(0f, target - instance.Time) / instance.PlayRate;
                remaining = Mathf.Max(0f, remaining - elapsed);
                instance.Time = target;
                UpdateFade(instance, elapsed);
                var positionRevision = instance.PositionRevision;
                TickActiveNotifies(instance, elapsed);
                if (instance.Removed || instance.Paused)
                    return;
                if (positionRevision != instance.PositionRevision || !CanDispatch(instance))
                    continue;
                if (elapsed > 0f)
                    DispatchAtCurrentTime(instance, elapsed);
                if (instance.Removed || instance.Paused)
                    return;
                if (positionRevision != instance.PositionRevision || !CanDispatch(instance))
                    continue;

                if (instance.Time >= trigger && GetNextSection(instance, section) == null &&
                    instance.Asset.AutoBlendOut && !instance.FadingOut)
                {
                    BeginFadeOut(instance, instance.Asset.BlendOut, MontageStopReason.Completed);
                    if (instance.Removed || instance.PositionRevision != positionRevision || !CanDispatch(instance))
                        continue;
                }
                if (instance.Time >= limit)
                {
                    // A notify at the section end may have changed its next-section link.
                    next = GetNextSection(instance, section);
                    if (next != null && !instance.FadingOut)
                    {
                        if (++transitions > 32)
                            break;
                        if (next.StartTime == instance.Time)
                        {
                            var previous = instance.CurrentSection;
                            instance.CurrentSection = next.Name;
                            instance.PositionRevision++;
                            SectionChanged?.Invoke(new MontageHandle(this, instance.Id), previous, next.Name);
                        }
                        else
                            MoveTo(instance, next.StartTime, next.Name);
                    }
                    else
                    {
                        instance.HoldTime = true;
                        CloseActiveNotifies(instance, 0f);
                        if (instance.PositionRevision != positionRevision)
                            continue;
                        if (instance.Asset.AutoBlendOut && !instance.Removed)
                            BeginFadeOut(instance, instance.Asset.BlendOut, MontageStopReason.Completed);
                        UpdateFade(instance, remaining);
                        return;
                    }
                }
            }

            if (remaining > 0f && !instance.Removed)
            {
                Debug.LogError($"Montage '{instance.Asset.name}' exceeded its per-frame transition limit.", instance.Asset);
                BeginFadeOut(instance, 0f, MontageStopReason.Stopped);
            }
        }

        private static MontageSection GetNextSection(MontageRuntimeInstance instance, MontageSection section)
        {
            if (section == null)
                return null;
            var nextName = instance.NextSections.TryGetValue(section.Name, out var overridden)
                ? overridden
                : section.NextSection;
            return instance.Asset.FindSection(nextName);
        }

        private void MoveTo(MontageRuntimeInstance instance, float time, string sectionName)
        {
            var revision = ++instance.PositionRevision;
            CloseActiveNotifies(instance, 0f);
            if (instance.Removed || instance.PositionRevision != revision || !CanDispatch(instance))
                return;
            var previous = instance.CurrentSection;
            instance.CurrentSection = sectionName;
            instance.Time = time;
            instance.HoldTime = false;
            UpdateTrackPlayables(instance);
            if (previous != sectionName)
                SectionChanged?.Invoke(new MontageHandle(this, instance.Id), previous, sectionName);
            if (instance.PositionRevision == revision && CanDispatch(instance))
                DispatchAtCurrentTime(instance, 0f);
        }

        private void DispatchAtCurrentTime(MontageRuntimeInstance instance, float deltaTime)
        {
            var revision = instance.PositionRevision;
            // End states first when two adjacent windows share a boundary.
            instance.NotifyScratch.Clear();
            instance.NotifyScratch.AddRange(instance.ActiveNotifies);
            for (var i = 0; i < instance.NotifyScratch.Count; i++)
            {
                var notify = instance.NotifyScratch[i];
                if (instance.Time >= notify.EndTime && instance.ActiveNotifies.Remove(notify))
                    DispatchStateEnd(instance, notify, deltaTime);
                if (!CanDispatch(instance) || instance.PositionRevision != revision)
                    return;
            }

            for (var i = 0; i < instance.Asset.Notifies.Count; i++)
            {
                var notify = instance.Asset.Notifies[i];
                if (notify == null)
                    continue;
                var context = CreateContext(instance, notify, deltaTime);
                if (notify.IsState)
                {
                    if (instance.Time >= notify.Time && instance.Time < notify.EndTime && instance.ActiveNotifies.Add(notify))
                    {
                        notify.Asset?.OnBegin(context);
                        NotifyStateBegin?.Invoke(context);
                    }
                }
                else if (notify.Time == instance.Time)
                {
                    notify.Asset?.OnNotify(context);
                    NotifyTriggered?.Invoke(context);
                }
                if (!CanDispatch(instance) || instance.PositionRevision != revision)
                    return;
            }
        }

        private void TickActiveNotifies(MontageRuntimeInstance instance, float deltaTime)
        {
            if (deltaTime <= 0f)
                return;
            var revision = instance.PositionRevision;
            instance.NotifyScratch.Clear();
            instance.NotifyScratch.AddRange(instance.ActiveNotifies);
            for (var i = 0; i < instance.NotifyScratch.Count; i++)
            {
                var notify = instance.NotifyScratch[i];
                var context = CreateContext(instance, notify, deltaTime);
                notify.Asset?.OnTick(context);
                NotifyStateTick?.Invoke(context);
                if (!CanDispatch(instance) || instance.PositionRevision != revision)
                    return;
            }
        }

        private MontageNotifyContext CreateContext(MontageRuntimeInstance instance, MontageNotify notify, float deltaTime) =>
            new MontageNotifyContext(this, instance.Asset, new MontageHandle(this, instance.Id), notify, instance.Time, deltaTime);

        private void DispatchStateEnd(MontageRuntimeInstance instance, MontageNotify notify, float deltaTime)
        {
            var context = CreateContext(instance, notify, deltaTime);
            notify.Asset?.OnEnd(context);
            NotifyStateEnd?.Invoke(context);
        }

        private void CloseActiveNotifies(MontageRuntimeInstance instance, float deltaTime)
        {
            if (instance.ActiveNotifies.Count == 0)
                return;
            var closing = new MontageNotify[instance.ActiveNotifies.Count];
            instance.ActiveNotifies.CopyTo(closing);
            // Clear before invoking user code, which can stop or jump again.
            instance.ActiveNotifies.Clear();
            for (var i = 0; i < closing.Length; i++)
                DispatchStateEnd(instance, closing[i], deltaTime);
        }

        private void BeginFadeOut(MontageRuntimeInstance instance, float duration, MontageStopReason reason)
        {
            if (instance.Removed)
                return;
            instance.Paused = false;
            if (instance.FadingOut)
            {
                if (reason != MontageStopReason.Completed && instance.StopReason == MontageStopReason.Completed)
                {
                    instance.StopReason = reason;
                    CloseActiveNotifies(instance, 0f);
                    if (reason == MontageStopReason.Interrupted)
                        Interrupted?.Invoke(new MontageHandle(this, instance.Id));
                }
                if (duration <= 0f)
                {
                    instance.FadeDuration = 0f;
                    instance.Weight = 0f;
                }
                return;
            }
            instance.FadingIn = false;
            instance.FadingOut = true;
            instance.FadeElapsed = 0f;
            instance.FadeDuration = Mathf.Max(0f, duration);
            instance.FadeStartWeight = instance.Weight;
            instance.StopReason = reason;
            if (instance.FadeDuration <= 0f)
                instance.Weight = 0f;
            if (reason != MontageStopReason.Completed)
                CloseActiveNotifies(instance, 0f);
            var handle = new MontageHandle(this, instance.Id);
            BlendingOut?.Invoke(handle, reason);
            if (reason == MontageStopReason.Interrupted)
                Interrupted?.Invoke(handle);
        }

        private static void UpdateFade(MontageRuntimeInstance instance, float deltaTime)
        {
            if (!instance.FadingIn && !instance.FadingOut)
                return;
            instance.FadeElapsed += deltaTime;
            var t = instance.FadeDuration <= 0f ? 1f : Mathf.Clamp01(instance.FadeElapsed / instance.FadeDuration);
            var curve = instance.FadingOut ? instance.Asset.BlendOutCurve : instance.Asset.BlendInCurve;
            var value = t >= 1f ? 1f : (curve == null ? t : Mathf.Clamp01(curve.Evaluate(t)));
            instance.Weight = instance.FadingOut ? instance.FadeStartWeight * (1f - value) : value;
            if (t >= 1f)
                instance.FadingIn = false;
        }
    }
}
