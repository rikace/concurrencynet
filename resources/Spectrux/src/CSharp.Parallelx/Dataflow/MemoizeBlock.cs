using FunctionalHelpers;

namespace CSharp.Parallelx.Dataflow
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;

    public class MemoizeBlock<T, R> : IPropagatorBlock<T, R>, IReceivableSourceBlock<R> where T : IComparable
    {
        static readonly ExecutionDataflowBlockOptions Default = new ExecutionDataflowBlockOptions();
        readonly TransformBlock<T, R> m_Buffer;
        readonly Func<T, R> m_projection;
        readonly object m_IncomingLock = new object();
        bool m_DeclineMessages;
        bool m_TargetDecliningPermanently;

        /// <summary>
        /// Initializes a new instance of the <see cref="FilterBlock{T}"/> class.
        /// </summary>
        /// <param name="filter">The filter to apply to incoming messages.</param>
        /// <param name="dataflowBlockOptions">The dataflow block options.</param>
        /// <remarks>Messages that do not pass the filter will be marked as accepted and dropped.</remarks>
        public MemoizeBlock(Func<T, R> projection, ExecutionDataflowBlockOptions dataflowBlockOptions) : this(
            projection, false,
            dataflowBlockOptions)
        {

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FilterBlock{T}"/> class.
        /// </summary>
        /// <param name="filter">The filter to apply to incoming messages.</param>
        /// <remarks>Messages that do not pass the filter will be marked as accepted and dropped.</remarks>
        public MemoizeBlock(Func<T, R> projection) : this(projection, false, Default)
        {
            // DataflowBlock.Encapsulate()
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FilterBlock{T}"/> class.
        /// </summary>
        /// <param name="filter">The filter to apply to incoming messages.</param>
        /// <param name="declineMessages">If true, messages that don't pass the filter will be declined. If false, messages that don't pass the filter will be accepted and dropped.</param>
        /// <param name="dataflowBlockOptions">The dataflow block options.</param>
        public MemoizeBlock(Func<T, R> projection, bool declineMessages,
            ExecutionDataflowBlockOptions dataflowBlockOptions)
        {
            m_projection = Memoization.MemoizeLazyThreadSafe(projection);
            m_DeclineMessages = declineMessages;
            m_Buffer = new TransformBlock<T, R>(projection, dataflowBlockOptions);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FilterBlock{T}"/> class.
        /// </summary>
        /// <param name="filter">The filter to apply to incoming messages.</param>
        /// <param name="declineMessages">If true, messages that don't pass the filter will be declined. If false, messages that don't pass the filter will be accepted and dropped.</param>
        public MemoizeBlock(Func<T, R> projection, bool declineMessages) : this(projection, declineMessages, Default)
        {
        }

        /// <summary>
        /// Gets a  <see cref="T:System.Threading.Tasks.Task"></see> that represents the asynchronous operation and completion of the dataflow block.
        /// </summary>
        /// <value>The completion.</value>
        public Task Completion => m_Buffer.Completion;




        /// <summary>
        /// Signals to the <see cref="T:System.Threading.Tasks.Dataflow.IDataflowBlock"></see> that it should not accept nor produce any more messages nor consume any more postponed messages.
        /// </summary>
        public void Complete()
        {
            lock (m_IncomingLock)
            {
                m_TargetDecliningPermanently = true;
            }

            m_Buffer.Complete();
        }

        R ISourceBlock<R>.ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<R> target,
            out bool messageConsumed)
        {
            return ((ISourceBlock<R>) m_Buffer).ConsumeMessage(messageHeader, target, out messageConsumed);
        }

        void IDataflowBlock.Fault(Exception exception)
        {
            ((ISourceBlock<R>) m_Buffer).Fault(exception);
        }

        /// <summary>
        /// Links the System.Threading.Tasks.Dataflow.ISourceBlock`1 to the specified System.Threading.Tasks.Dataflow.ITargetBlock`1.
        /// </summary>
        /// <param name="target">The System.Threading.Tasks.Dataflow.ITargetBlock`1 to which to connect this source.</param>
        /// <param name="linkOptions">
        /// A System.Threading.Tasks.Dataflow.DataflowLinkOptions instance that configures
        /// the link.
        /// </param>
        /// <returns>An IDisposable that, upon calling Dispose, will unlink the source from the target..</returns>
        public IDisposable LinkTo(ITargetBlock<R> target, DataflowLinkOptions linkOptions)
        {
            return m_Buffer.LinkTo(target, linkOptions);
        }

        public DataflowMessageStatus OfferMessage(DataflowMessageHeader messageHeader, T messageValue,
            ISourceBlock<T> source,
            bool consumeToAccept)
        {
            if (!messageHeader.IsValid) throw new ArgumentException("Invalid message header", nameof(messageHeader));
            if (source == null && consumeToAccept)
                throw new ArgumentException("Cannot consume from a null source", nameof(consumeToAccept));

            lock (m_IncomingLock)
            {
                if (m_TargetDecliningPermanently)
                    return DataflowMessageStatus.DecliningPermanently;


                return ((ITargetBlock<T>) m_Buffer).OfferMessage(messageHeader, messageValue, source, consumeToAccept);
            }
        }



        void ISourceBlock<R>.ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<R> target)
        {
            ((ISourceBlock<R>) m_Buffer).ReleaseReservation(messageHeader, target);
        }

        bool ISourceBlock<R>.ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<R> target)
        {
            return ((ISourceBlock<R>) m_Buffer).ReserveMessage(messageHeader, target);
        }

        public bool TryReceive(Predicate<R> filter, out R item)
        {
            return m_Buffer.TryReceive(filter, out item);
        }

        public bool TryReceiveAll(out IList<R> items)
        {
            return m_Buffer.TryReceiveAll(out items);
        }
    }
}