using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace HandOfGod.Gestures
{
    public sealed class GestureUdpReceiver : MonoBehaviour
    {
        [SerializeField] private int port = 5005;
        [SerializeField] private float timeoutSeconds = 0.35f;

        private readonly object gate = new object();
        private GestureFrame latest = GestureFrame.Neutral;
        private Thread thread;
        private UdpClient client;
        private volatile bool running;
        private volatile long lastReceivedTicks;

        public bool HasFreshFrame
        {
            get
            {
                if (lastReceivedTicks == 0L)
                {
                    return false;
                }

                var age = (DateTime.UtcNow.Ticks - lastReceivedTicks) / (float)TimeSpan.TicksPerSecond;
                return age <= timeoutSeconds;
            }
        }

        public GestureFrame Latest
        {
            get
            {
                lock (gate)
                {
                    return latest;
                }
            }
        }

        private void OnEnable()
        {
            running = true;
            thread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "Gesture UDP Receiver",
            };
            thread.Start();
        }

        private void OnDisable()
        {
            running = false;
            client?.Close();
            client = null;
            if (thread != null && thread.IsAlive)
            {
                thread.Join(150);
            }
            thread = null;
        }

        private void ReceiveLoop()
        {
            try
            {
                client = new UdpClient(port);
                var endpoint = new IPEndPoint(IPAddress.Any, port);
                while (running)
                {
                    var bytes = client.Receive(ref endpoint);
                    var json = Encoding.UTF8.GetString(bytes);
                    var frame = GestureFrameUtility.Clamp(JsonUtility.FromJson<GestureFrame>(json));
                    lock (gate)
                    {
                        latest = frame;
                    }
                    lastReceivedTicks = DateTime.UtcNow.Ticks;
                }
            }
            catch (SocketException)
            {
                // Socket closes during shutdown. Keep this quiet so play mode exits cleanly.
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Gesture UDP receiver stopped: {exception.Message}");
            }
        }
    }
}
