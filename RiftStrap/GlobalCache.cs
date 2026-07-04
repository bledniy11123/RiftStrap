using System.Collections.Concurrent;

namespace RiftStrap
{
    public static class GlobalCache
    {
        public static readonly ConcurrentDictionary<string, string?> ServerLocation = new();
    }
}
