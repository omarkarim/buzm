using System;
using System.Drawing;
using System.Windows.Forms;
using Buzm.Hives;
using Buzm.Schemas;
using Buzm.Utility;
using Buzm.Register;
using Buzm.Network.Feeds;

namespace Buzm
{
	public class HiveController : System.Windows.Forms.UserControl 
	{
		private User m_HiveUser;
		private Font m_BoldFont;
		private DateTime m_TodayDate;
		private HiveManager m_HiveManager;
		private Size m_CalendarDimensions;
		
		// default values for treeview node names
		private const string NODE_SUFFIX = " ({0})";
		private const string BUZBOX_PREFIX = "My Buzbox";
		private const string MEMBER_PENDING = " (invited)";
		private const string MEMBERS_NODE_NAME = "Members";
		private const string FEEDS_NODE_NAME = "Feeds";
		
		private System.Windows.Forms.Panel m_MainPanel;
		private System.Windows.Forms.TreeView m_TreeView;
		private System.Windows.Forms.Panel m_CalendarPanel;		
		private System.Windows.Forms.Timer m_CalendarTimer;
		private System.Windows.Forms.MonthCalendar m_Calendar;		
		private System.Windows.Forms.TreeNode m_RootHivesNode;		
		private System.Windows.Forms.LinkLabel m_SelectAllLabel;
		private System.Windows.Forms.ImageList m_TreeViewImageList;
		private System.Windows.Forms.Splitter m_HorizontalSplitter;		
		private System.ComponentModel.IContainer components;

		public HiveController( HiveManager hiveManager ) 
		{
			m_HiveManager = hiveManager; // save hive lifecycle manager
			m_TodayDate = DateTime.Now.Date; // record date for calendar
			InitializeComponent(); // windows forms designer generated code
			InitializeManualComponents(); // custom forms initialization code

			m_HiveManager.HiveAdded	+= new ModelEventHandler( HiveManager_HiveAdded );
			m_HiveManager.HiveRemoved += new ModelEventHandler( HiveManager_HiveRemoved );
			m_HiveManager.HiveSelected += new ModelEventHandler( HiveManager_HiveSelected );
		}

		public void HiveManager_HiveAdded( object sender, ModelEventArgs e )
		{
			if( e.Model is HiveModel ) // ensure model is the right type
			{
				// save HiveModel for processing
				HiveModel hive = (HiveModel)e.Model;

				string hiveName; // format hive name
				if( hive.UserOwned ) hiveName = hive.Name;
				else hiveName = hive.Name + String.Format( NODE_SUFFIX, hive.Host );

				// create folder node to organize hive feeds
				TreeNode feedsTreeNode = new TreeNode( FEEDS_NODE_NAME, 4, 5 );
				feedsTreeNode.Name = FEEDS_NODE_NAME; // set name for key lookup

				// create folder node to organize hive members
				TreeNode membersTreeNode = new TreeNode( MEMBERS_NODE_NAME, 6, 7 );
				membersTreeNode.Name = MEMBERS_NODE_NAME; // set name for key lookup

				// create hive node and add feed and member folders to it
				TreeNode[] folders = new TreeNode[] { feedsTreeNode, membersTreeNode };
				TreeNode hiveTreeNode = new TreeNode( hiveName, 2, 3, folders );

				hiveTreeNode.Name = hive.Guid; // set name for key lookup
				hiveTreeNode.Tag = hive; // set tag for hive processing
				m_RootHivesNode.Nodes.Add( hiveTreeNode ); // add hive

				// subscribe to HiveModel events for future updates
				hive.Updated += new ModelEventHandler(HiveModel_Updated);
				hive.FeedAdded += new ModelEventHandler( HiveModel_FeedAdded );
				hive.FeedRemoved += new ModelEventHandler( HiveModel_FeedRemoved );
				hive.MemberAdded += new ModelEventHandler( HiveModel_MemberAdded );
				hive.MemberRemoved += new ModelEventHandler( HiveModel_MemberRemoved );
			}
		}

