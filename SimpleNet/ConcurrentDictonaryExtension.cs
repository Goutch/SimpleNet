using System.Collections.Concurrent;

namespace SimpleNet
{
	public static class ConcurrentDictionaryExtension
	{
		public static void Add<K, V>(this ConcurrentDictionary<K, V> dic, K k, V v)
		{
			while (dic.TryAdd(k, v) != true) {};
		}

		public static void Remove<K, V>(this ConcurrentDictionary<K, V> dic, K k)
		{
			V v;
			while (dic.TryRemove(k,out v) != true){} ;
		}
	}
}