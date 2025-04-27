using System.Reflection;
using MadWizard.Insomnia.Configuration;

namespace MadWizard.Insomnia
{
    public abstract class Actor : EventSource, IDisposable
    {
        private readonly Dictionary<string, ActionHandler> _actionHandlers = [];

        private readonly Dictionary<string, DelayedInvocation> _actionInvocations = [];

        protected Actor()
        {
            foreach (var methodInfo in GetType().GetAllMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(m => m.GetCustomAttribute<ActionHandlerAttribute>() != null))
            {
                var handler = new ActionHandler(methodInfo);

                _actionHandlers.Add(handler.Name, handler);
            }
        }

        protected void AddEventAction(string eventName, NamedAction? action)
        {
            if (action == null)
                return;

            void invocation(Event eventRef)
            {
                try
                {
                    if (!this.HandleEventAction(eventRef, action))
                    {
                        throw new NotImplementedException($"action '{action.Name}' not found on {GetType().Name} for event {eventRef}");
                    }
                }
                catch (Exception ex)
                {
                    if (!HandleActionError(new ActionError(eventRef, action, ex)))
                    {
                        throw;
                    }
                }
            }

            if (action is ScheduledAction scheduledAction && scheduledAction.Delay != TimeSpan.Zero)
            {
                async void delayedInvocation(Event eventRef)
                {
                    if (!_actionInvocations.ContainsKey(eventName))
                    {
                        _actionInvocations[eventName] = new ScheduledInvocation(scheduledAction.Delay);

                        try
                        {
                            await _actionInvocations[eventName].Task;

                            invocation(eventRef);
                        }
                        catch (TaskCanceledException)
                        {
                            // ignore
                        }
                        finally
                        {
                            _actionInvocations.Remove(eventName);
                        }
                    }
                }

                AddEventHandler(eventName, delayedInvocation);
            }

            else if (action is ThrottledAction throttledAction && throttledAction.Times > 0)
            {
                async void throttledInvocation(Event eventRef)
                {
                    _actionInvocations.TryGetValue(eventName, out DelayedInvocation? delayed);

                    if (delayed is ThrottledInvocation throttled)
                        throttled.Trigger();

                    else
                    {
                        _actionInvocations[eventName] = delayed = new ThrottledInvocation(throttledAction.Times);

                        try
                        {
                            await delayed.Task;

                            invocation(eventRef);
                        }
                        catch (TaskCanceledException)
                        {
                            // ignore
                        }
                        finally
                        {
                            _actionInvocations.Remove(eventName);
                        }
                    }
                }

                AddEventHandler(eventName, throttledInvocation);
            }

            else
            {
                AddEventHandler(eventName, invocation);
            }
        }

        internal bool TryHandleEventAction(Event eventRef, NamedAction action) => HandleEventAction(eventRef, action);

        protected virtual bool HandleEventAction(Event eventObj, NamedAction action)
        {
            if (_actionHandlers.TryGetValue(action.Name, out var handler))
            {
                try
                {
                    handler.InvokeWithContext(this, action.Arguments, [.. eventObj.Context]);
                }
                catch (Exception ex)
                {
                    if (!HandleActionError(new ActionError(eventObj, action, ex) {Actor = this}))
                    {
                        throw;
                    }
                }

                return true;
            }

            return false;
        }

        protected virtual bool HandleActionError(ActionError error) => false;

        protected void CancelEventAction(string eventName)
        {
            if (_actionInvocations.Remove(eventName, out var invocation))
            {
                invocation?.Cancel();
            }
        }

        public virtual void Dispose()
        {
            foreach (var invocation in _actionInvocations.Values)
                invocation.Cancel();

            _actionInvocations.Clear();
        }

        private abstract class DelayedInvocation
        {
            protected CancellationTokenSource Source { get; } = new CancellationTokenSource();

            public abstract Task Task { get; }

            public void Cancel()
            {
                Source.Cancel();
            }
        }

        private class ScheduledInvocation(TimeSpan delay) : DelayedInvocation
        {
            public override Task Task => Task.Delay(delay, Source.Token);
        }

        private class ThrottledInvocation(uint times) : DelayedInvocation
        {
            private uint _timesLeft = times;

            private SemaphoreSlim _semaphore = new(0);

            public override Task Task => _semaphore.WaitAsync(Source.Token);

            public void Trigger()
            {
                if (Interlocked.Decrement(ref _timesLeft) == 0)
                {
                    _semaphore.Release();
                }
            }
        }
    }
}
