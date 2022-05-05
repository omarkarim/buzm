using System;
using System.Collections;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Buzm.Register;
using Buzm.Schemas;
using Buzm.Hives;

namespace Buzm
{
	/// <summary>Basic editor for user posts</summary>
	public class PostEditor : System.Windows.Forms.Form
	{
		private bool m_PostComplete;
		public event EventHandler Published;		

		private User m_HiveUser; // active user
		private ItemType m_PostItem; // post schema

		private Hashtable m_PlaceTable; // positions
		private string m_DefaultPlaceTag; // for xslt		
		
		private System.Windows.Forms.Label m_HiveLabel;
		private System.Windows.Forms.Label m_TagsLabel;
		private System.Windows.Forms.Label m_LinkLabel;
		private System.Windows.Forms.Label m_TitleLabel;		
		
		private System.Windows.Forms.Button m_CancelButton;
		private System.Windows.Forms.Button m_PublishButton;
		private System.Windows.Forms.ToolTip m_PlaceToolTip;		
		private System.ComponentModel.IContainer components;
		private System.Windows.Forms.ToolTip m_ExpireToolTip;
		private System.Windows.Forms.ComboBox m_HiveComboBox;		
		
		private Buzm.Utility.Forms.SmartTextBox m_TagsTextBox;
		private Buzm.Utility.Forms.SmartTextBox m_LinkTextBox;
		private Buzm.Utility.Forms.SmartTextBox m_TitleTextBox;
		private System.Windows.Forms.GroupBox m_ExpireGroupBox;
		private System.Windows.Forms.CheckBox m_ExpireCheckBox;
		private Buzm.Utility.Forms.SmartTextBox m_SummaryTextBox;						
		
		private System.Windows.Forms.GroupBox m_PlacementGroupBox;		
		private System.Windows.Forms.PictureBox m_SetPlacePictureBox;
		private System.Windows.Forms.PictureBox m_PlaceTcOnPictureBox;
		private System.Windows.Forms.PictureBox m_PlaceTcOffPictureBox;
		private System.Windows.Forms.PictureBox m_PlaceBrOffPictureBox;
		private System.Windows.Forms.PictureBox m_PlaceTrOffPictureBox;
		private System.Windows.Forms.PictureBox m_PlaceBlOffPictureBox;
		private System.Windows.Forms.PictureBox m_PlaceBcOffPictureBox;
		private System.Windows.Forms.PictureBox m_PlaceTlOffPictureBox;
		private System.Windows.Forms.PictureBox m_PlaceTlOnPictureBox;
		private System.Windows.Forms.PictureBox m_PlaceTrOnPictureBox;
		
		private const string POST_CANCEL_TEXT = "Do you want to discard changes?";
		private const string TITLE_EMPTY_TEXT = "Please enter a title for your post.";
		private const string TITLE_EXISTS_TEXT = "Another post has the same title. Please try a different one.";
		private const string EXPIRE_HELP_TEXT = "Uncheck the box if this is a permanent item such as a navigation link";
		private const string DISABLED_PLACE_TEXT = "Reserved for Feeds";		
		private const string INACTIVE_PLACE_TEXT = "Click to Select";		
		private const string EDIT_FORM_TEXT = "Edit Post - Buzm";
		private const string ACTIVE_PLACE_TEXT = "Selected";
		private const string REGEX_NEWLINE = "\r?\n";
				
