using System;
using System.Threading;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections;
using System.Windows.Forms;
using System.Collections.Specialized;

namespace Buzm.Graphing
{
	public class GraphNode : System.Windows.Forms.UserControl
	{		
		private bool m_Root;
		private bool m_Alive;
		private string m_Guid;
		private int m_DrawCount;

		private GraphNode m_Parent;
		private ListDictionary m_ChildrenList;
		private ListDictionary m_SiblingsList;
		
		// Drawing properties		
		private float m_Diameter;	
		private float m_DrawAngle;
		private float m_MaxDrawAngle;
		private PointF m_RootLocation;
		private PointF m_NodeLocation;
		private NodeStatus m_NodeStatus;
		private Color m_ActiveColor;
		private Color m_ShutdownColor;
		private Color m_DisconnectedColor;
		private System.Windows.Forms.Timer m_DisconnectTimer;
		private System.ComponentModel.IContainer components;
		private int m_NodeStatusBorder;

		// Empty construct for form designer
		public GraphNode( ) : this( "0" ){ }

		public GraphNode( string guid, GraphNode parent ) : this( guid )
		{ 
			m_Root = false;
			m_Parent = parent;
		}

		public GraphNode( string guid )
		{
			// Initialize UI
			InitializeComponent();			
			this.Visible = false;

			m_Guid  = guid;
			m_Root  = true;
			m_ChildrenList = new ListDictionary();
			m_SiblingsList = new ListDictionary();
			
			// Drawing Defaults
			m_DrawCount = 0;
			m_Diameter = 14.0F;
			m_DrawAngle = 0.0F;
			m_MaxDrawAngle = 90.0F;			
			m_NodeStatusBorder = 3;
			m_ActiveColor = BackColor;
			m_ShutdownColor = Color.Red;
			m_DisconnectedColor = Color.Gray;
			m_NodeLocation = new PointF( 0.0F, 0.0F );
			m_RootLocation = new PointF( 0.0F, 0.0F );

			// Setup NodeStatus 
			m_NodeStatus.Visible = false;
			m_NodeStatus.Header.Text = "id: " + guid;			
			m_NodeStatus.Location = new Point( Convert.ToInt32(m_Diameter) + m_NodeStatusBorder, 0);
			m_NodeStatus.Header.Click += new System.EventHandler(this.m_NodeStatusHeader_Click);
		}

		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			this.m_NodeStatus = new NodeStatus();
			this.m_DisconnectTimer = new System.Windows.Forms.Timer(this.components);
			this.SuspendLayout();
			// 
			// m_NodeStatus
			// 
			this.m_NodeStatus.BackColor = System.Drawing.Color.FromArgb(((System.Byte)(255)), ((System.Byte)(102)), ((System.Byte)(0)));
			this.m_NodeStatus.DockPadding.All = 1;
			this.m_NodeStatus.Location = new System.Drawing.Point(16, 0);
			this.m_NodeStatus.Name = "m_NodeStatus";
			this.m_NodeStatus.TabIndex = 1;
			// 
			// m_DisconnectTimer
			// 
			this.m_DisconnectTimer.Interval = 3000;
			this.m_DisconnectTimer.Tick += new System.EventHandler(this.m_DisconnectTimer_Tick);
			// 
			// GraphNode
			// 
			this.BackColor = System.Drawing.Color.FromArgb(((System.Byte)(255)), ((System.Byte)(102)), ((System.Byte)(0)));
			this.Controls.Add(this.m_NodeStatus);
			this.Name = "GraphNode";
			this.Size = new System.Drawing.Size(184, 152);
			this.Click += new System.EventHandler(this.m_GraphNode_Click);
			this.ResumeLayout(false);

		}

		public void Draw( ) 
		{
			// Create path for clipping region
			GraphicsPath path = new GraphicsPath();

			// Align node depending on root position
			if( m_NodeLocation.X >= m_RootLocation.X )
			{
				//path.AddEllipse( 0, 0, m_Diameter, m_Diameter );
				path.AddRectangle( new RectangleF( 0, 0, m_Diameter, m_Diameter ) );
				this.Location = new Point(	Convert.ToInt32( m_NodeLocation.X ),
											Convert.ToInt32( m_NodeLocation.Y ) );
			}
			else // Align right
			{
				//path.AddEllipse( m_NodeStatus.Location.X + m_NodeStatus.Size.Width + 3, 0, m_Diameter, m_Diameter );
				path.AddRectangle( new RectangleF( m_NodeStatus.Location.X + m_NodeStatus.Size.Width + m_NodeStatusBorder, 0, m_Diameter, m_Diameter ) );
				this.Location = new Point(	Convert.ToInt32( m_NodeLocation.X - this.Width + m_Diameter ),
											Convert.ToInt32( m_NodeLocation.Y ) );
			}
				
			// Add region for status window
			if( m_NodeStatus.Visible == true ) path.AddRectangle( new Rectangle( m_NodeStatus.Location, m_NodeStatus.Size ) );			
			this.Region = new Region( path );			
			this.Show();
		}