		public void HiveManager_HiveRemoved( object sender, ModelEventArgs e )
		{	
			if( e.Model is HiveModel ) // ensure model is of the right type
			{
				// save HiveModel for processing
				HiveModel hive = (HiveModel)e.Model;

				// iterate through hives looking for a match
				foreach( TreeNode hiveNode in m_RootHivesNode.Nodes )
				{ 
					if( hiveNode.Tag == hive ) // if match is found
					{
						hive.MemberRemoved -= new ModelEventHandler( HiveModel_MemberRemoved );
						hive.MemberAdded -= new ModelEventHandler( HiveModel_MemberAdded );
						hive.FeedRemoved -= new ModelEventHandler( HiveModel_FeedRemoved );
						hive.FeedAdded -= new ModelEventHandler( HiveModel_FeedAdded );
						hive.Updated -= new ModelEventHandler(HiveModel_Updated);						
						hiveNode.Remove(); break; // remove hive from tree
					}
				}
			}
		}

		public void HiveModel_FeedAdded( object sender, ModelEventArgs e )
		{
			if( ( sender is HiveModel ) && ( e.Model is FeedModel ) ) 
			{
				HiveModel hive = (HiveModel)sender;				
				FeedModel feed = (FeedModel)e.Model;

				TreeNode feedNode = new TreeNode( feed.Name, 9, 9 );
				feedNode.Tag = feed; // set tag for future processing
				feedNode.Name = feed.Guid; // set name for key lookup

				TreeNode feedsNode = FindChildNode( m_RootHivesNode, hive.Guid, FEEDS_NODE_NAME );
				if( feedsNode != null ) feedsNode.Nodes.Add( feedNode );				
			}
		}

		public void HiveModel_FeedRemoved( object sender, ModelEventArgs e )
		{
			// if event requires UI feedback and arguments are of the right type
			if( e.UpdateViews && (( sender is HiveModel ) && ( e.Model is FeedModel )) ) 
			{
				HiveModel hive = (HiveModel)sender; // cast event sender to Hive
				FeedModel feed = (FeedModel)e.Model; // cast event model to Feed

				TreeNode feedsNode = FindChildNode( m_RootHivesNode, hive.Guid, FEEDS_NODE_NAME );
				if( feedsNode != null ) feedsNode.Nodes.RemoveByKey( feed.Guid );				
			}
		}

		public void HiveModel_MemberAdded( object sender, ModelEventArgs e )
		{
			if( ( sender is HiveModel ) && ( e.Model is UserConfigType ) )
			{
				HiveModel hive = (HiveModel)sender;
				UserConfigType member = (UserConfigType)e.Model;

				string memberName = member.Login; // name active or pending members
				if( String.IsNullOrEmpty( memberName ) ) memberName = member.Email + MEMBER_PENDING;
				
				TreeNode memberNode = new TreeNode( memberName, 8, 8 );
				memberNode.Tag = member; // set tag for future processing
				memberNode.Name = member.Guid; // set name for key lookup

				TreeNode membersNode = FindChildNode( m_RootHivesNode, hive.Guid, MEMBERS_NODE_NAME );
				if( membersNode != null ) membersNode.Nodes.Add( memberNode );
			}
		}

		public void HiveModel_MemberRemoved( object sender, ModelEventArgs e )
		{
			// if event requires UI feedback and arguments are of the right type
			if( e.UpdateViews && ( ( sender is HiveModel ) && ( e.Model is UserConfigType ) ) )
			{
				HiveModel hive = (HiveModel)sender;
				UserConfigType member = (UserConfigType)e.Model;

				TreeNode membersNode = FindChildNode( m_RootHivesNode, hive.Guid, MEMBERS_NODE_NAME );
				if( membersNode != null ) membersNode.Nodes.RemoveByKey( member.Guid );
			}
		}

		private void m_TreeView_AfterSelect( object sender, System.Windows.Forms.TreeViewEventArgs e )
		{
			if( e.Action != TreeViewAction.Unknown ) // only respond to UI events
			{
				HiveModel model = FindNearestHive( e.Node ); // search for hive
				if( model != null ) m_HiveManager.SelectHive( model, this );
			}
		}

