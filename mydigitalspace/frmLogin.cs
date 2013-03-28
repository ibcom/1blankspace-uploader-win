using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Xml.Linq;

namespace mydigitalspace
{
    public partial class frmLogin : Form
    {
        string _cookieHeader = string.Empty;

        public frmLogin()
        {
            InitializeComponent();
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            string result = ProcessLogin(tbUserName.Text.Trim(), tbPassword.Text.Trim());
            if (!result.Equals("Failed"))
            {
                Form frm = new frmUpload(result, _cookieHeader);
                frm.Show();
                this.Hide();
            }
            else
            {
                lblErrorMessage.Visible = true;
            }
            Cursor = Cursors.Arrow;
        }

        private string ProcessLogin(string username, string password)
        {
            string requestURI = "https://secure.mydigitalspacelive.com/directory/ondemand/logon.asp?logon=" + username + "&password=" + password;
            string HTML = HTMLHelper.ReadHTMLPage(requestURI, out _cookieHeader);
            if (HTML.Contains("LOGONFAILED"))
            {
                return "Failed";
            }
            else if (HTML.Contains("PASSWORDOK"))
            {
                string[] results = HTML.Split('|');
                return results[2];
            }
            else
            {
                return "Failed";
            }
        }

        private static List<string> ParseXML(string xmlContent, string XName)
        {
            return ParseXML(xmlContent, XName, "");
        }

        private static List<string> ParseXML(string xmlContent, string XName, string Element)
        {
            List<string> List = new List<string>();

            try
            {
                if (xmlContent != string.Empty)
                {
                    XDocument xml = XDocument.Parse(xmlContent);

                    var items = xml.Descendants(XName);
                    foreach (var item in items)
                    {
                        if (Element != string.Empty)
                        {
                            List.Add(item.Element(Element).Value);
                        }
                        else
                        {
                            List.Add(item.Value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Write(ex.Message);
            }

            return List;
        }

        private void tbUserName_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
