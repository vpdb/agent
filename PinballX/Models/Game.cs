using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace VpdbAgent.PinballX.Models
{
	[XmlRoot("game")]
	public class Game
	{
		// "official hyperpin" fields
		// ----------------------------------
		[XmlAttribute("name")]
		public String Filename;

		[XmlElement("description")]
		public String Description;

		[XmlElement("manufacturer")]
		public String Manufacturer;

		[XmlElement("year")]
		public String Year;

		[XmlElement("type")]
		public String Type;

		// pinballx fields
		// ----------------------------------

		// vpdb fields
		// ----------------------------------
		[XmlAttribute("vpdb-release-id")]
		public String ReleaseId;
	}
}
