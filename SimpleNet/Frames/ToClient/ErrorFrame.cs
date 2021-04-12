namespace SimpleNet.Frames.ToClient
{
	public class ErrorFrame : Frame
	{
		public ErrorFrame(string error) : base(FrameType.Reliable)
		{
			data.Add((byte) ToClientFormat.Error);
			data.AddRange(ByteUtils.StringToBytes(error));
		}
	}
}