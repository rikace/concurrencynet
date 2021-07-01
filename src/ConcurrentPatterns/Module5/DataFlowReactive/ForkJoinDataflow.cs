﻿using ReactiveAgent.Agents;
using ReactiveAgent.Agents.Dataflow;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
namespace ParallelForkJoin
{
    public static class ForkJoinDataFlow
    {
        // TODO
        // Parallel Fork/Join using TPL DataFlow
        // public static async Task<R> ForkJoin<T1, T2, R>(
        //     this IEnumerable<T1> source, Func<T1, Task<IEnumerable<T2>>> map,
        //     Func<R> initialState,
        //     Func<R, T2, Task<R>> aggregate,
        //     CancellationTokenSource cts = null,
        //     int partitionLevel = 8, int boundCapacity = 20)
        // {
        //     cts ??= new CancellationTokenSource();
        //     var blockOptions = new ExecutionDataflowBlockOptions
        //     {
        //         MaxDegreeOfParallelism = partitionLevel,
        //         BoundedCapacity = boundCapacity,
        //         CancellationToken = cts.Token
        //     };
        //
        //     var inputBuffer = new BufferBlock<T1>(
        //             new DataflowBlockOptions
        //             {
        //                 CancellationToken = cts.Token,
        //                 BoundedCapacity = boundCapacity
        //             });
        //
        //     var mapperBlock = new TransformManyBlock<T1, T2>(map, blockOptions);
        //     var reducerAgent = Agent.Start(initialState(), aggregate, cts);
        //     var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };
        //     inputBuffer.LinkTo(mapperBlock, linkOptions);
        //
        //     IDisposable disposable = mapperBlock.AsObservable()
        //         .Subscribe(async item => await reducerAgent.Send(item));
        //
        //     foreach (var item in source)
        //         await inputBuffer.SendAsync(item);
        //     inputBuffer.Complete();
        //
        //     var tcs = new TaskCompletionSource<R>();
        //
        //     await inputBuffer.Completion.ContinueWith(task => mapperBlock.Complete());
        //     await mapperBlock.Completion.ContinueWith(task =>
        //     {
        //         var agent = reducerAgent as StatefulDataflowAgent<R, T2>;
        //         disposable.Dispose();
        //         tcs.SetResult(agent.State);
        //     });
        //
        //     return await tcs.Task;
        // }

        public static async Task<R> ForkJoin<T1, T2, R>(
            this IEnumerable<T1> source,
            Func<T1, Task<IEnumerable<T2>>> map,
            Func<R> initialState,
            Func<R, T2, R> aggregate,
            CancellationTokenSource cts = null,
            int partitionLevel = 8, int boundCapacity = 20)
        {
            cts ??= new CancellationTokenSource();
            var blockOptions = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = partitionLevel,
                BoundedCapacity = boundCapacity,
                CancellationToken = cts.Token
            };

            var inputBuffer = new BufferBlock<T1>(
                new DataflowBlockOptions
                {
                    CancellationToken = cts.Token,
                    BoundedCapacity = boundCapacity
                });

            var mapperBlock = new TransformManyBlock<T1, T2>(map, blockOptions);
            var reducerAgent = Agent.Start(initialState(), aggregate, cts);
            var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };
            inputBuffer.LinkTo(mapperBlock, linkOptions);

            IDisposable disposable = mapperBlock.AsObservable()
                .Subscribe(async item => await reducerAgent.Send(item));

            foreach (var item in source)
                await inputBuffer.SendAsync(item);
            inputBuffer.Complete();

            var tcs = new TaskCompletionSource<R>();

            await inputBuffer.Completion.ContinueWith(task => mapperBlock.Complete());
            await mapperBlock.Completion.ContinueWith(task =>
            {
                var agent = reducerAgent as StatefulDataflowAgent<R, T2>;
                disposable.Dispose();
                tcs.SetResult(agent.State);
            });

            return await tcs.Task;
        }
    }
}
