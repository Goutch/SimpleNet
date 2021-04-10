using System.Collections.Generic;

namespace SimpleNet.Properties
{
	public class Room
	{
		int ownerID;
		private List<uint> clients;
		private Dictionary<uint,NetworkLink> entities;
	}
}