using System;
using System.Collections.Generic;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Framework.Client;
using Microsoft.TeamFoundation.Framework.Common;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System.Xml.Serialization;
using System.IO;

namespace VSTSMigrateUsers.CLI
{
	class Program
	{
		// Documentation on IIdentityManagementService: https://msdn.microsoft.com/en-us/library/microsoft.teamfoundation.framework.client.iidentitymanagementservice_methods(v=vs.120).aspx

		private static PermissionCopyConfiguration _config;
		private static TfsTeamProjectCollection _tfs;
		private static IIdentityManagementService _ims;
		private static WorkItemStore _wistore;
		private static List<string> _excludedGroups;
		private static string _securityServiceGroup;
		private static List<UserMapping> _userMappings;
		private enum LogLevel
		{
			Default,
			Ok,
			Warning,
			Error
		}

		static void Main(string[] args)
		{
			WriteLog("*** Copy Permissions tool ***");

			// Validate arguments
			if (args.Length == 0)
			{
				WriteLog(LogLevel.Warning, "Please provide the path to the configuration file\nAn example: VSTSMigrUsers.exe .\\sample.xml");
				Console.ReadKey();
				return;
			}

			// Retrieve configuration from the provided configuration file
			if (!GetSettings(args[0]))
			{
				Console.ReadKey();
				return;
			}

			// Execute the work
			foreach (UserMapping mapping in _userMappings)
			{
				if (!CopyPermissions(mapping.sourceUser, mapping.targetUser))
				{
					WriteLog("Stopped execution");
					break;
				}
				if (!ReassignWorkitems(mapping.sourceUser, mapping.targetUser))
				{
					WriteLog("Stopped execution");
					break;
				}
			}

			// Finalize
			WriteLog("Press a key to continue...");
			Console.ReadKey();
		}

		private static bool GetSettings(string filePath)
		{
			if (!File.Exists(filePath))
			{
				WriteLog(LogLevel.Error, string.Format("The file provided cannot be found ({0})", filePath));
				return false;
			}
			_config = new PermissionCopyConfiguration();
			try
			{
				// Read configuration file and transfor to PermissionCopyConfiguration object
				XmlSerializer x = new XmlSerializer(_config.GetType());
				using (StreamReader sr = new StreamReader(filePath))
				{
					_config = (PermissionCopyConfiguration)x.Deserialize(sr);
					_tfs = new TfsTeamProjectCollection(new Uri(_config.vstsUri));
					_ims = (IIdentityManagementService)_tfs.GetService<IIdentityManagementService>();
					_wistore = new WorkItemStore(_tfs, WorkItemStoreFlags.BypassRules);
					_excludedGroups = _config.excludedGroups;
					_securityServiceGroup = _config.vstsSecurityServiceGroup;
					_userMappings = _config.userMappings;
				}
			}
			catch (Exception ex)
			{
				WriteLog(LogLevel.Error, string.Format("An error occurred reading the configuration file. Please verify the format. The error is:\n{0}", ex.ToString()));
				return false;
			}
			
			return true;
		}

		private static bool ReassignWorkitems(string sourceUserName, string targetUserName)
		{
			// Retrieve all Work Items where the sourceUserName is in the Assigned To field
			string wiql = string.Format("SELECT System.Id, System.AssignedTo FROM WorkItems WHERE System.AssignedTo = '{0}'", sourceUserName);
			WorkItemCollection results = _wistore.Query(wiql);

			WriteLog(string.Format("Reassigning Work Items from {0} to {1} ({2} Work Item(s) found)", sourceUserName, targetUserName, results.Count));
			try
			{
				foreach (WorkItem workitem in results)
				{
					// Update Work Item
					workitem.Open();
					workitem.Fields["System.AssignedTo"].Value = targetUserName;
					workitem.Fields["System.History"].Value = "Updated by the VSTSPermCopy tool";
					workitem.Save();
				}
			}
			catch (Exception ex)
			{
				WriteLog(LogLevel.Error, string.Format("An error occurred while updating a Work Item: {0}\n\nUpdates for the current user are aborted.\nDo you want to continue with the next user (y/n)?", ex.ToString()));
				if (Console.ReadKey().Key != ConsoleKey.Y)
				{
					return false;
				}
			}
			return true;
		}

