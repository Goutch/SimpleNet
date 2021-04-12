using System;

namespace SimpleNet.Frames.ToClient
{
	public class CreatedEntityFrame : Frame
	{
		public CreatedEntityFrame(uint entityID,uint owner, byte[] data) : base(FrameType.Reliable)
		{
			this.data.Add((byte) ToClientFormat.EntityCreated);
			this.data.AddRange(BitConverter.GetBytes(entityID));
			this.data.AddRange(BitConverter.GetBytes(owner));
			this.data.AddRange(data);
		}
	}
}