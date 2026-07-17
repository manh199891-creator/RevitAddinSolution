using System.IO;
using Newtonsoft.Json;
using Antigravity.HatchPatterns.Contracts;

namespace Antigravity.AutoCAD.HatchBridge
{
    public class BridgeFileExchange
    {
        public void ExportResponse(HatchBridgeResponse response)
        {
            string folder = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Antigravity", "HatchBridge", "responses");
            Directory.CreateDirectory(folder);
            string tempPath = Path.Combine(folder, $"{response.RequestId}.json");
            
            string json = JsonConvert.SerializeObject(response, Formatting.Indented);
            File.WriteAllText(tempPath, json);
        }
    }
}
