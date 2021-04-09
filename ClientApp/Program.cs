using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using SimpleNet;
using SimpleNet.Properties;

namespace ClientApp
{
	internal class Program
	{
		private static ConcurrentQueue<string> consoleInputs = new ConcurrentQueue<string>();
		private static Client client;
		private static bool connected = true;

		private static void HandleConsoleInput()
		{
			while (true)
			{
				string s = Console.ReadLine();
				consoleInputs.Enqueue(s);
				if (s == "quit")
				{
					break;
				}
			}
		}

		public static void Main(string[] args)
		{
			SNet.Init();
			client = new Client();
			client.Connect("127.0.0.1", 8888);
			client.OnReceiveData += OnReceiveData;
			client.OnConnectionSucces += OnConnectionSuccess;
			client.OnConnectionFailed += OnConnectionFailed;
			client.OnDisconnect += OnDisconnect;
			client.OnTimeout += OnTimeout;

			Thread thread = new Thread(() => { HandleConsoleInput(); });
			thread.Start();
			while (connected)
			{
				if (!consoleInputs.IsEmpty)
				{
					string output = consoleInputs.Dequeue();
					if (output == "quit")
						client.Disconnect();
					else if (output == "ping")
						Console.WriteLine(client.Ping());
					else
						client.Send(new Message(Encoding.ASCII.GetBytes(output)));
				}

				client.PollEvents();
			}

			SNet.Terminate();
		}


		private static void OnConnectionFailed()
		{
			Console.WriteLine("Failed to connect");
		}

		private static void OnConnectionSuccess()
		{
			Console.WriteLine("Connected!");
			connected = true;
		}

		private static void OnTimeout()
		{
			Console.WriteLine("Timed out");
			client.OnReceiveData -= OnReceiveData;
			client.OnConnectionSucces -= OnConnectionSuccess;
			client.OnConnectionFailed -= OnConnectionFailed;
			client.OnDisconnect -= OnDisconnect;
			client.OnTimeout -= OnTimeout;
			connected = false;
		}

		private static void OnDisconnect()
		{
			Console.WriteLine("Disconnected");
			client.OnReceiveData -= OnReceiveData;
			client.OnConnectionSucces -= OnConnectionSuccess;
			client.OnConnectionFailed -= OnConnectionFailed;
			client.OnDisconnect -= OnDisconnect;
			client.OnTimeout -= OnTimeout;
			connected = false;
		}

		public static void OnReceiveData(byte[] data)
		{
			Console.WriteLine(Encoding.ASCII.GetString(data));
		}
	}
}