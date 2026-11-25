using System.Globalization;
using System.Text.RegularExpressions;
using MYAcademy_IdentityEmail.Entities;

namespace MYAcademy_IdentityEmail.Helpers
{
    public static class VeloraDisplay
    {
        public static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
        public static string Name(AppUser? user, string? email = null) => user is null
            ? email ?? "Kullanıcı"
            : string.Join(" ", new[] { user.Name, user.Surname }.Where(x => !string.IsNullOrWhiteSpace(x)));
        public static string Initials(string? name)
        {
            var parts = (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? "V" : string.Concat(parts[0].Take(1)).ToUpper(Turkish)
                + (parts.Length > 1 ? string.Concat(parts[^1].Take(1)).ToUpper(Turkish) : "");
        }
        public static string Preview(string? text) => Regex.Replace(text ?? "", "<[^>]*>", " ").Trim();
        public static string Date(DateTime date)
        {
            var local = DateTime.SpecifyKind(date, DateTimeKind.Utc).ToLocalTime();
            if (local.Date == DateTime.Today) return local.ToString("HH:mm", Turkish);
            if (local.Date == DateTime.Today.AddDays(-1)) return "Dün " + local.ToString("HH:mm", Turkish);
            return local.ToString(local.Year == DateTime.Today.Year ? "d MMM" : "d MMM yyyy", Turkish);
        }
    }
}
