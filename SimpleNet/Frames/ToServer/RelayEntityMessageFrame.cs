using System;

namespace SimpleNet.Frames
{
	public class RelayEntityMessageFrame : Frame
	{
		public RelayEntityMessageFrame(FrameType type, uint entityID) : base(type)
		{
			data.Add((byte) Frame.ToServerFormat.RelayEntityMessage);
			data.Add((byte) type);
			data.AddRange(BitConverter.GetBytes(entityID));
		}
	}
}