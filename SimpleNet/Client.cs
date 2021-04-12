using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ENet;
using System.Threading;
using SimpleNet.Frames;

namespace SimpleNet
{
	public class Client
	{
		private volatile int id;
		private volatile int shouldDisconnect = 0;
		private volatile int connected = 0;
		private volatile float ping;
		private Thread IOThread;

		private ConcurrentQueue<Frame> sendQueue;

		#region Events

		public delegate void ConnectionStatusEventHandler();

		public delegate void ReceiveDataEventHandler(byte[] data);

		public delegate void ErrorEventHandler(string errorMessage);

		public delegate void NetEntityCreatedEventHandler(NetEntity entity, byte[] data);

		public delegate void NetEntityReceiveEventHandler(uint entityID, byte[] data);

		public delegate void DisconnectionEventHandler();

		public delegate void TimeoutEventHandler();

		public ConnectionStatusEventHandler OnConnectionSucces;
		public ConnectionStatusEventHandler OnConnectionFailed;
		public ErrorEventHandler OnError;
		public NetEntityCreatedEventHandler OnNetEntityCreated;
		public NetEntityReceiveEventHandler OnNetEntityReceiveData;
		public ReceiveDataEventHandler OnReceiveData;
		public DisconnectionEventHandler OnDisconnect;
		public TimeoutEventHandler OnTimeout;

		private volatile int raiseConnectionEvent = 0;
		private volatile int raiseDisconnectEvent = 0;
		private volatile int raiseTimeoutEvent = 0;
		private ConcurrentQueue<byte[]> pendingReceiveEvents;
		private ConcurrentQueue<string> pendingErrorEvents;
		private ConcurrentQueue<KeyValuePair<NetEntity, byte[]>> pendingNetEntityCreatedEvent;

		private ConcurrentQueue<KeyValuePair<uint, byte[]>> netEntityReceiveQueue;

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

			while (!pendingReceiveEvents.IsEmpty)
			{
				OnReceiveData?.Invoke(pendingReceiveEvents.Dequeue());
			}

			while (!pendingErrorEvents.IsEmpty)
			{
				OnError?.Invoke(pendingErrorEvents.Dequeue());
			}

			while (!pendingNetEntityCreatedEvent.IsEmpty)
			{
				KeyValuePair<NetEntity, byte[]> pair = pendingNetEntityCreatedEvent.Dequeue();
				OnNetEntityCreated?.Invoke(pair.Key, pair.Value);
			}

			while (!netEntityReceiveQueue.IsEmpty)
			{
				var pair = netEntityReceiveQueue.Dequeue();
				OnNetEntityReceiveData?.Invoke(pair.Key, pair.Value);
			}
		}

		public void Connect(string ip, ushort port)
		{
			pendingReceiveEvents = new();
			sendQueue = new();
			pendingErrorEvents = new();
			pendingNetEntityCreatedEvent = new();
			netEntityReceiveQueue = new();
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
								ParseReceiveData(netEvent.Packet.Data, netEvent.Packet.Length);

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
						Frame f = sendQueue.Dequeue();
						packet.Create(f.Data.ToArray(), (PacketFlags) f.Type);
						client.Broadcast(0, ref packet);
					}
				}

				client.Flush();
			}
		}

		private void ParseReceiveData(IntPtr data, int length)
		{
			int index = 0;
			Frame.ToClientFormat format = (Frame.ToClientFormat) ByteUtils.ReadByte(data, index);
			index++;
			switch (format)
			{
				case Frame.ToClientFormat.Connection:
					uint clientID = BitConverter.ToUInt32(ByteUtils.GetBytes(data, index, length - index), 0);
					Interlocked.Exchange(ref id, (int) clientID);
					Interlocked.Increment(ref raiseConnectionEvent);
					break;

				case Frame.ToClientFormat.EntityMessage:
					uint entityID = BitConverter.ToUInt32(ByteUtils.GetBytes(data, index, 4), 0);
					index += 4;
					byte[] entityMessage = ByteUtils.GetBytes(data, index, length - index);
					netEntityReceiveQueue.Enqueue(new KeyValuePair<uint, byte[]>(entityID, entityMessage));
					break;

				case Frame.ToClientFormat.ClientMessage:
					pendingReceiveEvents.Enqueue(ByteUtils.GetBytes(data, index, length - index));
					break;

				case Frame.ToClientFormat.Error:
					pendingErrorEvents.Enqueue(ByteUtils.BytesToString(ByteUtils.GetBytes(data, index, length - index)));
					break;

				case Frame.ToClientFormat.EntityCreated:
					uint createdEntityID = BitConverter.ToUInt32(ByteUtils.GetBytes(data, index, 4), 0);
					index += 4;
					uint entityOwner = BitConverter.ToUInt32(ByteUtils.GetBytes(data, index, 4), 0);
					index += 4;
					pendingNetEntityCreatedEvent.Enqueue(new KeyValuePair<NetEntity, byte[]>(
						new NetEntity(createdEntityID,
							entityOwner),
						ByteUtils.GetBytes(data, index, length - index)
					));
					break;
				case Frame.ToClientFormat.EntityChangedOwnership:
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		public void Send(Frame f)
		{
			RelayClientMessageFrame message = new RelayClientMessageFrame(f.Type);
			message.AddFrame(f);
			sendQueue.Enqueue(message);
		}

		public void Broadcast(Frame f)
		{
			BroadcastClientMessageFrame message = new BroadcastClientMessageFrame(f.Type);
			message.AddFrame(f);
			sendQueue.Enqueue(message);
		}

		public void Send(Frame f, NetEntity to)
		{
			if (to.Owner == id)
			{
				RelayEntityMessageFrame relayEntityMessage = new RelayEntityMessageFrame(f.Type, to.ID);
				relayEntityMessage.AddFrame(f);
				sendQueue.Enqueue(relayEntityMessage);
			}
			else
			{
				OnError?.Invoke("Entity must be owned to send data");
			}
		}

		public void Broadcast(Frame f, NetEntity to)
		{
			if (to.Owner == id)
			{
				BroadcastEntityMessageFrame broadcastEntityMessage = new BroadcastEntityMessageFrame(f.Type, to.ID);
				broadcastEntityMessage.AddFrame(f);
				sendQueue.Enqueue(broadcastEntityMessage);
			}
			else
			{
				OnError?.Invoke("Entity must be owned to send data");
			}
		}

		public void CreateEntity(byte[] userData)
		{
			sendQueue.Enqueue(new CreateEntityFrame(userData));
		}


		public float Ping()
		{
			return ping;
		}

		public int GetID()
		{
			return id;
		}
	}
}