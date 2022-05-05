
namespace Buzm.Utility
{
	// delegate used to pass information about a model update to its views
	public delegate void ModelEventHandler( object sender, ModelEventArgs e );

	/// <summary>Encapsulates a generic MVC design pattern event. The controller 
	/// that initiated the change is included to prevent circular calls </summary>
	public class ModelEventArgs
	{
		private object m_Model;		
		private string m_ModelGuid;		
		private object m_Controller;
		private bool m_UpdateViews;
		private bool m_NotifyUser;
		
		public ModelEventArgs( string modelGuid, object model ) : this( modelGuid, model, null ){}
		public ModelEventArgs( string modelGuid, object model, object controller )
		{
			m_Model = model;			
			m_ModelGuid = modelGuid;
			m_Controller = controller;
			m_UpdateViews = true;
			m_NotifyUser = false;
		}

		public object Model
		{ 
			get { return m_Model; } 
			set { m_Model = value; }
		}

		public string ModelGuid
		{ 
			get { return m_ModelGuid; } 
			set { m_ModelGuid = value; }
		}

		public bool UpdateViews
		{ 
			get { return m_UpdateViews; } 
			set { m_UpdateViews = value; }
		}

		public bool NotifyUser
		{
			get { return m_NotifyUser; }
			set { m_NotifyUser = value; }
		}
		
		public object Controller
		{ 
			get { return m_Controller; } 
			set { m_Controller = value; }
		}		
	}
}
