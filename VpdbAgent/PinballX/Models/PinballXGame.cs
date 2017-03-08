using System.Xml.Serialization;
using ReactiveUI;
using VpdbAgent.Models;

namespace VpdbAgent.PinballX.Models
{
	[XmlRoot("game")]
	public class PinballXGame : ReactiveObject
	{
	
		// "official hyperpin" fields
		// ----------------------------------

		/// <summary>
		/// Filename without extension and path. Maps to <see cref="Game.Filename"/>,
		/// apart from the latter including extension if file exists.
		/// </summary>
		[XmlAttribute("name")]
		public string Filename { get { return _fileName; } set { this.RaiseAndSetIfChanged(ref _fileName, value); } }

		/// <summary>
		/// The identifier used also for media. Maps to <see cref="Game.Id"/>.
		/// </summary>
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
		public string Enabled { get { return _enabled; } set { this.RaiseAndSetIfChanged(ref _enabled, value); } }

		[XmlElement("rating")]
		public double Rating { get; set; }

		[XmlElement("AlternateExe")]
		public string AlternateExe { get { return _alternateExe; } set { this.RaiseAndSetIfChanged(ref _alternateExe, value); } }

		[XmlElement("SendKeysOnStart")]
		public string SendKeysOnStart { get; set; }


		// internal fields (not serialized)
		// ----------------------------------
		[XmlIgnore]
		public string DatabaseFile { get { return _databaseFile; } set { this.RaiseAndSetIfChanged(ref _databaseFile, value); } }

		[XmlIgnore]
		public PinballXSystem System { get; set; }


		// watched props
		private string _fileName;
		private string _alternateExe;
		private string _enabled;
		private string _databaseFile;

		public void Update(PinballXGame newGame)
		{
			Filename = newGame.Filename;
			Manufacturer = newGame.Manufacturer;
			Year = newGame.Year;
			Type = newGame.Type;
			HideDmd = newGame.HideDmd;
			HideBackglass = newGame.HideBackglass;
			Enabled = newGame.Enabled;
			Rating = newGame.Rating;
			AlternateExe = newGame.AlternateExe;
			SendKeysOnStart = newGame.SendKeysOnStart;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) {
				return false;
			}
			if (ReferenceEquals(this, obj)) {
				return true;
			}
			var game = obj as PinballXGame;
			if (game == null) {
				return false;
			}

			// really, we only care about those.
			return 
				Filename == game.Filename &&
				Description == game.Description &&
				Enabled == game.Enabled &&
				AlternateExe == game.AlternateExe;
		}
		
		public override int GetHashCode()
		{
			// ReSharper disable once NonReadonlyMemberInGetHashCode
			return Description.GetHashCode();
		}
	}
}
