using System;

namespace SimpleNet.Frames
{
	public class BroadcastEntityMessageFrame:Frame
	{
		public BroadcastEntityMessageFrame(FrameType type,uint entityID) : base(type)
		{
			data.Add((byte)ToServerFormat.BroadCastEntityMessage);
			data.Add((byte) type);
			data.AddRange(BitConverter.GetBytes(entityID));
		}
	}
}