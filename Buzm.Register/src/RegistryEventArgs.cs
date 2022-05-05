using System;

namespace Buzm.Register
{
	// delegate used to transfer information about a registry request
	public delegate void RegistryEventHandler( object sender, RegistryEventArgs e );

	/// <summary>User registry request</summary>
	public class RegistryEventArgs
	{
		private User m_User;
		private string m_ActionGuid;
		private RegistryAction m_Action;
		private RegistryResult m_Result;
		private string m_ResultMessage;

		public RegistryEventArgs( User user, RegistryAction action, string actionGuid )
		{
			m_User = user;
			m_Action = action;
			m_ActionGuid = actionGuid;
			m_Result = RegistryResult.None;
			m_ResultMessage = ""; // no result
		}

		public RegistryEventArgs( User user, RegistryResult result, string resultMsg, string actionGuid )
		{
			m_User = user;
			m_Result = result;			
			m_ActionGuid = actionGuid;
			m_ResultMessage = resultMsg;
			m_Action = RegistryAction.None;
		}

		public User User
		{ 
			get { return m_User; } 
			set { m_User = value; }
		}

		public string ActionGuid
		{ 
			get { return m_ActionGuid; } 
			set { m_ActionGuid = value; }
		}
		
		public RegistryAction Action
		{ 
			get { return m_Action; } 
			set { m_Action = value; }
		}		

		public RegistryResult Result
		{ 
			get { return m_Result; } 
			set { m_Result = value; }
		}
		
		public string ResultMessage
		{ 
			get { return m_ResultMessage; } 
			set { m_ResultMessage = value; }
		}
	}
}