		public void DeleteMenuItem_Click( object sender, System.EventArgs e )
		{			
			TreeNode node = m_TreeView.SelectedNode;
			if( (node != null) && (node.Tag != null) ) 					
			{
				if( node.Tag is HiveModel )
				{
					HiveModel hive = (HiveModel)( node.Tag );
					ModelEventArgs args = new ModelEventArgs( hive.Guid, hive );
					m_HiveManager.RemoveHive_Click( this, args );
				}
				else if( node.Tag is FeedModel )
				{
					HiveModel hive = FindNearestHive( node );
					FeedModel feed = (FeedModel)( node.Tag );
					m_HiveManager.RemoveFeed_Click( hive, feed );
				}
				else if( node.Tag is UserConfigType )
				{
					HiveModel hive = FindNearestHive( node );
					UserConfigType member = (UserConfigType)( node.Tag );
					m_HiveManager.RemoveMember_Click( hive, member );
				}
			}			
		}

		private HiveModel FindNearestHive( TreeNode leafNode )
		{
			if( leafNode.Tag is HiveModel ) return (HiveModel)(leafNode.Tag);
			else // search recursively up the tree for nearest hive model
			{
				TreeNode parentNode = leafNode.Parent; // next level up
				if( parentNode != null ) return FindNearestHive( parentNode );
				else return null; // leaf is a top-level tree node
			}
		}

		private TreeNode FindChildNode( TreeNode parent, string childKey )
		{
			int childIndex = parent.Nodes.IndexOfKey( childKey );
			if( childIndex != -1 ) return parent.Nodes[childIndex];
			else return null; // no match found for child in parent
		}

		private TreeNode FindChildNode( TreeNode grandParent, string parentKey, string childKey )
		{			
			int parentIndex = grandParent.Nodes.IndexOfKey( parentKey );			
			if( parentIndex != -1 ) // if parent node was found
			{
				TreeNode parentNode = grandParent.Nodes[parentIndex];
				if( parentNode != null ) // and parent node exists
				{
					int childIndex = parentNode.Nodes.IndexOfKey( childKey );
					if( childIndex != -1 ) return parentNode.Nodes[childIndex];
				}
			}
			return null; // no match found for parent and/or child
		}

		public void HiveManager_HiveSelected( object sender, ModelEventArgs e )
		{
			HiveModel hive = e.Model as HiveModel;
			if( hive != null ) // if args contain hive
			{
				// set the calender view to hive date range
				SetCalendarDates( hive.StartDate, hive.EndDate );

				TreeNode hiveNode = FindChildNode( m_RootHivesNode, hive.Guid );
				if( hiveNode != null ) // if hive found in tree
				{
					if( e.Controller != this ) m_TreeView.SelectedNode = hiveNode;
					SetNodeFont( hiveNode, m_TreeView.Font );
				}
			}
		}

		public void HiveModel_Updated( object sender, ModelEventArgs e )
		{
			HiveModel hive = sender as HiveModel;
			if( hive != null ) // if sender is a hive
			{				
				if( hive == m_HiveManager.SelectedHive )
				{
					// set dates if selected hive has been updated
					SetCalendarDates( hive.StartDate, hive.EndDate );
				}
				else if( e.NotifyUser ) // highlight hive folder name in bold
				{
					TreeNode hiveNode = FindChildNode( m_RootHivesNode, hive.Guid );
					if( hiveNode != null ) SetNodeFont( hiveNode, m_BoldFont );
				}
			}
		}

		private void m_SelectAllLabel_LinkClicked( object sender, System.Windows.Forms.LinkLabelLinkClickedEventArgs e )
		{
			HiveModel model = m_HiveManager.SelectedHive;
			if( model != null ) // if selected model exists
			{
				// select date range from the creation of the hive to now
				SetCalendarDates( model.CreateDate.Date, DateTime.Now.Date );
			}
		}

