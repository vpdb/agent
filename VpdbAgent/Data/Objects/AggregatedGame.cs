using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VpdbAgent.PinballX.Models;

namespace VpdbAgent.Data.Objects
{
	public class AggregatedGame
	{
		public string FileName { get; set; }
		public long FileSize { get; set; }
		public PinballXGame PinballXGame { get; set; }

		public bool HasMapping => false;
		public bool HasLocalFile => false;
		public bool HasPinballXGame => PinballXGame != null;

		public AggregatedGame(PinballXGame game)
		{
			PinballXGame = game;
		}
	}
}
