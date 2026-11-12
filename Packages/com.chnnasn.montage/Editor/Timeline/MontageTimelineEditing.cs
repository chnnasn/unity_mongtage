using System;
using UnityEditor;
using UnityEngine;

namespace ChnnAsn.Montage.Editor
{
    internal static class MontageTimelineEditing
    {
        internal static int AddSlot(MontageAsset asset)
        {
            var serialized = new SerializedObject(asset);
            var tracks = serialized.FindProperty("slotTracks");
            var index = tracks.arraySize++;
            var track = tracks.GetArrayElementAtIndex(index);
            var name = index == 0 ? "FullBody" : "Slot" + index;
            while (HasSlot(asset, name))
                name += "_Copy";
            track.FindPropertyRelative("slot").stringValue = name;
            track.FindPropertyRelative("avatarMask").objectReferenceValue = null;
            track.FindPropertyRelative("additive").boolValue = false;
            track.FindPropertyRelative("segments").ClearArray();
            serialized.ApplyModifiedProperties();
            return index;
        }

        private static bool HasSlot(MontageAsset asset, string name)
        {
            foreach (var track in asset.SlotTracks)
                if (track != null && track.Slot == name)
                    return true;
            return false;
        }

        internal static int AddSegment(MontageAsset asset, int trackIndex, AnimationClip clip, float time)
        {
            if (clip == null || clip.legacy || clip.length <= 0f)
                return -1;
            var previousDuration = asset.Duration;
            var serialized = new SerializedObject(asset);
            var segments = GetSegments(serialized, trackIndex);
            var start = Mathf.Max(0f, time);
            var index = 0;
            // Find the first available gap at or after the drop position.
            while (index < asset.SlotTracks[trackIndex].Segments.Count)
            {
                var segment = asset.SlotTracks[trackIndex].Segments[index];
                if (start + clip.length <= segment.StartTime)
                    break;
                start = Mathf.Max(start, segment.EndTime);
                index++;
            }
            segments.InsertArrayElementAtIndex(index);
            var entry = segments.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("clip").objectReferenceValue = clip;
            entry.FindPropertyRelative("startTime").floatValue = start;
            entry.FindPropertyRelative("clipIn").floatValue = 0f;
            entry.FindPropertyRelative("clipOut").floatValue = 0f;
            entry.FindPropertyRelative("duration").floatValue = 0f;
            entry.FindPropertyRelative("playRate").floatValue = 1f;
            entry.FindPropertyRelative("loop").boolValue = false;
            entry.FindPropertyRelative("loopCount").intValue = 1;
            entry.FindPropertyRelative("applyFootIK").boolValue = false;
            entry.FindPropertyRelative("applyPlayableIK").boolValue = false;
            serialized.ApplyModifiedProperties();
            ExtendFinalSection(asset, previousDuration);
            return index;
        }

        private static void ExtendFinalSection(MontageAsset asset, float previousDuration)
        {
            if (asset.Sections.Count == 0)
            {
                AddSection(asset, 0f);
                return;
            }
            if (asset.Duration <= previousDuration)
                return;
            var serialized = new SerializedObject(asset);
            var sections = serialized.FindProperty("sections");
            for (var i = 0; i < asset.Sections.Count; i++)
                if (Mathf.Approximately(asset.Sections[i].EndTime, previousDuration))
                    sections.GetArrayElementAtIndex(i).FindPropertyRelative("endTime").floatValue = asset.Duration;
            serialized.ApplyModifiedProperties();
        }

        internal static SerializedProperty GetSegments(SerializedObject serialized, int trackIndex) =>
            serialized.FindProperty("slotTracks").GetArrayElementAtIndex(trackIndex).FindPropertyRelative("segments");

        internal static void ReorderSegment(MontageAsset asset, int trackIndex, int from, int to)
        {
            var serialized = new SerializedObject(asset);
            var segments = GetSegments(serialized, trackIndex);
            if (from < 0 || to < 0 || from >= segments.arraySize || to >= segments.arraySize)
                return;
            var start = asset.SlotTracks[trackIndex].Segments[0].StartTime;
            segments.MoveArrayElement(from, to);
            serialized.ApplyModifiedProperties();
            serialized.Update();
            segments = GetSegments(serialized, trackIndex);
            for (var i = 0; i < segments.arraySize; i++)
            {
                segments.GetArrayElementAtIndex(i).FindPropertyRelative("startTime").floatValue = start;
                start += asset.SlotTracks[trackIndex].Segments[i].Duration;
            }
            serialized.ApplyModifiedProperties();
        }

