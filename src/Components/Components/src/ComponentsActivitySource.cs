// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components.Infrastructure;

namespace Microsoft.AspNetCore.Components;

/// <summary>
/// This is instance scoped per renderer
/// </summary>
internal class ComponentsActivitySource
{
    internal const string Name = "Microsoft.AspNetCore.Components";
    internal const string OnRouteName = $"{Name}.Navigate";
    internal const string OnEventName = $"{Name}.HandleEvent";

    private static ActivitySource ActivitySource { get; } = new ActivitySource(Name);
    private ComponentsActivityLinkStore? _componentsActivityLinkStore;

    // Consecutive events are usually handled by the same few handlers, such as the keydown and input events of a
    // text box, so the display names of the most recent handlers are reused rather than formatted for every event.
    private readonly EventDisplayName?[] _eventDisplayNames = new EventDisplayName?[4];
    private int _nextEventDisplayName;

    // there is no System.Diagnostics.ActivitySource.IsSupported yet
    [FeatureSwitchDefinition("System.Diagnostics.Metrics.Meter.IsSupported")]
    internal static bool IsSupported { get; } =
        AppContext.TryGetSwitch("System.Diagnostics.Metrics.Meter.IsSupported", out var isSupported) ? isSupported : true;

    public void Init(ComponentsActivityLinkStore store)
    {
        _componentsActivityLinkStore = store;
    }

    public ComponentsActivityHandle StartNavigateActivity(string componentType, string route)
    {
        var activity = ActivitySource.CreateActivity(OnRouteName, ActivityKind.Internal, parentId: null, null, null);
        if (activity is not null)
        {
            var httpActivity = Activity.Current;
            activity.DisplayName = $"Route {route ?? "[unknown path]"} -> {componentType ?? "[unknown component]"}";
            Activity.Current = null; // do not inherit the parent activity
            activity.Start();

            if (activity.IsAllDataRequested)
            {
                if (componentType != null)
                {
                    activity.SetTag("aspnetcore.components.type", componentType);
                }
                if (route != null)
                {
                    activity.SetTag("aspnetcore.components.route", route);

                    // store self link
                    _componentsActivityLinkStore!.SetActivityContext(ComponentsActivityLinkStore.Route, activity.Context,
                        new KeyValuePair<string, object?>("aspnetcore.components.route", route));
                }
            }

            return new ComponentsActivityHandle { Activity = activity, Previous = httpActivity };
        }
        return default;
    }

    public void StopNavigateActivity(ComponentsActivityHandle activityHandle, Exception? ex)
    {
        StopComponentActivity(ComponentsActivityLinkStore.Route, activityHandle, ex);
    }

    public ComponentsActivityHandle StartHandleEventActivity(string? componentType, string? methodName, string? attributeName)
    {
        var activity = ActivitySource.CreateActivity(OnEventName, ActivityKind.Internal, parentId: null, null, null);

        if (activity is not null)
        {
            var previousActivity = Activity.Current;
            activity.DisplayName = GetEventDisplayName(componentType, methodName, attributeName);
            Activity.Current = null; // do not inherit the parent activity
            activity.Start();

            if (activity.IsAllDataRequested)
            {
                if (componentType != null)
                {
                    activity.SetTag("aspnetcore.components.type", componentType);
                }
                if (methodName != null)
                {
                    activity.SetTag("code.function.name", methodName);
                }
                if (attributeName != null)
                {
                    activity.SetTag("aspnetcore.components.attribute.name", attributeName);
                }
            }

            return new ComponentsActivityHandle { Activity = activity, Previous = previousActivity };
        }
        return default;
    }

    private string GetEventDisplayName(string? componentType, string? methodName, string? attributeName)
    {
        var displayNames = _eventDisplayNames;
        for (var i = 0; i < displayNames.Length; i++)
        {
            if (displayNames[i] is { } cached && cached.IsFor(componentType, methodName, attributeName))
            {
                return cached.DisplayName;
            }
        }

        var displayName = $"Event {attributeName ?? "[unknown attribute]"} -> {componentType ?? "[unknown component]"}.{methodName ?? "[unknown method]"}";

        var index = _nextEventDisplayName;
        displayNames[index] = new EventDisplayName(componentType, methodName, attributeName, displayName);
        _nextEventDisplayName = (index + 1) % displayNames.Length;

        return displayName;
    }

    public void StopHandleEventActivity(ComponentsActivityHandle activityHandle, Exception? ex)
    {
        StopComponentActivity(ComponentsActivityLinkStore.Event, activityHandle, ex);
    }

    public async Task CaptureHandleEventStopAsync(Task task, ComponentsActivityHandle activityHandle)
    {
        try
        {
            await task;
            StopHandleEventActivity(activityHandle, null);
        }
        catch (Exception ex)
        {
            StopHandleEventActivity(activityHandle, ex);
        }
    }

    private void StopComponentActivity(string category, ComponentsActivityHandle activityHandle, Exception? ex)
    {
        var activity = activityHandle.Activity;
        if (activity != null && !activity.IsStopped)
        {
            if (ex != null)
            {
                activity.SetTag("error.type", ex.GetType().FullName);
                activity.SetStatus(ActivityStatusCode.Error);
            }
            if (activity.IsAllDataRequested)
            {
                _componentsActivityLinkStore!.AddActivityContexts(category, activity);
            }
            activity.Stop();

            if (Activity.Current == null && activityHandle.Previous != null && !activityHandle.Previous.IsStopped)
            {
                Activity.Current = activityHandle.Previous;
            }
        }
    }

    private sealed class EventDisplayName(string? componentType, string? methodName, string? attributeName, string displayName)
    {
        public string DisplayName { get; } = displayName;

        public bool IsFor(string? eventComponentType, string? eventMethodName, string? eventAttributeName) =>
            string.Equals(componentType, eventComponentType, StringComparison.Ordinal) &&
            string.Equals(methodName, eventMethodName, StringComparison.Ordinal) &&
            string.Equals(attributeName, eventAttributeName, StringComparison.Ordinal);
    }
}

/// <summary>
/// Named tuple for restoring the previous activity after stopping the current one.
/// </summary>
internal struct ComponentsActivityHandle
{
    public Activity? Previous;
    public Activity? Activity;
}
