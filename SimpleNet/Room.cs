using System.Collections.Generic;

namespace SimpleNet.Properties
{
	public class Room
	{		
		private string name;
		private List<uint> clients;
		private Dictionary<uint,NetworkEntity> entities;
	}
}