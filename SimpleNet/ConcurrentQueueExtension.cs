using System;
using System.Collections.Concurrent;

namespace SimpleNet
{
	public static class ConcurrentQueueExtension
	{
		public static T Dequeue<T>(this ConcurrentQueue<T> queue)
		{
			T t;
			while (queue.TryDequeue(out t))
				return t;

			throw new ArgumentOutOfRangeException();
		}
	}
}