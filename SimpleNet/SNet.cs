using System.Net.Sockets;
using ENet;

namespace SimpleNet
{


	public class SNet
	{
		public static void Init()
		{
			Library.Initialize();
		}

		public static void Terminate()
		{
			Library.Deinitialize();
		}
	}
}