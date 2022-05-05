using System;
using System.Drawing;
using System.Diagnostics;
using System.Collections;
using System.Windows.Forms;
using System.Drawing.Drawing2D;

namespace Buzm.Graphing
{
	public class NetworkGraphView : System.Windows.Forms.UserControl
	{	
		private int m_DrawCount;
		private float m_Radius;
		private float m_Spacing;
		private float m_Diameter;		
		private float m_RootAngle;
		private float m_SiblingCurve;

		private GraphNode m_RootNode;
		private Hashtable m_NodeRegistry;
		
		public NetworkGraphView( )
		{
			// Initialize UI
			InitializeComponent();

			// Drawing settings
			m_DrawCount = 0;
			m_Spacing = 100.0F;			
			m_RootAngle = 360.0F;	
			m_Diameter = 14.0F;
			m_Radius = m_Diameter / 2.0F;
			m_SiblingCurve = 20.0F;

			// Initialize store for nodes
			m_NodeRegistry = new Hashtable();
		}

		public void AddRootNode( string rootNodeGuid )
		{
			// Create root node
			m_RootNode = new GraphNode( rootNodeGuid );
			m_RootNode.MaxDrawAngle = m_RootAngle;
			m_NodeRegistry.Add( m_RootNode.Guid, m_RootNode );

			// Add node to the control
			this.SuspendLayout();						
			this.Controls.AddRange( new System.Windows.Forms.Control[]{ m_RootNode } ); 
			this.ResumeLayout();
		}

		public void AddNode( string parentGuid, string childGuid )
		{
			GraphNode childNode;
			GraphNode parentNode; 			

			// Avoid stranded and redundant graph nodes
			if( m_NodeRegistry.Contains( parentGuid ) && (parentGuid != childGuid) )
			{
				// Retreive parent node from global registry
				parentNode = (GraphNode)m_NodeRegistry[ parentGuid ];

				// Both nodes are already in the registry
				if( m_NodeRegistry.Contains( childGuid ) )
				{
					// Retreive child node from global registry
					childNode = (GraphNode)m_NodeRegistry[ childGuid ];

					// Ensure that this relationship doesn't exist
					if( ( !parentNode.Children.Contains( childGuid ) &&
						  !parentNode.Siblings.Contains( childGuid ) ) &&
						( !childNode.Children.Contains( parentGuid ) &&
						  !childNode.Siblings.Contains( parentGuid ) ) )
					{
						// Apparently, circular references are not a
						// problem in .NET garbage collection :)
						parentNode.AddSibling( childNode );
						childNode.AddSibling( parentNode ); 
					}
				}
				else
				{
					childNode = new GraphNode( childGuid, parentNode );
					m_NodeRegistry.Add( childGuid, childNode );
					parentNode.AddChild( childNode );

					// Add node to the user control
					this.SuspendLayout();
					this.Controls.AddRange( new System.Windows.Forms.Control[]{ childNode } ); 
					this.ResumeLayout();
				}
				
				// Activate child node
				childNode.Alive = true;
			}
		}

		// Called if a node reports a shutdown
		public void RemoveNode( string nodeGuid )
		{
			if( m_NodeRegistry.Contains( nodeGuid ) )
			{
				GraphNode node = (GraphNode)m_NodeRegistry[ nodeGuid ];
				node.DisconnectAll(); 
				
				// Remove relationships
				// Currently, only the look is changed
				// for debugging purposes
				// m_NodeRegistry.Remove( node.Guid );
				// TODO: need to remove from container as well since
				// the node guid is now free to be added again
			}
		}

		// Not currently supported:
		// Called if a parent node loses connection
		// to a child node, but the child node 
		// might still be reachable on the network
		public void RemoveNode( string parentGuid, string childGuid )
		{
			/* Pseudo:
			 * a) Remove parent/child relationship
			 * b) Upgrade one of the child node's siblings to a parent
			 *    (Should store parent in child to optimize this)
			 * c) If no sibling, see if there are any non-orphaned
			 *    children that could become parents :)
			 */
		}

		public void SetNodeStatus( string nodeGuid, string status, Color flash )
		{
			if( m_NodeRegistry.Contains( nodeGuid ) )
			{
				GraphNode node = (GraphNode)m_NodeRegistry[ nodeGuid ];
				node.FlashStatus( flash );
				node.Status = status;
			}
		}

