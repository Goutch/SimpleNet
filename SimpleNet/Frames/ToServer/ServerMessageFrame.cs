namespace SimpleNet.Frames
{
	public class ServerMessageFrame:Frame
	{
		public ServerMessageFrame(FrameType type):base(type)
		{
			data.Add((byte)ToServerFormat.ServerMessage);
		}
	}
}