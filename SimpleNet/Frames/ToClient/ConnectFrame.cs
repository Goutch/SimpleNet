using System;

namespace SimpleNet.Frames.ToClient
{
	public class ConnectFrame:Frame
	{
		public ConnectFrame(uint id):base(FrameType.Reliable)
		{
			this.data.Add((byte)ToClientFormat.Connection);
			this.data.AddRange(BitConverter.GetBytes(id));
		}
	}
}