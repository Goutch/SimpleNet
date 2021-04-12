using System;

namespace SimpleNet.Frames
{
	public class CreateEntityFrame : Frame
	{
		public CreateEntityFrame(byte[] userData) : base(FrameType.Reliable)
		{
			data.Add((byte)Frame.ToServerFormat.CreateEntityRequest);
			this.data.AddRange(userData);
		}
	}
}