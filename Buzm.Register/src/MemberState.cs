using System;

namespace Buzm.Register
{
	/// <summary>The list of states a User
	/// can have as a Hive member </summary>
	public enum MemberState : int
	{
		None = 0,
		Invited = 1,
		Active = 2
	}
}