using System.Collections.Generic;

namespace VpdbAgent.Vpdb.Models
{
	public class VpdbUser
	{
		public string Id { get; set; }
		public string Name { get; set; }
		public string Username { get; set; }
		public string GravatarId { get; set; }
		public VpdbCounter Counter { get; set; }

		public class VpdbCounter
		{
			public int Comments { get; set; }
			public int Downloads { get; set; }
			public int Stars { get; set; }
		}
	}

	public class VpdbUserFull : VpdbUser
	{
		public VpdbPlan Plan { get; set; }
		public string Provider { get; set; }
		public string Email { get; set; }
		public string CreatedAt { get; set; }
		public bool IsActive { get; set; }
		public List<string> Roles { get; set; }
		public VpdbChannelConfig ChannelConfig { get; set; }
		public VpdbPermission Permissions { get; set; }
		public VpdbQuota Quota { get; set; }

		public class VpdbPermission
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

		public class VpdbChannelConfig
		{
			public string ApiKey { get; set; }
			public List<string> SubscribedReleases { get; set; }
			public bool SubscribeToStarred { get; set; }
		}

		public class VpdbPlan
		{
			public string Id { get; set; }
			public bool AppTokensEnabled { get; set; }
			public bool PushNotificationsEnabled { get; set; }
		}

		public class VpdbQuota
		{
			public int Limit { get; set; }
			public VpdbPeriod Period { get; set; }
			public int Remaining { get; set; }
			public long Reset { get; set; }
			public bool Unlimited { get; set; }
		}

		public enum VpdbPeriod
		{
			Minute, Hour, Day, Week, Never
		}
	}
}
