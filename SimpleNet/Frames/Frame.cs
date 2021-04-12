using System;
using System.Collections.Generic;
using SimpleNet.Properties;

namespace SimpleNet.Frames
{
	public class Frame
	{
		[Flags]
		public enum FrameType
		{
			UnreliableOrdered = 0,
			Reliable = 1,
			UnreliableUnordered = 2,
		}

		public enum ToServerFormat : byte
		{
			ServerMessage,
			RelayEntityMessage,
			BroadCastEntityMessage,
			RelayClientMessage,
			BroadcastClientMessage,
			CreateEntityRequest,
			GiveEntityOwnershipRequest
		}

		public enum ToClientFormat : byte
		{
			Connection,
			ClientMessage,
			EntityMessage,
			Error,
			EntityCreated,
			EntityChangedOwnership,
		}


		protected List<byte> data;
		public List<byte> Data => data;
		private FrameType type;
		public FrameType Type => type;

		public Frame(FrameType type)
		{
			this.type = type;
			data = new List<byte>();
		}

		public void AddFrame(Frame frame)
		{
			if (frame.type != Type)
				throw new Exception("Frame should be the same type");
			this.data.AddRange(frame.Data);
		}

		public void AddData(byte[] data)
		{
			this.data.AddRange(data);
		}
	}
}