        internal static int AddSection(MontageAsset asset, float time)
        {
            if (asset.Duration <= 0f)
                return -1;
            time = Mathf.Clamp(time, 0f, asset.Duration);
            if (time >= asset.Duration)
                return -1;
            if (asset.Sections.Count == 0 && time > 0f)
                AddSection(asset, 0f);
            for (var i = 0; i < asset.Sections.Count; i++)
                if (Mathf.Approximately(asset.Sections[i].StartTime, time))
                    return i;

            var name = asset.Sections.Count == 0 ? "Default" : "Section" + asset.Sections.Count;
            while (asset.FindSection(name) != null)
                name += "_Copy";
            var serialized = new SerializedObject(asset);
            var sections = serialized.FindProperty("sections");
            var end = asset.Duration;
            var next = string.Empty;
            for (var i = 0; i < asset.Sections.Count; i++)
            {
                var existing = asset.Sections[i];
                if (existing.StartTime < time && existing.EndTime > time)
                {
                    end = existing.EndTime;
                    next = existing.NextSection;
                    var property = sections.GetArrayElementAtIndex(i);
                    property.FindPropertyRelative("endTime").floatValue = time;
                    property.FindPropertyRelative("nextSection").stringValue = name;
                }
                else if (existing.StartTime > time)
                    end = Mathf.Min(end, existing.StartTime);
            }
            var index = sections.arraySize++;
            var section = sections.GetArrayElementAtIndex(index);
            section.FindPropertyRelative("id").stringValue = Guid.NewGuid().ToString("N");
            section.FindPropertyRelative("name").stringValue = name;
            section.FindPropertyRelative("startTime").floatValue = time;
            section.FindPropertyRelative("endTime").floatValue = end;
            section.FindPropertyRelative("nextSection").stringValue = next;
            serialized.ApplyModifiedProperties();
            return index;
        }

        internal static void RenameSection(MontageAsset asset, int index, string name)
        {
            if (string.IsNullOrWhiteSpace(name) || asset.Sections[index].Name == name || asset.FindSection(name) != null)
                return;
            var previous = asset.Sections[index].Name;
            var serialized = new SerializedObject(asset);
            var sections = serialized.FindProperty("sections");
            sections.GetArrayElementAtIndex(index).FindPropertyRelative("name").stringValue = name;
            for (var i = 0; i < sections.arraySize; i++)
            {
                var next = sections.GetArrayElementAtIndex(i).FindPropertyRelative("nextSection");
                if (next.stringValue == previous)
                    next.stringValue = name;
            }
            serialized.ApplyModifiedProperties();
        }

        internal static void RemoveSection(MontageAsset asset, int index)
        {
            var removed = asset.Sections[index];
            var serialized = new SerializedObject(asset);
            var sections = serialized.FindProperty("sections");
            for (var i = 0; i < asset.Sections.Count; i++)
            {
                if (i == index)
                    continue;
                var other = sections.GetArrayElementAtIndex(i);
                if (other.FindPropertyRelative("nextSection").stringValue == removed.Name)
                    other.FindPropertyRelative("nextSection").stringValue = removed.NextSection == removed.Name ? string.Empty : removed.NextSection;
                if (Mathf.Approximately(asset.Sections[i].EndTime, removed.StartTime))
                    other.FindPropertyRelative("endTime").floatValue = removed.EndTime;
                if (removed.StartTime == 0f && Mathf.Approximately(asset.Sections[i].StartTime, removed.EndTime))
                    other.FindPropertyRelative("startTime").floatValue = 0f;
            }
            sections.DeleteArrayElementAtIndex(index);
            serialized.ApplyModifiedProperties();
        }

        internal static int AddNotify(MontageAsset asset, float time, bool state, int lane, MontageNotifyAsset payload = null)
        {
            var serialized = new SerializedObject(asset);
            var notifies = serialized.FindProperty("notifies");
            var index = notifies.arraySize++;
            var entry = notifies.GetArrayElementAtIndex(index);
            time = Mathf.Clamp(time, 0f, Mathf.Max(0f, asset.Duration - (state ? 0.01f : 0f)));
            entry.FindPropertyRelative("id").stringValue = Guid.NewGuid().ToString("N");
            entry.FindPropertyRelative("eventName").stringValue = state ? "NotifyState" : "Notify";
            entry.FindPropertyRelative("track").intValue = lane;
            entry.FindPropertyRelative("time").floatValue = time;
            entry.FindPropertyRelative("duration").floatValue = state ? Mathf.Min(0.25f, asset.Duration - time) : 0f;
            entry.FindPropertyRelative("notify").objectReferenceValue = payload;
            serialized.ApplyModifiedProperties();
            return index;
        }
    }
}
