using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiftStrap.Utility
{
    public class InterProcessLock : IDisposable
    {
        // A named Semaphore(1,1) has no thread affinity, so it can be released/disposed from a
        // different thread than the one that acquired it (unlike Mutex, which throws when the
        // owning thread is not the releasing thread — e.g. Watcher cleanup on a worker thread).
        public Semaphore Semaphore { get; private set; }

        public bool IsAcquired { get; private set; }

        public InterProcessLock(string name) : this(name, TimeSpan.Zero) { }

        public InterProcessLock(string name, TimeSpan timeout)
        {
            Semaphore = new Semaphore(1, 1, "RiftStrap-" + name);

            IsAcquired = Semaphore.WaitOne(timeout);
        }

        public void Dispose()
        {
            try
            {
                if (IsAcquired)
                {
                    Semaphore.Release();
                    IsAcquired = false;
                }
            }
            finally
            {
                Semaphore.Dispose();
            }

            GC.SuppressFinalize(this);
        }
    }
}
