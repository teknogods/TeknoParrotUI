namespace TeknoParrotLobbies
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.lobbyListView = new System.Windows.Forms.ListView();
            this.createLobbyBtn = new System.Windows.Forms.Button();
            this.refreshLobbiesBtn = new System.Windows.Forms.Button();
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.joinLobbyBtn = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lobbyListView
            // 
            this.lobbyListView.Location = new System.Drawing.Point(12, 42);
            this.lobbyListView.MultiSelect = false;
            this.lobbyListView.Name = "lobbyListView";
            this.lobbyListView.Size = new System.Drawing.Size(760, 368);
            this.lobbyListView.TabIndex = 0;
            this.lobbyListView.UseCompatibleStateImageBehavior = false;
            this.lobbyListView.View = System.Windows.Forms.View.Details;
            this.lobbyListView.DoubleClick += new System.EventHandler(this.handleListViewClick);
            // 
            // createLobbyBtn
            // 
            this.createLobbyBtn.Location = new System.Drawing.Point(13, 13);
            this.createLobbyBtn.Name = "createLobbyBtn";
            this.createLobbyBtn.Size = new System.Drawing.Size(75, 23);
            this.createLobbyBtn.TabIndex = 1;
            this.createLobbyBtn.Text = "Create";
            this.createLobbyBtn.UseVisualStyleBackColor = true;
            this.createLobbyBtn.Click += new System.EventHandler(this.createLobbyBtn_Click);
            // 
            // refreshLobbiesBtn
            // 
            this.refreshLobbiesBtn.Location = new System.Drawing.Point(697, 13);
            this.refreshLobbiesBtn.Name = "refreshLobbiesBtn";
            this.refreshLobbiesBtn.Size = new System.Drawing.Size(75, 23);
            this.refreshLobbiesBtn.TabIndex = 2;
            this.refreshLobbiesBtn.Text = "Refresh";
            this.refreshLobbiesBtn.UseVisualStyleBackColor = true;
            this.refreshLobbiesBtn.Click += new System.EventHandler(this.refreshLobbiesBtn_Click);
            // 
            // comboBox1
            // 
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Location = new System.Drawing.Point(570, 13);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(121, 21);
            this.comboBox1.TabIndex = 3;
            // 
            // joinLobbyBtn
            // 
            this.joinLobbyBtn.Location = new System.Drawing.Point(95, 13);
            this.joinLobbyBtn.Name = "joinLobbyBtn";
            this.joinLobbyBtn.Size = new System.Drawing.Size(75, 23);
            this.joinLobbyBtn.TabIndex = 4;
            this.joinLobbyBtn.Text = "Join";
            this.joinLobbyBtn.UseVisualStyleBackColor = true;
            this.joinLobbyBtn.Click += new System.EventHandler(this.joinLobbyBtn_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 422);
            this.Controls.Add(this.joinLobbyBtn);
            this.Controls.Add(this.comboBox1);
            this.Controls.Add(this.refreshLobbiesBtn);
            this.Controls.Add(this.createLobbyBtn);
            this.Controls.Add(this.lobbyListView);
            this.Name = "Form1";
            this.Text = "TeknoParrot Lobbies";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Form1_FormClosed);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListView lobbyListView;
        private System.Windows.Forms.Button createLobbyBtn;
        private System.Windows.Forms.Button refreshLobbiesBtn;
        private System.Windows.Forms.ComboBox comboBox1;
        private System.Windows.Forms.Button joinLobbyBtn;
    }
}

