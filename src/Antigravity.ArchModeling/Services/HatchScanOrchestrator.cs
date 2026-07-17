using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Antigravity.HatchPatterns.Contracts;

namespace Antigravity.ArchModeling.Services
{
    public class HatchScanOrchestrator
    {
        private readonly dynamic _acadDoc;
        private readonly string _requestsFolder;
        private readonly string _responsesFolder;

        public HatchScanOrchestrator(dynamic acadDoc)
        {
            _acadDoc = acadDoc;
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _requestsFolder = Path.Combine(localAppData, "Antigravity", "HatchBridge", "requests");
            _responsesFolder = Path.Combine(localAppData, "Antigravity", "HatchBridge", "responses");
            
            Directory.CreateDirectory(_requestsFolder);
            Directory.CreateDirectory(_responsesFolder);
        }

        public Dictionary<string, HatchBridgeResponseItem> GetHatchDetails(List<string> handles, int timeoutMs = 15000)
        {
            if (handles == null || handles.Count == 0)
                return new Dictionary<string, HatchBridgeResponseItem>();

            string requestId = Guid.NewGuid().ToString();
            var request = new HatchBridgeRequest
            {
                RequestId = requestId,
                RequestedAtUtc = DateTime.UtcNow.ToString("O"),
                SourceDocumentFingerprint = _acadDoc.Name.ToString(),
                EntityHandles = handles
            };

            string requestFile = Path.Combine(_requestsFolder, $"{requestId}.json");
            File.WriteAllText(requestFile, JsonConvert.SerializeObject(request, Formatting.Indented));

            string responseFile = Path.Combine(_responsesFolder, $"{requestId}.json");
            if (File.Exists(responseFile)) File.Delete(responseFile);

            // Execute AutoCAD command
            try
            {
                _acadDoc.SendCommand($"_AGHATCHEXPORT\n{requestId}\n");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to send command to AutoCAD: {ex.Message}");
            }

            // Poll for response
            int elapsed = 0;
            int pollInterval = 500;
            while (elapsed < timeoutMs)
            {
                if (File.Exists(responseFile))
                {
                    try
                    {
                        // Wait a bit to ensure AutoCAD finished writing the file
                        Thread.Sleep(100);
                        string json = File.ReadAllText(responseFile);
                        var response = JsonConvert.DeserializeObject<HatchBridgeResponse>(json);
                        
                        // Clean up
                        try { File.Delete(requestFile); File.Delete(responseFile); } catch { }

                        return response.Items.ToDictionary(item => item.Handle, item => item);
                    }
                    catch (IOException)
                    {
                        // File might be locked, continue polling
                    }
                }
                Thread.Sleep(pollInterval);
                elapsed += pollInterval;
            }

            throw new TimeoutException($"Timed out waiting for HatchBridge response (Request ID: {requestId})");
        }
    }
}
