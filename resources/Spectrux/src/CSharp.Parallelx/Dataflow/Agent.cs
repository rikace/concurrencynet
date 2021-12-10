namespace CSharp.Parallelx.Dataflow
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using System.Threading;

    public sealed class StatefulDataflowAgent<TState, TMessage>
    {
        private TState state;
        private readonly ActionBlock<TMessage> actionBlock;

        public StatefulDataflowAgent(
            TState initialState,
            Func<TState, TMessage, Task<TState>> action,
            CancellationTokenSource cts = null)
        {
            state = initialState;
            var options = new ExecutionDataflowBlockOptions
            {
                CancellationToken = cts?.Token ?? CancellationToken.None
            };
            actionBlock = new ActionBlock<TMessage>(
                async msg => state = await action(state, msg), options);
        }

        public Task Send(TMessage message) => actionBlock.SendAsync(message);

        public void Post(TMessage message) => actionBlock.Post(message);

        public StatefulDataflowAgent(TState initialState, Func<TState, TMessage, TState> action,
            CancellationTokenSource cts = null)
        {
            state = initialState;
            var options = new ExecutionDataflowBlockOptions
            {
                CancellationToken = cts?.Token ?? CancellationToken.None
            };
            actionBlock = new ActionBlock<TMessage>(
                msg => state = action(state, msg), options);
        }

        public TState State => state;
    }
}