namespace SimpleNet.Frames
{
	public class BroadcastClientMessageFrame : Frame
	{
		public BroadcastClientMessageFrame(FrameType type) : base(type)
		{
			data.Add((byte) ToServerFormat.BroadcastClientMessage);
			data.Add((byte) type);
		}
	}
}