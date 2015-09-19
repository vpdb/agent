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
		[XmlIgnore]
		public PinballXSystem System { get; set; }

		// "official hyperpin" fields
		// ----------------------------------
		[XmlAttribute("name")]
		public String Filename { get; set; }

		[XmlElement("description")]
		public String Description { get; set; }

		[XmlElement("manufacturer")]
		public String Manufacturer { get; set; }

		[XmlElement("year")]
		public String Year { get; set; }

		[XmlElement("type")]
		public String Type { get; set; }

		// pinballx fields
		// ----------------------------------

		// vpdb fields
		// ----------------------------------
		[XmlAttribute("vpdb-release-id")]
		public String ReleaseId;
	}
}
