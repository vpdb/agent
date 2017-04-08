using System.Collections.Generic;
using System.Runtime.Serialization;

namespace VpdbAgent.Vpdb.Models
{
	public class VpdbUser
	{
		[DataMember] public string Id { get; set; }
		[DataMember] public string Name { get; set; }
		[DataMember] public string Username { get; set; }
		[DataMember] public string GravatarId { get; set; }
		[DataMember] public VpdbCounter Counter { get; set; }

		public class VpdbCounter
		{
			[DataMember] public int Comments { get; set; }
			[DataMember] public int Downloads { get; set; }
			[DataMember] public int Stars { get; set; }
		}
	}

	public class VpdbUserFull : VpdbUser
	{
		[DataMember] public VpdbPlan Plan { get; set; }
		[DataMember] public string Provider { get; set; }
		[DataMember] public string Email { get; set; }
		[DataMember] public string CreatedAt { get; set; }
		[DataMember] public bool IsActive { get; set; }
		[DataMember] public List<string> Roles { get; set; }
		[DataMember] public VpdbChannelConfig ChannelConfig { get; set; }
		[DataMember] public VpdbPermission Permissions { get; set; }
		[DataMember] public VpdbQuota Quota { get; set; }

		public class VpdbPermission
		{
			[DataMember] public List<string> Users { get; set; }
			[DataMember] public List<string> Roles { get; set; }
			[DataMember] public List<string> Ipdb { get; set; }
			[DataMember] public List<string> Builds { get; set; }
			[DataMember] public List<string> Tags { get; set; }
			[DataMember] public List<string> Games { get; set; }
			[DataMember] public List<string> Roms { get; set; }
			[DataMember] public List<string> Files { get; set; }
			[DataMember] public List<string> Releases { get; set; }
			[DataMember] public List<string> Comments { get; set; }
			[DataMember] public List<string> Tokens { get; set; }
			[DataMember] public List<string> User { get; set; }
			[DataMember] public List<string> Messages { get; set; }
		}

		public class VpdbChannelConfig
		{
			[DataMember] public string ApiKey { get; set; }
			[DataMember] public List<string> SubscribedReleases { get; set; }
			[DataMember] public bool SubscribeToStarred { get; set; }
		}

		public class VpdbPlan
		{
			[DataMember] public string Id { get; set; }
			[DataMember] public bool AppTokensEnabled { get; set; }
			[DataMember] public bool PushNotificationsEnabled { get; set; }
		}

		public class VpdbQuota
		{
			[DataMember] public int Limit { get; set; }
			[DataMember] public VpdbPeriod Period { get; set; }
			[DataMember] public int Remaining { get; set; }
			[DataMember] public long Reset { get; set; }
			[DataMember] public bool Unlimited { get; set; }
		}

		public enum VpdbPeriod
		{
			Minute, Hour, Day, Week, Never
		}
	}
}
