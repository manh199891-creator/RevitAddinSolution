namespace Antigravity.Autojoin.Services
{
    public static class JoinStatusFormatter
    {
        public static string Format(JoinAction action, JoinResult result)
        {
            if (result == null) return "No join result.";

            if (action == JoinAction.Join)
                return $"✅  Mới join: {result.JoinCount} — Đã có: {result.AlreadyCount} — Bỏ qua: {result.SkipCount}";

            return $"✅  Đã bỏ join: {result.UnjoinCount} cặp phần tử.";
        }
    }
}
