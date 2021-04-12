using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using ENet;
using Microsoft.SqlServer.Server;
using SimpleNet.Frames;
using SimpleNet.Frames.ToClient;
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

		private ConcurrentQueue<Frame> broadcastQueue = new();
		private ConcurrentQueue<KeyValuePair<uint, Frame>> sendExcludedQueue = new();
		private ConcurrentQueue<KeyValuePair<uint[], Frame>> sendTargetsQueue = new();

		private uint entitiesIDCount = 0;

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
								sendTargetsQueue.Enqueue(new KeyValuePair<uint[], Frame>(
									new uint[1] {netEvent.Peer.ID},
									new ConnectFrame(netEvent.Peer.ID)));
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
								ParseReceiveData(netEvent.Packet.Data, netEvent.Packet.Length, netEvent.Peer.ID);
								netEvent.Packet.Dispose();
								break;
						}
					}

					//Send 

					while (!broadcastQueue.IsEmpty)
					{
						Packet packet = default(Packet);
						Frame f = broadcastQueue.Dequeue();
						packet.Create(f.Data.ToArray(), (PacketFlags) f.Type);
						server.Broadcast(0, ref packet);
					}

					while (!sendExcludedQueue.IsEmpty)
					{
						Packet packet = default(Packet);
						KeyValuePair<uint, Frame> m = sendExcludedQueue.Dequeue();
						packet.Create(m.Value.Data.ToArray(), (PacketFlags) m.Value.Type);
						server.Broadcast(0, ref packet, peers[m.Key]);
					}

					while (!sendTargetsQueue.IsEmpty)
					{
						Packet packet = default(Packet);
						KeyValuePair<uint[], Frame> m = sendTargetsQueue.Dequeue();
						packet.Create(m.Value.Data.ToArray(), (PacketFlags) m.Value.Type);
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

		private void ParseReceiveData(IntPtr data, int length, uint clientID)
		{
			int index = 0;
			Frame.ToServerFormat format = (Frame.ToServerFormat) ByteUtils.ReadByte(data, index);
			index++;
			switch (format)
			{
				case Frame.ToServerFormat.CreateEntityRequest:
					broadcastQueue.Enqueue(new CreatedEntityFrame(entitiesIDCount, clientID, ByteUtils.GetBytes(data, index, length - index)));
					entitiesIDCount++;
					break;
				case Frame.ToServerFormat.GiveEntityOwnershipRequest:
					break;
				case Frame.ToServerFormat.ServerMessage:
					receiveQueue.Enqueue(new KeyValuePair<uint, byte[]>(clientID, ByteUtils.GetBytes(data, index, length - index)));
					break;
				case Frame.ToServerFormat.BroadcastClientMessage:
					Frame.FrameType BroadcastFrameType = (Frame.FrameType) ByteUtils.ReadByte(data, index);
					index++;
					ClientMessageFrame clientMessage = new ClientMessageFrame(BroadcastFrameType);
					clientMessage.AddData(ByteUtils.GetBytes(data, index, length - index));
					broadcastQueue.Enqueue(clientMessage);
					break;
				case Frame.ToServerFormat.RelayEntityMessage:
					Frame.FrameType relayFrameType = (Frame.FrameType) ByteUtils.ReadByte(data, index);
					index++;
					EntityMessageFrame relayMessage = new EntityMessageFrame(relayFrameType);
					relayMessage.AddData(ByteUtils.GetBytes(data, index, length - index));
					sendExcludedQueue.Enqueue(new KeyValuePair<uint, Frame>(clientID, relayMessage));
					break;
				case Frame.ToServerFormat.BroadCastEntityMessage:
					Frame.FrameType broadcastFrameType = (Frame.FrameType) ByteUtils.ReadByte(data, index);
					index++;
					EntityMessageFrame broadcastMessage = new EntityMessageFrame(broadcastFrameType);
					broadcastMessage.AddData(ByteUtils.GetBytes(data, index, length - index));
					broadcastQueue.Enqueue(broadcastMessage);
					break;
				default:
					sendTargetsQueue.Enqueue(new KeyValuePair<uint[], Frame>(new uint[1] {clientID}, new ErrorFrame("Frame format nor supported")));
					break;
			}
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

		public bool IsRunning()
		{
			return IOThread.IsAlive;
		}

		public void Send(Frame f)
		{
			ClientMessageFrame messageFrame = new ClientMessageFrame(f.Type);
			messageFrame.AddFrame(f);
			broadcastQueue.Enqueue(messageFrame);
		}

		public void Send(Frame f, uint excludedID)
		{
			ClientMessageFrame messageFrame = new ClientMessageFrame(f.Type);
			messageFrame.AddFrame(f);
			sendExcludedQueue.Enqueue(new KeyValuePair<uint, Frame>(excludedID, messageFrame));
		}

		public void Send(Frame f, uint[] targetsIDs)
		{
			ClientMessageFrame messageFrame = new ClientMessageFrame(f.Type);
			messageFrame.AddFrame(f);
			sendTargetsQueue.Enqueue(new KeyValuePair<uint[], Frame>(targetsIDs, messageFrame));
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