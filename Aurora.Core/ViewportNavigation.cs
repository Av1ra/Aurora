using System.Text;

namespace Aurora.Core;

public static class ViewportNavigation
{
    public static bool IsAllowed(string uri, string? trustedHtml)
    {
        if (uri.Equals("about:blank", StringComparison.OrdinalIgnoreCase)) return true;
        if (trustedHtml is null) return false;
        // NavigateToString can report its document as an encoded data URI in the
        // navigation event. Permit exactly our embedded document, not arbitrary data.
        string[] prefixes = ["data:text/html;base64,", "data:text/html;charset=utf-8;base64,"];
        foreach (string prefix in prefixes)
        {
            if (!uri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            string expected = Convert.ToBase64String(Encoding.UTF8.GetBytes(trustedHtml));
            return uri.AsSpan(prefix.Length).SequenceEqual(expected);
        }
        return false;
    }
}
