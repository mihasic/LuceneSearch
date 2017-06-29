namespace System.Net.Http
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// Represents an HttpMessageHanlder that can invoke a request directly against an Asp.Net Core pipeline (an 'RequestDelegate').
    /// </summary>
    public class AspNetCoreHttpMessageHandler : HttpMessageHandler
    {
        /// <summary>
        /// The default number of redirects that will be auto followed.
        /// </summary>
        public const int DefaultAutoRedirectLimit = 20;
        private readonly RequestDelegate _appFunc;
        private CookieContainer _cookieContainer = new CookieContainer();
        private bool _useCookies;
        private bool _operationStarted; //popsicle immutability
        private bool _disposed;
        private bool _allowAutoRedirect;
        private int _autoRedirectLimit = DefaultAutoRedirectLimit;

        /// <summary>
        /// Initializes a new instance of the <see cref="AspNetCoreHttpMessageHandler"/> class.
        /// </summary>
        /// <param name="midFunc">An AspNetCore middleware function that will be terminated with a 404 Not Found.</param>
        /// <exception cref="System.ArgumentNullException">midFunc</exception>
        public AspNetCoreHttpMessageHandler(Func<RequestDelegate, RequestDelegate> midFunc)
        {
            _appFunc = midFunc?.Invoke(context =>
            {
                context.Response.StatusCode = 404;
                return Task.FromResult(0);
            }) ?? throw new ArgumentNullException(nameof(midFunc));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AspNetCoreHttpMessageHandler"/> class.
        /// </summary>
        /// <param name="appFunc">An AspNetCore application function.</param>
        /// <exception cref="System.ArgumentNullException">appFunc</exception>
        public AspNetCoreHttpMessageHandler(RequestDelegate appFunc)
        {
            _appFunc = appFunc ?? throw new ArgumentNullException(nameof(appFunc));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _disposed = true;
        }

        /// <summary>
        ///     Gets or sets a value that indicates whether the handler uses the 
        ///     <see cref="P:System.Net.Http.HttpClientHandler.CookieContainer"/> property
        ///     to store server cookies and uses these cookies when sending requests.
        /// </summary>
        /// <returns>
        ///     Returns <see cref="T:System.Boolean"/>.true if the if the handler supports
        ///     uses the <see cref="P:System.Net.Http.HttpClientHandler.CookieContainer"/> property
        ///     to store server cookies and uses these cookies when sending requests; otherwise false.
        ///     The default value is true.
        /// </returns>
        public bool UseCookies
        {
            get => _useCookies;
            set
            {
                CheckDisposedOrStarted();
                _useCookies = value;
            }
        }

        /// <summary>
        ///     Gets or sets a value that indicates whether the handler should follow redirection responses.
        ///     Authorization headers are stripped on redirect.
        /// </summary>
        /// <returns>
        ///     Returns <see cref="T:System.Boolean"/>.true if the if the handler should follow redirection
        ///     responses; otherwise false. The default value is true.
        /// </returns>
        public bool AllowAutoRedirect
        {
            get => _allowAutoRedirect;
            set
            {
                CheckDisposedOrStarted();
                _allowAutoRedirect = value;
            }
        }

        /// <summary>
        ///     Gets or sets the automatic redirect limit. Default is <see cref="DefaultAutoRedirectLimit"/>.
        /// </summary>
        /// <value>
        ///     The automatic redirect limit.
        /// </value>
        public int AutoRedirectLimit
        {
            get => _autoRedirectLimit;
            set
            {
                CheckDisposedOrStarted();
                if(value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Auto redirect limit must be greater than or equal to one.");
                }
                _autoRedirectLimit = value;
            }
        }

        /// <summary>
        ///     Gets or sets the cookie container used to store server cookies by the handler.
        /// </summary>
        /// <returns>
        ///     Returns <see cref="T:System.Net.CookieContainer"/>.The cookie container used to store
        ///     server cookies by the handler.
        /// </returns>
        public CookieContainer CookieContainer 
        {
            get => _cookieContainer;
            set
            {
                CheckDisposedOrStarted();
                _cookieContainer = value;
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            _operationStarted = true;

            var response = await SendInternalAsync(request, cancellationToken).ConfigureAwait(false);

            var redirectCount = 0;
            var statusCode = (int)response.StatusCode;

            while (_allowAutoRedirect && 
                (IsRedirectToGet(statusCode) || 
                IsBodylessRequest(request) && statusCode == 307))
            {
                if(redirectCount >= _autoRedirectLimit)
                {
                    throw new InvalidOperationException($"Too many redirects. Limit = {redirectCount}");
                }
                var location = response.Headers.Location;
                if (!location.IsAbsoluteUri)
                {
                    location = new Uri(response.RequestMessage.RequestUri, location);
                }

                var redirectMethod = IsRedirectToGet(statusCode) ? HttpMethod.Get : request.Method;
                request.RequestUri = location;
                request.Method = redirectMethod;
                request.Headers.Authorization = null;
                CheckSetCookie(request, response);

                response = await SendInternalAsync(request, cancellationToken).ConfigureAwait(false);

                statusCode = (int) response.StatusCode;
                redirectCount++;
            }
            return response;
        }

        private static bool IsRedirectToGet(int code) => code == 301 || code == 302 || code == 303;

        private static bool IsBodylessRequest(HttpRequestMessage req) => 
            req.Method == HttpMethod.Get || req.Method == HttpMethod.Head || req.Method == HttpMethod.Delete;

        private async Task<HttpResponseMessage> SendInternalAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (_useCookies)
            {
                var cookieHeader = _cookieContainer.GetCookieHeader(request.RequestUri);
                if (!string.IsNullOrEmpty(cookieHeader))
                {
                    request.Headers.Add("Cookie", cookieHeader);
                }
            }

            var state = new RequestState(request, cancellationToken);

            var requestContent = request.Content ?? new StreamContent(Stream.Null);
            var body = await requestContent.ReadAsStreamAsync().ConfigureAwait(false);
            if (body.CanSeek)
            {
                // This body may have been consumed before, rewind it.
                body.Seek(0, SeekOrigin.Begin);
            }
            state.Context.Request.Body = body;
            var registration = cancellationToken.Register(state.Abort);

            // Async offload, don't let the test code block the caller.
#pragma warning disable 4014
            Task.Run(async () =>
#pragma warning restore 4014
            {
                try
                {
                    await _appFunc(state.Context).ConfigureAwait(false);
                    state.CompleteResponse();
                }
                catch (Exception ex)
                {
                    state.Abort(ex);
                }
                finally
                {
                    registration.Dispose();
                    state.Dispose();
                }
            }, cancellationToken);

            var response = await state.ResponseTask.ConfigureAwait(false);
            CheckSetCookie(request, response);
            return response;
        }

        private void CheckSetCookie(HttpRequestMessage request, HttpResponseMessage response)
        {
            if(_useCookies && response.Headers.Contains("Set-Cookie"))
            {
                var cookieHeader = string.Join(",", response.Headers.GetValues("Set-Cookie"));
                _cookieContainer.SetCookies(request.RequestUri, cookieHeader);
            }
        }

        private void CheckDisposedOrStarted()
        {
            CheckDisposed();
            if (_operationStarted)
            {
                throw new InvalidOperationException("Handler has started operations");
            }
        }

        private void CheckDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        private class RequestState : IDisposable
        {
            private readonly HttpRequestMessage _request;
            private Func<Task> _sendingHeaders;
            private readonly TaskCompletionSource<HttpResponseMessage> _responseTcs;
            private readonly ResponseStream _responseStream;

            internal RequestState(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                _request = request;
                _responseTcs = new TaskCompletionSource<HttpResponseMessage>();
                _sendingHeaders = () => Task.FromResult(0);

                request.Headers.Host = request.RequestUri.IsDefaultPort 
                    ? request.RequestUri.Host
                    : request.RequestUri.GetComponents(UriComponents.HostAndPort, UriFormat.UriEscaped);

                Context = new DefaultHttpContext();
                var cRequest = Context.Request;
                cRequest.Protocol = "HTTP/" + request.Version.ToString(2);
                cRequest.Scheme = request.RequestUri.Scheme;
                cRequest.Method = request.Method.ToString();
                cRequest.Path = PathString.FromUriComponent(request.RequestUri);
                cRequest.PathBase = PathString.Empty;
                cRequest.QueryString = QueryString.FromUriComponent(request.RequestUri);
                Context.RequestAborted = cancellationToken;
                //Context.Response.OnStarting(_sendingHeaders);

                foreach (var header in request.Headers)
                {
                    cRequest.Headers.Add(header.Key, header.Value.ToArray());
                }
                if (request.Content != null)
                {
                    // Need to touch the ContentLength property for it to be calculated and added
                    // to the request.Content.Headers collection.
                    var d = request.Content.Headers.ContentLength;

                    foreach (var header in request.Content.Headers)
                    {
                        request.Headers.Add(header.Key, header.Value.ToArray());
                    }
                }

                _responseStream = new ResponseStream(CompleteResponse);
                Context.Response.Body = _responseStream;
                Context.Response.StatusCode = 200;
            }

            public HttpContext Context { get; }

            public Task<HttpResponseMessage> ResponseTask => _responseTcs.Task;

            internal void CompleteResponse()
            {
                if (!_responseTcs.Task.IsCompleted)
                {
                    var response = GenerateResponse();
                    // Dispatch, as TrySetResult will synchronously execute the waiters callback and block our Write.
                    Task.Factory.StartNew(() => _responseTcs.TrySetResult(response));
                }
            }

            private HttpResponseMessage GenerateResponse()
            {
                _sendingHeaders();

                var response = new HttpResponseMessage
                {
                    StatusCode = (HttpStatusCode) Context.Response.StatusCode,
                    RequestMessage = _request,
                    Content = new StreamContent(_responseStream)
                };
                // response.Version = response.Protocol;

                foreach (var header in Context.Response.Headers)
                {
                    if (!response.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
                    {
                        response.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                    }
                }
                return response;
            }

            internal void Abort()
            {
                Abort(new OperationCanceledException());
            }

            internal void Abort(Exception exception)
            {
                _responseStream.Abort(exception);
                _responseTcs.TrySetException(exception);
            }

            public void Dispose()
            {
                _responseStream.Dispose();
                // Do not dispose the request, that will be disposed by the caller.
            }
        }
    }
}