		public PostEditor( User user, Hashtable hives )
		{
			m_HiveUser = user;
			InitializeComponent();
			m_PostComplete = false;

			InitHiveComboBox( hives );
			m_PlaceTable = new Hashtable();

			// save xslt positions in place tags
			SetPlaceTag( m_PlaceTlOffPictureBox, "0,1" );
			SetPlaceTag( m_PlaceBlOffPictureBox, "0,2" );
			SetPlaceTag( m_PlaceTcOffPictureBox, "1,1" );
			SetPlaceTag( m_PlaceBcOffPictureBox, "1,2" );
			SetPlaceTag( m_PlaceTrOffPictureBox, "2,1" );
			SetPlaceTag( m_PlaceBrOffPictureBox, "2,2" );
			
			// setup default xslt position
			m_DefaultPlaceTag = "1,1"; // tc
			m_PlaceTcOffPictureBox.SendToBack();
			m_SetPlacePictureBox = m_PlaceTcOffPictureBox;

			// set tooltip text for post expiration checkbox
			m_ExpireToolTip.SetToolTip( m_ExpireCheckBox, EXPIRE_HELP_TEXT );

			// set tooltip text for available and disabled placement options
			m_PlaceToolTip.SetToolTip( m_PlaceTlOnPictureBox, ACTIVE_PLACE_TEXT );			
			m_PlaceToolTip.SetToolTip( m_PlaceTcOnPictureBox, ACTIVE_PLACE_TEXT );			
			m_PlaceToolTip.SetToolTip( m_PlaceTrOnPictureBox, ACTIVE_PLACE_TEXT );
			m_PlaceToolTip.SetToolTip( m_PlaceTlOffPictureBox, INACTIVE_PLACE_TEXT );			
			m_PlaceToolTip.SetToolTip( m_PlaceTcOffPictureBox, INACTIVE_PLACE_TEXT );			
			m_PlaceToolTip.SetToolTip( m_PlaceTrOffPictureBox, INACTIVE_PLACE_TEXT );
			m_PlaceToolTip.SetToolTip( m_PlaceBlOffPictureBox, DISABLED_PLACE_TEXT );
			m_PlaceToolTip.SetToolTip( m_PlaceBcOffPictureBox, DISABLED_PLACE_TEXT );
			m_PlaceToolTip.SetToolTip( m_PlaceBrOffPictureBox, DISABLED_PLACE_TEXT );
		}

		/// <summary>Loads and presents an existing post to the user for editing </summary>		
		public PostEditor( ItemType post, User user, Hashtable hives ) : this( user, hives )
		{
			m_PostItem = post; // set post to edit
			this.Text = EDIT_FORM_TEXT; // set title
			m_HiveComboBox.Enabled = false; // lock hive
			
			// load existing data into post text fields
			m_TitleTextBox.UserText = m_PostItem.Title;
			m_LinkTextBox.UserText = m_PostItem.Link;
			m_TagsTextBox.UserText = m_PostItem.Tags;			
			
			string summary = m_PostItem.Summary;
			if( summary != null ) // normalize line breaks
			{
				m_SummaryTextBox.UserText =
				Regex.Replace( summary, REGEX_NEWLINE, Environment.NewLine );
			}

			// select place box based on position
			string position = m_PostItem.Position;
			if( (position != null) && (position != m_DefaultPlaceTag) )
			{
				object placePicBox = m_PlaceTable[position];
				if( placePicBox != null )
				{
					PlacePictureBox_Click( placePicBox, null );
					m_DefaultPlaceTag = position; 
				}
			}
		}

		private void m_PublishButton_Click( object sender, System.EventArgs e )
		{			
			if( IsTitleValid() ) // if the user entered a unique title
			{
				if( IsPostModified() ) // do nothing if post unedited
				{
					string login = m_HiveUser.Login; // get editor login
					if( m_PostItem != null ) m_PostItem.AddVersion( login );
					else m_PostItem = new ItemType( login ); // new post

					m_PostItem.Link = m_LinkTextBox.UserText;
					m_PostItem.Tags = m_TagsTextBox.UserText;

					m_PostItem.Title = m_TitleTextBox.UserText;
					m_PostItem.Summary = m_SummaryTextBox.UserText;
					
					m_PostItem.Position = m_SetPlacePictureBox.Tag.ToString();
					if( Published != null ) Published( this, new EventArgs() );
				}
				m_PostComplete = true; // indicate successful post
				this.Close(); // close and cleanup post editor
			}			
		}

		/// <summary>Returns an xml format
		/// string of the new post</summary>
		public string ToXml()
		{
			if( m_PostItem != null ) return m_PostItem.ToXml();
			else return String.Empty; // since no post published
		}

		public ItemType PostItem
		{
			get { return m_PostItem; }
			set { m_PostItem = value; }
		}

		public HiveModel SelectedHive 
		{
			get { return (HiveModel)m_HiveComboBox.SelectedItem; } 
			set { m_HiveComboBox.SelectedItem = value; }
		}