		private void m_Calendar_DateChanged( object sender, System.Windows.Forms.DateRangeEventArgs e )
		{	
			HiveModel model = m_HiveManager.SelectedHive;
			if( model != null ) // if selected model exists
			{
				// Only rebuild the hive if the current dates are different
				if( DateTime.Compare( model.StartDate.Date, e.Start.Date ) != 0 ||
					DateTime.Compare( model.EndDate.Date, e.End.Date ) != 0 )
				{
					model.StartDate = e.Start.Date;
					model.EndDate = e.End.Date;
					model.UpdateViews();
				}	
			}
		}

		private void m_CalendarTimer_Tick( object sender, EventArgs e )
		{
			if( m_Calendar.TodayDate != m_TodayDate )
			{
				// reset hive dates past midnight
				m_TodayDate = m_Calendar.TodayDate;
				m_HiveManager.ResetAllHiveDates();
			}
		}

		private void HorizontalSplitter_SplitterMoving( object sender, SplitterEventArgs e)
		{
			// Snap the the splitter to the appropriate position
			e.SplitY = ( this.Height - SnapToHeight( this.Height - e.SplitY ) );
		}

		private void HorizontalSplitter_SplitterMoved( object sender, SplitterEventArgs e)
		{
			ResetCalendarHeight(); // since it was inaccurate while moving
		}

		private void HiveController_Load( object sender, EventArgs e )
		{
			ResetCalendarHeight(); // since it may be inaccurate on start
		}

		private int CalculateDimensions( int length, int multipleLength )
		{
			int multiple  = length / multipleLength;
			int remainder = length % multipleLength;
			
			if( remainder > (multipleLength / 2) ) multiple++;
			if( multiple  > 0 ) return multiple;
			else return 1;
		}

		public int SnapToWidth( int width )
		{
			int xDimension = CalculateDimensions( width, m_Calendar.SingleMonthSize.Width );
			m_Calendar.CalendarDimensions = new Size( xDimension, m_Calendar.CalendarDimensions.Height );
			return m_Calendar.Width;
		}

		public int SnapToHeight( int height )
		{
			int yDimension = CalculateDimensions( height, m_Calendar.SingleMonthSize.Height );
			int calHeight  = yDimension * (m_Calendar.SingleMonthSize.Height - 5) + 5;
			m_CalendarDimensions = new Size( m_Calendar.CalendarDimensions.Width, yDimension );
			return calHeight;
		}

		private void ResetCalendarHeight()
		{
			m_Calendar.CalendarDimensions = m_CalendarDimensions;
			m_CalendarPanel.Height = m_Calendar.Height;
		}

		private void SetCalendarDates( DateTime start, DateTime end )
		{
			if( ( m_Calendar.SelectionStart.Date != start.Date ) 
			 || ( m_Calendar.SelectionEnd.Date != end.Date ) )
			{
				// set date range if current dates are different
				m_Calendar.SetSelectionRange( start.Date, end.Date );
			}
		}

		private void SetNodeFont( TreeNode node, Font font )
		{
			if( node.NodeFont != font ) // if font not already set
			{
				node.NodeFont = font; // set and force update
				node.Text = node.Text + String.Empty;
			}
		}

		public User HiveUser
		{
			get { return m_HiveUser; }
			set 
			{ 
				m_HiveUser = value; 
				if( m_RootHivesNode != null )
				{
					// update user login in buzbox text on sign-in & sign-out
					if( m_HiveUser == null ) m_RootHivesNode.Text = BUZBOX_PREFIX;
					else m_RootHivesNode.Text = BUZBOX_PREFIX + String.Format( NODE_SUFFIX, m_HiveUser.Login );
				}		
			}
		}
		
