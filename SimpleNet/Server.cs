using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using ENet;
using SimpleNet.Properties;

namespace SimpleNet
{
	public class Server
	{
		private Host server;
		private ConcurrentDictionary<uint, ClientData> clients;
		private ConcurrentDictionary<uint, Peer> peers;

		private Thread IOThread;
		private int shouldStop = 0;

		private ConcurrentQueue<Message> sendQueue = new();
		private ConcurrentQueue<KeyValuePair<uint, Message>> sendExcludedQueue = new();
		private ConcurrentQueue<KeyValuePair<uint[], Message>> sendTargetsQueue = new();

		#region Events

		public delegate void ClientConnectEventHandler(uint id);

		public ClientConnectEventHandler OnClientConnect;
		private ConcurrentQueue<uint> pendingConnectEvents = new();

		public delegate void ReceiveDataEventHandler(uint from, byte[] data);

		public ReceiveDataEventHandler OnReceiveData;
		private ConcurrentQueue<KeyValuePair<uint, byte[]>> receiveQueue = new();

		public delegate void ClientDisconnectEventHandler(uint id);

		public ClientDisconnectEventHandler OnClientDisconnect;
		private ConcurrentQueue<uint> pendingDisconnectEvents = new();

		public delegate void ClientTimeoutEventHandler(uint id);

		public ClientTimeoutEventHandler OnClientTimeout;
		private ConcurrentQueue<uint> pendingTimeoutEvents = new();

		#endregion


		public void Start(ushort port, int maxClients)
		{
			IOThread = new Thread(() => { HandleIO(port, maxClients); });
			IOThread.Start();
		}

		public void PollEvents()
		{
			while (!receiveQueue.IsEmpty)
			{
				KeyValuePair<uint, byte[]> receivedPacket = receiveQueue.Dequeue();
				OnReceiveData?.Invoke(receivedPacket.Key, receivedPacket.Value);
			}

			while (!pendingConnectEvents.IsEmpty)
			{
				OnClientConnect?.Invoke(pendingConnectEvents.Dequeue());
			}

			while (!pendingDisconnectEvents.IsEmpty)
			{
				uint id = pendingDisconnectEvents.Dequeue();
				OnClientDisconnect?.Invoke(id);
				clients.Remove(id);
			}

			while (!pendingTimeoutEvents.IsEmpty)
			{
				uint id = pendingTimeoutEvents.Dequeue();
				OnClientTimeout?.Invoke(id);
				clients.Remove(id);
			}
		}

		public void HandleIO(ushort port, int maxClients)
		{
			using (server = new Host())
			{
				Address address = new Address();

				address.Port = port;

				server.Create(address, maxClients);

				Event netEvent;
				clients = new();
				peers = new();
				while (shouldStop == 0)
				{
					while (server.Service(5, out netEvent) > 0)
					{
						switch (netEvent.Type)
						{
							case EventType.None:
								break;

							case EventType.Connect:
								ClientData clientData = new ClientData(netEvent.Peer.ID, netEvent.Peer.IP);
								clients.Add(netEvent.Peer.ID, clientData);
								peers.Add(netEvent.Peer.ID, netEvent.Peer);

								pendingConnectEvents.Enqueue(netEvent.Peer.ID);
								List<byte> bytes = new List<byte>();
								bytes.Add((byte) ClientMessageFormat.ClientID);
								bytes.AddRange(BitConverter.GetBytes(netEvent.Peer.ID));
								Message message = new Message(bytes.ToArray());
								sendTargetsQueue.Enqueue(new KeyValuePair<uint[], Message>(new uint[1] {netEvent.Peer.ID}, message));
								break;

							case EventType.Disconnect:
								pendingDisconnectEvents.Enqueue(netEvent.Peer.ID);
								peers.Remove(netEvent.Peer.ID);
								break;

							case EventType.Timeout:
								pendingTimeoutEvents.Enqueue(netEvent.Peer.ID);
								peers.Remove(netEvent.Peer.ID);
								break;

							case EventType.Receive:
								switch ((ServerMessageFormat) ByteUtils.ReadByte(netEvent.Packet.Data, 0))
								{
									case ServerMessageFormat.CreateRoom:
										break;
									case ServerMessageFormat.JoinRoom:
										break;
									case ServerMessageFormat.RequestPublicRooms:
										break;
									default:
										throw new ArgumentOutOfRangeException();
								}

								byte[] buffer = ByteUtils.GetBytes(netEvent.Packet.Data, 0, netEvent.Packet.Length);
								receiveQueue.Enqueue(new KeyValuePair<uint, byte[]>(netEvent.Peer.ID, buffer));
								netEvent.Packet.Dispose();
								break;
						}
					}

					//Send 

					while (!sendQueue.IsEmpty)
					{
						Packet packet = default(Packet);
						Message m = sendQueue.Dequeue();
						packet.Create(m.data, (PacketFlags) m.type);
						server.Broadcast(0, ref packet);
					}

					while (!sendExcludedQueue.IsEmpty)
					{
						Packet packet = default(Packet);
						KeyValuePair<uint, Message> m = sendExcludedQueue.Dequeue();
						packet.Create(m.Value.data, (PacketFlags) m.Value.type);
						server.Broadcast(0, ref packet, peers[m.Key]);
					}

					while (!sendTargetsQueue.IsEmpty)
					{
						Packet packet = default(Packet);
						KeyValuePair<uint[], Message> m = sendTargetsQueue.Dequeue();
						packet.Create(m.Value.data, (PacketFlags) m.Value.type);
						List<Peer> targets = new();
						for (int i = 0; i < m.Key.Length; i++)
						{
							targets.Add(peers[m.Key[i]]);
						}

						server.Broadcast(0, ref packet, targets.ToArray());
					}
				}

				foreach (var peer in peers)
				{
					peer.Value.Disconnect(0);
				}

				server.Flush();
			}
		}

		public bool IsRunning()
		{
			return IOThread.IsAlive;
		}

		public void Send(Message m)
		{
			sendQueue.Enqueue(m);
		}

		public void Send(Message m, uint excludedID)
		{
			sendExcludedQueue.Enqueue(new KeyValuePair<uint, Message>(excludedID, m));
		}

		public void Send(Message m, uint[] targetsIDs)
		{
			sendTargetsQueue.Enqueue(new KeyValuePair<uint[], Message>(targetsIDs, m));
		}

		public void Stop()
		{
			Interlocked.Increment(ref shouldStop);
		}

		public ClientData GetClient(uint id)
		{
			return clients[id];
		}
	}
}