		private void InitHiveComboBox( Hashtable hives )
		{
			foreach( HiveModel hive in hives.Values )
			{ 
				// uses DisplayMember property 
				m_HiveComboBox.Items.Add( hive );
			}
		}
	
		/// <summary>Returns true if user has populated
		/// any of the editable post text fields</summary>
		private bool IsPostTextSet()
		{
			if( ( m_TitleTextBox.Modified || m_LinkTextBox.Modified ) 
			 ||	( m_TagsTextBox.Modified || m_SummaryTextBox.Modified ) ) 
				 return true; // user modified some text
			else return false; // no user text entered
		}

		private bool IsPostModified()
		{
			string placeTag = m_SetPlacePictureBox.Tag.ToString();
			if( IsPostTextSet() || ( placeTag != m_DefaultPlaceTag ) ) 
				 return true; // user edited text or position
			else return false; // no changes were made
		}

		private bool IsTitleValid()
		{
			if( m_TitleTextBox.Populated )
			{
				HiveModel hive = SelectedHive;
				if( hive != null ) // if hive selected
				{
					string title = m_TitleTextBox.UserText;
					ItemType oldItem = hive.GetItemTypeByTitle( title );
					if( oldItem != null ) // if item with same title exists
					{
						if( ( m_PostItem == null ) // this is a new post
						 || ( !String.IsNullOrEmpty( m_PostItem.Guid )
						 &&	( !String.IsNullOrEmpty( oldItem.Guid ) )
						 && ( m_PostItem.Guid != oldItem.Guid ) ) )
						{
							MessageBox.Show( this, TITLE_EXISTS_TEXT, "Buzm Alert",
							MessageBoxButtons.OK, MessageBoxIcon.Information );
							return false; // title already in use
						}
					}
				}
				return true; // title is valid
			}
			else // no title specified by user
			{
				MessageBox.Show( this, TITLE_EMPTY_TEXT, "Buzm Alert",
				MessageBoxButtons.OK, MessageBoxIcon.Information );
				return false; // invalid title
			}			
		}

		private void PlacePictureBox_Click( object sender, System.EventArgs e )
		{
			PictureBox placePictureBox = ((PictureBox)sender);
			placePictureBox.SendToBack(); // reveal the on image
			m_SetPlacePictureBox.BringToFront(); // hide on image
			m_SetPlacePictureBox = placePictureBox; // save off image
		}

		private void SetPlaceTag( PictureBox placePictureBox, string tag )
		{
			placePictureBox.Tag = tag; // to find the tag by picturebox
			m_PlaceTable.Add( tag, placePictureBox ); // reverse lookup
		}

		private void m_CancelButton_Click( object sender, EventArgs e ){ Close(); }
		private void PostEditor_Closing( object sender, CancelEventArgs e )
		{
			if( !m_PostComplete && IsPostTextSet() ) 
			{
				// ask user if they really want to discard changes
				if( MessageBox.Show( POST_CANCEL_TEXT, "Buzm Alert", 
					MessageBoxButtons.YesNo ) ==  DialogResult.No ) 
					e.Cancel = true; // if not, cancel close event
			}
		}

		private void PostEditor_Shown( object sender, EventArgs e )
		{
			this.BringToFront(); // make sure form is visible
		}

		public string Title
		{
			get { return m_TitleTextBox.UserText; }
			set { m_TitleTextBox.PresetText = value; }
		}