		private static bool CopyPermissions(string sourceUserName, string targetUserName)
		{
			// Retrieve user to copy from
			TeamFoundationIdentity sourceUser = GetIdentityByName(sourceUserName);
			TeamFoundationIdentity targetUser = GetIdentityByName(targetUserName);

			if (sourceUser == null)
			{
				WriteLog(LogLevel.Error, string.Format("The source user {0} cannot be found\nPlease add this user to the users list first before running this tool. Do you want to continue with the next user (y/n)?", sourceUserName));
				if (Console.ReadKey().Key != ConsoleKey.Y)
				{
					return false;
				}
			}

			if (targetUser == null)
			{
				WriteLog(LogLevel.Error, string.Format("The target user {0} cannot be found\nPlease add this user to the users list first before running this tool. Do you want to continue with the next user (y/n)?", targetUserName));
				if (Console.ReadKey().Key != ConsoleKey.Y)
				{
					return false;
				}
			}

			// Copy permissions based on direct group membership
			WriteLog(string.Format("Copying permissions from {0} to {1}", sourceUserName, targetUserName));
			foreach (IdentityDescriptor group in sourceUser.MemberOf)
			{
				TeamFoundationIdentity groupIdentity = GetIdentityBySid(group.Identifier);
				if (_excludedGroups.Contains(groupIdentity.DisplayName))
				{
					WriteLog(string.Format("Skipping group {0} because it is part of the exclusion list", groupIdentity.DisplayName));
					continue;
				}
				if (groupIdentity.DisplayName == _securityServiceGroup)
				{
					WriteLog(LogLevel.Warning, "WARNING: this user might have permissions on specific objects (outside groups), these permissions are not copied!");
					continue;
				}

				WriteLog(LogLevel.Ok, string.Format("Adding user {0} to group {1}", targetUserName, groupIdentity.DisplayName));

				try
				{
					_ims.AddMemberToApplicationGroup(group, targetUser.Descriptor);
				}
				catch (AddMemberIdentityAlreadyMemberException)
				{
					// Is already member, skip
					WriteLog(LogLevel.Ok, string.Format("The user {0} is already a member of the group {1}", targetUserName, groupIdentity.DisplayName));
				}
				catch (Exception ex)
				{
					WriteLog(LogLevel.Error, string.Format("The user {0} cannot be added to the group {1}: {2}\n\nDo you want to continue with the next user/group (y/n)?", targetUserName, groupIdentity.DisplayName, ex.ToString()));
					if (Console.ReadKey().Key != ConsoleKey.Y)
					{
						return false;
					}
				}
								
			}
			return true;
		}

		private static TeamFoundationIdentity GetIdentityByName(string accountName)
		{
			return _ims.ReadIdentity(IdentitySearchFactor.AccountName, accountName, MembershipQuery.Direct, ReadIdentityOptions.ExtendedProperties);
		}

		private static TeamFoundationIdentity GetIdentityBySid(string SID)
		{
			return _ims.ReadIdentity(IdentitySearchFactor.Identifier, SID, MembershipQuery.Direct, ReadIdentityOptions.ExtendedProperties);
		}

		private static void WriteLog(string logText)
		{
			WriteLog(LogLevel.Default, logText);
		}

		private static void WriteLog(LogLevel level, string logText)
		{
			switch (level)
			{
				case LogLevel.Ok:
					Console.ForegroundColor = ConsoleColor.Green;
					break;
				case LogLevel.Warning:
					Console.ForegroundColor = ConsoleColor.Yellow;
					break;
				case LogLevel.Error:
					Console.ForegroundColor = ConsoleColor.Red;
					break;
			}
			Console.WriteLine(logText);
			Console.ResetColor();
		}
	}
}