		#region Windows Form Designer and Custom UI Initializers
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() 
		{
			this.components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(HiveController));
			this.m_MainPanel = new System.Windows.Forms.Panel();
			this.m_TreeView = new System.Windows.Forms.TreeView();
			this.m_TreeViewImageList = new System.Windows.Forms.ImageList(this.components);
			this.m_HorizontalSplitter = new System.Windows.Forms.Splitter();
			this.m_CalendarPanel = new System.Windows.Forms.Panel();
			this.m_SelectAllLabel = new System.Windows.Forms.LinkLabel();
			this.m_Calendar = new System.Windows.Forms.MonthCalendar();
			this.m_CalendarTimer = new System.Windows.Forms.Timer(this.components);
			this.m_MainPanel.SuspendLayout();
			this.m_CalendarPanel.SuspendLayout();
			this.SuspendLayout();
			// 
			// m_MainPanel
			// 
			this.m_MainPanel.Controls.Add(this.m_TreeView);
			this.m_MainPanel.Controls.Add(this.m_HorizontalSplitter);
			this.m_MainPanel.Controls.Add(this.m_CalendarPanel);
			this.m_MainPanel.Dock = System.Windows.Forms.DockStyle.Fill;
			this.m_MainPanel.Location = new System.Drawing.Point(0, 0);
			this.m_MainPanel.Name = "m_MainPanel";
			this.m_MainPanel.Size = new System.Drawing.Size(176, 550);
			this.m_MainPanel.TabIndex = 2;
			// 
			// m_TreeView
			// 
			this.m_TreeView.BackColor = System.Drawing.SystemColors.Window;
			this.m_TreeView.Dock = System.Windows.Forms.DockStyle.Fill;
			this.m_TreeView.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_TreeView.HideSelection = false;
			this.m_TreeView.ImageIndex = 0;
			this.m_TreeView.ImageList = this.m_TreeViewImageList;
			this.m_TreeView.Location = new System.Drawing.Point(0, 0);
			this.m_TreeView.Name = "m_TreeView";
			this.m_TreeView.SelectedImageIndex = 0;
			this.m_TreeView.Size = new System.Drawing.Size(176, 412);
			this.m_TreeView.Sorted = true;
			this.m_TreeView.TabIndex = 1;
			this.m_TreeView.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.m_TreeView_AfterSelect);
			// 
			// m_TreeViewImageList
			// 
			this.m_TreeViewImageList.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("m_TreeViewImageList.ImageStream")));
			this.m_TreeViewImageList.TransparentColor = System.Drawing.Color.Fuchsia;
			this.m_TreeViewImageList.Images.SetKeyName(0, "VSFolder_closed.bmp");
			this.m_TreeViewImageList.Images.SetKeyName(1, "VSFolder_open.bmp");
			this.m_TreeViewImageList.Images.SetKeyName(2, "VSFolder_closed.bmp");
			this.m_TreeViewImageList.Images.SetKeyName(3, "VSFolder_open.bmp");
			this.m_TreeViewImageList.Images.SetKeyName(4, "VSFolder_closed.bmp");
			this.m_TreeViewImageList.Images.SetKeyName(5, "VSFolder_open.bmp");
			this.m_TreeViewImageList.Images.SetKeyName(6, "VSFolder_closed.bmp");
			this.m_TreeViewImageList.Images.SetKeyName(7, "VSFolder_open.bmp");
			this.m_TreeViewImageList.Images.SetKeyName(8, "");
			this.m_TreeViewImageList.Images.SetKeyName(9, "");
			// 
			// m_HorizontalSplitter
			// 
			this.m_HorizontalSplitter.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.m_HorizontalSplitter.Location = new System.Drawing.Point(0, 412);
			this.m_HorizontalSplitter.Name = "m_HorizontalSplitter";
			this.m_HorizontalSplitter.Size = new System.Drawing.Size(176, 2);
			this.m_HorizontalSplitter.TabIndex = 1;
			this.m_HorizontalSplitter.TabStop = false;
			this.m_HorizontalSplitter.SplitterMoved += new System.Windows.Forms.SplitterEventHandler(this.HorizontalSplitter_SplitterMoved);
			this.m_HorizontalSplitter.SplitterMoving += new System.Windows.Forms.SplitterEventHandler(this.HorizontalSplitter_SplitterMoving);
			// 
			// m_CalendarPanel
			// 
			this.m_CalendarPanel.BackColor = System.Drawing.SystemColors.Control;
			this.m_CalendarPanel.Controls.Add(this.m_SelectAllLabel);
			this.m_CalendarPanel.Controls.Add(this.m_Calendar);
			this.m_CalendarPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.m_CalendarPanel.Location = new System.Drawing.Point(0, 414);
			this.m_CalendarPanel.Name = "m_CalendarPanel";
			this.m_CalendarPanel.Size = new System.Drawing.Size(176, 136);
			this.m_CalendarPanel.TabIndex = 0;
			// 
			// m_SelectAllLabel
			// 
			this.m_SelectAllLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.m_SelectAllLabel.Font = new System.Drawing.Font("Arial", 6.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_SelectAllLabel.ForeColor = System.Drawing.SystemColors.WindowText;
			this.m_SelectAllLabel.LinkColor = System.Drawing.SystemColors.WindowText;
			this.m_SelectAllLabel.Location = new System.Drawing.Point(126, 121);
			this.m_SelectAllLabel.Name = "m_SelectAllLabel";
			this.m_SelectAllLabel.Size = new System.Drawing.Size(45, 12);
			this.m_SelectAllLabel.TabIndex = 1;
			this.m_SelectAllLabel.TabStop = true;
			this.m_SelectAllLabel.Text = "Select All";
			this.m_SelectAllLabel.Visible = false;
			this.m_SelectAllLabel.VisitedLinkColor = System.Drawing.SystemColors.WindowText;
			this.m_SelectAllLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.m_SelectAllLabel_LinkClicked);
			// 
			// m_Calendar
			// 
			this.m_Calendar.BackColor = System.Drawing.SystemColors.Control;
			this.m_Calendar.Font = new System.Drawing.Font("Arial", 6.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_Calendar.Location = new System.Drawing.Point(0, 0);
			this.m_Calendar.MaxSelectionCount = 10000;
			this.m_Calendar.Name = "m_Calendar";
			this.m_Calendar.TabIndex = 0;
			this.m_Calendar.TitleBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(153)))), ((int)(((byte)(153)))), ((int)(((byte)(153)))));
			this.m_Calendar.TitleForeColor = System.Drawing.Color.Black;
			this.m_Calendar.TrailingForeColor = System.Drawing.SystemColors.ControlDarkDark;
			this.m_Calendar.DateChanged += new System.Windows.Forms.DateRangeEventHandler(this.m_Calendar_DateChanged);
			// 
			// m_CalendarTimer
			// 
			this.m_CalendarTimer.Enabled = true;
			this.m_CalendarTimer.Interval = 180000;
			this.m_CalendarTimer.Tick += new System.EventHandler(this.m_CalendarTimer_Tick);
			// 
			// HiveController
			// 
			this.Controls.Add(this.m_MainPanel);
			this.Name = "HiveController";
			this.Size = new System.Drawing.Size(176, 550);
			this.Load += new System.EventHandler(this.HiveController_Load);
			this.m_MainPanel.ResumeLayout(false);
			this.m_CalendarPanel.ResumeLayout(false);
			this.ResumeLayout(false);

		}		

		/// <summary>This method initializes any UI properties
		/// the IDE Designer doesn't properly support </summary>
		private void InitializeManualComponents( )
		{
			// setup tree node that will contain all the hives
			m_RootHivesNode = new System.Windows.Forms.TreeNode( BUZBOX_PREFIX, 0, 1 );
			m_TreeView.Nodes.AddRange( new TreeNode[]{ m_RootHivesNode } );
			m_RootHivesNode.Expand();

			// setup user interface components
			m_CalendarDimensions = new Size( 1, 1 );
			m_BoldFont = new Font( m_TreeView.Font, FontStyle.Bold );
		}

		#endregion

		/// <summary> Clean up used resources </summary>
		protected override void Dispose( bool disposing ) 
		{
			if( disposing )
			{
				if(components != null)
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}
	}
}
