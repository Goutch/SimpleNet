using System.Net.Sockets;
using ENet;

namespace SimpleNet
{
	public enum ServerMessageFormat : byte
	{
		CreateRoom = 0, //ServerMessageFormat(1)|private(1)|MaxPlayers(1)|name(~)
		JoinRoom = 1, //ServerMessageFormat(1)|RoomID(4)|
		RequestPublicRooms=3,//ServerMessageFormat(1)|
	}

	public enum ClientMessageFormat : byte
	{
		JoinedRoom,//ClientMessageFormat(1)|ID(4)
		ClientID,//ClientMessageFormat(1)|Room{OwnerID(4),Name(16)}
		RemoteClientJoinedRoom,//ClientMessageFormat(1)|ClientID(4)
		AvailablePublicRooms,//ClientMessageFormat(1)|RoomsArray(~)
		UserMessage, //ClientMessageFormat(1)|data(~)
		LinkMessage //ClientMessageFormat(1)|LinkMessageFormat(1)
	}

	public enum LinkMessageFormat : byte
	{
		ChangedOwnership = 0, //HeaderType(1)|ownerID
		UserMessage = 1, //HeaderType(1)|
	}

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