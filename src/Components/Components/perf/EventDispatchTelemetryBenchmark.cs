// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.AspNetCore.Components.Performance;

public class EventDispatchTelemetryBenchmark
{
    private ActivityListener _listener;
    private EventRenderer _renderer;
    private ulong[] _eventHandlerIds;
    private int _nextHandler;

    [Params(false, true)]
    public bool Tracing { get; set; }

    // The number of different event handlers that the events are dispatched to in turn.
    [Params(1, 2, 8)]
    public int Handlers { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        if (Tracing)
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == ComponentsActivitySource.Name,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            };
            ActivitySource.AddActivityListener(_listener);
        }

        _renderer = new EventRenderer();

        var component = new EventHandlersComponent(Handlers);
        var componentId = _renderer.AssignRootComponentId(component);
        _renderer.RenderRootComponentAsync(componentId, ParameterView.Empty).GetAwaiter().GetResult();

        _eventHandlerIds = _renderer.EventHandlerIds.ToArray();
        if (_eventHandlerIds.Length != Handlers)
        {
            throw new InvalidOperationException($"Expected {Handlers} event handlers but found {_eventHandlerIds.Length}.");
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _renderer.Dispose();
        _listener?.Dispose();
    }

    [Benchmark]
    public Task DispatchEvent()
    {
        var eventHandlerIds = _eventHandlerIds;
        var index = _nextHandler;
        _nextHandler = index + 1 == eventHandlerIds.Length ? 0 : index + 1;

        return _renderer.DispatchEventAsync(eventHandlerIds[index], fieldInfo: null, EventArgs.Empty);
    }

    // Renders one button per handler. Handlers are plain delegates, so dispatching an event doesn't re-render the component.
    private sealed class EventHandlersComponent(int handlers) : IComponent
    {
        private RenderHandle _renderHandle;

        public int Count { get; private set; }

        public void Attach(RenderHandle renderHandle) => _renderHandle = renderHandle;

        public Task SetParametersAsync(ParameterView parameters)
        {
            _renderHandle.Render(builder =>
            {
                Action[] callbacks = [OnClick0, OnClick1, OnClick2, OnClick3, OnClick4, OnClick5, OnClick6, OnClick7];

                for (var i = 0; i < handlers; i++)
                {
                    builder.OpenElement(0, "button");
                    builder.AddAttribute(1, "onclick", callbacks[i]);
                    builder.CloseElement();
                }
            });

            return Task.CompletedTask;
        }

        private void OnClick0() => Count++;
        private void OnClick1() => Count++;
        private void OnClick2() => Count++;
        private void OnClick3() => Count++;
        private void OnClick4() => Count++;
        private void OnClick5() => Count++;
        private void OnClick6() => Count++;
        private void OnClick7() => Count++;
    }

    private sealed class EventRenderer : Renderer
    {
        public EventRenderer()
            : base(new ActivitySourceServiceProvider(), NullLoggerFactory.Instance)
        {
        }

        public List<ulong> EventHandlerIds { get; } = [];

        // Runs work inline so that events can be dispatched from the benchmark thread.
        public override Dispatcher Dispatcher { get; } = new InlineDispatcher();

        protected override void HandleException(Exception exception) => throw exception;

        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch)
        {
            var frames = renderBatch.ReferenceFrames;
            for (var i = 0; i < frames.Count; i++)
            {
                ref var frame = ref frames.Array[i];
                if (frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeEventHandlerId != 0)
                {
                    EventHandlerIds.Add(frame.AttributeEventHandlerId);
                }
            }

            return Task.CompletedTask;
        }
    }

    private sealed class ActivitySourceServiceProvider : IServiceProvider
    {
        private readonly ComponentsActivitySource _activitySource = new();

        public object GetService(Type serviceType) => serviceType == typeof(ComponentsActivitySource) ? _activitySource : null;
    }

    private sealed class InlineDispatcher : Dispatcher
    {
        public override bool CheckAccess() => true;

        public override Task InvokeAsync(Action workItem)
        {
            workItem();
            return Task.CompletedTask;
        }

        public override Task InvokeAsync(Func<Task> workItem) => workItem();

        public override Task<TResult> InvokeAsync<TResult>(Func<TResult> workItem) => Task.FromResult(workItem());

        public override Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> workItem) => workItem();
    }
}
