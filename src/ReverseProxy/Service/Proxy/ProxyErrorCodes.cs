// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Service.Proxy
{
    /// <summary>
    /// Errors reported when proxying a request to the destination.
    /// </summary>
    public enum ProxyErrorCode
    {
        /// <summary>
        /// No error.
        /// </summary>
        None,

        /// <summary>
        /// Failed to connect or send the request.
        /// </summary>
        Request,

        /// <summary>
        /// Canceled when trying to connection or send the request.
        /// </summary>
        RequestCanceled,

        /// <summary>
        /// Canceled while copying the request body.
        /// </summary>
        RequestBodyCanceled,

        /// <summary>
        /// Failed reading the request body from the client.
        /// </summary>
        RequestBodyClient,

        /// <summary>
        /// Failed writing the request body to the destination.
        /// </summary>
        RequestBodyDestination,

        /// <summary>
        /// Canceled while copying the response body.
        /// </summary>
        ResponseBodyCanceled,

        /// <summary>
        /// Failed when writing response body to the client.
        /// </summary>
        ResponseBodyClient,

        /// <summary>
        /// Failed when reading response body from the destination.
        /// </summary>
        ResponseBodyDestination,

        /// <summary>
        /// Canceled while copying the upgraded response body.
        /// </summary>
        UpgradeRequestCanceled,

        /// <summary>
        /// Failed reading the upgraded request body from the client.
        /// </summary>
        UpgradeRequestClient,

        /// <summary>
        /// Failed writing the upgraded request body to the destination.
        /// </summary>
        UpgradeRequestDestination,

        /// <summary>
        /// Canceled while copying the upgraded response body.
        /// </summary>
        UpgradeResponseCanceled,

        /// <summary>
        /// Failed when writing the upgraded response body to the client.
        /// </summary>
        UpgradeResponseClient,

        /// <summary>
        /// Failed when reading the upgraded response body from the destination.
        /// </summary>
        UpgradeResponseDestination,
    }
}