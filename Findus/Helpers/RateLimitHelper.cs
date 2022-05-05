using System;
using System.Collections.Generic;

namespace Findus.Helpers
{
    // https://stackoverflow.com/a/28173630
    public class RateLimitHelper
    {
        private readonly int _requestsPerInterval;
        private readonly Queue<DateTime> _history;
        private readonly TimeSpan _interval;

        public RateLimitHelper() : this(240, new TimeSpan(0, 1, 0)) { }

        public RateLimitHelper(int requestsPerInterval, TimeSpan interval)
        {
            _requestsPerInterval = requestsPerInterval;
            _history = new Queue<DateTime>();
            _interval = interval;
        }

        public void SleepAsNeeded()
        {
            DateTime now = DateTime.Now;

            _history.Enqueue(now);

            if (_history.Count >= _requestsPerInterval)
            {
                var last = _history.Dequeue();
                TimeSpan difference = now - last;

                if (difference < _interval)
                {
                    System.Threading.Thread.Sleep(_interval - difference);
                }
            }
        }
    }
}
