using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace dz09_07.Server
{
    public partial class MainWindow : Window
    {
        private const int Port = 8888;
        private UdpClient udpServer;
        private readonly List<IPEndPoint> connectedClients = new List<IPEndPoint>();
        private readonly Dictionary<IPEndPoint, int> clientRequestCount = new Dictionary<IPEndPoint, int>();
        private readonly Dictionary<IPEndPoint, DateTime> lastActivityTime = new Dictionary<IPEndPoint, DateTime>();
        private readonly object clientLock = new object();
        private readonly TimeSpan inactiveTimeout = TimeSpan.FromMinutes(10);

        public MainWindow()
        {
            InitializeComponent();
            StartServer();
        }

        private void StartServer()
        {
            InitializeServer();
            Log("Server started. Listening on port " + Port);

            StartBackgroundTasks();
        }

        private void InitializeServer()
        {
            udpServer = new UdpClient(new IPEndPoint(IPAddress.Parse("10.0.0.139"), Port));
        }

        private void StartBackgroundTasks()
        {
            Task.Run(() => ReceiveMessages());
            Task.Run(() => CheckInactiveClients());
        }

        private async Task ReceiveMessages()
        {
            while (true)
            {
                try
                {
                    var (clientEndPoint, message) = await ReceiveMessageAsync();

                    HandleNewConnection(clientEndPoint);
                    await SendResponseAsync(clientEndPoint, message);

                    TrackClientRequest(clientEndPoint);
                }
                catch (Exception ex)
                {
                    Log($"Error: {ex.Message}");
                }
            }
        }

        private async Task<(IPEndPoint, string)> ReceiveMessageAsync()
        {
            UdpReceiveResult result = await udpServer.ReceiveAsync();
            IPEndPoint clientEndPoint = result.RemoteEndPoint;
            string message = Encoding.ASCII.GetString(result.Buffer);
            return (clientEndPoint, message);
        }

        private void HandleNewConnection(IPEndPoint clientEndPoint)
        {
            if (!connectedClients.Contains(clientEndPoint))
            {
                connectedClients.Add(clientEndPoint);
                Log($"Client connected: {clientEndPoint}");
            }
        }

        private async Task SendResponseAsync(IPEndPoint clientEndPoint, string message)
        {
            string response = ProcessRequest(message);
            byte[] responseData = Encoding.ASCII.GetBytes(response);
            await udpServer.SendAsync(responseData, responseData.Length, clientEndPoint);

            Log($"Response sent to {clientEndPoint}: {response}");
        }

        private string ProcessRequest(string request)
        {
            return $"Server received: {request}";
        }

        private void TrackClientRequest(IPEndPoint clientEndPoint)
        {
            lock (clientLock)
            {
                if (clientRequestCount.ContainsKey(clientEndPoint))
                {
                    clientRequestCount[clientEndPoint]++;
                }
                else
                {
                    clientRequestCount[clientEndPoint] = 1;
                }

                lastActivityTime[clientEndPoint] = DateTime.Now;
            }
        }

        private async Task CheckInactiveClients()
        {
            while (true)
            {
                await Task.Delay(1000);
                RemoveInactiveClients();
            }
        }

        private void RemoveInactiveClients()
        {
            lock (clientLock)
            {
                List<IPEndPoint> disconnectedClients = GetInactiveClients();
                foreach (var clientEndPoint in disconnectedClients)
                {
                    DisconnectClient(clientEndPoint);
                }
            }
        }

        private List<IPEndPoint> GetInactiveClients()
        {
            List<IPEndPoint> inactiveClients = new List<IPEndPoint>();
            foreach (var clientEndPoint in lastActivityTime.Keys)
            {
                if (IsClientInactive(clientEndPoint))
                {
                    inactiveClients.Add(clientEndPoint);
                }
            }
            return inactiveClients;
        }

        private bool IsClientInactive(IPEndPoint clientEndPoint)
        {
            return (DateTime.Now - lastActivityTime[clientEndPoint]) > inactiveTimeout;
        }

        private void DisconnectClient(IPEndPoint clientEndPoint)
        {
            if (connectedClients.Contains(clientEndPoint))
            {
                connectedClients.Remove(clientEndPoint);
                Log($"Client disconnected due to inactivity: {clientEndPoint}");

                clientRequestCount.Remove(clientEndPoint);
                lastActivityTime.Remove(clientEndPoint);
            }
        }

        private void Log(string message)
        {
            Dispatcher.Invoke(() =>
            {
                txtLog.AppendText($"{DateTime.Now}: {message}\n");
            });
        }
    }
}
