using LobbyLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TeknoParrotLobbies
{
    public partial class CreateLobbyForm : Form
    {
        public CreateLobbyForm()
        {
            InitializeComponent();
        }

        public class ComboItem
        {
            public GameId ID { get; set; }
            public string Text { get; set; }
        }

        private void CreateLobbyForm_Load(object sender, EventArgs e)
        {
            comboBox1.DropDownStyle = ComboBoxStyle.DropDown;
            comboBox1.DisplayMember = "Text";
            comboBox1.ValueMember = "ID";
            comboBox1.DataSource = new ComboItem[]
            {
                new ComboItem { ID = GameId.ID6, Text = "Initial D 6 AA" },
                new ComboItem { ID = GameId.ID7, Text = "Initial D 7 AA X" },
                new ComboItem { ID = GameId.MKDX, Text = "Mario Kart DX" }
            };
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Form1.lobbyName = textBox1.Text;
            Form1.lobbyGame = (GameId)comboBox1.SelectedValue;
            Form1.createLobby = true;
            this.Close();
        }
    }
}
