﻿// © James Singleton. EUPL-1.2 (see the LICENSE file for the full license governing this code).

using Microsoft.AspNetCore.WebUtilities;
using OpenLDBWS;
using System;
using System.Net;
using Xunit;

namespace Huxley2Tests.OpenLDBWS
{
    public class BaseServiceItemTests
    {
        [Fact]
        public void ServiceItemIdIsPercentEncoded()
        {
            var serviceItem = new BaseServiceItem
            {
                serviceID = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            };
            var expected = WebUtility.UrlEncode(serviceItem.serviceID);
            Assert.Equal(expected, serviceItem.ServiceIdPercentEncoded);
        }

        [Fact]
        public void ServiceItemIdIsUrlSafeEncoded()
        {
            var serviceItem = new BaseServiceItem
            {
                serviceID = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            };
            var expected = WebUtility.UrlEncode(serviceItem.serviceID);
            Assert.Equal(expected, serviceItem.ServiceIdUrlSafe);
        }


    }
}
