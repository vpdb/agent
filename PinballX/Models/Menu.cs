using System.Collections.Generic;
using System.Xml.Serialization;

namespace VpdbAgent.PinballX.Models
{
	[XmlRoot("menu")]
	public class Menu
	{
		[XmlElement("game")]
		public List<Game> Games = new List<Game>();
	}
}
