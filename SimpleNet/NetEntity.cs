namespace SimpleNet
{
	public class NetEntity
	{
		private uint owner;
		public uint Owner => owner;
		private uint id;
		public uint ID => id;
		
		public NetEntity(uint id,uint owner)
		{
			this.id = id;
			this.owner = owner;
		}
	}
}