using System;
using System.Collections;
using System.Windows.Forms;
using Buzm.Network.Feeds;
using Buzm.Register;

namespace Buzm.Hives
{
	/// <summary>UI for editing feeds</summary>
	public class FeedEditor : RegistryEditor
	{
		User m_HiveUser;		
		string m_FeedXml;
		
		FeedModel m_NewFeed;
		FeedModel m_OldFeed;

		HiveModel m_OldHive;
		HiveModel m_CurrentHive;
		HiveManager m_HiveManager;
		
		private Hashtable m_PlaceTable;
		private string m_DefaultPlaceTag;

		private System.Windows.Forms.ComboBox m_HiveComboBox;
		private System.Windows.Forms.GroupBox m_PlacementGroupBox;
		private System.ComponentModel.IContainer components;
		private System.Windows.Forms.ToolTip m_PlaceToolTip;
		private System.Windows.Forms.ColumnHeader m_SearchResultNameCol;
		private System.Windows.Forms.ColumnHeader m_SearchResultDescCol;
		private System.Windows.Forms.Button m_SearchButton;
		private System.Windows.Forms.ComboBox m_PriorityComboBox;
		private System.Windows.Forms.ComboBox m_RefreshComboBox;
		private Buzm.Utility.Forms.SmartTextBox m_FeedTextBox;
		private System.Windows.Forms.Button m_CancelButton;
		private System.Windows.Forms.Button m_SaveButton;
		private System.Windows.Forms.Panel m_BottomPanel;
		private System.Windows.Forms.Panel m_HeaderPanel;
		private System.Windows.Forms.Panel m_CenterPanel;
		private System.Windows.Forms.ListView m_ResultsListView;
		private System.Windows.Forms.Label m_PriorityLabel;
		private System.Windows.Forms.Label m_RefreshLabel;
		private System.Windows.Forms.Label m_NameLabel;
		private System.Windows.Forms.TextBox m_NameTextBox;
		private System.Windows.Forms.Label m_QuantityLabel;
		private System.Windows.Forms.TextBox m_QuantityTextBox;
		private System.Windows.Forms.Label m_HiveLabel;
		private System.Windows.Forms.GroupBox m_SettingsGroupBox;
		private System.Windows.Forms.Label m_FeedLabel;

		private System.Windows.Forms.PictureBox m_PlaceBlOnPictureBox;
		private System.Windows.Forms.PictureBox m_PlaceBcOnPictureBox;
		private System.Windows.Forms.PictureBox m_PlaceBrOnPictureBox;
		private System.Windows.Forms.PictureBox m_PlaceTcOffPictureBox;
		private System.Windows.Forms.PictureBox m_PlaceBrOffPictureBox;
		private System.Windows.Forms.PictureBox m_PlaceTrOffPictureBox;
		private System.Windows.Forms.PictureBox m_PlaceBlOffPictureBox;
		private System.Windows.Forms.PictureBox m_PlaceBcOffPictureBox;
		private System.Windows.Forms.PictureBox m_PlaceTlOffPictureBox;
		private System.Windows.Forms.PictureBox m_CurrentPlacePictureBox;
		
		private const string DISABLED_PLACE_TEXT = "Reserved for Posts";		
		private const string INACTIVE_PLACE_TEXT = "Click to Select";		
		private const string EDIT_FORM_TEXT = "Edit Feed - Buzm";
		private const string SAVE_BUTTON_TEXT = "Save Feed";
		private const string ACTIVE_PLACE_TEXT = "Selected";

		public FeedEditor( User user, HiveManager hiveManager, HiveModel[] userHives )
		{
			m_HiveUser = user;			
			m_HiveManager = hiveManager;
				
			InitializeComponent(); // designer code
			m_SaveButton.Text = SAVE_BUTTON_TEXT;

			m_HiveComboBox.Items.AddRange( userHives );
			Action = RegistryAction.InsertFeeds;			

			// hashtable to store placements
			m_PlaceTable = new Hashtable();

			// save xslt positions in placement tags
			SetPlaceTag( m_PlaceTlOffPictureBox, "0,1" );
			SetPlaceTag( m_PlaceBlOffPictureBox, "0,2" );
			SetPlaceTag( m_PlaceTcOffPictureBox, "1,1" );
			SetPlaceTag( m_PlaceBcOffPictureBox, "1,2" );
			SetPlaceTag( m_PlaceTrOffPictureBox, "2,1" );
			SetPlaceTag( m_PlaceBrOffPictureBox, "2,2" );
			
			// set default bottom placement
			m_DefaultPlaceTag = "1,2"; // bc
			m_PlaceBcOffPictureBox.SendToBack();
			m_CurrentPlacePictureBox = m_PlaceBcOffPictureBox;

			// set tooltip text for available and disabled placement options
			m_PlaceToolTip.SetToolTip( m_PlaceBlOnPictureBox, ACTIVE_PLACE_TEXT );
			m_PlaceToolTip.SetToolTip( m_PlaceBcOnPictureBox, ACTIVE_PLACE_TEXT );
			m_PlaceToolTip.SetToolTip( m_PlaceBrOnPictureBox, ACTIVE_PLACE_TEXT );
			m_PlaceToolTip.SetToolTip( m_PlaceBlOffPictureBox, INACTIVE_PLACE_TEXT );
			m_PlaceToolTip.SetToolTip( m_PlaceBcOffPictureBox, INACTIVE_PLACE_TEXT );			
			m_PlaceToolTip.SetToolTip( m_PlaceBrOffPictureBox, INACTIVE_PLACE_TEXT );
			m_PlaceToolTip.SetToolTip( m_PlaceTlOffPictureBox, DISABLED_PLACE_TEXT );
			m_PlaceToolTip.SetToolTip( m_PlaceTcOffPictureBox, DISABLED_PLACE_TEXT );						
			m_PlaceToolTip.SetToolTip( m_PlaceTrOffPictureBox, DISABLED_PLACE_TEXT );						
		}
		
		public FeedEditor( FeedModel feed, HiveModel hive, User user, 
			HiveManager hiveManager, HiveModel[] userHives )
		  :	this( user, hiveManager, userHives )
		{
			m_OldHive = hive; // save default hive
			m_OldFeed = feed; // save feed to edit
			
			this.Text = EDIT_FORM_TEXT; // set title
			SelectHive( hive ); // select default hive
						
			m_FeedTextBox.UserText = feed.Url; // set feed url
			LoadPlacement( feed.Placement ); // select place box
		}

		private bool CreateFeed( string url, out FeedModel feed )
		{
			// create new FeedModel for input url
			string guid = Guid.NewGuid().ToString();
			feed = new FeedModel( guid, url, m_HiveUser.DataFolder );
			feed.Placement = m_CurrentPlacePictureBox.Tag.ToString();
			
			if( feed.CheckForUpdates( ) ) // if the feed source can be parsed
			{				
				m_FeedXml = feed.ToXml(); // save returned feed xml for hive addition
				if( ( m_FeedXml != null ) && ( m_FeedXml != String.Empty ) ) return true; 
				else AlertUser( "The requested feed uses an unsupported RSS version. Please try a different one." );
			}
			else AlertUser( "No RSS feed was found at the specified URL. Please try a different one." );
			return false; // if the code reached this point feed creation must have failed
		}

		private void m_SaveButton_Click( object sender, System.EventArgs e )
		{
			// if a hive has been selected by user
			if( m_HiveComboBox.SelectedItem != null )
			{
				// and that hive still exists in active hive table
				m_CurrentHive = (HiveModel)m_HiveComboBox.SelectedItem;
				if( m_HiveManager.HiveModels.Contains( m_CurrentHive.Guid ) )
				{
					if( m_FeedTextBox.Populated ) // if feed URL was specified
					{
						if( IsFeedModified() ) // and user made some field edits
						{
							DisableInterface(); // prevent user from resubmitting							

							// create feed from textbox url and validate its format
							if( CreateFeed( m_FeedTextBox.Text.Trim(), out m_NewFeed ) )
							{
								// submit new feed configuration to registry
								SetupRegistryRequest( m_NewFeed, m_CurrentHive );
							}
							else EnableInterface(); // allow user to try textbox input again							
						}
						else this.Close(); // no changes were made so no need to do anything
					}
					else AlertUser( "Please type or paste a URL for the desired RSS feed." );
				}
				else AlertUser( "The selected Hive has been deleted from this profile." );
			}
			else AlertUser( "Please select the Hive where this feed should be saved." );
		}

		public override void RegistryEditor_RegistryResponse( object sender, RegistryEventArgs e )
		{
			if( EndRegistryRequest( e ) ) // if request complete
			{
				if( e.Result == RegistryResult.Success )
				{
					bool removedOldFeed = false; // track feed edits
					if( (m_NewFeed != null) && (m_CurrentHive != null) )
					{
						// if new feed was successfully inserted
						if( Action == RegistryAction.InsertFeeds )
						{
							// and an old feed needs to be cleaned up
							if( (m_OldFeed != null) && (m_OldHive != null) )
							{
								Action = RegistryAction.DeleteFeeds;
								SetupRegistryRequest( m_OldFeed, m_OldHive );
								return; // continue delete workflow
							}
						}
						else if( Action == RegistryAction.DeleteFeeds
						&& ( m_OldFeed != null ) && ( m_OldHive != null )
						&& m_HiveManager.HiveModels.Contains( m_OldHive.Guid ) )
						{
							m_OldHive.RemoveFeed( m_OldFeed, false, this );
							removedOldFeed = true; // without saving hive
						}						
						if( !String.IsNullOrEmpty( m_FeedXml ) // have feed xml
						&& m_HiveManager.HiveModels.Contains( m_CurrentHive.Guid ) )
						{
							m_CurrentHive.AddFeed( m_NewFeed, this ); // add new feed
							m_CurrentHive.AddContent( m_FeedXml ); // add feed xml
							m_HiveManager.SelectHive( m_CurrentHive, this );
						}						
						if( removedOldFeed && (m_OldHive != m_CurrentHive) )
						{
							m_OldHive.SaveToStore(); // save old hive
							m_OldHive.UpdateViews(); // and render
						}
					}
					this.Close(); // hide feed editor interface
				}
				else 
				{
					AlertUser( e.ResultMessage ); // show error message
					EnableInterface(); // allow user to try input again
				}
			}
		}

		private void SetupRegistryRequest( FeedModel feed, HiveModel hive )
		{			
			ActionUser = m_HiveUser.CloneIdentity();
			ActionUser.SetHive( hive.ConfigToXml() );

			ActionUser.SetFeed( hive.Guid, feed.ConfigToXml() );
			BeginRegistryRequest(); // asynchronous update
		}

		private void m_PlacePictureBox_Click(object sender, System.EventArgs e)
		{
			PictureBox placePictureBox = ((PictureBox)sender);
			placePictureBox.SendToBack();
			m_CurrentPlacePictureBox.BringToFront();
			m_CurrentPlacePictureBox = placePictureBox;
		}

		private void SetPlaceTag( PictureBox placePictureBox, string tag )
		{
			placePictureBox.Tag = tag; // to find the tag by picturebox
			m_PlaceTable.Add( tag, placePictureBox ); // reverse lookup
		}

		private void LoadPlacement( string place )
		{
			if( !String.IsNullOrEmpty( place ) && (place != m_DefaultPlaceTag) )
			{
				object placePicBox = m_PlaceTable[place];
				if( placePicBox != null )
				{
					m_PlacePictureBox_Click( placePicBox, null );
					m_DefaultPlaceTag = place;
				}
			}
		}

		private bool IsFeedModified()
		{
			string place = m_CurrentPlacePictureBox.Tag.ToString();
			if( m_FeedTextBox.Modified // feed url edited
				|| ( place != m_DefaultPlaceTag ) 
				|| ( m_CurrentHive != m_OldHive ) )
				return true; // user edited feed config
			else return false; // no changes were made
		}
	
		private void m_CancelButton_Click( object sender, System.EventArgs e )
		{
			this.Close();
		}

		public void SelectHive( HiveModel hive )
		{
			if( m_HiveComboBox.Items.Contains( hive ) )
				m_HiveComboBox.SelectedItem = hive;
			else if( m_HiveComboBox.Items.Count > 0 )
				m_HiveComboBox.SelectedIndex = 0;
		}