		public void AddChild( GraphNode child )
		{
			m_ChildrenList.Add( child.Guid, child );
		}

		public void RemoveChild( GraphNode child )
		{
			m_ChildrenList.Remove( child.Guid );
		}

		public void AddSibling( GraphNode sibling )
		{
			m_SiblingsList.Add( sibling.Guid, sibling );
		}

		public void RemoveSibling( GraphNode sibling )
		{
			m_SiblingsList.Remove( sibling.Guid );
		}

		public void DisconnectAll( )
		{
			// Display disconnecting message
			Status = "exiting network...";
			BackColor = m_ShutdownColor;			
			m_NodeStatus.Visible = true; 
			Alive = false;
			Draw();		

			// Notify all children of disconnect
			foreach( GraphNode child in m_ChildrenList.Values )
			{
				child.DisconnectParent( this );
			}
            
			// For debugging purposes
			// the node is redrawn but
			// retains its connections:

			/*// Disconnect from parent
			m_Parent.RemoveChild( this );

			// Disconnect from all siblings
			foreach( GraphNode sibling in m_SiblingsList.Values )
			{
				sibling.RemoveSibling( this );
			}

			// Clear relationships
			m_SiblingsList.Clear();
			
			// TODO: This leaves children
			// visible but orphaned
			m_ChildrenList.Clear();

			// Remove node from parent
			m_DisconnectTimer.Enabled = true;*/
		}

		public void DisconnectParent( GraphNode parent )
		{
			// if this node is not reachable by another peer
			// theoretically, it can still be reached via a child
			if( m_SiblingsList.Count == 0 )
			{	
				// Display disconnecting message				
				Status = "lost connection...";
				BackColor = m_DisconnectedColor;			
				m_NodeStatus.Visible = true; 
				Alive = false;
				Draw();		
				
				// Notify all children of disconnect
				foreach( GraphNode child in m_ChildrenList.Values )
				{
					child.DisconnectParent( this );
				}
			}
		}

		private void m_DisconnectTimer_Tick(object sender, System.EventArgs e)
		{			
			m_DisconnectTimer.Enabled = false;
			Status = "exiting the network...";
			if( Parent != null ) Parent.Controls.Remove( this );
		}	

		public void FlashStatus( Color flashColor )
		{
			BackColor = flashColor;
			Refresh();
			Thread.Sleep( 500 );
			BackColor = m_ActiveColor;
		}

		public string Guid
		{
			get { return m_Guid; }
			set 
			{ 
				m_Guid = value; 
				m_NodeStatus.Header.Text = "id: " + value; 
			}
		}

		public string Status
		{
			get { return m_NodeStatus.Status.Text; }
			set 
			{ 
				m_NodeStatus.Status.AppendText( Environment.NewLine + "["
					+ DateTime.Now.ToString( "HH:mm" ) + "] " + value );
				m_NodeStatus.Status.Focus();
				m_NodeStatus.Status.ScrollToCaret();
			}
		}

		public bool Root
		{
			get { return m_Root; }
			set { m_Root = value; }
		}

		public bool Alive
		{
			get { return m_Alive; }
			set 
			{ 				
				if( value ) // If alive will be set to true
				{ 
					BackColor = m_ActiveColor;
					if( !m_Alive ) Status = "entering network...";
				}
				m_Alive = value; 
			}
		}

		public PointF NodeLocation
		{
			get { return m_NodeLocation; }
			set { m_NodeLocation = value; }
		}

		public PointF RootLocation
		{
			get { return m_RootLocation; }
			set { m_RootLocation = value; }
		}

		public float DrawAngle
		{
			get { return m_DrawAngle; }
			set { m_DrawAngle = value; }
		}

		public float MaxDrawAngle
		{
			get { return m_MaxDrawAngle; }
			set { m_MaxDrawAngle = value; }
		}

		public int DrawCount
		{
			get { return m_DrawCount; }
			set { m_DrawCount = value; }
		}

		public IDictionary Children { get { return m_ChildrenList; } }
		public IDictionary Siblings { get { return m_SiblingsList; } }

		private void m_GraphNode_Click(object sender, System.EventArgs e)
		{
			if( m_NodeStatus.Visible == true ) m_NodeStatus.Visible = false;
			else m_NodeStatus.Visible = true; 
			Draw(); // Redraw to set clipping			
		}	

		private void m_NodeStatusHeader_Click(object sender, System.EventArgs e)
		{
			this.BringToFront();
		}		
	}
}