using System;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Timers;
using System.IO;
using System.Net;

namespace CanoeService
{
    public partial class CanoeService : ServiceBase
    {
        Timer timer = new Timer();
        public CanoeService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            WriteToFile($"CanoeService started at {DateTime.Now}");
            timer.Elapsed += new ElapsedEventHandler(OnElapsedTime);
            timer.Interval = (60 * 1000); // number in ms
            timer.Enabled = true;
            _ = StartCanoe();
        }

        protected override void OnStop()
        {
            WriteToFile($"CanoeService stopped at {DateTime.Now}");
        }

        public void WriteToFile(string Message)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\Logs";
            if (!Directory.Exists(path)) { Directory.CreateDirectory(path); }

            string filepath = AppDomain.CurrentDomain.BaseDirectory + "\\Logs\\ServiceLog_" + DateTime.Now.ToShortDateString().Replace('/', '_') + ".txt";
            if (!File.Exists(filepath))
            {
                using (StreamWriter sw = File.CreateText(filepath))
                {
                    sw.WriteLine(Message);
                }
            }
            else
            {
                using (StreamWriter sw = File.AppendText(filepath))
                {
                    sw.WriteLine(Message);
                }
            }
        }
        private void OnElapsedTime(object source, ElapsedEventArgs e)
        {
            WriteToFile($"CanoeService is running at {DateTime.Now}");
        }

        private async Task StartCanoe()
        {
            // Set up an HTTP listener to accept connections on all available network interfaces
            var listener = new HttpListener();
            
            // listener.Prefixes.Add("http://*:80/");
            // listener.Prefixes.Add("http://*:8080/");
            listener.Prefixes.Add("https://*:443/");
            listener.Prefixes.Add("https://*:8443/");

            // Configure HTTP Basic Auth
            // listener.AuthenticationSchemes = AuthenticationSchemes.Basic;

            try
            {
                listener.Start();
                WriteToFile($"HttpListener() listener started at {DateTime.Now}");
            }
            catch (Exception ex) { WriteToFile(ex.ToString()); }
            while (true)
            {
                HttpListenerContext context = await listener.GetContextAsync();
                _ = ProcessWebSocketRequest(context);
            }
        }
        private async Task ProcessWebSocketRequest(HttpListenerContext context)
        {
            WriteToFile($"Creating new Canoe() at {DateTime.Now}");
            var canoe = new Canoe();
            await canoe.StartAsync(context);
        }
    }
}
