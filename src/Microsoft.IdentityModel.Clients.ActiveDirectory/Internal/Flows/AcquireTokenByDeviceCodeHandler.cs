﻿//----------------------------------------------------------------------
//
// Copyright (c) Microsoft Corporation.
// All rights reserved.
//
// This code is licensed under the MIT License.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files(the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions :
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
//------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Core;
using Microsoft.Identity.Core.Cache;
using Microsoft.IdentityModel.Clients.ActiveDirectory.Internal.OAuth2;

namespace Microsoft.IdentityModel.Clients.ActiveDirectory.Internal.Flows
{
    internal class AcquireTokenByDeviceCodeHandler : AcquireTokenHandlerBase
    {
        private readonly DeviceCodeResult _deviceCodeResult;
        private readonly CancellationToken _cancellationToken;

        public AcquireTokenByDeviceCodeHandler(
            IServiceBundle serviceBundle, 
            RequestData requestData, 
            DeviceCodeResult deviceCodeResult)
            : base(serviceBundle, requestData)
        {
            LoadFromCache = false; //no cache lookup for token
            StoreToCache = (requestData.TokenCache != null);
            SupportADFS = true;
            _deviceCodeResult = deviceCodeResult;
            _cancellationToken = cancellationToken;
        }

        protected internal /* internal for test only */ override async Task<AdalResultWrapper> SendTokenRequestAsync()
        {
            TimeSpan timeRemaining = _deviceCodeResult.ExpiresOn - DateTimeOffset.UtcNow;
            AdalResultWrapper resultEx = null;

            while (timeRemaining.TotalSeconds > 0)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    resultEx = await base.SendTokenRequestAsync().ConfigureAwait(false);
                    break;
                }
                catch (AdalServiceException exc)
                {
                    if (!exc.ErrorCode.Equals(AdalError.DeviceCodeAuthorizationPendingError, StringComparison.OrdinalIgnoreCase))
                    {
                        throw;
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(_deviceCodeResult.Interval)).ConfigureAwait(false);
                timeRemaining = _deviceCodeResult.ExpiresOn - DateTimeOffset.UtcNow;
            }

            if (resultEx == null)
            {
                throw new AdalServiceException(
                    AdalError.DeviceCodeAuthorizationCodeExpired, 
                    AdalErrorMessage.DeviceCodeAuthorizationCodeExpired);
            }

            return resultEx;
        }

        protected override void AddAdditionalRequestParameters(DictionaryRequestParameters requestParameters)
        {
            requestParameters[OAuthParameter.GrantType] = OAuthGrantType.DeviceCode;
            requestParameters[OAuthParameter.Code] = _deviceCodeResult.DeviceCode;
        }
    }
}