		private void EnableInterface( )
		{
			this.Cursor = Cursors.Default;
			m_HiveComboBox.Enabled = true;
			m_FeedTextBox.Enabled = true;			
			m_PlacementGroupBox.Enabled = true;
			// m_SettingsGroupBox.Enabled = true;
			m_SaveButton.Enabled = true;
		}

		private void DisableInterface( )
		{
			this.Cursor = Cursors.WaitCursor;
			m_HiveComboBox.Enabled = false;
			m_FeedTextBox.Enabled = false;			
			m_PlacementGroupBox.Enabled = false;
			m_SettingsGroupBox.Enabled = false;
			m_SaveButton.Enabled = false;
		}

		private void FeedEditor_Shown( object sender, EventArgs e )
		{
			this.BringToFront(); // make sure form is visible
		}

		#region Disabled Search Code - Revisit for search implementation

		/*private void m_SaveButton_Click( object sender, System.EventArgs e )
		{
			// if a hive has been selected by user
			if( m_HiveComboBox.SelectedItem != null )
			{
				FeedModel feed = null; // loaded from search or textbox
				HiveModel hive = (HiveModel)m_HiveComboBox.SelectedItem;

				if( m_ResultsListView.SelectedItems.Count > 0 )
				{
					ListViewItem item = m_ResultsListView.SelectedItems[0];
					if( item.Tag is FeedModel ) feed = (FeedModel)item.Tag;
				}
				else feed = CreateFeed( m_FeedTextBox.Text );

				// register feed in user profile and hive
				if( feed != null ) SaveFeed( feed, hive );				
			}
			else AlertUser( "Please select the Hive where the feed should be added." );
		}*/

		private void m_SearchButton_Click( object sender, System.EventArgs e )
		{
			/*this.Height = 480; // resize window to show search results
			this.Cursor = Cursors.WaitCursor; // indicate processing
			m_SearchButton.Enabled = false; // avoid re-click

			// try to initialize global feed object
			if( InitializeFeed( m_FeedTextBox.Text ) )
			{
				string[] columns = new string[]{ m_Feed.Name, "" };
				ListViewItem result = new ListViewItem( columns );
				m_ResultsListView.Items.Add( result );				
				result.Selected = true;
				result.Tag = feed;
			}

			// allow new feed search
			m_SearchButton.Enabled = true;
			this.Cursor = Cursors.Default; */
		}

