using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace openHAbot.openhab
{
    public static class UriHelper
    {

        public static Uri GetBaseUri(string url)
        {

            if (Uri.TryCreate(url, UriKind.Absolute, out var outUri)
               && (outUri.Scheme == Uri.UriSchemeHttp || outUri.Scheme == Uri.UriSchemeHttps))
            {
                return outUri;
            }
            else return null;
        }

    }
}
