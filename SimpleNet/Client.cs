using System;
using System.Collections.Concurrent;
using ENet;
using System.Threading;
using SimpleNet.Properties;

namespace SimpleNet
{
	public class Client
	{
		private volatile int id;
		private volatile int shouldDisconnect = 0;
		private volatile int connected = 0;
		private volatile float ping;
		private Thread IOThread;

		private ConcurrentQueue<Message> sendQueue;

		#region Events

		public delegate void ConnectionStatusEventHandler();

		public delegate void ReceiveDataEventHandler(byte[] data);

		public delegate void DisconnectionEventHandler();

		public delegate void TimeoutEventHandler();

		public ConnectionStatusEventHandler OnConnectionSucces;
		public ConnectionStatusEventHandler OnConnectionFailed;
		public ReceiveDataEventHandler OnReceiveData;
		public DisconnectionEventHandler OnDisconnect;
		public TimeoutEventHandler OnTimeout;

		private volatile int raiseConnectionEvent = 0;
		private volatile int raiseDisconnectEvent = 0;
		private volatile int raiseTimeoutEvent = 0;
		private ConcurrentQueue<byte[]> receiveQueue;

		#endregion


		public void PollEvents()
		{
			while (raiseConnectionEvent > 0)
			{
				if (connected > 0)
				{
					OnConnectionSucces?.Invoke();
				}
				else
				{
					OnConnectionFailed?.Invoke();
				}

				Interlocked.Exchange(ref raiseConnectionEvent, 0);
			}

			if (raiseDisconnectEvent > 0)
			{
				OnDisconnect?.Invoke();
				Interlocked.Exchange(ref raiseDisconnectEvent, 0);
			}

			if (raiseTimeoutEvent > 0)
			{
				OnTimeout?.Invoke();
				Interlocked.Exchange(ref raiseTimeoutEvent, 0);
			}

			while (!receiveQueue.IsEmpty)
			{
				OnReceiveData?.Invoke(receiveQueue.Dequeue());
			}
		}

		public void Connect(string ip, ushort port)
		{
			receiveQueue = new();
			sendQueue = new();
			IOThread = new Thread(() => HandleIO(ip, port));
			IOThread.Start();
		}

		public void Disconnect()
		{
			Interlocked.Increment(ref shouldDisconnect);
		}

		private void HandleIO(string ip, ushort port)
		{
			using (Host client = new Host())
			{
				Address address = new Address();

				address.SetHost(ip);
				address.Port = port;
				client.Create();

				Peer server = client.Connect(address);

				Event netEvent;
				if (client.Service(5000, out netEvent) > 0 &&
				    netEvent.Type == EventType.Connect)
				{
					Interlocked.Increment(ref connected);
				}
				else
				{
					Interlocked.Increment(ref raiseConnectionEvent);
				}

				while (connected > 0)
				{
					while (client.Service(10, out netEvent) > 0)
					{
						//Receive
						switch (netEvent.Type)
						{
							case EventType.None:
								break;

							case EventType.Disconnect:
								Interlocked.Exchange(ref connected, 0);
								Interlocked.Increment(ref raiseDisconnectEvent);
								break;

							case EventType.Timeout:
								Interlocked.Exchange(ref connected, 0);
								Interlocked.Increment(ref raiseTimeoutEvent);
								break;

							case EventType.Receive:
								byte[] buffer1 = ByteUtils.GetBytes(netEvent.Packet.Data, 0, netEvent.Packet.Length);
								Console.WriteLine(buffer1[1]);
								switch ((ClientMessageFormat) ByteUtils.ReadByte(netEvent.Packet.Data, 0))
								{
									case ClientMessageFormat.ClientID:
										byte[] buffer = ByteUtils.GetBytes(netEvent.Packet.Data, 1, netEvent.Packet.Length-1);
										Interlocked.Exchange(ref id, (int) BitConverter.ToUInt32(buffer, 0));
										Interlocked.Increment(ref raiseConnectionEvent);
										break;
									case ClientMessageFormat.JoinedRoom:
										break;
									case ClientMessageFormat.LinkMessage:
										break;
									case ClientMessageFormat.UserMessage:
										receiveQueue.Enqueue(ByteUtils.GetBytes(netEvent.Packet.Data, 1, netEvent.Packet.Length - 1));
										break;
									default:
										throw new ArgumentOutOfRangeException();
								}

								netEvent.Packet.Dispose();
								break;
						}
					}

					Interlocked.Exchange(ref ping, server.LastRoundTripTime);

					if (shouldDisconnect > 0)
					{
						server.Disconnect(0);
						Interlocked.Exchange(ref shouldDisconnect, 0);
					}

					//Send
					while (!sendQueue.IsEmpty)
					{
						Packet packet = default(Packet);
						Message m = sendQueue.Dequeue();
						packet.Create(m.data, (PacketFlags) m.type);
						client.Broadcast(0, ref packet);
					}
				}

				client.Flush();
			}
		}

		public void Send(Message m)
		{
			sendQueue.Enqueue(m);
		}

		public float Ping()
		{
			return ping;
		}

		public int GetID()
		{
			return id;
		}

		private void CreateRoom()
		{
			
		}
	}
}