		public string Link
		{
			get { return m_LinkTextBox.UserText; }
			set { m_LinkTextBox.PresetText = value; }
		}

		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PostEditor));
			this.m_TitleTextBox = new Buzm.Utility.Forms.SmartTextBox();
			this.m_TitleLabel = new System.Windows.Forms.Label();
			this.m_PublishButton = new System.Windows.Forms.Button();
			this.m_LinkTextBox = new Buzm.Utility.Forms.SmartTextBox();
			this.m_LinkLabel = new System.Windows.Forms.Label();
			this.m_HiveLabel = new System.Windows.Forms.Label();
			this.m_HiveComboBox = new System.Windows.Forms.ComboBox();
			this.m_PlacementGroupBox = new System.Windows.Forms.GroupBox();
			this.m_PlaceTrOffPictureBox = new System.Windows.Forms.PictureBox();
			this.m_PlaceTlOffPictureBox = new System.Windows.Forms.PictureBox();
			this.m_PlaceTcOffPictureBox = new System.Windows.Forms.PictureBox();
			this.m_PlaceTlOnPictureBox = new System.Windows.Forms.PictureBox();
			this.m_PlaceTrOnPictureBox = new System.Windows.Forms.PictureBox();
			this.m_PlaceTcOnPictureBox = new System.Windows.Forms.PictureBox();
			this.m_PlaceBlOffPictureBox = new System.Windows.Forms.PictureBox();
			this.m_PlaceBcOffPictureBox = new System.Windows.Forms.PictureBox();
			this.m_PlaceBrOffPictureBox = new System.Windows.Forms.PictureBox();
			this.m_CancelButton = new System.Windows.Forms.Button();
			this.m_PlaceToolTip = new System.Windows.Forms.ToolTip(this.components);
			this.m_SummaryTextBox = new Buzm.Utility.Forms.SmartTextBox();
			this.m_ExpireGroupBox = new System.Windows.Forms.GroupBox();
			this.m_ExpireCheckBox = new System.Windows.Forms.CheckBox();
			this.m_ExpireToolTip = new System.Windows.Forms.ToolTip(this.components);
			this.m_TagsTextBox = new Buzm.Utility.Forms.SmartTextBox();
			this.m_TagsLabel = new System.Windows.Forms.Label();
			this.m_PlacementGroupBox.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.m_PlaceTrOffPictureBox)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.m_PlaceTlOffPictureBox)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.m_PlaceTcOffPictureBox)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.m_PlaceTlOnPictureBox)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.m_PlaceTrOnPictureBox)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.m_PlaceTcOnPictureBox)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.m_PlaceBlOffPictureBox)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.m_PlaceBcOffPictureBox)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.m_PlaceBrOffPictureBox)).BeginInit();
			this.m_ExpireGroupBox.SuspendLayout();
			this.SuspendLayout();
			// 
			// m_TitleTextBox
			// 
			this.m_TitleTextBox.FocusColor = System.Drawing.SystemColors.WindowText;
			this.m_TitleTextBox.FocusText = "";
			this.m_TitleTextBox.ForeColor = System.Drawing.SystemColors.GrayText;
			this.m_TitleTextBox.HelpText = "Enter a title for your post here...";
			this.m_TitleTextBox.Location = new System.Drawing.Point(49, 32);
			this.m_TitleTextBox.MaxLength = 10000;
			this.m_TitleTextBox.Name = "m_TitleTextBox";
			this.m_TitleTextBox.Size = new System.Drawing.Size(423, 21);
			this.m_TitleTextBox.TabIndex = 2;
			this.m_TitleTextBox.Text = "Enter a title for your post here...";
			// 
			// m_TitleLabel
			// 
			this.m_TitleLabel.Location = new System.Drawing.Point(9, 32);
			this.m_TitleLabel.Name = "m_TitleLabel";
			this.m_TitleLabel.Size = new System.Drawing.Size(48, 21);
			this.m_TitleLabel.TabIndex = 1;
			this.m_TitleLabel.Text = "Title:";
			this.m_TitleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// m_PublishButton
			// 
			this.m_PublishButton.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_PublishButton.Location = new System.Drawing.Point(264, 400);
			this.m_PublishButton.Name = "m_PublishButton";
			this.m_PublishButton.Size = new System.Drawing.Size(100, 25);
			this.m_PublishButton.TabIndex = 6;
			this.m_PublishButton.Text = "Save Post";
			this.m_PublishButton.Click += new System.EventHandler(this.m_PublishButton_Click);
			// 
			// m_LinkTextBox
			// 
			this.m_LinkTextBox.FocusColor = System.Drawing.SystemColors.WindowText;
			this.m_LinkTextBox.FocusText = "http://";
			this.m_LinkTextBox.ForeColor = System.Drawing.SystemColors.GrayText;
			this.m_LinkTextBox.HelpText = "Add a URL for your title (optional)";
			this.m_LinkTextBox.Location = new System.Drawing.Point(49, 56);
			this.m_LinkTextBox.MaxLength = 10000;
			this.m_LinkTextBox.Name = "m_LinkTextBox";
			this.m_LinkTextBox.Size = new System.Drawing.Size(423, 21);
			this.m_LinkTextBox.TabIndex = 3;
			this.m_LinkTextBox.Text = "Add a URL for your title (optional)";
			// 
			// m_LinkLabel
			// 
			this.m_LinkLabel.Location = new System.Drawing.Point(9, 56);
			this.m_LinkLabel.Name = "m_LinkLabel";
			this.m_LinkLabel.Size = new System.Drawing.Size(48, 21);
			this.m_LinkLabel.TabIndex = 6;
			this.m_LinkLabel.Text = "Link:";
			this.m_LinkLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// m_HiveLabel
			// 
			this.m_HiveLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_HiveLabel.Location = new System.Drawing.Point(9, 8);
			this.m_HiveLabel.Name = "m_HiveLabel";
			this.m_HiveLabel.Size = new System.Drawing.Size(30, 21);
			this.m_HiveLabel.TabIndex = 7;
			this.m_HiveLabel.Text = "Hive:";
			this.m_HiveLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// m_HiveComboBox
			// 
			this.m_HiveComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.m_HiveComboBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.m_HiveComboBox.Location = new System.Drawing.Point(49, 8);
			this.m_HiveComboBox.Name = "m_HiveComboBox";
			this.m_HiveComboBox.Size = new System.Drawing.Size(423, 21);
			this.m_HiveComboBox.Sorted = true;
			this.m_HiveComboBox.TabIndex = 1;
			// 
			// m_PlacementGroupBox
			// 
			this.m_PlacementGroupBox.Controls.Add(this.m_PlaceTrOffPictureBox);
			this.m_PlacementGroupBox.Controls.Add(this.m_PlaceTlOffPictureBox);
			this.m_PlacementGroupBox.Controls.Add(this.m_PlaceTcOffPictureBox);
			this.m_PlacementGroupBox.Controls.Add(this.m_PlaceTlOnPictureBox);
			this.m_PlacementGroupBox.Controls.Add(this.m_PlaceTrOnPictureBox);
			this.m_PlacementGroupBox.Controls.Add(this.m_PlaceTcOnPictureBox);
			this.m_PlacementGroupBox.Controls.Add(this.m_PlaceBlOffPictureBox);
			this.m_PlacementGroupBox.Controls.Add(this.m_PlaceBcOffPictureBox);
			this.m_PlacementGroupBox.Controls.Add(this.m_PlaceBrOffPictureBox);
			this.m_PlacementGroupBox.Location = new System.Drawing.Point(8, 288);
			this.m_PlacementGroupBox.Name = "m_PlacementGroupBox";
			this.m_PlacementGroupBox.Size = new System.Drawing.Size(464, 104);
			this.m_PlacementGroupBox.TabIndex = 18;
			this.m_PlacementGroupBox.TabStop = false;
			this.m_PlacementGroupBox.Text = "Placement (optional)";
			// 
			// m_PlaceTrOffPictureBox
			// 
			this.m_PlaceTrOffPictureBox.Cursor = System.Windows.Forms.Cursors.Hand;
			this.m_PlaceTrOffPictureBox.Image = ((System.Drawing.Image)(resources.GetObject("m_PlaceTrOffPictureBox.Image")));
			this.m_PlaceTrOffPictureBox.Location = new System.Drawing.Point(312, 24);
			this.m_PlaceTrOffPictureBox.Name = "m_PlaceTrOffPictureBox";
			this.m_PlaceTrOffPictureBox.Size = new System.Drawing.Size(66, 72);
			this.m_PlaceTrOffPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
			this.m_PlaceTrOffPictureBox.TabIndex = 6;
			this.m_PlaceTrOffPictureBox.TabStop = false;
			this.m_PlaceTrOffPictureBox.Click += new System.EventHandler(this.PlacePictureBox_Click);
			// 
			// m_PlaceTlOffPictureBox
			// 
			this.m_PlaceTlOffPictureBox.Cursor = System.Windows.Forms.Cursors.Hand;
			this.m_PlaceTlOffPictureBox.Image = ((System.Drawing.Image)(resources.GetObject("m_PlaceTlOffPictureBox.Image")));
			this.m_PlaceTlOffPictureBox.Location = new System.Drawing.Point(8, 24);
			this.m_PlaceTlOffPictureBox.Name = "m_PlaceTlOffPictureBox";
			this.m_PlaceTlOffPictureBox.Size = new System.Drawing.Size(66, 72);
			this.m_PlaceTlOffPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
			this.m_PlaceTlOffPictureBox.TabIndex = 2;
			this.m_PlaceTlOffPictureBox.TabStop = false;
			this.m_PlaceTlOffPictureBox.Click += new System.EventHandler(this.PlacePictureBox_Click);
			// 
			// m_PlaceTcOffPictureBox
			// 
			this.m_PlaceTcOffPictureBox.Cursor = System.Windows.Forms.Cursors.Hand;
			this.m_PlaceTcOffPictureBox.Image = ((System.Drawing.Image)(resources.GetObject("m_PlaceTcOffPictureBox.Image")));
			this.m_PlaceTcOffPictureBox.Location = new System.Drawing.Point(160, 24);
			this.m_PlaceTcOffPictureBox.Name = "m_PlaceTcOffPictureBox";
			this.m_PlaceTcOffPictureBox.Size = new System.Drawing.Size(66, 72);
			this.m_PlaceTcOffPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
			this.m_PlaceTcOffPictureBox.TabIndex = 8;
			this.m_PlaceTcOffPictureBox.TabStop = false;
			this.m_PlaceTcOffPictureBox.Click += new System.EventHandler(this.PlacePictureBox_Click);
			// 
			// m_PlaceTlOnPictureBox
			// 
			this.m_PlaceTlOnPictureBox.Image = ((System.Drawing.Image)(resources.GetObject("m_PlaceTlOnPictureBox.Image")));
			this.m_PlaceTlOnPictureBox.Location = new System.Drawing.Point(8, 24);
			this.m_PlaceTlOnPictureBox.Name = "m_PlaceTlOnPictureBox";
			this.m_PlaceTlOnPictureBox.Size = new System.Drawing.Size(66, 72);
			this.m_PlaceTlOnPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
			this.m_PlaceTlOnPictureBox.TabIndex = 9;
			this.m_PlaceTlOnPictureBox.TabStop = false;
			// 
			// m_PlaceTrOnPictureBox
			// 
			this.m_PlaceTrOnPictureBox.Image = ((System.Drawing.Image)(resources.GetObject("m_PlaceTrOnPictureBox.Image")));
			this.m_PlaceTrOnPictureBox.Location = new System.Drawing.Point(312, 24);
			this.m_PlaceTrOnPictureBox.Name = "m_PlaceTrOnPictureBox";
			this.m_PlaceTrOnPictureBox.Size = new System.Drawing.Size(66, 72);
			this.m_PlaceTrOnPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
			this.m_PlaceTrOnPictureBox.TabIndex = 12;
			this.m_PlaceTrOnPictureBox.TabStop = false;
			// 
			// m_PlaceTcOnPictureBox
			// 
			this.m_PlaceTcOnPictureBox.Image = ((System.Drawing.Image)(resources.GetObject("m_PlaceTcOnPictureBox.Image")));
			this.m_PlaceTcOnPictureBox.Location = new System.Drawing.Point(160, 24);
			this.m_PlaceTcOnPictureBox.Name = "m_PlaceTcOnPictureBox";
			this.m_PlaceTcOnPictureBox.Size = new System.Drawing.Size(66, 72);
			this.m_PlaceTcOnPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
			this.m_PlaceTcOnPictureBox.TabIndex = 3;
			this.m_PlaceTcOnPictureBox.TabStop = false;
			// 
			// m_PlaceBlOffPictureBox
			// 
			this.m_PlaceBlOffPictureBox.Cursor = System.Windows.Forms.Cursors.Default;
			this.m_PlaceBlOffPictureBox.Image = ((System.Drawing.Image)(resources.GetObject("m_PlaceBlOffPictureBox.Image")));
			this.m_PlaceBlOffPictureBox.Location = new System.Drawing.Point(84, 24);
			this.m_PlaceBlOffPictureBox.Name = "m_PlaceBlOffPictureBox";
			this.m_PlaceBlOffPictureBox.Size = new System.Drawing.Size(66, 72);
			this.m_PlaceBlOffPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
			this.m_PlaceBlOffPictureBox.TabIndex = 5;
			this.m_PlaceBlOffPictureBox.TabStop = false;
			// 
			// m_PlaceBcOffPictureBox
			// 
			this.m_PlaceBcOffPictureBox.Cursor = System.Windows.Forms.Cursors.Default;
			this.m_PlaceBcOffPictureBox.Image = ((System.Drawing.Image)(resources.GetObject("m_PlaceBcOffPictureBox.Image")));
			this.m_PlaceBcOffPictureBox.Location = new System.Drawing.Point(236, 24);
			this.m_PlaceBcOffPictureBox.Name = "m_PlaceBcOffPictureBox";
			this.m_PlaceBcOffPictureBox.Size = new System.Drawing.Size(66, 72);
			this.m_PlaceBcOffPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
			this.m_PlaceBcOffPictureBox.TabIndex = 4;
			this.m_PlaceBcOffPictureBox.TabStop = false;
			// 
			// m_PlaceBrOffPictureBox
			// 
			this.m_PlaceBrOffPictureBox.Cursor = System.Windows.Forms.Cursors.Default;
			this.m_PlaceBrOffPictureBox.Image = ((System.Drawing.Image)(resources.GetObject("m_PlaceBrOffPictureBox.Image")));
			this.m_PlaceBrOffPictureBox.Location = new System.Drawing.Point(388, 24);
			this.m_PlaceBrOffPictureBox.Name = "m_PlaceBrOffPictureBox";
			this.m_PlaceBrOffPictureBox.Size = new System.Drawing.Size(66, 72);
			this.m_PlaceBrOffPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
			this.m_PlaceBrOffPictureBox.TabIndex = 7;
			this.m_PlaceBrOffPictureBox.TabStop = false;
			// 
			// m_CancelButton
			// 
			this.m_CancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.m_CancelButton.Location = new System.Drawing.Point(372, 400);
			this.m_CancelButton.Name = "m_CancelButton";
			this.m_CancelButton.Size = new System.Drawing.Size(100, 25);
			this.m_CancelButton.TabIndex = 7;
			this.m_CancelButton.Text = "Cancel";
			this.m_CancelButton.Click += new System.EventHandler(this.m_CancelButton_Click);
			// 
			// m_PlaceToolTip
			// 
			this.m_PlaceToolTip.AutomaticDelay = 1000;
			this.m_PlaceToolTip.AutoPopDelay = 10000;
			this.m_PlaceToolTip.InitialDelay = 100;
			this.m_PlaceToolTip.ReshowDelay = 100;
			// 
			// m_SummaryTextBox
			// 
			this.m_SummaryTextBox.FocusColor = System.Drawing.SystemColors.WindowText;
			this.m_SummaryTextBox.FocusText = "";
			this.m_SummaryTextBox.ForeColor = System.Drawing.SystemColors.GrayText;
			this.m_SummaryTextBox.HelpText = "Type or paste the content of your post here...";
			this.m_SummaryTextBox.Location = new System.Drawing.Point(8, 104);
			this.m_SummaryTextBox.MaxLength = 0;
			this.m_SummaryTextBox.Multiline = true;
			this.m_SummaryTextBox.Name = "m_SummaryTextBox";
			this.m_SummaryTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.m_SummaryTextBox.Size = new System.Drawing.Size(464, 176);
			this.m_SummaryTextBox.TabIndex = 5;
			this.m_SummaryTextBox.Text = "Type or paste the content of your post here...";
			// 
			// m_ExpireGroupBox
			// 
			this.m_ExpireGroupBox.Controls.Add(this.m_ExpireCheckBox);
			this.m_ExpireGroupBox.Location = new System.Drawing.Point(8, 394);
			this.m_ExpireGroupBox.Name = "m_ExpireGroupBox";
			this.m_ExpireGroupBox.Size = new System.Drawing.Size(240, 31);
			this.m_ExpireGroupBox.TabIndex = 20;
			this.m_ExpireGroupBox.TabStop = false;
			this.m_ExpireGroupBox.Visible = false;
			// 
			// m_ExpireCheckBox
			// 
			this.m_ExpireCheckBox.Checked = true;
			this.m_ExpireCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
			this.m_ExpireCheckBox.Location = new System.Drawing.Point(12, 9);
			this.m_ExpireCheckBox.Name = "m_ExpireCheckBox";
			this.m_ExpireCheckBox.Size = new System.Drawing.Size(218, 20);
			this.m_ExpireCheckBox.TabIndex = 8;
			this.m_ExpireCheckBox.Text = "Enable calendar archiving for this post";
			this.m_ExpireCheckBox.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			// 
			// m_ExpireToolTip
			// 
			this.m_ExpireToolTip.AutoPopDelay = 5000;
			this.m_ExpireToolTip.InitialDelay = 500;
			this.m_ExpireToolTip.ReshowDelay = 100;
			// 
			// m_TagsTextBox
			// 
			this.m_TagsTextBox.FocusColor = System.Drawing.SystemColors.WindowText;
			this.m_TagsTextBox.FocusText = "";
			this.m_TagsTextBox.ForeColor = System.Drawing.SystemColors.GrayText;
			this.m_TagsTextBox.HelpText = "Add space separated tags (optional)";
			this.m_TagsTextBox.Location = new System.Drawing.Point(49, 80);
			this.m_TagsTextBox.MaxLength = 10000;
			this.m_TagsTextBox.Name = "m_TagsTextBox";
			this.m_TagsTextBox.Size = new System.Drawing.Size(423, 21);
			this.m_TagsTextBox.TabIndex = 4;
			this.m_TagsTextBox.Text = "Add space separated tags (optional)";
			// 
			// m_TagsLabel
			// 
			this.m_TagsLabel.Location = new System.Drawing.Point(9, 80);
			this.m_TagsLabel.Name = "m_TagsLabel";
			this.m_TagsLabel.Size = new System.Drawing.Size(48, 21);
			this.m_TagsLabel.TabIndex = 21;
			this.m_TagsLabel.Text = "Tags:";
			this.m_TagsLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// PostEditor
			// 
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 14);
			this.CancelButton = this.m_CancelButton;
			this.ClientSize = new System.Drawing.Size(480, 431);
			this.Controls.Add(this.m_TagsTextBox);
			this.Controls.Add(this.m_TagsLabel);
			this.Controls.Add(this.m_ExpireGroupBox);
			this.Controls.Add(this.m_SummaryTextBox);
			this.Controls.Add(this.m_LinkTextBox);
			this.Controls.Add(this.m_TitleTextBox);
			this.Controls.Add(this.m_LinkLabel);
			this.Controls.Add(this.m_TitleLabel);
			this.Controls.Add(this.m_CancelButton);
			this.Controls.Add(this.m_PlacementGroupBox);
			this.Controls.Add(this.m_PublishButton);
			this.Controls.Add(this.m_HiveLabel);
			this.Controls.Add(this.m_HiveComboBox);
			this.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.MaximizeBox = false;
			this.Name = "PostEditor";
			this.Padding = new System.Windows.Forms.Padding(2);
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "New Post - Buzm";
			this.Closing += new System.ComponentModel.CancelEventHandler(this.PostEditor_Closing);
			this.Shown += new System.EventHandler(this.PostEditor_Shown);
			this.m_PlacementGroupBox.ResumeLayout(false);
			this.m_PlacementGroupBox.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.m_PlaceTrOffPictureBox)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.m_PlaceTlOffPictureBox)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.m_PlaceTcOffPictureBox)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.m_PlaceTlOnPictureBox)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.m_PlaceTrOnPictureBox)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.m_PlaceTcOnPictureBox)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.m_PlaceBlOffPictureBox)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.m_PlaceBcOffPictureBox)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.m_PlaceBrOffPictureBox)).EndInit();
			this.m_ExpireGroupBox.ResumeLayout(false);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

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
