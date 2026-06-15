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
        private long lastReceivedTicks;
        private double lastFrameTimestamp;

        public bool HasFreshFrame
        {
            get
            {
                var receivedTicks = Interlocked.Read(ref lastReceivedTicks);
                if (receivedTicks == 0L)
                {
                    return false;
                }

                var age = (DateTime.UtcNow.Ticks - receivedTicks) / (float)TimeSpan.TicksPerSecond;
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

        public float ReceiveAgeSeconds
        {
            get
            {
                var receivedTicks = Interlocked.Read(ref lastReceivedTicks);
                if (receivedTicks == 0L)
                {
                    return -1f;
                }

                return (DateTime.UtcNow.Ticks - receivedTicks) / (float)TimeSpan.TicksPerSecond;
            }
        }

        public float FrameAgeSeconds
        {
            get
            {
                GestureFrame frame;
                lock (gate)
                {
                    frame = latest;
                }

                if (frame.timestamp <= 0d)
                {
                    return -1f;
                }

                var unixNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d;
                return Mathf.Max(0f, (float)(unixNow - frame.timestamp));
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
                client.Client.ReceiveBufferSize = 32768;
                var endpoint = new IPEndPoint(IPAddress.Any, port);
                while (running)
                {
                    var bytes = client.Receive(ref endpoint);
                    while (client.Available > 0)
                    {
                        bytes = client.Receive(ref endpoint);
                    }
                    var json = Encoding.UTF8.GetString(bytes);
                    var frame = GestureFrameUtility.Clamp(JsonUtility.FromJson<GestureFrame>(json));
                    lock (gate)
                    {
                        if (frame.timestamp > 0d && lastFrameTimestamp > 0d && frame.timestamp < lastFrameTimestamp)
                        {
                            continue;
                        }

                        latest = frame;
                        if (frame.timestamp > 0d)
                        {
                            lastFrameTimestamp = frame.timestamp;
                        }
                    }
                    Interlocked.Exchange(ref lastReceivedTicks, DateTime.UtcNow.Ticks);
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
