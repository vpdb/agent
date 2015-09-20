using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VpdbAgent.Models
{
	public class Game
	{
		public string Id { get; set; }
		public string Filename { get; set; }
		public string ReleaseId { get; set; }
		public bool Enabled { get; set; } = true;
	}
}
