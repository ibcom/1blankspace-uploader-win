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
using System.Threading;
using System.Xml;
using System.Xml.XPath;
using System.Collections.Specialized;
using HttpLibrary;
using System.Reflection;

namespace mydigitalspace
{
    public partial class frmUpload : Form
    {
        DataTable dt = new DataTable();
        string _sid = string.Empty;
        string _cookieHeader = string.Empty;
        bool _cancelled = false;
        bool _complete = false;
        Thread th;
        HttpConnection http;
        string selectedSiteID;

        private class Site
        {
            public string id { get; set; }
            public string title { get; set; }
        }

        public frmUpload()
        {
            InitializeComponent();
            dt.Columns.Add("FileName");
            dt.Columns.Add("Status");
            dt.Columns.Add("FullPath");
        }

        public frmUpload(string sid, string cookieHeader)
        {
            InitializeComponent();

            //get available sites
            string requestURI = "https://secure.mydigitalspacelive.com/directory/ondemand/setup.asp?method=SETUP_SITE_SEARCH&rf=XML&sid=" + sid;
            string HTML = HTMLHelper.ReadHTMLPage(requestURI);
            List<Site> Sites = ParseXML(HTML, "row");

            dt.Columns.Add("FileName");
            dt.Columns.Add("Status");
            dt.Columns.Add("FullPath");
            _sid = sid;
            _cookieHeader = cookieHeader;

            cbSite.DataSource = Sites;
            cbSite.DisplayMember = "title";
            cbSite.ValueMember = "id";
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            folderBrowserDialog1.ShowDialog();
            tbFolder.Text = folderBrowserDialog1.SelectedPath;
            dt.Rows.Clear();

            try
            {
                DirectoryInfo di = new DirectoryInfo(tbFolder.Text.Trim());
                FileInfo[] fis = di.GetFiles();
                foreach (FileInfo fi in fis)
                {
                    dt.Rows.Add(fi.Name, "Waiting...", fi.FullName);
                }
                dataGridView1.DataSource = dt;
                FormatGrid();
            }
            catch
            {

            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            try
            {
                http.Dispose();
                http = null;
            }
            catch (Exception ex)
            {
            }
            finally
            {
                this.Close();
                Application.Exit();
            }
        }

        private void btnUpload_Click(object sender, EventArgs e)
        {
            if (btnUpload.Text.Equals("Upload"))
            {
                //begin upload process
                _cancelled = false;
                _complete = false;
                btnClose.Enabled = false;
                btnBrowse.Enabled = false;
                btnUpload.Text = "Cancel";
                this.Refresh();
                timer1.Enabled = true;
                timer1.Start();

                th = new Thread(new ThreadStart(ProcessUpload));
                th.IsBackground = false;
                th.Start();
            }
            else
            {
                //stop upload process
                _cancelled = true;
                btnClose.Enabled = true;
                btnBrowse.Enabled = true;
                btnUpload.Text = "Upload";
            }
        }

        private void FormatGrid()
        {
            dataGridView1.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dataGridView1.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            dataGridView1.Columns[2].Visible = false;
            dataGridView1.Refresh();
        }

        private bool UploadFile(string FileName, string LinkID)
        {
            NameValueCollection querystring = new NameValueCollection();
            querystring["sid"] = _sid;
            querystring.Add("maxfiles", "1");
            querystring.Add("linktype", "40");
            querystring.Add("linkid", LinkID);
            querystring.Add("filename0", Path.GetFileName(FileName));

            http = new HttpWebRequestConnection();

            string requestURI = "https://secure.mydigitalspacelive.com/directory/ondemand/attach.asp";

            var postBody = new HttpPostBodyBuilder.Multipart();
            foreach (string key in querystring.Keys)
            {
                postBody.AddParameter(key, querystring.Get(key));
            }
            
            var fileStream = new FileStream(FileName, FileMode.Open, FileAccess.Read);

            postBody.AddData(
            "oFile0",
            fileStream,
            Path.GetFileName(FileName),
            HTMLHelper.GetMimeType(FileName)
                );

            var bodyStream = postBody.PrepareData();
            bodyStream.Position = 0;
            var req = new HttpMessage.Request(bodyStream, "POST");
            req.ContentLength = bodyStream.Length;
            req.ContentType = postBody.GetContentType();
            req.Headers["Referer"] = requestURI;

            var response = http.Send(requestURI, req);
            if (response.Status == HttpStatusCode.OK)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private static List<Site> ParseXML(string xmlContent, string XName)
        {
            return ParseXML(xmlContent, XName, "");
        }

        private static List<Site> ParseXML(string xmlContent, string XName, string Element)
        {
            List<Site> List = new List<Site>();

            try
            {
                if (xmlContent != string.Empty)
                {
                    XDocument xml = XDocument.Parse(xmlContent);

                    var items = from x in xml.Descendants(XName)
                                  select new
                                  {
                                      id = x.Descendants("id").First().Value,
                                      title = x.Descendants("title").First().Value
                                  };
                    foreach(var item in items)
                    {
                        Site s = new Site();
                        s.id = item.id;
                        s.title = item.title;
                        List.Add(s);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Write(ex.Message);
            }

            return List;
        }

        private void ProcessUpload()
        {
            //process upload
            foreach (DataGridViewRow dgvr in dataGridView1.Rows)
            {
                if (_cancelled) //break out of loop
                {
                    break;
                }
                if (!dgvr.Cells[1].Value.Equals("Uploaded"))
                {
                    //dgvr.Cells[1].Value = "Uploading...";
                    SetControlPropertyThreadSafe(dataGridView1, "", "Uploading...", dgvr.Index);
                    dgvr.Selected = true;

                    if (UploadFile(dgvr.Cells[2].Value.ToString(), selectedSiteID))
                    {
                        SetControlPropertyThreadSafe(dataGridView1, "", "Uploaded", dgvr.Index);
                    }
                    else
                    {
                        SetControlPropertyThreadSafe(dataGridView1, "", "Failed", dgvr.Index);
                    }

                    Thread.Sleep(1000); //pause thread to prevent overload, this value may need to be tweaked
                    try
                    {
                        http.Dispose();
                    }
                    catch
                    {
                    }
                }
            }
            if (_cancelled == false)
            {
                _complete = true;
            }
        }

        private delegate void SetControlPropertyThreadSafeDelegate(Control control, string propertyName, object propertyValue, int Index);

        public static void SetControlPropertyThreadSafe(Control control, string propertyName, object propertyValue, int Index)
        {
            if (control.InvokeRequired)
            {
                control.Invoke(new SetControlPropertyThreadSafeDelegate(SetControlPropertyThreadSafe), new object[] { control, propertyName, propertyValue, Index });
            }
            else
            {
                DataGridView gridView = control as DataGridView;
                gridView.Rows[Index].Cells[1].Value = propertyValue;
            }
        }

        private void cbSite_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                selectedSiteID = (cbSite.SelectedValue as Site).id;
            }
            catch
            {
                selectedSiteID = cbSite.SelectedValue.ToString();
            }
        }

        private void frmUpload_FormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                http.Dispose();
                http = null;
            }
            catch
            {
            }
            finally
            {
                Application.Exit();
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (_complete)
            {
                timer1.Stop();
                timer1.Enabled = false;

                //MessageBox.Show("Complete");
                btnClose.Enabled = true;
                btnBrowse.Enabled = true;
                btnUpload.Text = "Upload";
            }
        }
    }
}
