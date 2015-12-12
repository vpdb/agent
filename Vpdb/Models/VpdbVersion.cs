﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using System.Runtime.Serialization;
using System.Windows.Forms;

namespace VpdbAgent.Vpdb.Models
{
	public class VpdbVersion : ReactiveObject
	{
		[JsonProperty(PropertyName = "version")]
		[DataMember] public string Name { get; set; }
		[DataMember] public List<VpdbTableFile> Files { get; set; }

		public override string ToString()
		{
			return $"{Name} ({Files.Count} files)";
		}
	}
}