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
		[XmlElement("hidedmd")]
		public String HideDmd { get; set; }

		[XmlElement("hidebackglass")]
		public String HideBackglass { get; set; }

		[XmlElement("enabled")]
		public String Enabled { get; set; }

		[XmlElement("rating")]
		public Double Rating { get; set; }

		[XmlElement("AlternateExe")]
		public String AlternateExe { get; set; }

		[XmlElement("SendKeysOnStart")]
		public String SendKeysOnStart { get; set; }

	}
}
