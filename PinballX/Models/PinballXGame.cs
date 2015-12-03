using System.Xml.Serialization;

namespace VpdbAgent.PinballX.Models
{
	[XmlRoot("game")]
	public class PinballXGame
	{
	
		// "official hyperpin" fields
		// ----------------------------------
		[XmlAttribute("name")]
		public string Filename { get; set; }

		[XmlElement("description")]
		public string Description { get; set; }

		[XmlElement("manufacturer")]
		public string Manufacturer { get; set; }

		[XmlElement("year")]
		public string Year { get; set; }

		[XmlElement("type")]
		public string Type { get; set; }


		// pinballx fields
		// ----------------------------------
		[XmlElement("hidedmd")]
		public string HideDmd { get; set; }

		[XmlElement("hidebackglass")]
		public string HideBackglass { get; set; }

		[XmlElement("enabled")]
		public string Enabled { get; set; }

		[XmlElement("rating")]
		public double Rating { get; set; }

		[XmlElement("AlternateExe")]
		public string AlternateExe { get; set; }

		[XmlElement("SendKeysOnStart")]
		public string SendKeysOnStart { get; set; }

		// vpdb fields (not serialized)
		// ----------------------------------
		[XmlIgnore]
		public string DatabaseFile { get; set; }
	}
}
