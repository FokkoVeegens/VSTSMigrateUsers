# VSTSMigrateUsers
Easily migrate a Visual Studio Team Services (VSTS) account from using MSA's (Microsoft Accounts) to AAD (Azure Active Directory) accounts

# What it does
The output of this code is a (Windows) command line utility that is able to do two things:
* Copy direct group membership from one to another user inside VSTS (and do this for multiple users)
* Reassign Work Items from one to another user

# Prerequisites
On beforehand, the following tasks need to be done manually:
* Ensure the MSA and the AAD account are available in the VSTS instance

# Configuration
The tool requires a configuration file as input. A sample configuration file is provided. The most important settings to change are:
* Provide your VSTS account (vstsUri)
* Provide the user mapping(s) (UserMapping)
* Change "contoso" to the subdomain of your VSTS account (vstsSecurityServiceGroup) 
* It is recommended to leave other settings as they are

# Usage
To use it, open a command prompt and call the executable with the configuration file as a parameter:
```
VSTSPermCopy.exe .\sample.xml
```

# Note
Keep in mind that direct object permissions (like e.g. permissions given to a user on a Source Control folder) are not copied!

# Changes
I'm open to pull requests and feature requests.
