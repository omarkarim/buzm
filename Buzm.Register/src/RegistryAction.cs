using System;

namespace Buzm.Register
{
	/// <summary>The list of actions that can be
	/// requested from a Registry. Explicit values 
	/// assigned for backward compatibility </summary>
	public enum RegistryAction : int
	{
		None = 0,
		LoginUser = 1,
		InsertUser = 2,
		UpdateUser = 3,
		DeleteUser = 4,
		InsertHive = 5,
		UpdateHive = 6,
		DeleteHive = 7,
		InsertFeeds = 8,
		UpdateFeeds = 9,
		DeleteFeeds = 10,
		AcceptInvite = 11,
		InsertMembers = 12,
		UpdateMembers = 13,
		DeleteMembers = 14
	}
}
