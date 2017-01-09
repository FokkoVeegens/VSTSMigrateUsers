using System.Collections.Generic;

namespace VSTSMigrateUsers.CLI
{
	public class PermissionCopyConfiguration
	{
		public string vstsUri { get; set; }
		public string vstsSecurityServiceGroup { get; set; }
		public List<string> excludedGroups { get; set; }
		public List<UserMapping> userMappings { get; set; }
	}
}
