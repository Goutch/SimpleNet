using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using SimpleNet;
using SimpleNet.Frames;


namespace ClientApp
{
	internal class Program
	{
		private static ConcurrentQueue<string> consoleInputs = new ConcurrentQueue<string>();
		private static Client client;
		private static bool connected = true;
		private static Dictionary<uint, NetEntity> entities = new();
		private static Dictionary<uint, string> entitiesNames = new();

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
			client.OnNetEntityCreated += OnNetEntityCreated;
			client.OnNetEntityReceiveData += OnNetEntityReceiveData;
			client.OnError += OnError;
			Thread thread = new Thread(() => { HandleConsoleInput(); });
			thread.Start();
			while (connected)
			{
				if (!consoleInputs.IsEmpty)
				{
					string output = consoleInputs.Dequeue();
					if (output == "quit")
						client.Disconnect();
					if (output == "create")
						client.CreateEntity(ByteUtils.StringToBytes("myFirstEntity"));
					else if (output == "ping")
						Console.WriteLine(client.Ping());
					else
					{
						Frame frame = new Frame(Frame.FrameType.Reliable);
						frame.AddData(Encoding.ASCII.GetBytes(output));
						if (entities.Count == 0)
						{
							client.Broadcast(frame);
						}
						else
						{
							client.Broadcast(frame, entities[0]);
						}
					}
				}

				client.PollEvents();
			}

			SNet.Terminate();
		}

		private static void OnError(string errorMessage)
		{
			Console.WriteLine(errorMessage);
		}

		private static void OnNetEntityReceiveData(uint entityID, byte[] data)
		{
			Console.WriteLine("Received message for entity " + entityID);
		}


		private static void OnNetEntityCreated(NetEntity netEntity, byte[] data)
		{
			Console.WriteLine("Created entity |ID=" + netEntity.ID + "|Owner=" + netEntity.Owner + "|name=" + ByteUtils.BytesToString(data));
			entities.Add(netEntity.ID, netEntity);
			entitiesNames.Add(netEntity.ID, ByteUtils.BytesToString(data));
		}


		private static void OnConnectionFailed()
		{
			Console.WriteLine("Failed to connect");
		}

		private static void OnConnectionSuccess()
		{
			Console.WriteLine("Connected! ID=" + client.GetID());
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
			client.OnNetEntityCreated -= OnNetEntityCreated;
			client.OnNetEntityReceiveData -= OnNetEntityReceiveData;
			client.OnError -= OnError;
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
			client.OnNetEntityCreated -= OnNetEntityCreated;
			client.OnNetEntityReceiveData -= OnNetEntityReceiveData;
			client.OnError -= OnError;
			connected = false;
		}

		public static void OnReceiveData(byte[] data)
		{
			Console.WriteLine(Encoding.ASCII.GetString(data));
		}
	}
}