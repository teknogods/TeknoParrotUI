using CefSharp;
using CefSharp.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MessageBox = System.Windows.MessageBox;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for UserLogin.xaml
    /// </summary>
    public partial class UserLogin
    {
        public bool IsActive = false;
        public UserLogin()
        {
            InitializeComponent();
            Thread t2 = new Thread(delegate ()
            {
                while (true)
                {
                    Thread.Sleep(1000);
                    if (IsActive)
                    {
                        Browser.GetSourceAsync().ContinueWith(taskHtml =>
                        {
                            var html = taskHtml.Result;
                            // Make proper checks so this cannot be just pasted in chat lol...
                            if (html.Contains("START GAME: "))
                            {
                                for (int i = 0; i < html.Length-40; i++)
                                {
                                    var str = html.Substring(i, 40);
                                    if (str == "<label id=\"GameLaunchLabel\">START GAME: ")
                                    {
                                        for (int x = 40; x < html.Length; x++)
                                        {
                                            var str2 = html.Substring(i + x, 8);
                                            if (str2 == "</label>")
                                            {
                                                var data = html.Substring(i + 40, x);
                                                var datas = data.Split("|".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                                                Browser.GetBrowser().ExecuteScriptAsync("document.getElementById(\"GameLaunchLabel\").innerHTML = \"Game Started!\";");
                                                MessageBox.Show("Wants to start a game pls: " + datas[0] + " " + datas[1] + " " + datas[2] + " " + datas[3]);
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        });
                    }
                }
            });
            t2.Start();
        }

        public void RefreshBrowserToStart()
        {
            Browser.Address = "https://localhost:44339/Home/Chat";
        }

        private void UserLogin_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                RefreshBrowserToStart();
                IsActive = true;
            }
            else
            {
                IsActive = false;
            }
        }
    }
}
