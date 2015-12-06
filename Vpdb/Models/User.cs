using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace VpdbAgent.Vpdb.Models
{
	public class User
	{
		public string Id { get; set; }
		public string Name { get; set; }
		public string Username { get; set; }
		public string GravatarId { get; set; }
		public UserCounter Counter { get; set; }

		public class UserCounter
		{
			public int Comments { get; set; }
			public int Downloads { get; set; }
			public int Stars { get; set; }
		}
	}

	public class UserFull : User
	{
		public PlanDetails Plan { get; set; }
		public string Provider { get; set; }
		public string Email { get; set; }
		public string CreatedAt { get; set; }
		public bool IsActive { get; set; }
		public List<string> Roles { get; set; }
		public Permission Permissions { get; set; }
		public QuotaDetails Quota { get; set; }

		public class Permission
		{
			public List<string> Users { get; set; }
			public List<string> Roles { get; set; }
			public List<string> Ipdb { get; set; }
			public List<string> Builds { get; set; }
			public List<string> Tags { get; set; }
			public List<string> Games { get; set; }
			public List<string> Roms { get; set; }
			public List<string> Files { get; set; }
			public List<string> Releases { get; set; }
			public List<string> Comments { get; set; }
			public List<string> Tokens { get; set; }
			public List<string> User { get; set; }
			public List<string> Messages { get; set; }
		}

		public class PlanDetails
		{
			public string Id { get; set; }
			public bool AppTokensEnabled { get; set; }
			public bool PushNotificationsEnabled { get; set; }
		}

		public class QuotaDetails
		{
			public int Limit { get; set; }
			public Period Period { get; set; }
			public int Remaining { get; set; }
			public long Reset { get; set; }
			public bool Unlimited { get; set; }
		}

		public enum Period
		{
			Minute, Hour, Day, Week, Never
		}
	}
}
