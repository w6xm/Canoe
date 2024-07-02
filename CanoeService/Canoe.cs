using System.IO;
using System.Net.WebSockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System;
using NAudio.Wave;
using System.Text.Json;

namespace CanoeService
{
    class Canoe
    {
        private WasapiLoopbackCapture loopbackCapture;
        private MemoryStream audioBuffer;
        public CanoeState canoeState = new CanoeState();

        public static CanoeState ParseMessage(string message)
        {
            // TODO: Assess whether this could just be returned directly
            // with no intermediate "state" object.
            CanoeState state = JsonSerializer.Deserialize<CanoeState>(message);
            return state;
        }

        public async Task StartCanoeAsync(HttpListenerContext context)
        {

            if (context.Request.IsWebSocketRequest)
            {
                WriteLineToCanoeLog(context.Request.RemoteEndPoint.ToString() + " connected");

                var webSocketContext = await context.AcceptWebSocketAsync(null);
                WebSocket webSocket = webSocketContext.WebSocket;

                // Start continuously sending/receiving audio with the websocket
                _ = ReceiveDataAsync(webSocket);
                _ = SendAudioAsync(webSocket);
                // TODO: Currently, as far as I can tell, if either of these 
                // tasks encounter problems they'll just quietly die. There
                // should probably be some way for the program to notice this
                // and do something (attempt to reestablish a connection, maybe?).


                // Delay the thread indefinitely so the program
                // doesn't return while the audio processing tasks
                // are running.
                // TODO: Investigate better ways to do this; at the
                // very least it should probabaly let the method return
                // when the websocket closes.
                while (true)
                {
                    await Task.Delay(1000);
                }
            }

            else
            {
                // If Canoe receives a regular http request instead of a
                // websocket, send back a very basic html response

                WriteLineToCanoeLog("Not a WebSocket Request");
                HttpListenerResponse response = context.Response;

                string responseString = $"<html><body><h1>Canoe</h1>The time is {DateTime.Now}<br></body></html>";
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);

                response.ContentLength64 = buffer.Length;
                Stream output = response.OutputStream;
                output.Write(buffer, 0, buffer.Length);
                output.Close();
                response.Close();

                // context.Response.StatusCode = 400;
                // context.Response.Close();
                return;
            }
        }

        private async Task SendAudioAsync(WebSocket webSocket)
        {
            WriteLineToCanoeLog($"SendMessageAsync started at {DateTime.Now}");

            loopbackCapture = new WasapiLoopbackCapture
            {
                WaveFormat = new WaveFormat(8000, 16, 1)
            };

            loopbackCapture.DataAvailable += (object sender, WaveInEventArgs e) =>
            {
                // Write audio data to the buffer
                audioBuffer.Write(e.Buffer, 0, e.BytesRecorded);
                audioBuffer.Flush();
            };

            try
            {
                loopbackCapture.StartRecording();
            }
            catch
            {
                WriteLineToCanoeLog("SendMessageAsync: unable to start loopbackCapture");
            }

            audioBuffer = new MemoryStream();

            int sentMessagesCount = 0;

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    if (audioBuffer.Length > 0)
                    {
                        byte[] audioData = audioBuffer.ToArray();

                        if (this.canoeState.SpkEnabled == true)
                        {
                            await webSocket.SendAsync(new ArraySegment<byte>(audioData), WebSocketMessageType.Binary, true, default);
                            sentMessagesCount += 1;
                            if (sentMessagesCount % 1000 == 0)
                            {
                                long unixTime = ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds();
                                WriteLineToCanoeLog($"UnixTime: {unixTime}, Messages: {sentMessagesCount}, audioBuffer.Length: {audioBuffer.Length}");
                            }
                        }
                        audioBuffer.SetLength(0);
                    }

                    await Task.Delay(10); // Adjust delay as needed
                }

                loopbackCapture.StopRecording();        // Clean up resources when the WebSocket connection is closed
                loopbackCapture.Dispose();              //
                audioBuffer.Dispose();                  // 
                WriteLineToCanoeLog($"SendMessageAsync: Cleaned up audio resources at {DateTime.Now}");
            }
            catch (Exception ex)
            {
                WriteLineToCanoeLog($"SendMessagesAsync WebSocket send error: {ex.Message}");
            }
        }

        // Receive audio and control messages for as long as the 
        // websocket is open
        private async Task ReceiveDataAsync(WebSocket webSocket)
        {
            WriteLineToCanoeLog($"ReceiveMessageAsync started at {DateTime.Now}");

            WaveOut waveOut = new WaveOut
            {
                DeviceNumber = GetVacDeviceNumber()
            };

            var bufferedWaveProvider = new BufferedWaveProvider(new WaveFormat(8000, 16, 1))
            {
                BufferLength = 2560 * 16,
                DiscardOnBufferOverflow = true
            };

            waveOut.Init(bufferedWaveProvider);
            waveOut.Play();

            var buffer = new byte[1024 * 8];

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string receivedMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        WriteLineToCanoeLog(receivedMessage);

                        this.canoeState = ParseMessage(receivedMessage);
                        WriteLineToCanoeLog($"Jsonserializer: {JsonSerializer.Serialize(this.canoeState)}");

                    }

                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        bufferedWaveProvider.AddSamples(buffer, 0, result.Count);

                    }

                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLineToCanoeLog($"WebSocket receive error: {ex.Message}");
            }
        }

        public int GetVacDeviceNumber()
        {
            for (int n = -1; n < WaveOut.DeviceCount; n++)
            {
                var caps = WaveOut.GetCapabilities(n);
                if (caps.ProductName.Contains("VAC"))
                {
                    WriteLineToCanoeLog($"GetVacDeviceNumber: Device #{n} is {caps.ProductName}");
                    return n;
                }
            }
            return -1; // the default device
        }

        public void WriteLineToCanoeLog(string Message)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\Logs";
            if (!Directory.Exists(path)) { Directory.CreateDirectory(path); }

            string filepath = AppDomain.CurrentDomain.BaseDirectory + "\\Logs\\CanoeLog_" + DateTime.Now.ToShortDateString().Replace('/', '_') + ".txt";

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

            /* 
            // CURRENTLY UNTESTED!
            // Potential alternative way that's a bit less readable but doesn't have duplicate code
            // Might come in useful if we decide to do more with the logging.
            
            // If the file does not exist, create it. Otherwise, append to it.
            using (StreamWriter sw = !File.Exists(filepath) ? File.CreateText(filepath) : File.AppendText(filepath))
            {
                sw.WriteLine(Message);
            }
            
             */
        }
    }
}