		private void DrawGraph( PaintEventArgs e )
		{	
			if( m_RootNode != null )
			{
				// Find the start location for drawing
				m_RootNode.NodeLocation = new PointF( (this.Width / 2.0F) + this.AutoScrollPosition.X, 
					(this.Height / 2.0F)  + this.AutoScrollPosition.Y );
				
				// Apparently it's more efficient to 
				// define Pens every time than globally
				Pen childPen = new Pen(Color.Black, 2);			
				Pen siblingPen = new Pen(Color.Black, 1);
				siblingPen.DashStyle = DashStyle.Dash;
				
				//Start drawing recursion
				DrawNode( m_RootNode, e, childPen, siblingPen );
			}
		}

		private void DrawNode( GraphNode parentNode, PaintEventArgs e, Pen childPen, Pen siblingPen )
		{			
			GraphicsPath path;
			PointF[] siblingPoints;
			RectangleF childLocation;			

			int index = 0;
			float nodeAngle = 0.0F;
			int nodeCount = parentNode.Children.Count;
			float angleIncrement = parentNode.MaxDrawAngle / nodeCount;
			float spacing = m_Spacing * nodeCount;
			parentNode.DrawCount = m_DrawCount;
			PointF parentLocation = parentNode.NodeLocation;			
			
			// Draw children of the current node			
			foreach( GraphNode childNode in parentNode.Children.Values )
			{
				// Calculate node angle to draw
				if( parentNode.Root ) nodeAngle = angleIncrement * index;
				else 
				{	// TODO : Move partial nodeAngle calc out of loop and optimize/unhack
					if( nodeCount > 1 ) nodeAngle = ((parentNode.MaxDrawAngle/(nodeCount - 1.0F)) * index) + (parentNode.DrawAngle - (parentNode.MaxDrawAngle/2.0F));
					else nodeAngle = parentNode.DrawAngle;
				}
				
				// Find node angle on arc
				path = new GraphicsPath();
				path.AddArc( parentLocation.X - (spacing / 2.0F), parentLocation.Y - (spacing / 2.0F), spacing, spacing, nodeAngle, 1);
				
				// Find node location on arc
				childLocation = path.GetBounds();
				e.Graphics.DrawLine( childPen, parentLocation.X + m_Radius, parentLocation.Y + m_Radius, childLocation.X + m_Radius, childLocation.Y + m_Radius );
				
				// Recurse node children and draw them
				childNode.NodeLocation = new PointF( childLocation.X, childLocation.Y );
				childNode.RootLocation = m_RootNode.NodeLocation;
				childNode.DrawAngle = nodeAngle;
				DrawNode( childNode, e, childPen, siblingPen );
				index++;
			}

			// Draw lines to siblings of the current node			
			foreach( GraphNode siblingNode in parentNode.Siblings.Values )
			{
				// If the sibling has been drawn this time around
				if( siblingNode.DrawCount >= parentNode.DrawCount )
				{
					// Calculate curve path and draw
					siblingPoints = new PointF[] {	new PointF( parentLocation.X + m_Radius, parentLocation.Y + m_Radius ), 
													new PointF( (parentLocation.X + siblingNode.NodeLocation.X)/2 + m_SiblingCurve, (parentLocation.Y + siblingNode.NodeLocation.Y)/2  + m_SiblingCurve ),
													new PointF( siblingNode.NodeLocation.X + m_Radius, siblingNode.NodeLocation.Y + m_Radius ) };
					e.Graphics.DrawCurve( siblingPen, siblingPoints );
				}
			}

			// Draw current node last to overlap lines
			parentNode.Draw();
		}

		protected override void OnPaint(PaintEventArgs e) 
		{
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			m_DrawCount++; // Used by nodes to synchronize iterations
			DrawGraph( e ); // Recursively draws the network graph
		}

		protected override void OnResize(EventArgs e) 
		{
			base.OnResize( e );
			Invalidate();
		}

		private void InitializeComponent()
		{
			// 
			// NetworkGraphView
			// 
			this.AutoScroll = true;
			this.AutoScrollMargin = new System.Drawing.Size(10, 10);
			this.Name = "NetworkGraphView";
			this.Size = new System.Drawing.Size(1024, 768);

		}		
	}
}