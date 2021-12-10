using System;
using System.Threading;

namespace CSharp.Parallelx.Synchronizers
{
    public class RWLSynchronizer<TImpl, TIRead, TIWrite> where TImpl : TIWrite, TIRead
    {
        ReaderWriterLockSlim rwl = new ReaderWriterLockSlim();
        TImpl shared;

        public RWLSynchronizer(TImpl shared) =>
            shared = shared;

        public void Read(Action<TIRead> read)
        {
            rwl.EnterReadLock();
            try
            {
                read(shared);
            }
            finally
            {
                rwl.ExitReadLock();
            }
        }

        public void Write(Action<TIWrite> write)
        {
            rwl.EnterWriteLock();
            try
            {
                write(shared);
            }
            finally
            {
                rwl.ExitWriteLock();
            }
        }
    }
}

    