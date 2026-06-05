using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace HandOfGod.Gestures
{
    public sealed class CameraFrameReceiver : MonoBehaviour
    {
        [SerializeField] private int port = 5006;
        [SerializeField] private float timeoutSeconds = 0.75f;
        [SerializeField] private int maxFrameBytes = 512 * 1024;

        private readonly object gate = new object();
        private Thread thread;
        private TcpListener listener;
        private volatile bool running;
        private byte[] pendingBytes;
        private Texture2D texture;
        private long lastReceivedTicks;

        public Texture2D Texture => texture;

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
                return age <= timeoutSeconds && texture != null;
            }
        }

        private void OnEnable()
        {
            texture = new Texture2D(2, 2, TextureFormat.RGB24, false)
            {
                name = "Camera Stream Texture",
            };
            running = true;
            thread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "Camera Frame Receiver",
            };
            thread.Start();
        }

        private void Update()
        {
            byte[] bytes = null;
            lock (gate)
            {
                if (pendingBytes != null)
                {
                    bytes = pendingBytes;
                    pendingBytes = null;
                }
            }

            if (bytes != null)
            {
                texture.LoadImage(bytes, false);
                Interlocked.Exchange(ref lastReceivedTicks, DateTime.UtcNow.Ticks);
            }
        }

        private void OnDisable()
        {
            running = false;
            listener?.Stop();
            listener = null;
            if (thread != null && thread.IsAlive)
            {
                thread.Join(250);
            }
            thread = null;
        }

        private void ReceiveLoop()
        {
            try
            {
                listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                while (running)
                {
                    using var client = listener.AcceptTcpClient();
                    client.NoDelay = true;
                    using var stream = client.GetStream();
                    while (running && client.Connected)
                    {
                        var length = ReadLength(stream);
                        if (length <= 0 || length > maxFrameBytes)
                        {
                            break;
                        }

                        var bytes = ReadExact(stream, length);
                        if (bytes == null)
                        {
                            break;
                        }

                        lock (gate)
                        {
                            pendingBytes = bytes;
                        }
                    }
                }
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (IOException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Camera frame receiver stopped: {exception.Message}");
            }
        }

        private static int ReadLength(Stream stream)
        {
            var header = ReadExact(stream, 4);
            if (header == null)
            {
                return -1;
            }

            return (header[0] << 24) | (header[1] << 16) | (header[2] << 8) | header[3];
        }

        private static byte[] ReadExact(Stream stream, int length)
        {
            var bytes = new byte[length];
            var offset = 0;
            while (offset < length)
            {
                var read = stream.Read(bytes, offset, length - offset);
                if (read <= 0)
                {
                    return null;
                }
                offset += read;
            }
            return bytes;
        }
    }
}
