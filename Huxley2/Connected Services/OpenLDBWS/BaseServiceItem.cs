// © James Singleton. EUPL-1.2 (see the LICENSE file for the full license governing this code).

using Huxley2.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace OpenLDBWS
{
    public partial class BaseServiceItem
    {
        // The NRE data feed is no longer sending Base64 compliant ServiceID strings so just URLEncode
        public string ServiceIdPercentEncoded {
            get {
                string urlEncodedString = WebUtility.UrlEncode(serviceIDField);
                // log here if needed
                return urlEncodedString;
            }
        }

        // Deprecated 
        public Guid ServiceIdGuid => new Guid();

        [SuppressMessage("Design", "CA1056:Uri properties should not be strings", Justification = "Not a URL")]
        public string ServiceIdUrlSafe {
            get {
                string urlEncodedString = WebUtility.UrlEncode(serviceIDField);
                // log here if needed
                return urlEncodedString;
            }
        }
    }
}
