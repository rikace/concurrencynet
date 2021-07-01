using System;
using System.Collections.Generic;
using System.Linq;

namespace CSharp.Parallelx.Threads
{
    public class ProcessorSet : ICollection<int>, IEnumerable<int>
    {
        #region Fields

        private readonly SortedSet<int> processorIDs;

        private bool isReadOnly = false;
                
        private static readonly Lazy<ProcessorSet> empty =
            new Lazy<ProcessorSet>(() => 
                ProcessorSet.FromRange(0).MakeReadOnly());

        private static readonly Lazy<ProcessorSet> full =
            new Lazy<ProcessorSet>(() =>
                ProcessorSet.FromRange(EnvironmentCached.ProcessorCount).MakeReadOnly());

        private static readonly Lazy<ProcessorSet> first =
            new Lazy<ProcessorSet>(() =>
                ProcessorSet.FromSingle(0).MakeReadOnly());

        private static readonly Lazy<ProcessorSet> last =
            new Lazy<ProcessorSet>(() =>
                ProcessorSet.FromSingle(EnvironmentCached.ProcessorCount - 1).MakeReadOnly());
        
        #endregion

        #region Properties
        
        public int Count
        {
            get { return this.processorIDs.Count; }
        }

        public bool IsReadOnly
        {
            get { return this.isReadOnly; }
        }

        public static ProcessorSet Empty
        {
            get { return empty.Value; }
        }

        public static ProcessorSet Full
        {
            get { return full.Value; }
        }

        public static ProcessorSet First
        {
            get { return first.Value; }
        }

        public static ProcessorSet Last
        {
            get { return last.Value; }
        }

        #endregion

        #region Constructors

        private ProcessorSet(IEnumerable<int> newProcessorIDs)
        {
            this.processorIDs = new SortedSet<int>();
            foreach (int processorID in newProcessorIDs)
                this.Add(processorID);
        }

        // Assumes that argument is already a private copy.
        private ProcessorSet(SortedSet<int> newProcessorIDs)
        {
            this.processorIDs = newProcessorIDs;
        }

        #endregion

        #region Factory Methods
        
        public static ProcessorSet FromSingle(int processorID)
        {
            var processorIDs = new SortedSet<int> { processorID };
            return new ProcessorSet(processorIDs);
        }

        public static ProcessorSet FromSpecific(params int[] processorIDs)
        {
            return FromCollection(processorIDs);
        }

        public static ProcessorSet FromRange(int numProcessors, bool disperse = false)
        {
            IEnumerable<int> processorIDs;

            if (disperse)
                processorIDs = EnumerateDispersed(numProcessors);
            else
                processorIDs = Enumerable.Range(0, numProcessors);

            return new ProcessorSet(processorIDs);
        }

        public static ProcessorSet FromRange(int startProcessorID, int numProcessors)
        {
            IEnumerable<int> processorIDs = Enumerable.Range(startProcessorID, numProcessors);
            return new ProcessorSet(processorIDs);
        }
        
        public static ProcessorSet FromCollection(IEnumerable<int> processorIDs)
        {
            return new ProcessorSet(processorIDs);
        }

        #endregion

        #region Helper Methods

        public static IEnumerable<int> EnumerateDispersed(int targetProcessors)
        {
            return EnumerateDispersed(targetProcessors, totalProcessors: EnvironmentCached.ProcessorCount);
        }

        public static IEnumerable<int> EnumerateDispersed(int targetProcessors, int totalProcessors)
        {
            double interval = (double)totalProcessors / targetProcessors;
            double processorID = 0;
            for (int i = 0; i < targetProcessors; ++i)
            {
                yield return (int)Math.Round(processorID);
                processorID += interval;
            }
        }

        #endregion

        #region IEnumerable Methods

        public IEnumerator<int> GetEnumerator()
        {
            return ((IEnumerable<int>)this.processorIDs).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        #endregion

        #region ICollection Methods

        public void Add(int processorID)
        {
            this.VerifyNotReadOnly();

            int processorCount = EnvironmentCached.ProcessorCount;

            if (processorID < 0 || processorID >= processorCount)
                throw new ArgumentOutOfRangeException("processorID");

            if (this.AddInner(processorID) == false)
                throw new ArgumentException("The processor already exists in the processor set.", "processorID");
        }

        internal bool AddInner(int processorID)
        {
            return this.processorIDs.Add(processorID);
        }

        public void Clear()
        {
            this.VerifyNotReadOnly();

            this.processorIDs.Clear();
        }

        public bool Contains(int processorID)
        {
            return this.processorIDs.Contains(processorID);
        }

        public void CopyTo(int[] array, int arrayIndex)
        {
            this.processorIDs.CopyTo(array, arrayIndex);
        }

        public bool Remove(int processorID)
        {
            this.VerifyNotReadOnly();

            return this.processorIDs.Remove(processorID);
        }

        #endregion

        #region Instance Methods
        
        public ProcessorSet Clone(bool asReadOnly = false)
        {
            SortedSet<int> cloneProcessorIDs = new SortedSet<int>(this.processorIDs);
            ProcessorSet cloneProcessorSet = new ProcessorSet(cloneProcessorIDs);
            cloneProcessorSet.isReadOnly = asReadOnly;
            return cloneProcessorSet;
        }

        private ProcessorSet MakeReadOnly()
        {
            this.isReadOnly = true;
            return this;
        }

        private void VerifyNotReadOnly()
        {
            if (this.isReadOnly)
                throw new InvalidOperationException("The current processor set is read-only.");
        }

        public override string ToString()
        {
            return string.Format("{{ {0} }}", string.Join(", ", this.processorIDs.ToArray()));
        }
        
        #endregion
    }
}