namespace SimpleNet.Frames.ToClient
{
	public class EntityMessageFrame : Frame
	{
		public EntityMessageFrame(FrameType type) : base(type)
		{
			data.Add((byte) Frame.ToClientFormat.EntityMessage);
		}
	}
}