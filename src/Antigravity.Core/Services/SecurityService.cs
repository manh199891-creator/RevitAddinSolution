using Autodesk.Revit.ApplicationServices;
using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace Antigravity.Core.Services
{
    public static class SecurityService
    {
        // === CẤU HÌNH BẢO MẬT ===
        // IP công ty được phép sử dụng không cần mật khẩu
        private static readonly string[] AllowedCompanyIPs = { "117.4.247.106" };

        // Mật khẩu master (đã mã hóa SHA256 từ "090698")
        private static readonly string MasterPasswordHash = ComputeSHA256("090698");

        /// <summary>
        /// Kiểm tra người dùng có quyền sử dụng Add-in hay không.
        /// - Nếu IP khớp IP công ty → cho vào luôn.
        /// - Nếu không khớp → yêu cầu nhập mật khẩu.
        /// </summary>
        public static bool IsAuthorized(Application revitApp, out bool needPassword)
        {
            needPassword = false;

            try
            {
                string publicIp = GetPublicIP();
                foreach (string ip in AllowedCompanyIPs)
                {
                    if (publicIp.Trim() == ip.Trim())
                        return true; // ✅ Đang ở văn phòng, cho vào luôn
                }
            }
            catch { /* Bỏ qua lỗi mạng, chuyển sang hỏi mật khẩu */ }

            // Không thuộc IP công ty → yêu cầu mật khẩu
            needPassword = true;
            return false;
        }

        /// <summary>
        /// Kiểm tra mật khẩu người dùng nhập vào.
        /// </summary>
        public static bool CheckPassword(string inputPassword)
        {
            if (string.IsNullOrEmpty(inputPassword)) return false;
            string inputHash = ComputeSHA256(inputPassword);
            return string.Equals(inputHash, MasterPasswordHash, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetPublicIP()
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(5);
                try
                {
                    return client.GetStringAsync("https://api.ipify.org").Result.Trim();
                }
                catch
                {
                    // Service dự phòng nếu ipify bị lỗi
                    return client.GetStringAsync("https://icanhazip.com").Result.Trim();
                }
            }
        }

        private static string ComputeSHA256(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder sb = new StringBuilder();
                foreach (byte b in bytes)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
