namespace Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public static class TaskAsComplete
    {
        public static IEnumerable<Task<R>> ContinueAsComplete<T, R>(
            this IEnumerable<T> input,
            Func<T, Task<R>> selector)
        {
            var inputTaskList = (from el in input select selector(el)).ToList();

            var completionSourceList = new List<TaskCompletionSource<R>>(inputTaskList.Count);
            for (var i = 0; i < inputTaskList.Count; i++)
                completionSourceList.Add(new TaskCompletionSource<R>());

            // TODO LAB
            // with a large set of Tasks running in parallel,
            // the Task.WaitAny generates a bad performance problem
            // because the support for interleaving scenario.
            // Every call to WhenAny will result in a continuation being registered with each task,
            // which for N tasks will amount to O(N2) continuations created over the lifetime of the interleaving operation.
            // To address that if working with a large set of tasks, we should use a combinatory dedicated to the goal
            //
            // To minimize the resource consumption, try to avoid the usage pf Task.WhenAny
            // Suggestion, the TaskCompletionSource (or a collection) is a good alternative

            // TODO LAB
            Action<Task<R>> continuataion = null;  // replace "null" with missing code here

            // TODO (3.a)
            int prevIndex = -1;


            foreach (var inputTask in inputTaskList)
            {
                // TODO complete this code
                // inputTask.ContinueWith
            }

            return completionSourceList.Select(source => source.Task);
        }
    }
}
