using System;
using System.Collections.Concurrent;
using System.Threading;
using SimpleNet;
using SimpleNet.Frames;

namespace ServerApp
{
	internal class Program
	{
		private static ConcurrentQueue<string> consoleInputs = new ConcurrentQueue<string>();
		private static Server server;
		private static void HandleConsoleInput()
		{
			while (true)
			{
				string s = Console.ReadLine();
				consoleInputs.Enqueue(s);
				if (s == "stop")
				{
					break;
				}
			}
		}

		public static void Main(string[] args)
		{
			SNet.Init();
			server = new Server();
			server.OnReceiveData += OnReceiveData;
			server.OnClientConnect += OnClientConnect;
			server.OnClientDisconnect += OnClientDisconnect;
			server.OnClientTimeout += OnClientTimeout;
			Thread inputThread = new Thread(() => { HandleConsoleInput(); });
			inputThread.Start();

			server.Start(8888, 8);
			while (server.IsRunning())
			{
				while (!consoleInputs.IsEmpty)
				{
					string input = consoleInputs.Dequeue();
					if (input=="stop")
					{
						server.Stop();
					}
				}
				server.PollEvents();
			}
			
			server.OnReceiveData -= OnReceiveData;
			server.OnClientConnect -= OnClientConnect;
			server.OnClientDisconnect -= OnClientDisconnect;
			server.OnClientTimeout -= OnClientTimeout;
			server.Stop();
			SNet.Terminate();
		}

		private static void OnClientTimeout(uint id)
		{
			Console.WriteLine("Client#" + id + " timed out");
		}

		private static void OnClientDisconnect(uint id)
		{
			Console.WriteLine("Client#" + id + " disconnected");
		}

		private static void OnClientConnect(uint id)
		{
			Console.WriteLine("Client#" + id + " connected");
		}

		public static void OnReceiveData(uint from, byte[] data)
		{
			string s = "Client#" + from + ":" + ByteUtils.BytesToString(data);
			Console.WriteLine(s);
			Frame frame = new Frame(Frame.FrameType.Reliable);
			frame.AddData(ByteUtils.StringToBytes(s));
			server.Send(frame);
		}
	}
}