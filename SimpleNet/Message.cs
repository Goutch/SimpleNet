using System;
using ENet;

namespace SimpleNet.Properties
{
	public class Message
	{
		[Flags]
		public enum MessageType
		{
			UnreliableOrdered=0,
			Reliable=1,
			UnreliableUnordered=2,
		}

		public MessageType type=MessageType.Reliable;
		public byte[] data;

		public Message()
		{
		}

		public Message(byte[] data, MessageType type = MessageType.Reliable)
		{
			this.data = data;
			
			this.type = type;
		}
	}
}