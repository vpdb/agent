using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
