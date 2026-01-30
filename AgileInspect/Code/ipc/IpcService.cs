using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;

namespace AgileInspect.Code.Ipc
{
    public class IpcService
    {
        private readonly ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<string> _inspectMessageQueue = new ConcurrentQueue<string>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly CancellationTokenSource _inSpectCancellationTokenSource = new CancellationTokenSource();
        public static IpcService Instance { get; set; }
        public IpcService()
        {
            Instance = this;
        }

        public class MessageWrapper
        {
        }
        public class InspectMessage
        {
            public string actions { get; set; }
            public Dictionary<string, object> @params { get; set; }
        }

        public void SendRequest(string message)
        {
            _messageQueue.Enqueue(message);
        }

        public async Task<string> SendRequestWithResponseAsync(string message, int timeoutMs)
        {
            try
            {
                using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", "agileinspect_qaKOab5VPyK4ar4A6sfm2VZ0G", PipeDirection.InOut))
                {
                    // Use a reasonable connection timeout (2 seconds) separate from response timeout
                    int connectionTimeout = Math.Min(2000, timeoutMs);
                    await pipeClient.ConnectAsync(connectionTimeout);

                    // Check if connected
                    if (pipeClient.IsConnected)
                    {
                        DebugLog.WriteLine("[IPC] Successfully connected to AgileMark for request-response.");

                        using (StreamWriter streamWriter = new StreamWriter(pipeClient) { AutoFlush = true })
                        using (StreamReader streamReader = new StreamReader(pipeClient))
                        {
                            // Send the request
                            await streamWriter.WriteLineAsync(message);
                            await streamWriter.FlushAsync();
                            DebugLog.WriteLine($"[IPC] Sent request to AgileMark: {message}");

                            // Wait for response with timeout
                            using (var cts = new CancellationTokenSource(timeoutMs))
                            {
                                try
                                {
                                    string response = await streamReader.ReadLineAsync().WaitAsync(cts.Token);
                                    DebugLog.WriteLine($"[IPC] Received response from AgileMark: {response}");
                                    return response ?? string.Empty;
                                }
                                catch (OperationCanceledException)
                                {
                                    DebugLog.WriteLine($"[IPC] Timeout waiting for response from AgileMark after {timeoutMs}ms");
                                    return string.Empty;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine("[IPC] Error sending request with response: " + ex.Message);
                return string.Empty;
            }
            return string.Empty;
        }

        public void startSendingProcess()
        {
            Task.Run(() => ProcessMessageInQueue(_cancellationTokenSource.Token));
        }

        private async void ProcessMessageInQueue(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_messageQueue.TryDequeue(out string message))
                {
                    DebugLog.WriteLine($"[IPC - sent] Attempting to connect to AgileMark...");

                    try
                    {
                        using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", "agileinspect_qaKOab5VPyK4ar4A6sfm2VZ0", PipeDirection.InOut))
                        {
                            await pipeClient.ConnectAsync(5);

                            // Check if connected
                            if (pipeClient.IsConnected)
                            {
                                DebugLog.WriteLine("[IPC] Successfully connected to AgileMark.");

                                using (StreamWriter streamWriter = new StreamWriter(pipeClient) { AutoFlush = true })
                                {
                                    DebugLog.WriteLine($"[IPC] Preparing to send request with message {message}...");
                                    string logMessage = message;
                                    // Send the request
                                    await streamWriter.WriteLineAsync(message);
                                    await streamWriter.FlushAsync();

                                    // Write to log
                                    if (logMessage != null)
                                    {
                                        DebugLog.WriteLine($"[IPC] Sent message to AgileMark: {logMessage}");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLog.WriteLine("[IPC] Error sending request: " + ex.Message);
                    }
                }

                else
                {
                    await Task.Delay(50); // Avoid CPU overuse when queue is empty
                }
            }
        }
    }
}