using System;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;


#pragma warning disable IDE1006
namespace pWhois
{
    public partial class Form1 : Form
    {
        private const string FILENAME = "pWhois.dat";
        private List<IPInfo.IPInfo> infoList = null;
        private IPInfo.IPInfo ipinfo;
        public Form1()
        {
            InitializeComponent();
        }

        private void BtnExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private bool CheckIPSyntax(string ipText)
        {
            if (String.IsNullOrEmpty(ipText)) return false;
            if (ipText.Equals("ERROR")) return false;
            // ниже неправильный regex: не понимает 8.8.8.8
            Regex regexIP = new Regex(@"^([01]?[0-9]?[0-9]|2[0-4][0-9]|25[0-5])\.([01]?[0-9]?[0-9]|2[0-4][0-9]|25[0-5])\.([01]?[0-9]?[0-9]|2[0-4][0-9]|25[0-5])\.([01]?[0-9]?[0-9]|2[0-4][0-9]|25[0-5])$");
            if(regexIP.IsMatch(ipText))
            {
                label2.Text = "";
                return true;
            } else
            {
                label2.Text = "Неверный адрес IP";
                return false;
            }
        }

        private void BtnShow_Click(object sender, EventArgs e)
        {
            string ipAddr = textIP.Text.Trim();
            // Если это не адрес IP, то это доменное имя; разрешаем его в IP
            if (!CheckIPSyntax(ipAddr))
            {
                label2.Text = "Разрешение имени...";
                try
                {
                    // GetHostEntry() выдает ошибку, если указан не домен, а адрес IP. Плюс тормозит. Так что Resolve() лучше.
                    CheckHostSyntax(ref ipAddr);
#pragma warning disable CS0618 // Тип или член устарел
                    // ниже надо бы сделать асинхронно...
                    textIP.Text = ipAddr = Dns.Resolve(ipAddr).AddressList[0].ToString();
#pragma warning restore CS0618 // Тип или член устарел
                }
                catch
                {
                    ipAddr = textIP.Text;
                    textIP.Text = "ERROR";
                }
            }
            label2.Text = "";
            // проверяем конечный полученный адрес IP. Если он неверный - выходим.
            if (!CheckIPSyntax(ipAddr))
            {
                BtnMap.Enabled = false;
                return;
            }
            
            // загружаем информацию по адресу IP
            this.Cursor = Cursors.WaitCursor;
            // сначала получаем info из файла (кэш). Если успешно, то выводим инфо и выходим. 
            GetInfoFromFile(textIP.Text);
            if(ipinfo!=null)
            {
                testResult.BackColor = Color.Gainsboro;
                testResult.ForeColor = Color.Black;
                ShowResult(ipAddr);
                return;
            }
            // Если в кеше инфы нет, то получаем по URL. ShowInfo() вызывается из GetInfoFromURL()
            GetInfoFromURL(ipAddr);
            this.Cursor = Cursors.Default;
        }

        private void textIP_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (CheckIPSyntax(textIP.Text))
            {
                textIP.ForeColor = Color.Black;
            }
            else
            {
                textIP.ForeColor = Color.Blue;
            }
            testResult.Text = "";
        }

   
        private void BtnMap_Click(object sender, EventArgs e)
        {
            if (ipinfo != null)
            {
                StringBuilder url = new StringBuilder();
                url.Append("https://www.google.com/maps/@?api=1&map_action=map");
                url.Append("&center=");
                url.Append(ipinfo.latitude);
                url.Append(",");
                url.Append(ipinfo.longitude);
                url.Append("&zoom=9");
                Process.Start(url.ToString());
            }
        }

        private void GetInfoFromFile(string ipAddr)
        {
            ipinfo = null;
            foreach (IPInfo.IPInfo i in infoList)
            {
                if (i.ip == ipAddr)
                {
                    i.success = true;
                    ipinfo = i;
                }
            }
        }

        private async void GetInfoFromURL(string ipAddr, int timeout=10000)
        {
            ipinfo = null;
            bool cancelled = false;
            string jsonStr = null;
            using (WebClient wc = new WebClient())
            {
                wc.DownloadStringCompleted += (s, e) =>
                {
                    if (e.Error != null || e.Cancelled)
                    {
                        cancelled = true;
                        jsonStr = null;
                        ipinfo = null;
                        textIP.Text = "ERROR";
                    }
                    else
                    {
                        jsonStr = e.Result;
                        ipinfo = JsonSerializer.Deserialize<IPInfo.IPInfo>(jsonStr);
                        SaveInfoToFile(ipinfo);
                    }
                    ShowResult(ipAddr);
                };
                Uri uri = new Uri($"http://free.ipwhois.io/json/{ipAddr}");
                wc.DownloadStringAsync(uri); //
                // ожидаем ответа
                var currentTime = DateTime.Now;
                while (jsonStr == null && !cancelled && DateTime.Now.Subtract(currentTime).TotalMilliseconds < timeout)
                    await Task.Delay(100);
            }
        }

        private void SaveInfoToFile(IPInfo.IPInfo info = null)
        {
            if (info != null)
                infoList.Add(info);
            string serializedText = JsonSerializer.Serialize(infoList);
            File.WriteAllText(FILENAME, serializedText);
        }

        private void LoadIpList()
        {

            try
            {
                infoList = JsonSerializer.Deserialize<List<IPInfo.IPInfo>>(File.ReadAllText(FILENAME));
            } catch
            {
                infoList = new List<IPInfo.IPInfo>();
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            LoadIpList();
            label2.Text = "";
        }

        void CheckHostSyntax(ref string hostname)
        {
            if (String.IsNullOrEmpty(hostname))
            {
                label2.Text = "";
                throw new Exception("Неверное имя");
            }
            if (hostname.Equals("ERROR")) return;
            Match match = Regex.Match(hostname, @"^(?:\w+://)?([^/?]*)");
            if(match==null || match.Groups.Count==0)
            {
                label2.Text = "Неверное имя";
                throw new Exception("Неверное имя");
            } else
            {
                hostname = match.Groups[1].Value;
                label2.Text = hostname;
            }
        }

        private void ShowResult(string hostname=null)
        {
            if (hostname != null)
                testResult.Text = "Имя: " + hostname + Environment.NewLine;
            else testResult.Text = "";
            if (ipinfo != null)
            {
                label2.Text = "";
                testResult.Text += ipinfo.ToString();
            } else
            {
                label2.Text = "Ошибка";
                testResult.Text = "";
            }
            this.Cursor = Cursors.Default;
        }
    }
}