		#endregion
	
		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			System.Resources.ResourceManager resources = new System.Resources.ResourceManager(typeof(FeedEditor));
			this.m_FeedTextBox = new Buzm.Utility.Forms.SmartTextBox();
			this.m_FeedLabel = new System.Windows.Forms.Label();
			this.m_HiveLabel = new System.Windows.Forms.Label();
			this.m_HiveComboBox = new System.Windows.Forms.ComboBox();
			this.m_PriorityLabel = new System.Windows.Forms.Label();
			this.m_RefreshLabel = new System.Windows.Forms.Label();
			this.m_PlacementGroupBox = new System.Windows.Forms.GroupBox();
			this.m_PlaceBrOffPictureBox = new System.Windows.Forms.PictureBox();
			this.m_PlaceBlOffPictureBox = new System.Windows.Forms.PictureBox();
			this.m_PlaceBcOffPictureBox = new System.Windows.Forms.PictureBox();
			this.m_PlaceBlOnPictureBox = new System.Windows.Forms.PictureBox();
			this.m_PlaceBcOnPictureBox = new System.Windows.Forms.PictureBox();
			this.m_PlaceBrOnPictureBox = new System.Windows.Forms.PictureBox();
			this.m_PlaceTlOffPictureBox = new System.Windows.Forms.PictureBox();
			this.m_PlaceTcOffPictureBox = new System.Windows.Forms.PictureBox();
			this.m_PlaceTrOffPictureBox = new System.Windows.Forms.PictureBox();
			this.m_SettingsGroupBox = new System.Windows.Forms.GroupBox();
			this.m_QuantityTextBox = new System.Windows.Forms.TextBox();
			this.m_QuantityLabel = new System.Windows.Forms.Label();
			this.m_NameTextBox = new System.Windows.Forms.TextBox();
			this.m_NameLabel = new System.Windows.Forms.Label();
			this.m_PriorityComboBox = new System.Windows.Forms.ComboBox();
			this.m_RefreshComboBox = new System.Windows.Forms.ComboBox();
			this.m_PlaceToolTip = new System.Windows.Forms.ToolTip(this.components);
			this.m_ResultsListView = new System.Windows.Forms.ListView();
			this.m_SearchResultNameCol = new System.Windows.Forms.ColumnHeader();
			this.m_SearchResultDescCol = new System.Windows.Forms.ColumnHeader();
			this.m_SearchButton = new System.Windows.Forms.Button();
			this.m_CancelButton = new System.Windows.Forms.Button();
			this.m_SaveButton = new System.Windows.Forms.Button();
			this.m_BottomPanel = new System.Windows.Forms.Panel();
			this.m_HeaderPanel = new System.Windows.Forms.Panel();
			this.m_CenterPanel = new System.Windows.Forms.Panel();
			this.m_PlacementGroupBox.SuspendLayout();
			this.m_SettingsGroupBox.SuspendLayout();
			this.m_BottomPanel.SuspendLayout();
			this.m_HeaderPanel.SuspendLayout();
			this.m_CenterPanel.SuspendLayout();
			this.SuspendLayout();
			// 
			// m_ActionProgressBar
			// 
			this.m_ActionProgressBar.Location = ((System.Drawing.Point)(resources.GetObject("m_ActionProgressBar.Location")));
			this.m_ActionProgressBar.Name = "m_ActionProgressBar";
			this.m_ActionProgressBar.Size = ((System.Drawing.Size)(resources.GetObject("m_ActionProgressBar.Size")));
			// 
			// m_FeedTextBox
			// 
			this.m_FeedTextBox.AccessibleDescription = resources.GetString("m_FeedTextBox.AccessibleDescription");
			this.m_FeedTextBox.AccessibleName = resources.GetString("m_FeedTextBox.AccessibleName");
			this.m_FeedTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_FeedTextBox.Anchor")));
			this.m_FeedTextBox.AutoSize = ((bool)(resources.GetObject("m_FeedTextBox.AutoSize")));
			this.m_FeedTextBox.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("m_FeedTextBox.BackgroundImage")));
			this.m_FeedTextBox.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_FeedTextBox.Dock")));
			this.m_FeedTextBox.Enabled = ((bool)(resources.GetObject("m_FeedTextBox.Enabled")));
			this.m_FeedTextBox.FocusText = "";
			this.m_FeedTextBox.Font = ((System.Drawing.Font)(resources.GetObject("m_FeedTextBox.Font")));
			this.m_FeedTextBox.HelpText = "Type or paste URL for the RSS feed here...";
			this.m_FeedTextBox.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_FeedTextBox.ImeMode")));
			this.m_FeedTextBox.Location = ((System.Drawing.Point)(resources.GetObject("m_FeedTextBox.Location")));
			this.m_FeedTextBox.MaxLength = ((int)(resources.GetObject("m_FeedTextBox.MaxLength")));
			this.m_FeedTextBox.Multiline = ((bool)(resources.GetObject("m_FeedTextBox.Multiline")));
			this.m_FeedTextBox.Name = "m_FeedTextBox";
			this.m_FeedTextBox.PasswordChar = ((char)(resources.GetObject("m_FeedTextBox.PasswordChar")));
			this.m_FeedTextBox.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_FeedTextBox.RightToLeft")));
			this.m_FeedTextBox.ScrollBars = ((System.Windows.Forms.ScrollBars)(resources.GetObject("m_FeedTextBox.ScrollBars")));
			this.m_FeedTextBox.Size = ((System.Drawing.Size)(resources.GetObject("m_FeedTextBox.Size")));
			this.m_FeedTextBox.TabIndex = ((int)(resources.GetObject("m_FeedTextBox.TabIndex")));
			this.m_FeedTextBox.Text = resources.GetString("m_FeedTextBox.Text");
			this.m_FeedTextBox.TextAlign = ((System.Windows.Forms.HorizontalAlignment)(resources.GetObject("m_FeedTextBox.TextAlign")));
			this.m_PlaceToolTip.SetToolTip(this.m_FeedTextBox, resources.GetString("m_FeedTextBox.ToolTip"));
			this.m_FeedTextBox.Visible = ((bool)(resources.GetObject("m_FeedTextBox.Visible")));
			this.m_FeedTextBox.WordWrap = ((bool)(resources.GetObject("m_FeedTextBox.WordWrap")));
			// 
			// m_FeedLabel
			// 
			this.m_FeedLabel.AccessibleDescription = resources.GetString("m_FeedLabel.AccessibleDescription");
			this.m_FeedLabel.AccessibleName = resources.GetString("m_FeedLabel.AccessibleName");
			this.m_FeedLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_FeedLabel.Anchor")));
			this.m_FeedLabel.AutoSize = ((bool)(resources.GetObject("m_FeedLabel.AutoSize")));
			this.m_FeedLabel.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_FeedLabel.Dock")));
			this.m_FeedLabel.Enabled = ((bool)(resources.GetObject("m_FeedLabel.Enabled")));
			this.m_FeedLabel.Font = ((System.Drawing.Font)(resources.GetObject("m_FeedLabel.Font")));
			this.m_FeedLabel.Image = ((System.Drawing.Image)(resources.GetObject("m_FeedLabel.Image")));
			this.m_FeedLabel.ImageAlign = ((System.Drawing.ContentAlignment)(resources.GetObject("m_FeedLabel.ImageAlign")));
			this.m_FeedLabel.ImageIndex = ((int)(resources.GetObject("m_FeedLabel.ImageIndex")));
			this.m_FeedLabel.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_FeedLabel.ImeMode")));
			this.m_FeedLabel.Location = ((System.Drawing.Point)(resources.GetObject("m_FeedLabel.Location")));
			this.m_FeedLabel.Name = "m_FeedLabel";
			this.m_FeedLabel.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_FeedLabel.RightToLeft")));
			this.m_FeedLabel.Size = ((System.Drawing.Size)(resources.GetObject("m_FeedLabel.Size")));
			this.m_FeedLabel.TabIndex = ((int)(resources.GetObject("m_FeedLabel.TabIndex")));
			this.m_FeedLabel.Text = resources.GetString("m_FeedLabel.Text");
			this.m_FeedLabel.TextAlign = ((System.Drawing.ContentAlignment)(resources.GetObject("m_FeedLabel.TextAlign")));
			this.m_PlaceToolTip.SetToolTip(this.m_FeedLabel, resources.GetString("m_FeedLabel.ToolTip"));
			this.m_FeedLabel.Visible = ((bool)(resources.GetObject("m_FeedLabel.Visible")));
			// 
			// m_HiveLabel
			// 
			this.m_HiveLabel.AccessibleDescription = resources.GetString("m_HiveLabel.AccessibleDescription");
			this.m_HiveLabel.AccessibleName = resources.GetString("m_HiveLabel.AccessibleName");
			this.m_HiveLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_HiveLabel.Anchor")));
			this.m_HiveLabel.AutoSize = ((bool)(resources.GetObject("m_HiveLabel.AutoSize")));
			this.m_HiveLabel.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_HiveLabel.Dock")));
			this.m_HiveLabel.Enabled = ((bool)(resources.GetObject("m_HiveLabel.Enabled")));
			this.m_HiveLabel.Font = ((System.Drawing.Font)(resources.GetObject("m_HiveLabel.Font")));
			this.m_HiveLabel.Image = ((System.Drawing.Image)(resources.GetObject("m_HiveLabel.Image")));
			this.m_HiveLabel.ImageAlign = ((System.Drawing.ContentAlignment)(resources.GetObject("m_HiveLabel.ImageAlign")));
			this.m_HiveLabel.ImageIndex = ((int)(resources.GetObject("m_HiveLabel.ImageIndex")));
			this.m_HiveLabel.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_HiveLabel.ImeMode")));
			this.m_HiveLabel.Location = ((System.Drawing.Point)(resources.GetObject("m_HiveLabel.Location")));
			this.m_HiveLabel.Name = "m_HiveLabel";
			this.m_HiveLabel.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_HiveLabel.RightToLeft")));
			this.m_HiveLabel.Size = ((System.Drawing.Size)(resources.GetObject("m_HiveLabel.Size")));
			this.m_HiveLabel.TabIndex = ((int)(resources.GetObject("m_HiveLabel.TabIndex")));
			this.m_HiveLabel.Text = resources.GetString("m_HiveLabel.Text");
			this.m_HiveLabel.TextAlign = ((System.Drawing.ContentAlignment)(resources.GetObject("m_HiveLabel.TextAlign")));
			this.m_PlaceToolTip.SetToolTip(this.m_HiveLabel, resources.GetString("m_HiveLabel.ToolTip"));
			this.m_HiveLabel.Visible = ((bool)(resources.GetObject("m_HiveLabel.Visible")));
			// 
			// m_HiveComboBox
			// 
			this.m_HiveComboBox.AccessibleDescription = resources.GetString("m_HiveComboBox.AccessibleDescription");
			this.m_HiveComboBox.AccessibleName = resources.GetString("m_HiveComboBox.AccessibleName");
			this.m_HiveComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_HiveComboBox.Anchor")));
			this.m_HiveComboBox.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("m_HiveComboBox.BackgroundImage")));
			this.m_HiveComboBox.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_HiveComboBox.Dock")));
			this.m_HiveComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.m_HiveComboBox.Enabled = ((bool)(resources.GetObject("m_HiveComboBox.Enabled")));
			this.m_HiveComboBox.Font = ((System.Drawing.Font)(resources.GetObject("m_HiveComboBox.Font")));
			this.m_HiveComboBox.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_HiveComboBox.ImeMode")));
			this.m_HiveComboBox.IntegralHeight = ((bool)(resources.GetObject("m_HiveComboBox.IntegralHeight")));
			this.m_HiveComboBox.ItemHeight = ((int)(resources.GetObject("m_HiveComboBox.ItemHeight")));
			this.m_HiveComboBox.Location = ((System.Drawing.Point)(resources.GetObject("m_HiveComboBox.Location")));
			this.m_HiveComboBox.MaxDropDownItems = ((int)(resources.GetObject("m_HiveComboBox.MaxDropDownItems")));
			this.m_HiveComboBox.MaxLength = ((int)(resources.GetObject("m_HiveComboBox.MaxLength")));
			this.m_HiveComboBox.Name = "m_HiveComboBox";
			this.m_HiveComboBox.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_HiveComboBox.RightToLeft")));
			this.m_HiveComboBox.Size = ((System.Drawing.Size)(resources.GetObject("m_HiveComboBox.Size")));
			this.m_HiveComboBox.Sorted = true;
			this.m_HiveComboBox.TabIndex = ((int)(resources.GetObject("m_HiveComboBox.TabIndex")));
			this.m_HiveComboBox.Text = resources.GetString("m_HiveComboBox.Text");
			this.m_PlaceToolTip.SetToolTip(this.m_HiveComboBox, resources.GetString("m_HiveComboBox.ToolTip"));
			this.m_HiveComboBox.Visible = ((bool)(resources.GetObject("m_HiveComboBox.Visible")));
			// 
			// m_PriorityLabel
			// 
			this.m_PriorityLabel.AccessibleDescription = resources.GetString("m_PriorityLabel.AccessibleDescription");
			this.m_PriorityLabel.AccessibleName = resources.GetString("m_PriorityLabel.AccessibleName");
			this.m_PriorityLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_PriorityLabel.Anchor")));
			this.m_PriorityLabel.AutoSize = ((bool)(resources.GetObject("m_PriorityLabel.AutoSize")));
			this.m_PriorityLabel.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_PriorityLabel.Dock")));
			this.m_PriorityLabel.Enabled = ((bool)(resources.GetObject("m_PriorityLabel.Enabled")));
			this.m_PriorityLabel.Font = ((System.Drawing.Font)(resources.GetObject("m_PriorityLabel.Font")));
			this.m_PriorityLabel.Image = ((System.Drawing.Image)(resources.GetObject("m_PriorityLabel.Image")));
			this.m_PriorityLabel.ImageAlign = ((System.Drawing.ContentAlignment)(resources.GetObject("m_PriorityLabel.ImageAlign")));
			this.m_PriorityLabel.ImageIndex = ((int)(resources.GetObject("m_PriorityLabel.ImageIndex")));
			this.m_PriorityLabel.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_PriorityLabel.ImeMode")));
			this.m_PriorityLabel.Location = ((System.Drawing.Point)(resources.GetObject("m_PriorityLabel.Location")));
			this.m_PriorityLabel.Name = "m_PriorityLabel";
			this.m_PriorityLabel.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_PriorityLabel.RightToLeft")));
			this.m_PriorityLabel.Size = ((System.Drawing.Size)(resources.GetObject("m_PriorityLabel.Size")));
			this.m_PriorityLabel.TabIndex = ((int)(resources.GetObject("m_PriorityLabel.TabIndex")));
			this.m_PriorityLabel.Text = resources.GetString("m_PriorityLabel.Text");
			this.m_PriorityLabel.TextAlign = ((System.Drawing.ContentAlignment)(resources.GetObject("m_PriorityLabel.TextAlign")));
			this.m_PlaceToolTip.SetToolTip(this.m_PriorityLabel, resources.GetString("m_PriorityLabel.ToolTip"));
			this.m_PriorityLabel.Visible = ((bool)(resources.GetObject("m_PriorityLabel.Visible")));
			// 
			// m_RefreshLabel
			// 
			this.m_RefreshLabel.AccessibleDescription = resources.GetString("m_RefreshLabel.AccessibleDescription");
			this.m_RefreshLabel.AccessibleName = resources.GetString("m_RefreshLabel.AccessibleName");
			this.m_RefreshLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_RefreshLabel.Anchor")));
			this.m_RefreshLabel.AutoSize = ((bool)(resources.GetObject("m_RefreshLabel.AutoSize")));
			this.m_RefreshLabel.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_RefreshLabel.Dock")));
			this.m_RefreshLabel.Enabled = ((bool)(resources.GetObject("m_RefreshLabel.Enabled")));
			this.m_RefreshLabel.Font = ((System.Drawing.Font)(resources.GetObject("m_RefreshLabel.Font")));
			this.m_RefreshLabel.Image = ((System.Drawing.Image)(resources.GetObject("m_RefreshLabel.Image")));
			this.m_RefreshLabel.ImageAlign = ((System.Drawing.ContentAlignment)(resources.GetObject("m_RefreshLabel.ImageAlign")));
			this.m_RefreshLabel.ImageIndex = ((int)(resources.GetObject("m_RefreshLabel.ImageIndex")));
			this.m_RefreshLabel.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_RefreshLabel.ImeMode")));
			this.m_RefreshLabel.Location = ((System.Drawing.Point)(resources.GetObject("m_RefreshLabel.Location")));
			this.m_RefreshLabel.Name = "m_RefreshLabel";
			this.m_RefreshLabel.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_RefreshLabel.RightToLeft")));
			this.m_RefreshLabel.Size = ((System.Drawing.Size)(resources.GetObject("m_RefreshLabel.Size")));
			this.m_RefreshLabel.TabIndex = ((int)(resources.GetObject("m_RefreshLabel.TabIndex")));
			this.m_RefreshLabel.Text = resources.GetString("m_RefreshLabel.Text");
			this.m_RefreshLabel.TextAlign = ((System.Drawing.ContentAlignment)(resources.GetObject("m_RefreshLabel.TextAlign")));
			this.m_PlaceToolTip.SetToolTip(this.m_RefreshLabel, resources.GetString("m_RefreshLabel.ToolTip"));
			this.m_RefreshLabel.Visible = ((bool)(resources.GetObject("m_RefreshLabel.Visible")));
			// 
			// m_PlacementGroupBox
			// 
			this.m_PlacementGroupBox.AccessibleDescription = resources.GetString("m_PlacementGroupBox.AccessibleDescription");
			this.m_PlacementGroupBox.AccessibleName = resources.GetString("m_PlacementGroupBox.AccessibleName");
			this.m_PlacementGroupBox.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_PlacementGroupBox.Anchor")));
			this.m_PlacementGroupBox.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("m_PlacementGroupBox.BackgroundImage")));
			this.m_PlacementGroupBox.Controls.Add(this.m_PlaceBrOffPictureBox);
			this.m_PlacementGroupBox.Controls.Add(this.m_PlaceBlOffPictureBox);
			this.m_PlacementGroupBox.Controls.Add(this.m_PlaceBcOffPictureBox);
			this.m_PlacementGroupBox.Controls.Add(this.m_PlaceBlOnPictureBox);
			this.m_PlacementGroupBox.Controls.Add(this.m_PlaceBcOnPictureBox);
			this.m_PlacementGroupBox.Controls.Add(this.m_PlaceBrOnPictureBox);
			this.m_PlacementGroupBox.Controls.Add(this.m_PlaceTlOffPictureBox);
			this.m_PlacementGroupBox.Controls.Add(this.m_PlaceTcOffPictureBox);
			this.m_PlacementGroupBox.Controls.Add(this.m_PlaceTrOffPictureBox);
			this.m_PlacementGroupBox.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_PlacementGroupBox.Dock")));
			this.m_PlacementGroupBox.Enabled = ((bool)(resources.GetObject("m_PlacementGroupBox.Enabled")));
			this.m_PlacementGroupBox.Font = ((System.Drawing.Font)(resources.GetObject("m_PlacementGroupBox.Font")));
			this.m_PlacementGroupBox.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_PlacementGroupBox.ImeMode")));
			this.m_PlacementGroupBox.Location = ((System.Drawing.Point)(resources.GetObject("m_PlacementGroupBox.Location")));
			this.m_PlacementGroupBox.Name = "m_PlacementGroupBox";
			this.m_PlacementGroupBox.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_PlacementGroupBox.RightToLeft")));
			this.m_PlacementGroupBox.Size = ((System.Drawing.Size)(resources.GetObject("m_PlacementGroupBox.Size")));
			this.m_PlacementGroupBox.TabIndex = ((int)(resources.GetObject("m_PlacementGroupBox.TabIndex")));
			this.m_PlacementGroupBox.TabStop = false;
			this.m_PlacementGroupBox.Text = resources.GetString("m_PlacementGroupBox.Text");
			this.m_PlaceToolTip.SetToolTip(this.m_PlacementGroupBox, resources.GetString("m_PlacementGroupBox.ToolTip"));
			this.m_PlacementGroupBox.Visible = ((bool)(resources.GetObject("m_PlacementGroupBox.Visible")));
			// 
			// m_PlaceBrOffPictureBox
			// 
			this.m_PlaceBrOffPictureBox.AccessibleDescription = resources.GetString("m_PlaceBrOffPictureBox.AccessibleDescription");
			this.m_PlaceBrOffPictureBox.AccessibleName = resources.GetString("m_PlaceBrOffPictureBox.AccessibleName");
			this.m_PlaceBrOffPictureBox.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_PlaceBrOffPictureBox.Anchor")));
			this.m_PlaceBrOffPictureBox.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("m_PlaceBrOffPictureBox.BackgroundImage")));
			this.m_PlaceBrOffPictureBox.Cursor = System.Windows.Forms.Cursors.Hand;
			this.m_PlaceBrOffPictureBox.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_PlaceBrOffPictureBox.Dock")));
			this.m_PlaceBrOffPictureBox.Enabled = ((bool)(resources.GetObject("m_PlaceBrOffPictureBox.Enabled")));
			this.m_PlaceBrOffPictureBox.Font = ((System.Drawing.Font)(resources.GetObject("m_PlaceBrOffPictureBox.Font")));
			this.m_PlaceBrOffPictureBox.Image = ((System.Drawing.Image)(resources.GetObject("m_PlaceBrOffPictureBox.Image")));
			this.m_PlaceBrOffPictureBox.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_PlaceBrOffPictureBox.ImeMode")));
			this.m_PlaceBrOffPictureBox.Location = ((System.Drawing.Point)(resources.GetObject("m_PlaceBrOffPictureBox.Location")));
			this.m_PlaceBrOffPictureBox.Name = "m_PlaceBrOffPictureBox";
			this.m_PlaceBrOffPictureBox.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_PlaceBrOffPictureBox.RightToLeft")));
			this.m_PlaceBrOffPictureBox.Size = ((System.Drawing.Size)(resources.GetObject("m_PlaceBrOffPictureBox.Size")));
			this.m_PlaceBrOffPictureBox.SizeMode = ((System.Windows.Forms.PictureBoxSizeMode)(resources.GetObject("m_PlaceBrOffPictureBox.SizeMode")));
			this.m_PlaceBrOffPictureBox.TabIndex = ((int)(resources.GetObject("m_PlaceBrOffPictureBox.TabIndex")));
			this.m_PlaceBrOffPictureBox.TabStop = false;
			this.m_PlaceBrOffPictureBox.Text = resources.GetString("m_PlaceBrOffPictureBox.Text");
			this.m_PlaceToolTip.SetToolTip(this.m_PlaceBrOffPictureBox, resources.GetString("m_PlaceBrOffPictureBox.ToolTip"));
			this.m_PlaceBrOffPictureBox.Visible = ((bool)(resources.GetObject("m_PlaceBrOffPictureBox.Visible")));
			this.m_PlaceBrOffPictureBox.Click += new System.EventHandler(this.m_PlacePictureBox_Click);
			// 
			// m_PlaceBlOffPictureBox
			// 
			this.m_PlaceBlOffPictureBox.AccessibleDescription = resources.GetString("m_PlaceBlOffPictureBox.AccessibleDescription");
			this.m_PlaceBlOffPictureBox.AccessibleName = resources.GetString("m_PlaceBlOffPictureBox.AccessibleName");
			this.m_PlaceBlOffPictureBox.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_PlaceBlOffPictureBox.Anchor")));
			this.m_PlaceBlOffPictureBox.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("m_PlaceBlOffPictureBox.BackgroundImage")));
			this.m_PlaceBlOffPictureBox.Cursor = System.Windows.Forms.Cursors.Hand;
			this.m_PlaceBlOffPictureBox.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_PlaceBlOffPictureBox.Dock")));
			this.m_PlaceBlOffPictureBox.Enabled = ((bool)(resources.GetObject("m_PlaceBlOffPictureBox.Enabled")));
			this.m_PlaceBlOffPictureBox.Font = ((System.Drawing.Font)(resources.GetObject("m_PlaceBlOffPictureBox.Font")));
			this.m_PlaceBlOffPictureBox.Image = ((System.Drawing.Image)(resources.GetObject("m_PlaceBlOffPictureBox.Image")));
			this.m_PlaceBlOffPictureBox.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_PlaceBlOffPictureBox.ImeMode")));
			this.m_PlaceBlOffPictureBox.Location = ((System.Drawing.Point)(resources.GetObject("m_PlaceBlOffPictureBox.Location")));
			this.m_PlaceBlOffPictureBox.Name = "m_PlaceBlOffPictureBox";
			this.m_PlaceBlOffPictureBox.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_PlaceBlOffPictureBox.RightToLeft")));
			this.m_PlaceBlOffPictureBox.Size = ((System.Drawing.Size)(resources.GetObject("m_PlaceBlOffPictureBox.Size")));
			this.m_PlaceBlOffPictureBox.SizeMode = ((System.Windows.Forms.PictureBoxSizeMode)(resources.GetObject("m_PlaceBlOffPictureBox.SizeMode")));
			this.m_PlaceBlOffPictureBox.TabIndex = ((int)(resources.GetObject("m_PlaceBlOffPictureBox.TabIndex")));
			this.m_PlaceBlOffPictureBox.TabStop = false;
			this.m_PlaceBlOffPictureBox.Text = resources.GetString("m_PlaceBlOffPictureBox.Text");
			this.m_PlaceToolTip.SetToolTip(this.m_PlaceBlOffPictureBox, resources.GetString("m_PlaceBlOffPictureBox.ToolTip"));
			this.m_PlaceBlOffPictureBox.Visible = ((bool)(resources.GetObject("m_PlaceBlOffPictureBox.Visible")));
			this.m_PlaceBlOffPictureBox.Click += new System.EventHandler(this.m_PlacePictureBox_Click);
			// 
			// m_PlaceBcOffPictureBox
			// 
			this.m_PlaceBcOffPictureBox.AccessibleDescription = resources.GetString("m_PlaceBcOffPictureBox.AccessibleDescription");
			this.m_PlaceBcOffPictureBox.AccessibleName = resources.GetString("m_PlaceBcOffPictureBox.AccessibleName");
			this.m_PlaceBcOffPictureBox.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_PlaceBcOffPictureBox.Anchor")));
			this.m_PlaceBcOffPictureBox.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("m_PlaceBcOffPictureBox.BackgroundImage")));
			this.m_PlaceBcOffPictureBox.Cursor = System.Windows.Forms.Cursors.Hand;
			this.m_PlaceBcOffPictureBox.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_PlaceBcOffPictureBox.Dock")));
			this.m_PlaceBcOffPictureBox.Enabled = ((bool)(resources.GetObject("m_PlaceBcOffPictureBox.Enabled")));
			this.m_PlaceBcOffPictureBox.Font = ((System.Drawing.Font)(resources.GetObject("m_PlaceBcOffPictureBox.Font")));
			this.m_PlaceBcOffPictureBox.Image = ((System.Drawing.Image)(resources.GetObject("m_PlaceBcOffPictureBox.Image")));
			this.m_PlaceBcOffPictureBox.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_PlaceBcOffPictureBox.ImeMode")));
			this.m_PlaceBcOffPictureBox.Location = ((System.Drawing.Point)(resources.GetObject("m_PlaceBcOffPictureBox.Location")));
			this.m_PlaceBcOffPictureBox.Name = "m_PlaceBcOffPictureBox";
			this.m_PlaceBcOffPictureBox.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_PlaceBcOffPictureBox.RightToLeft")));
			this.m_PlaceBcOffPictureBox.Size = ((System.Drawing.Size)(resources.GetObject("m_PlaceBcOffPictureBox.Size")));
			this.m_PlaceBcOffPictureBox.SizeMode = ((System.Windows.Forms.PictureBoxSizeMode)(resources.GetObject("m_PlaceBcOffPictureBox.SizeMode")));
			this.m_PlaceBcOffPictureBox.TabIndex = ((int)(resources.GetObject("m_PlaceBcOffPictureBox.TabIndex")));
			this.m_PlaceBcOffPictureBox.TabStop = false;
			this.m_PlaceBcOffPictureBox.Text = resources.GetString("m_PlaceBcOffPictureBox.Text");
			this.m_PlaceToolTip.SetToolTip(this.m_PlaceBcOffPictureBox, resources.GetString("m_PlaceBcOffPictureBox.ToolTip"));
			this.m_PlaceBcOffPictureBox.Visible = ((bool)(resources.GetObject("m_PlaceBcOffPictureBox.Visible")));
			this.m_PlaceBcOffPictureBox.Click += new System.EventHandler(this.m_PlacePictureBox_Click);
			// 
			// m_PlaceBlOnPictureBox
			// 
			this.m_PlaceBlOnPictureBox.AccessibleDescription = resources.GetString("m_PlaceBlOnPictureBox.AccessibleDescription");
			this.m_PlaceBlOnPictureBox.AccessibleName = resources.GetString("m_PlaceBlOnPictureBox.AccessibleName");
			this.m_PlaceBlOnPictureBox.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_PlaceBlOnPictureBox.Anchor")));
			this.m_PlaceBlOnPictureBox.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("m_PlaceBlOnPictureBox.BackgroundImage")));
			this.m_PlaceBlOnPictureBox.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_PlaceBlOnPictureBox.Dock")));
			this.m_PlaceBlOnPictureBox.Enabled = ((bool)(resources.GetObject("m_PlaceBlOnPictureBox.Enabled")));
			this.m_PlaceBlOnPictureBox.Font = ((System.Drawing.Font)(resources.GetObject("m_PlaceBlOnPictureBox.Font")));
			this.m_PlaceBlOnPictureBox.Image = ((System.Drawing.Image)(resources.GetObject("m_PlaceBlOnPictureBox.Image")));
			this.m_PlaceBlOnPictureBox.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_PlaceBlOnPictureBox.ImeMode")));
			this.m_PlaceBlOnPictureBox.Location = ((System.Drawing.Point)(resources.GetObject("m_PlaceBlOnPictureBox.Location")));
			this.m_PlaceBlOnPictureBox.Name = "m_PlaceBlOnPictureBox";
			this.m_PlaceBlOnPictureBox.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_PlaceBlOnPictureBox.RightToLeft")));
			this.m_PlaceBlOnPictureBox.Size = ((System.Drawing.Size)(resources.GetObject("m_PlaceBlOnPictureBox.Size")));
			this.m_PlaceBlOnPictureBox.SizeMode = ((System.Windows.Forms.PictureBoxSizeMode)(resources.GetObject("m_PlaceBlOnPictureBox.SizeMode")));
			this.m_PlaceBlOnPictureBox.TabIndex = ((int)(resources.GetObject("m_PlaceBlOnPictureBox.TabIndex")));
			this.m_PlaceBlOnPictureBox.TabStop = false;
			this.m_PlaceBlOnPictureBox.Text = resources.GetString("m_PlaceBlOnPictureBox.Text");
			this.m_PlaceToolTip.SetToolTip(this.m_PlaceBlOnPictureBox, resources.GetString("m_PlaceBlOnPictureBox.ToolTip"));
			this.m_PlaceBlOnPictureBox.Visible = ((bool)(resources.GetObject("m_PlaceBlOnPictureBox.Visible")));
			// 
			// m_PlaceBcOnPictureBox
			// 
			this.m_PlaceBcOnPictureBox.AccessibleDescription = resources.GetString("m_PlaceBcOnPictureBox.AccessibleDescription");
			this.m_PlaceBcOnPictureBox.AccessibleName = resources.GetString("m_PlaceBcOnPictureBox.AccessibleName");
			this.m_PlaceBcOnPictureBox.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_PlaceBcOnPictureBox.Anchor")));
			this.m_PlaceBcOnPictureBox.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("m_PlaceBcOnPictureBox.BackgroundImage")));
			this.m_PlaceBcOnPictureBox.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_PlaceBcOnPictureBox.Dock")));
			this.m_PlaceBcOnPictureBox.Enabled = ((bool)(resources.GetObject("m_PlaceBcOnPictureBox.Enabled")));
			this.m_PlaceBcOnPictureBox.Font = ((System.Drawing.Font)(resources.GetObject("m_PlaceBcOnPictureBox.Font")));
			this.m_PlaceBcOnPictureBox.Image = ((System.Drawing.Image)(resources.GetObject("m_PlaceBcOnPictureBox.Image")));
			this.m_PlaceBcOnPictureBox.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_PlaceBcOnPictureBox.ImeMode")));
			this.m_PlaceBcOnPictureBox.Location = ((System.Drawing.Point)(resources.GetObject("m_PlaceBcOnPictureBox.Location")));
			this.m_PlaceBcOnPictureBox.Name = "m_PlaceBcOnPictureBox";
			this.m_PlaceBcOnPictureBox.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_PlaceBcOnPictureBox.RightToLeft")));
			this.m_PlaceBcOnPictureBox.Size = ((System.Drawing.Size)(resources.GetObject("m_PlaceBcOnPictureBox.Size")));
			this.m_PlaceBcOnPictureBox.SizeMode = ((System.Windows.Forms.PictureBoxSizeMode)(resources.GetObject("m_PlaceBcOnPictureBox.SizeMode")));
			this.m_PlaceBcOnPictureBox.TabIndex = ((int)(resources.GetObject("m_PlaceBcOnPictureBox.TabIndex")));
			this.m_PlaceBcOnPictureBox.TabStop = false;
			this.m_PlaceBcOnPictureBox.Text = resources.GetString("m_PlaceBcOnPictureBox.Text");
			this.m_PlaceToolTip.SetToolTip(this.m_PlaceBcOnPictureBox, resources.GetString("m_PlaceBcOnPictureBox.ToolTip"));
			this.m_PlaceBcOnPictureBox.Visible = ((bool)(resources.GetObject("m_PlaceBcOnPictureBox.Visible")));
			// 
			// m_PlaceBrOnPictureBox
			// 
			this.m_PlaceBrOnPictureBox.AccessibleDescription = resources.GetString("m_PlaceBrOnPictureBox.AccessibleDescription");
			this.m_PlaceBrOnPictureBox.AccessibleName = resources.GetString("m_PlaceBrOnPictureBox.AccessibleName");
			this.m_PlaceBrOnPictureBox.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_PlaceBrOnPictureBox.Anchor")));
			this.m_PlaceBrOnPictureBox.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("m_PlaceBrOnPictureBox.BackgroundImage")));
			this.m_PlaceBrOnPictureBox.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_PlaceBrOnPictureBox.Dock")));
			this.m_PlaceBrOnPictureBox.Enabled = ((bool)(resources.GetObject("m_PlaceBrOnPictureBox.Enabled")));
			this.m_PlaceBrOnPictureBox.Font = ((System.Drawing.Font)(resources.GetObject("m_PlaceBrOnPictureBox.Font")));
			this.m_PlaceBrOnPictureBox.Image = ((System.Drawing.Image)(resources.GetObject("m_PlaceBrOnPictureBox.Image")));
			this.m_PlaceBrOnPictureBox.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_PlaceBrOnPictureBox.ImeMode")));
			this.m_PlaceBrOnPictureBox.Location = ((System.Drawing.Point)(resources.GetObject("m_PlaceBrOnPictureBox.Location")));
			this.m_PlaceBrOnPictureBox.Name = "m_PlaceBrOnPictureBox";
			this.m_PlaceBrOnPictureBox.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_PlaceBrOnPictureBox.RightToLeft")));
			this.m_PlaceBrOnPictureBox.Size = ((System.Drawing.Size)(resources.GetObject("m_PlaceBrOnPictureBox.Size")));
			this.m_PlaceBrOnPictureBox.SizeMode = ((System.Windows.Forms.PictureBoxSizeMode)(resources.GetObject("m_PlaceBrOnPictureBox.SizeMode")));
			this.m_PlaceBrOnPictureBox.TabIndex = ((int)(resources.GetObject("m_PlaceBrOnPictureBox.TabIndex")));
			this.m_PlaceBrOnPictureBox.TabStop = false;
			this.m_PlaceBrOnPictureBox.Text = resources.GetString("m_PlaceBrOnPictureBox.Text");
			this.m_PlaceToolTip.SetToolTip(this.m_PlaceBrOnPictureBox, resources.GetString("m_PlaceBrOnPictureBox.ToolTip"));
			this.m_PlaceBrOnPictureBox.Visible = ((bool)(resources.GetObject("m_PlaceBrOnPictureBox.Visible")));
			// 
			// m_PlaceTlOffPictureBox
			// 
			this.m_PlaceTlOffPictureBox.AccessibleDescription = resources.GetString("m_PlaceTlOffPictureBox.AccessibleDescription");
			this.m_PlaceTlOffPictureBox.AccessibleName = resources.GetString("m_PlaceTlOffPictureBox.AccessibleName");
			this.m_PlaceTlOffPictureBox.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_PlaceTlOffPictureBox.Anchor")));
			this.m_PlaceTlOffPictureBox.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("m_PlaceTlOffPictureBox.BackgroundImage")));
			this.m_PlaceTlOffPictureBox.Cursor = System.Windows.Forms.Cursors.Default;
			this.m_PlaceTlOffPictureBox.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_PlaceTlOffPictureBox.Dock")));
			this.m_PlaceTlOffPictureBox.Enabled = ((bool)(resources.GetObject("m_PlaceTlOffPictureBox.Enabled")));
			this.m_PlaceTlOffPictureBox.Font = ((System.Drawing.Font)(resources.GetObject("m_PlaceTlOffPictureBox.Font")));
			this.m_PlaceTlOffPictureBox.Image = ((System.Drawing.Image)(resources.GetObject("m_PlaceTlOffPictureBox.Image")));
			this.m_PlaceTlOffPictureBox.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_PlaceTlOffPictureBox.ImeMode")));
			this.m_PlaceTlOffPictureBox.Location = ((System.Drawing.Point)(resources.GetObject("m_PlaceTlOffPictureBox.Location")));
			this.m_PlaceTlOffPictureBox.Name = "m_PlaceTlOffPictureBox";
			this.m_PlaceTlOffPictureBox.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_PlaceTlOffPictureBox.RightToLeft")));
			this.m_PlaceTlOffPictureBox.Size = ((System.Drawing.Size)(resources.GetObject("m_PlaceTlOffPictureBox.Size")));
			this.m_PlaceTlOffPictureBox.SizeMode = ((System.Windows.Forms.PictureBoxSizeMode)(resources.GetObject("m_PlaceTlOffPictureBox.SizeMode")));
			this.m_PlaceTlOffPictureBox.TabIndex = ((int)(resources.GetObject("m_PlaceTlOffPictureBox.TabIndex")));
			this.m_PlaceTlOffPictureBox.TabStop = false;
			this.m_PlaceTlOffPictureBox.Text = resources.GetString("m_PlaceTlOffPictureBox.Text");
			this.m_PlaceToolTip.SetToolTip(this.m_PlaceTlOffPictureBox, resources.GetString("m_PlaceTlOffPictureBox.ToolTip"));
			this.m_PlaceTlOffPictureBox.Visible = ((bool)(resources.GetObject("m_PlaceTlOffPictureBox.Visible")));
			// 
			// m_PlaceTcOffPictureBox
			// 
			this.m_PlaceTcOffPictureBox.AccessibleDescription = resources.GetString("m_PlaceTcOffPictureBox.AccessibleDescription");
			this.m_PlaceTcOffPictureBox.AccessibleName = resources.GetString("m_PlaceTcOffPictureBox.AccessibleName");
			this.m_PlaceTcOffPictureBox.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_PlaceTcOffPictureBox.Anchor")));
			this.m_PlaceTcOffPictureBox.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("m_PlaceTcOffPictureBox.BackgroundImage")));
			this.m_PlaceTcOffPictureBox.Cursor = System.Windows.Forms.Cursors.Default;
			this.m_PlaceTcOffPictureBox.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_PlaceTcOffPictureBox.Dock")));
			this.m_PlaceTcOffPictureBox.Enabled = ((bool)(resources.GetObject("m_PlaceTcOffPictureBox.Enabled")));
			this.m_PlaceTcOffPictureBox.Font = ((System.Drawing.Font)(resources.GetObject("m_PlaceTcOffPictureBox.Font")));
			this.m_PlaceTcOffPictureBox.Image = ((System.Drawing.Image)(resources.GetObject("m_PlaceTcOffPictureBox.Image")));
			this.m_PlaceTcOffPictureBox.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_PlaceTcOffPictureBox.ImeMode")));
			this.m_PlaceTcOffPictureBox.Location = ((System.Drawing.Point)(resources.GetObject("m_PlaceTcOffPictureBox.Location")));
			this.m_PlaceTcOffPictureBox.Name = "m_PlaceTcOffPictureBox";
			this.m_PlaceTcOffPictureBox.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_PlaceTcOffPictureBox.RightToLeft")));
			this.m_PlaceTcOffPictureBox.Size = ((System.Drawing.Size)(resources.GetObject("m_PlaceTcOffPictureBox.Size")));
			this.m_PlaceTcOffPictureBox.SizeMode = ((System.Windows.Forms.PictureBoxSizeMode)(resources.GetObject("m_PlaceTcOffPictureBox.SizeMode")));
			this.m_PlaceTcOffPictureBox.TabIndex = ((int)(resources.GetObject("m_PlaceTcOffPictureBox.TabIndex")));
			this.m_PlaceTcOffPictureBox.TabStop = false;
			this.m_PlaceTcOffPictureBox.Text = resources.GetString("m_PlaceTcOffPictureBox.Text");
			this.m_PlaceToolTip.SetToolTip(this.m_PlaceTcOffPictureBox, resources.GetString("m_PlaceTcOffPictureBox.ToolTip"));
			this.m_PlaceTcOffPictureBox.Visible = ((bool)(resources.GetObject("m_PlaceTcOffPictureBox.Visible")));
			// 
			// m_PlaceTrOffPictureBox
			// 
			this.m_PlaceTrOffPictureBox.AccessibleDescription = resources.GetString("m_PlaceTrOffPictureBox.AccessibleDescription");
			this.m_PlaceTrOffPictureBox.AccessibleName = resources.GetString("m_PlaceTrOffPictureBox.AccessibleName");
			this.m_PlaceTrOffPictureBox.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_PlaceTrOffPictureBox.Anchor")));
			this.m_PlaceTrOffPictureBox.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("m_PlaceTrOffPictureBox.BackgroundImage")));
			this.m_PlaceTrOffPictureBox.Cursor = System.Windows.Forms.Cursors.Default;
			this.m_PlaceTrOffPictureBox.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_PlaceTrOffPictureBox.Dock")));
			this.m_PlaceTrOffPictureBox.Enabled = ((bool)(resources.GetObject("m_PlaceTrOffPictureBox.Enabled")));
			this.m_PlaceTrOffPictureBox.Font = ((System.Drawing.Font)(resources.GetObject("m_PlaceTrOffPictureBox.Font")));
			this.m_PlaceTrOffPictureBox.Image = ((System.Drawing.Image)(resources.GetObject("m_PlaceTrOffPictureBox.Image")));
			this.m_PlaceTrOffPictureBox.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_PlaceTrOffPictureBox.ImeMode")));
			this.m_PlaceTrOffPictureBox.Location = ((System.Drawing.Point)(resources.GetObject("m_PlaceTrOffPictureBox.Location")));
			this.m_PlaceTrOffPictureBox.Name = "m_PlaceTrOffPictureBox";
			this.m_PlaceTrOffPictureBox.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_PlaceTrOffPictureBox.RightToLeft")));
			this.m_PlaceTrOffPictureBox.Size = ((System.Drawing.Size)(resources.GetObject("m_PlaceTrOffPictureBox.Size")));
			this.m_PlaceTrOffPictureBox.SizeMode = ((System.Windows.Forms.PictureBoxSizeMode)(resources.GetObject("m_PlaceTrOffPictureBox.SizeMode")));
			this.m_PlaceTrOffPictureBox.TabIndex = ((int)(resources.GetObject("m_PlaceTrOffPictureBox.TabIndex")));
			this.m_PlaceTrOffPictureBox.TabStop = false;
			this.m_PlaceTrOffPictureBox.Text = resources.GetString("m_PlaceTrOffPictureBox.Text");
			this.m_PlaceToolTip.SetToolTip(this.m_PlaceTrOffPictureBox, resources.GetString("m_PlaceTrOffPictureBox.ToolTip"));
			this.m_PlaceTrOffPictureBox.Visible = ((bool)(resources.GetObject("m_PlaceTrOffPictureBox.Visible")));
			// 
			// m_SettingsGroupBox
			// 
			this.m_SettingsGroupBox.AccessibleDescription = resources.GetString("m_SettingsGroupBox.AccessibleDescription");
			this.m_SettingsGroupBox.AccessibleName = resources.GetString("m_SettingsGroupBox.AccessibleName");
			this.m_SettingsGroupBox.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_SettingsGroupBox.Anchor")));
			this.m_SettingsGroupBox.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("m_SettingsGroupBox.BackgroundImage")));
			this.m_SettingsGroupBox.Controls.Add(this.m_QuantityTextBox);
			this.m_SettingsGroupBox.Controls.Add(this.m_QuantityLabel);
			this.m_SettingsGroupBox.Controls.Add(this.m_NameTextBox);
			this.m_SettingsGroupBox.Controls.Add(this.m_NameLabel);
			this.m_SettingsGroupBox.Controls.Add(this.m_PriorityComboBox);
			this.m_SettingsGroupBox.Controls.Add(this.m_PriorityLabel);
			this.m_SettingsGroupBox.Controls.Add(this.m_RefreshLabel);
			this.m_SettingsGroupBox.Controls.Add(this.m_RefreshComboBox);
			this.m_SettingsGroupBox.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_SettingsGroupBox.Dock")));
			this.m_SettingsGroupBox.Enabled = ((bool)(resources.GetObject("m_SettingsGroupBox.Enabled")));
			this.m_SettingsGroupBox.Font = ((System.Drawing.Font)(resources.GetObject("m_SettingsGroupBox.Font")));
			this.m_SettingsGroupBox.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_SettingsGroupBox.ImeMode")));
			this.m_SettingsGroupBox.Location = ((System.Drawing.Point)(resources.GetObject("m_SettingsGroupBox.Location")));
			this.m_SettingsGroupBox.Name = "m_SettingsGroupBox";
			this.m_SettingsGroupBox.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_SettingsGroupBox.RightToLeft")));
			this.m_SettingsGroupBox.Size = ((System.Drawing.Size)(resources.GetObject("m_SettingsGroupBox.Size")));
			this.m_SettingsGroupBox.TabIndex = ((int)(resources.GetObject("m_SettingsGroupBox.TabIndex")));
			this.m_SettingsGroupBox.TabStop = false;
			this.m_SettingsGroupBox.Text = resources.GetString("m_SettingsGroupBox.Text");
			this.m_PlaceToolTip.SetToolTip(this.m_SettingsGroupBox, resources.GetString("m_SettingsGroupBox.ToolTip"));
			this.m_SettingsGroupBox.Visible = ((bool)(resources.GetObject("m_SettingsGroupBox.Visible")));
			// 
			// m_QuantityTextBox
			// 
			this.m_QuantityTextBox.AccessibleDescription = resources.GetString("m_QuantityTextBox.AccessibleDescription");
			this.m_QuantityTextBox.AccessibleName = resources.GetString("m_QuantityTextBox.AccessibleName");
			this.m_QuantityTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_QuantityTextBox.Anchor")));
			this.m_QuantityTextBox.AutoSize = ((bool)(resources.GetObject("m_QuantityTextBox.AutoSize")));
			this.m_QuantityTextBox.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("m_QuantityTextBox.BackgroundImage")));
			this.m_QuantityTextBox.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_QuantityTextBox.Dock")));
			this.m_QuantityTextBox.Enabled = ((bool)(resources.GetObject("m_QuantityTextBox.Enabled")));
			this.m_QuantityTextBox.Font = ((System.Drawing.Font)(resources.GetObject("m_QuantityTextBox.Font")));
			this.m_QuantityTextBox.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_QuantityTextBox.ImeMode")));
			this.m_QuantityTextBox.Location = ((System.Drawing.Point)(resources.GetObject("m_QuantityTextBox.Location")));
			this.m_QuantityTextBox.MaxLength = ((int)(resources.GetObject("m_QuantityTextBox.MaxLength")));
			this.m_QuantityTextBox.Multiline = ((bool)(resources.GetObject("m_QuantityTextBox.Multiline")));
			this.m_QuantityTextBox.Name = "m_QuantityTextBox";
			this.m_QuantityTextBox.PasswordChar = ((char)(resources.GetObject("m_QuantityTextBox.PasswordChar")));
			this.m_QuantityTextBox.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_QuantityTextBox.RightToLeft")));
			this.m_QuantityTextBox.ScrollBars = ((System.Windows.Forms.ScrollBars)(resources.GetObject("m_QuantityTextBox.ScrollBars")));
			this.m_QuantityTextBox.Size = ((System.Drawing.Size)(resources.GetObject("m_QuantityTextBox.Size")));
			this.m_QuantityTextBox.TabIndex = ((int)(resources.GetObject("m_QuantityTextBox.TabIndex")));
			this.m_QuantityTextBox.Text = resources.GetString("m_QuantityTextBox.Text");
			this.m_QuantityTextBox.TextAlign = ((System.Windows.Forms.HorizontalAlignment)(resources.GetObject("m_QuantityTextBox.TextAlign")));
			this.m_PlaceToolTip.SetToolTip(this.m_QuantityTextBox, resources.GetString("m_QuantityTextBox.ToolTip"));
			this.m_QuantityTextBox.Visible = ((bool)(resources.GetObject("m_QuantityTextBox.Visible")));
			this.m_QuantityTextBox.WordWrap = ((bool)(resources.GetObject("m_QuantityTextBox.WordWrap")));
			// 
			// m_QuantityLabel
			// 
			this.m_QuantityLabel.AccessibleDescription = resources.GetString("m_QuantityLabel.AccessibleDescription");
			this.m_QuantityLabel.AccessibleName = resources.GetString("m_QuantityLabel.AccessibleName");
			this.m_QuantityLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_QuantityLabel.Anchor")));
			this.m_QuantityLabel.AutoSize = ((bool)(resources.GetObject("m_QuantityLabel.AutoSize")));
			this.m_QuantityLabel.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_QuantityLabel.Dock")));
			this.m_QuantityLabel.Enabled = ((bool)(resources.GetObject("m_QuantityLabel.Enabled")));
			this.m_QuantityLabel.Font = ((System.Drawing.Font)(resources.GetObject("m_QuantityLabel.Font")));
			this.m_QuantityLabel.Image = ((System.Drawing.Image)(resources.GetObject("m_QuantityLabel.Image")));
			this.m_QuantityLabel.ImageAlign = ((System.Drawing.ContentAlignment)(resources.GetObject("m_QuantityLabel.ImageAlign")));
			this.m_QuantityLabel.ImageIndex = ((int)(resources.GetObject("m_QuantityLabel.ImageIndex")));
			this.m_QuantityLabel.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_QuantityLabel.ImeMode")));
			this.m_QuantityLabel.Location = ((System.Drawing.Point)(resources.GetObject("m_QuantityLabel.Location")));
			this.m_QuantityLabel.Name = "m_QuantityLabel";
			this.m_QuantityLabel.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_QuantityLabel.RightToLeft")));
			this.m_QuantityLabel.Size = ((System.Drawing.Size)(resources.GetObject("m_QuantityLabel.Size")));
			this.m_QuantityLabel.TabIndex = ((int)(resources.GetObject("m_QuantityLabel.TabIndex")));
			this.m_QuantityLabel.Text = resources.GetString("m_QuantityLabel.Text");
			this.m_QuantityLabel.TextAlign = ((System.Drawing.ContentAlignment)(resources.GetObject("m_QuantityLabel.TextAlign")));
			this.m_PlaceToolTip.SetToolTip(this.m_QuantityLabel, resources.GetString("m_QuantityLabel.ToolTip"));
			this.m_QuantityLabel.Visible = ((bool)(resources.GetObject("m_QuantityLabel.Visible")));
			// 
			// m_NameTextBox
			// 
			this.m_NameTextBox.AccessibleDescription = resources.GetString("m_NameTextBox.AccessibleDescription");
			this.m_NameTextBox.AccessibleName = resources.GetString("m_NameTextBox.AccessibleName");
			this.m_NameTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_NameTextBox.Anchor")));
			this.m_NameTextBox.AutoSize = ((bool)(resources.GetObject("m_NameTextBox.AutoSize")));
			this.m_NameTextBox.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("m_NameTextBox.BackgroundImage")));
			this.m_NameTextBox.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_NameTextBox.Dock")));
			this.m_NameTextBox.Enabled = ((bool)(resources.GetObject("m_NameTextBox.Enabled")));
			this.m_NameTextBox.Font = ((System.Drawing.Font)(resources.GetObject("m_NameTextBox.Font")));
			this.m_NameTextBox.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_NameTextBox.ImeMode")));
			this.m_NameTextBox.Location = ((System.Drawing.Point)(resources.GetObject("m_NameTextBox.Location")));
			this.m_NameTextBox.MaxLength = ((int)(resources.GetObject("m_NameTextBox.MaxLength")));
			this.m_NameTextBox.Multiline = ((bool)(resources.GetObject("m_NameTextBox.Multiline")));
			this.m_NameTextBox.Name = "m_NameTextBox";
			this.m_NameTextBox.PasswordChar = ((char)(resources.GetObject("m_NameTextBox.PasswordChar")));
			this.m_NameTextBox.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_NameTextBox.RightToLeft")));
			this.m_NameTextBox.ScrollBars = ((System.Windows.Forms.ScrollBars)(resources.GetObject("m_NameTextBox.ScrollBars")));
			this.m_NameTextBox.Size = ((System.Drawing.Size)(resources.GetObject("m_NameTextBox.Size")));
			this.m_NameTextBox.TabIndex = ((int)(resources.GetObject("m_NameTextBox.TabIndex")));
			this.m_NameTextBox.Text = resources.GetString("m_NameTextBox.Text");
			this.m_NameTextBox.TextAlign = ((System.Windows.Forms.HorizontalAlignment)(resources.GetObject("m_NameTextBox.TextAlign")));
			this.m_PlaceToolTip.SetToolTip(this.m_NameTextBox, resources.GetString("m_NameTextBox.ToolTip"));
			this.m_NameTextBox.Visible = ((bool)(resources.GetObject("m_NameTextBox.Visible")));
			this.m_NameTextBox.WordWrap = ((bool)(resources.GetObject("m_NameTextBox.WordWrap")));
			// 
			// m_NameLabel
			// 
			this.m_NameLabel.AccessibleDescription = resources.GetString("m_NameLabel.AccessibleDescription");
			this.m_NameLabel.AccessibleName = resources.GetString("m_NameLabel.AccessibleName");
			this.m_NameLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_NameLabel.Anchor")));
			this.m_NameLabel.AutoSize = ((bool)(resources.GetObject("m_NameLabel.AutoSize")));
			this.m_NameLabel.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_NameLabel.Dock")));
			this.m_NameLabel.Enabled = ((bool)(resources.GetObject("m_NameLabel.Enabled")));
			this.m_NameLabel.Font = ((System.Drawing.Font)(resources.GetObject("m_NameLabel.Font")));
			this.m_NameLabel.Image = ((System.Drawing.Image)(resources.GetObject("m_NameLabel.Image")));
			this.m_NameLabel.ImageAlign = ((System.Drawing.ContentAlignment)(resources.GetObject("m_NameLabel.ImageAlign")));
			this.m_NameLabel.ImageIndex = ((int)(resources.GetObject("m_NameLabel.ImageIndex")));
			this.m_NameLabel.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_NameLabel.ImeMode")));
			this.m_NameLabel.Location = ((System.Drawing.Point)(resources.GetObject("m_NameLabel.Location")));
			this.m_NameLabel.Name = "m_NameLabel";
			this.m_NameLabel.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_NameLabel.RightToLeft")));
			this.m_NameLabel.Size = ((System.Drawing.Size)(resources.GetObject("m_NameLabel.Size")));
			this.m_NameLabel.TabIndex = ((int)(resources.GetObject("m_NameLabel.TabIndex")));
			this.m_NameLabel.Text = resources.GetString("m_NameLabel.Text");
			this.m_NameLabel.TextAlign = ((System.Drawing.ContentAlignment)(resources.GetObject("m_NameLabel.TextAlign")));
			this.m_PlaceToolTip.SetToolTip(this.m_NameLabel, resources.GetString("m_NameLabel.ToolTip"));
			this.m_NameLabel.Visible = ((bool)(resources.GetObject("m_NameLabel.Visible")));
			// 
			// m_PriorityComboBox
			// 
			this.m_PriorityComboBox.AccessibleDescription = resources.GetString("m_PriorityComboBox.AccessibleDescription");
			this.m_PriorityComboBox.AccessibleName = resources.GetString("m_PriorityComboBox.AccessibleName");
			this.m_PriorityComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_PriorityComboBox.Anchor")));
			this.m_PriorityComboBox.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("m_PriorityComboBox.BackgroundImage")));
			this.m_PriorityComboBox.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_PriorityComboBox.Dock")));
			this.m_PriorityComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.m_PriorityComboBox.Enabled = ((bool)(resources.GetObject("m_PriorityComboBox.Enabled")));
			this.m_PriorityComboBox.Font = ((System.Drawing.Font)(resources.GetObject("m_PriorityComboBox.Font")));
			this.m_PriorityComboBox.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_PriorityComboBox.ImeMode")));
			this.m_PriorityComboBox.IntegralHeight = ((bool)(resources.GetObject("m_PriorityComboBox.IntegralHeight")));
			this.m_PriorityComboBox.ItemHeight = ((int)(resources.GetObject("m_PriorityComboBox.ItemHeight")));
			this.m_PriorityComboBox.Items.AddRange(new object[] {
																	resources.GetString("m_PriorityComboBox.Items"),
																	resources.GetString("m_PriorityComboBox.Items1"),
																	resources.GetString("m_PriorityComboBox.Items2"),
																	resources.GetString("m_PriorityComboBox.Items3")});
			this.m_PriorityComboBox.Location = ((System.Drawing.Point)(resources.GetObject("m_PriorityComboBox.Location")));
			this.m_PriorityComboBox.MaxDropDownItems = ((int)(resources.GetObject("m_PriorityComboBox.MaxDropDownItems")));
			this.m_PriorityComboBox.MaxLength = ((int)(resources.GetObject("m_PriorityComboBox.MaxLength")));
			this.m_PriorityComboBox.Name = "m_PriorityComboBox";
			this.m_PriorityComboBox.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_PriorityComboBox.RightToLeft")));
			this.m_PriorityComboBox.Size = ((System.Drawing.Size)(resources.GetObject("m_PriorityComboBox.Size")));
			this.m_PriorityComboBox.TabIndex = ((int)(resources.GetObject("m_PriorityComboBox.TabIndex")));
			this.m_PriorityComboBox.Text = resources.GetString("m_PriorityComboBox.Text");
			this.m_PlaceToolTip.SetToolTip(this.m_PriorityComboBox, resources.GetString("m_PriorityComboBox.ToolTip"));
			this.m_PriorityComboBox.Visible = ((bool)(resources.GetObject("m_PriorityComboBox.Visible")));
			// 
			// m_RefreshComboBox
			// 
			this.m_RefreshComboBox.AccessibleDescription = resources.GetString("m_RefreshComboBox.AccessibleDescription");
			this.m_RefreshComboBox.AccessibleName = resources.GetString("m_RefreshComboBox.AccessibleName");
			this.m_RefreshComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_RefreshComboBox.Anchor")));
			this.m_RefreshComboBox.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("m_RefreshComboBox.BackgroundImage")));
			this.m_RefreshComboBox.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_RefreshComboBox.Dock")));
			this.m_RefreshComboBox.Enabled = ((bool)(resources.GetObject("m_RefreshComboBox.Enabled")));
			this.m_RefreshComboBox.Font = ((System.Drawing.Font)(resources.GetObject("m_RefreshComboBox.Font")));
			this.m_RefreshComboBox.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_RefreshComboBox.ImeMode")));
			this.m_RefreshComboBox.IntegralHeight = ((bool)(resources.GetObject("m_RefreshComboBox.IntegralHeight")));
			this.m_RefreshComboBox.ItemHeight = ((int)(resources.GetObject("m_RefreshComboBox.ItemHeight")));
			this.m_RefreshComboBox.Location = ((System.Drawing.Point)(resources.GetObject("m_RefreshComboBox.Location")));
			this.m_RefreshComboBox.MaxDropDownItems = ((int)(resources.GetObject("m_RefreshComboBox.MaxDropDownItems")));
			this.m_RefreshComboBox.MaxLength = ((int)(resources.GetObject("m_RefreshComboBox.MaxLength")));
			this.m_RefreshComboBox.Name = "m_RefreshComboBox";
			this.m_RefreshComboBox.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_RefreshComboBox.RightToLeft")));
			this.m_RefreshComboBox.Size = ((System.Drawing.Size)(resources.GetObject("m_RefreshComboBox.Size")));
			this.m_RefreshComboBox.TabIndex = ((int)(resources.GetObject("m_RefreshComboBox.TabIndex")));
			this.m_RefreshComboBox.Text = resources.GetString("m_RefreshComboBox.Text");
			this.m_PlaceToolTip.SetToolTip(this.m_RefreshComboBox, resources.GetString("m_RefreshComboBox.ToolTip"));
			this.m_RefreshComboBox.Visible = ((bool)(resources.GetObject("m_RefreshComboBox.Visible")));
			// 
			// m_PlaceToolTip
			// 
			this.m_PlaceToolTip.AutomaticDelay = 1000;
			this.m_PlaceToolTip.AutoPopDelay = 10000;
			this.m_PlaceToolTip.InitialDelay = 100;
			this.m_PlaceToolTip.ReshowDelay = 100;
			// 
			// m_ResultsListView
			// 
			this.m_ResultsListView.AccessibleDescription = resources.GetString("m_ResultsListView.AccessibleDescription");
			this.m_ResultsListView.AccessibleName = resources.GetString("m_ResultsListView.AccessibleName");
			this.m_ResultsListView.Alignment = ((System.Windows.Forms.ListViewAlignment)(resources.GetObject("m_ResultsListView.Alignment")));
			this.m_ResultsListView.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_ResultsListView.Anchor")));
			this.m_ResultsListView.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("m_ResultsListView.BackgroundImage")));
			this.m_ResultsListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
																								this.m_SearchResultNameCol,
																								this.m_SearchResultDescCol});
			this.m_ResultsListView.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_ResultsListView.Dock")));
			this.m_ResultsListView.Enabled = ((bool)(resources.GetObject("m_ResultsListView.Enabled")));
			this.m_ResultsListView.Font = ((System.Drawing.Font)(resources.GetObject("m_ResultsListView.Font")));
			this.m_ResultsListView.FullRowSelect = true;
			this.m_ResultsListView.GridLines = true;
			this.m_ResultsListView.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_ResultsListView.ImeMode")));
			this.m_ResultsListView.LabelWrap = ((bool)(resources.GetObject("m_ResultsListView.LabelWrap")));
			this.m_ResultsListView.Location = ((System.Drawing.Point)(resources.GetObject("m_ResultsListView.Location")));
			this.m_ResultsListView.MultiSelect = false;
			this.m_ResultsListView.Name = "m_ResultsListView";
			this.m_ResultsListView.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_ResultsListView.RightToLeft")));
			this.m_ResultsListView.Size = ((System.Drawing.Size)(resources.GetObject("m_ResultsListView.Size")));
			this.m_ResultsListView.TabIndex = ((int)(resources.GetObject("m_ResultsListView.TabIndex")));
			this.m_ResultsListView.Text = resources.GetString("m_ResultsListView.Text");
			this.m_PlaceToolTip.SetToolTip(this.m_ResultsListView, resources.GetString("m_ResultsListView.ToolTip"));
			this.m_ResultsListView.View = System.Windows.Forms.View.Details;
			this.m_ResultsListView.Visible = ((bool)(resources.GetObject("m_ResultsListView.Visible")));
			// 
			// m_SearchResultNameCol
			// 
			this.m_SearchResultNameCol.Text = resources.GetString("m_SearchResultNameCol.Text");
			this.m_SearchResultNameCol.TextAlign = ((System.Windows.Forms.HorizontalAlignment)(resources.GetObject("m_SearchResultNameCol.TextAlign")));
			this.m_SearchResultNameCol.Width = ((int)(resources.GetObject("m_SearchResultNameCol.Width")));
			// 
			// m_SearchResultDescCol
			// 
			this.m_SearchResultDescCol.Text = resources.GetString("m_SearchResultDescCol.Text");
			this.m_SearchResultDescCol.TextAlign = ((System.Windows.Forms.HorizontalAlignment)(resources.GetObject("m_SearchResultDescCol.TextAlign")));
			this.m_SearchResultDescCol.Width = ((int)(resources.GetObject("m_SearchResultDescCol.Width")));
			// 
			// m_SearchButton
			// 
			this.m_SearchButton.AccessibleDescription = resources.GetString("m_SearchButton.AccessibleDescription");
			this.m_SearchButton.AccessibleName = resources.GetString("m_SearchButton.AccessibleName");
			this.m_SearchButton.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_SearchButton.Anchor")));
			this.m_SearchButton.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("m_SearchButton.BackgroundImage")));
			this.m_SearchButton.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_SearchButton.Dock")));
			this.m_SearchButton.Enabled = ((bool)(resources.GetObject("m_SearchButton.Enabled")));
			this.m_SearchButton.FlatStyle = ((System.Windows.Forms.FlatStyle)(resources.GetObject("m_SearchButton.FlatStyle")));
			this.m_SearchButton.Font = ((System.Drawing.Font)(resources.GetObject("m_SearchButton.Font")));
			this.m_SearchButton.Image = ((System.Drawing.Image)(resources.GetObject("m_SearchButton.Image")));
			this.m_SearchButton.ImageAlign = ((System.Drawing.ContentAlignment)(resources.GetObject("m_SearchButton.ImageAlign")));
			this.m_SearchButton.ImageIndex = ((int)(resources.GetObject("m_SearchButton.ImageIndex")));
			this.m_SearchButton.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_SearchButton.ImeMode")));
			this.m_SearchButton.Location = ((System.Drawing.Point)(resources.GetObject("m_SearchButton.Location")));
			this.m_SearchButton.Name = "m_SearchButton";
			this.m_SearchButton.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_SearchButton.RightToLeft")));
			this.m_SearchButton.Size = ((System.Drawing.Size)(resources.GetObject("m_SearchButton.Size")));
			this.m_SearchButton.TabIndex = ((int)(resources.GetObject("m_SearchButton.TabIndex")));
			this.m_SearchButton.Text = resources.GetString("m_SearchButton.Text");
			this.m_SearchButton.TextAlign = ((System.Drawing.ContentAlignment)(resources.GetObject("m_SearchButton.TextAlign")));
			this.m_PlaceToolTip.SetToolTip(this.m_SearchButton, resources.GetString("m_SearchButton.ToolTip"));
			this.m_SearchButton.Visible = ((bool)(resources.GetObject("m_SearchButton.Visible")));
			this.m_SearchButton.Click += new System.EventHandler(this.m_SearchButton_Click);
			// 
			// m_CancelButton
			// 
			this.m_CancelButton.AccessibleDescription = resources.GetString("m_CancelButton.AccessibleDescription");
			this.m_CancelButton.AccessibleName = resources.GetString("m_CancelButton.AccessibleName");
			this.m_CancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_CancelButton.Anchor")));
			this.m_CancelButton.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("m_CancelButton.BackgroundImage")));
			this.m_CancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.m_CancelButton.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_CancelButton.Dock")));
			this.m_CancelButton.Enabled = ((bool)(resources.GetObject("m_CancelButton.Enabled")));
			this.m_CancelButton.FlatStyle = ((System.Windows.Forms.FlatStyle)(resources.GetObject("m_CancelButton.FlatStyle")));
			this.m_CancelButton.Font = ((System.Drawing.Font)(resources.GetObject("m_CancelButton.Font")));
			this.m_CancelButton.Image = ((System.Drawing.Image)(resources.GetObject("m_CancelButton.Image")));
			this.m_CancelButton.ImageAlign = ((System.Drawing.ContentAlignment)(resources.GetObject("m_CancelButton.ImageAlign")));
			this.m_CancelButton.ImageIndex = ((int)(resources.GetObject("m_CancelButton.ImageIndex")));
			this.m_CancelButton.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_CancelButton.ImeMode")));
			this.m_CancelButton.Location = ((System.Drawing.Point)(resources.GetObject("m_CancelButton.Location")));
			this.m_CancelButton.Name = "m_CancelButton";
			this.m_CancelButton.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_CancelButton.RightToLeft")));
			this.m_CancelButton.Size = ((System.Drawing.Size)(resources.GetObject("m_CancelButton.Size")));
			this.m_CancelButton.TabIndex = ((int)(resources.GetObject("m_CancelButton.TabIndex")));
			this.m_CancelButton.Text = resources.GetString("m_CancelButton.Text");
			this.m_CancelButton.TextAlign = ((System.Drawing.ContentAlignment)(resources.GetObject("m_CancelButton.TextAlign")));
			this.m_PlaceToolTip.SetToolTip(this.m_CancelButton, resources.GetString("m_CancelButton.ToolTip"));
			this.m_CancelButton.Visible = ((bool)(resources.GetObject("m_CancelButton.Visible")));
			this.m_CancelButton.Click += new System.EventHandler(this.m_CancelButton_Click);
			// 
			// m_SaveButton
			// 
			this.m_SaveButton.AccessibleDescription = resources.GetString("m_SaveButton.AccessibleDescription");
			this.m_SaveButton.AccessibleName = resources.GetString("m_SaveButton.AccessibleName");
			this.m_SaveButton.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_SaveButton.Anchor")));
			this.m_SaveButton.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("m_SaveButton.BackgroundImage")));
			this.m_SaveButton.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_SaveButton.Dock")));
			this.m_SaveButton.Enabled = ((bool)(resources.GetObject("m_SaveButton.Enabled")));
			this.m_SaveButton.FlatStyle = ((System.Windows.Forms.FlatStyle)(resources.GetObject("m_SaveButton.FlatStyle")));
			this.m_SaveButton.Font = ((System.Drawing.Font)(resources.GetObject("m_SaveButton.Font")));
			this.m_SaveButton.Image = ((System.Drawing.Image)(resources.GetObject("m_SaveButton.Image")));
			this.m_SaveButton.ImageAlign = ((System.Drawing.ContentAlignment)(resources.GetObject("m_SaveButton.ImageAlign")));
			this.m_SaveButton.ImageIndex = ((int)(resources.GetObject("m_SaveButton.ImageIndex")));
			this.m_SaveButton.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_SaveButton.ImeMode")));
			this.m_SaveButton.Location = ((System.Drawing.Point)(resources.GetObject("m_SaveButton.Location")));
			this.m_SaveButton.Name = "m_SaveButton";
			this.m_SaveButton.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_SaveButton.RightToLeft")));
			this.m_SaveButton.Size = ((System.Drawing.Size)(resources.GetObject("m_SaveButton.Size")));
			this.m_SaveButton.TabIndex = ((int)(resources.GetObject("m_SaveButton.TabIndex")));
			this.m_SaveButton.Text = resources.GetString("m_SaveButton.Text");
			this.m_SaveButton.TextAlign = ((System.Drawing.ContentAlignment)(resources.GetObject("m_SaveButton.TextAlign")));
			this.m_PlaceToolTip.SetToolTip(this.m_SaveButton, resources.GetString("m_SaveButton.ToolTip"));
			this.m_SaveButton.Visible = ((bool)(resources.GetObject("m_SaveButton.Visible")));
			this.m_SaveButton.Click += new System.EventHandler(this.m_SaveButton_Click);
			// 
			// m_BottomPanel
			// 
			this.m_BottomPanel.AccessibleDescription = resources.GetString("m_BottomPanel.AccessibleDescription");
			this.m_BottomPanel.AccessibleName = resources.GetString("m_BottomPanel.AccessibleName");
			this.m_BottomPanel.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_BottomPanel.Anchor")));
			this.m_BottomPanel.AutoScroll = ((bool)(resources.GetObject("m_BottomPanel.AutoScroll")));
			this.m_BottomPanel.AutoScrollMargin = ((System.Drawing.Size)(resources.GetObject("m_BottomPanel.AutoScrollMargin")));
			this.m_BottomPanel.AutoScrollMinSize = ((System.Drawing.Size)(resources.GetObject("m_BottomPanel.AutoScrollMinSize")));
			this.m_BottomPanel.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("m_BottomPanel.BackgroundImage")));
			this.m_BottomPanel.Controls.Add(this.m_CancelButton);
			this.m_BottomPanel.Controls.Add(this.m_SaveButton);
			this.m_BottomPanel.Controls.Add(this.m_SettingsGroupBox);
			this.m_BottomPanel.Controls.Add(this.m_PlacementGroupBox);
			this.m_BottomPanel.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_BottomPanel.Dock")));
			this.m_BottomPanel.Enabled = ((bool)(resources.GetObject("m_BottomPanel.Enabled")));
			this.m_BottomPanel.Font = ((System.Drawing.Font)(resources.GetObject("m_BottomPanel.Font")));
			this.m_BottomPanel.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_BottomPanel.ImeMode")));
			this.m_BottomPanel.Location = ((System.Drawing.Point)(resources.GetObject("m_BottomPanel.Location")));
			this.m_BottomPanel.Name = "m_BottomPanel";
			this.m_BottomPanel.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_BottomPanel.RightToLeft")));
			this.m_BottomPanel.Size = ((System.Drawing.Size)(resources.GetObject("m_BottomPanel.Size")));
			this.m_BottomPanel.TabIndex = ((int)(resources.GetObject("m_BottomPanel.TabIndex")));
			this.m_BottomPanel.Text = resources.GetString("m_BottomPanel.Text");
			this.m_PlaceToolTip.SetToolTip(this.m_BottomPanel, resources.GetString("m_BottomPanel.ToolTip"));
			this.m_BottomPanel.Visible = ((bool)(resources.GetObject("m_BottomPanel.Visible")));
			// 
			// m_HeaderPanel
			// 
			this.m_HeaderPanel.AccessibleDescription = resources.GetString("m_HeaderPanel.AccessibleDescription");
			this.m_HeaderPanel.AccessibleName = resources.GetString("m_HeaderPanel.AccessibleName");
			this.m_HeaderPanel.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_HeaderPanel.Anchor")));
			this.m_HeaderPanel.AutoScroll = ((bool)(resources.GetObject("m_HeaderPanel.AutoScroll")));
			this.m_HeaderPanel.AutoScrollMargin = ((System.Drawing.Size)(resources.GetObject("m_HeaderPanel.AutoScrollMargin")));
			this.m_HeaderPanel.AutoScrollMinSize = ((System.Drawing.Size)(resources.GetObject("m_HeaderPanel.AutoScrollMinSize")));
			this.m_HeaderPanel.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("m_HeaderPanel.BackgroundImage")));
			this.m_HeaderPanel.Controls.Add(this.m_HiveComboBox);
			this.m_HeaderPanel.Controls.Add(this.m_HiveLabel);
			this.m_HeaderPanel.Controls.Add(this.m_FeedLabel);
			this.m_HeaderPanel.Controls.Add(this.m_FeedTextBox);
			this.m_HeaderPanel.Controls.Add(this.m_SearchButton);
			this.m_HeaderPanel.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_HeaderPanel.Dock")));
			this.m_HeaderPanel.Enabled = ((bool)(resources.GetObject("m_HeaderPanel.Enabled")));
			this.m_HeaderPanel.Font = ((System.Drawing.Font)(resources.GetObject("m_HeaderPanel.Font")));
			this.m_HeaderPanel.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_HeaderPanel.ImeMode")));
			this.m_HeaderPanel.Location = ((System.Drawing.Point)(resources.GetObject("m_HeaderPanel.Location")));
			this.m_HeaderPanel.Name = "m_HeaderPanel";
			this.m_HeaderPanel.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_HeaderPanel.RightToLeft")));
			this.m_HeaderPanel.Size = ((System.Drawing.Size)(resources.GetObject("m_HeaderPanel.Size")));
			this.m_HeaderPanel.TabIndex = ((int)(resources.GetObject("m_HeaderPanel.TabIndex")));
			this.m_HeaderPanel.Text = resources.GetString("m_HeaderPanel.Text");
			this.m_PlaceToolTip.SetToolTip(this.m_HeaderPanel, resources.GetString("m_HeaderPanel.ToolTip"));
			this.m_HeaderPanel.Visible = ((bool)(resources.GetObject("m_HeaderPanel.Visible")));
			// 
			// m_CenterPanel
			// 
			this.m_CenterPanel.AccessibleDescription = resources.GetString("m_CenterPanel.AccessibleDescription");
			this.m_CenterPanel.AccessibleName = resources.GetString("m_CenterPanel.AccessibleName");
			this.m_CenterPanel.Anchor = ((System.Windows.Forms.AnchorStyles)(resources.GetObject("m_CenterPanel.Anchor")));
			this.m_CenterPanel.AutoScroll = ((bool)(resources.GetObject("m_CenterPanel.AutoScroll")));
			this.m_CenterPanel.AutoScrollMargin = ((System.Drawing.Size)(resources.GetObject("m_CenterPanel.AutoScrollMargin")));
			this.m_CenterPanel.AutoScrollMinSize = ((System.Drawing.Size)(resources.GetObject("m_CenterPanel.AutoScrollMinSize")));
			this.m_CenterPanel.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("m_CenterPanel.BackgroundImage")));
			this.m_CenterPanel.Controls.Add(this.m_ResultsListView);
			this.m_CenterPanel.Dock = ((System.Windows.Forms.DockStyle)(resources.GetObject("m_CenterPanel.Dock")));
			this.m_CenterPanel.DockPadding.Left = 8;
			this.m_CenterPanel.DockPadding.Right = 8;
			this.m_CenterPanel.DockPadding.Top = 4;
			this.m_CenterPanel.Enabled = ((bool)(resources.GetObject("m_CenterPanel.Enabled")));
			this.m_CenterPanel.Font = ((System.Drawing.Font)(resources.GetObject("m_CenterPanel.Font")));
			this.m_CenterPanel.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("m_CenterPanel.ImeMode")));
			this.m_CenterPanel.Location = ((System.Drawing.Point)(resources.GetObject("m_CenterPanel.Location")));
			this.m_CenterPanel.Name = "m_CenterPanel";
			this.m_CenterPanel.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("m_CenterPanel.RightToLeft")));
			this.m_CenterPanel.Size = ((System.Drawing.Size)(resources.GetObject("m_CenterPanel.Size")));
			this.m_CenterPanel.TabIndex = ((int)(resources.GetObject("m_CenterPanel.TabIndex")));
			this.m_CenterPanel.Text = resources.GetString("m_CenterPanel.Text");
			this.m_PlaceToolTip.SetToolTip(this.m_CenterPanel, resources.GetString("m_CenterPanel.ToolTip"));
			this.m_CenterPanel.Visible = ((bool)(resources.GetObject("m_CenterPanel.Visible")));
			// 
			// FeedEditor
			// 
			this.AcceptButton = this.m_SaveButton;
			this.AccessibleDescription = resources.GetString("$this.AccessibleDescription");
			this.AccessibleName = resources.GetString("$this.AccessibleName");
			this.AutoScaleBaseSize = ((System.Drawing.Size)(resources.GetObject("$this.AutoScaleBaseSize")));
			this.AutoScroll = ((bool)(resources.GetObject("$this.AutoScroll")));
			this.AutoScrollMargin = ((System.Drawing.Size)(resources.GetObject("$this.AutoScrollMargin")));
			this.AutoScrollMinSize = ((System.Drawing.Size)(resources.GetObject("$this.AutoScrollMinSize")));
			this.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("$this.BackgroundImage")));
			this.CancelButton = this.m_CancelButton;
			this.ClientSize = ((System.Drawing.Size)(resources.GetObject("$this.ClientSize")));
			this.Controls.Add(this.m_CenterPanel);
			this.Controls.Add(this.m_HeaderPanel);
			this.Controls.Add(this.m_BottomPanel);
			this.Enabled = ((bool)(resources.GetObject("$this.Enabled")));
			this.Font = ((System.Drawing.Font)(resources.GetObject("$this.Font")));
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.ImeMode = ((System.Windows.Forms.ImeMode)(resources.GetObject("$this.ImeMode")));
			this.Location = ((System.Drawing.Point)(resources.GetObject("$this.Location")));
			this.MaximizeBox = false;
			this.MaximumSize = ((System.Drawing.Size)(resources.GetObject("$this.MaximumSize")));
			this.MinimumSize = ((System.Drawing.Size)(resources.GetObject("$this.MinimumSize")));
			this.Name = "FeedEditor";
			this.RightToLeft = ((System.Windows.Forms.RightToLeft)(resources.GetObject("$this.RightToLeft")));
			this.StartPosition = ((System.Windows.Forms.FormStartPosition)(resources.GetObject("$this.StartPosition")));
			this.Text = resources.GetString("$this.Text");
			this.m_PlaceToolTip.SetToolTip(this, resources.GetString("$this.ToolTip"));
			this.Shown += new System.EventHandler(this.FeedEditor_Shown);
			this.Controls.SetChildIndex(this.m_BottomPanel, 0);
			this.Controls.SetChildIndex(this.m_HeaderPanel, 0);
			this.Controls.SetChildIndex(this.m_CenterPanel, 0);
			this.Controls.SetChildIndex(this.m_ActionProgressBar, 0);
			this.m_PlacementGroupBox.ResumeLayout(false);
			this.m_SettingsGroupBox.ResumeLayout(false);
			this.m_BottomPanel.ResumeLayout(false);
			this.m_HeaderPanel.ResumeLayout(false);
			this.m_CenterPanel.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion

		/// <summary> Clean up any resources </summary>
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
