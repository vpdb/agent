using System.Collections.Generic;
using System.Xml.Serialization;

namespace VpdbAgent.PinballX.Models
{
	[XmlRoot("menu")]
	public class PinballXMenu
	{
		[XmlElement("game")]
		public List<PinballXGame> Games = new List<PinballXGame>();
	}
}
