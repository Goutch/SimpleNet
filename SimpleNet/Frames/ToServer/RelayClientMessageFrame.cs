namespace SimpleNet.Frames
{
	public class RelayClientMessageFrame:Frame
	{
		public RelayClientMessageFrame(FrameType type) : base(type)
		{
			data.Add((byte)ToServerFormat.RelayClientMessage);
			data.Add((byte)type);
		}
	}
}