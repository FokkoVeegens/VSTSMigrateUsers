# VSTSMigrateUsers
Easily migrate a Visual Studio Team Services (VSTS) account from using MSA's (Microsoft Accounts) to AAD (Azure Active Directory) accounts

# What it does
The output of this code is a (Windows) command line utility that is able to do two things:
* Copy direct group membership from one to another user inside VSTS
* Reassign Work Items from one to another user

# Prerequisites
On beforehand, the following tasks need to be done manually:
* Ensure the MSA and the AAD account are available in the VSTS instance

# Note
Keep in mind that direct object permissions (like e.g. permissions given to a user on a Source Control folder) are not copied!

# Changes
I'm open to pull requests and feature requests.
