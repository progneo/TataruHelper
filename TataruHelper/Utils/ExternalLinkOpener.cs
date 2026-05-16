using System;
using System.Diagnostics;

namespace FFXIVTataruHelper.Utils;

internal static class ExternalLinkOpener
{
    public static bool TryOpen(Uri uri)
    {
        if (uri == null)
        {
            Trace.TraceWarning("ExternalLinkOpener: URI is null.");
            return false;
        }

        var scheme = uri.Scheme;
        var isSupportedScheme = string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                                || string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        if (!isSupportedScheme)
        {
            Trace.TraceWarning($"ExternalLinkOpener: Unsupported URI scheme '{scheme}'. URI: '{uri}'.");
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = uri.AbsoluteUri, UseShellExecute = true });

            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"ExternalLinkOpener: Failed to open URI '{uri}'. Error: {ex}");
            return false;
        }
    }
}