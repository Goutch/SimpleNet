namespace SimpleNet.Frames.ToClient
{
	public class ClientMessageFrame:Frame
	{
		public ClientMessageFrame(FrameType type) : base(type)
		{
			data.Add((byte)ToClientFormat.ClientMessage);
		}
	}
}