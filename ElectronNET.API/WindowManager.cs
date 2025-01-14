﻿using ElectronNET.API.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ElectronNET.API.Interfaces;

namespace ElectronNET.API
{
    /// <summary>
    /// 
    /// </summary>
    public sealed class WindowManager : IWindowManager
    {
        private static WindowManager _windowManager;
        private static object _syncRoot = new object();

        internal WindowManager() { }

        internal static WindowManager Instance
        {
            get
            {
                if (_windowManager == null)
                {
                    lock (_syncRoot)
                    {
                        if (_windowManager == null)
                        {
                            _windowManager = new WindowManager();
                        }
                    }
                }

                return _windowManager;
            }
        }

        /// <summary>
        /// Quit when all windows are closed. (Default is true)
        /// </summary>
        /// <value>
        ///   <c>true</c> if [quit window all closed]; otherwise, <c>false</c>.
        /// </value>
        public bool IsQuitOnWindowAllClosed
        {
            get { return _isQuitOnWindowAllClosed; }
            set
            {
                BridgeConnector.Emit("quit-app-window-all-closed-event", value);
                _isQuitOnWindowAllClosed = value;
            }
        }
        private bool _isQuitOnWindowAllClosed = true;

        /// <summary>
        /// Gets the browser windows.
        /// </summary>
        /// <value>
        /// The browser windows.
        /// </value>
        public IReadOnlyCollection<BrowserWindow> BrowserWindows { get { return _browserWindows.AsReadOnly(); } }
        private List<BrowserWindow> _browserWindows = new List<BrowserWindow>();

        /// <summary>
        /// Gets the browser views.
        /// </summary>
        /// <value>
        /// The browser view.
        /// </value>
        public IReadOnlyCollection<BrowserView> BrowserViews { get { return _browserViews.AsReadOnly(); } }
        private List<BrowserView> _browserViews = new List<BrowserView>();

        /// <summary>
        /// Creates the window asynchronous.
        /// </summary>
        /// <param name="loadUrl">The load URL.</param>
        /// <returns></returns>
        public async Task<BrowserWindow> CreateWindowAsync(string loadUrl = "/")
        {
            return await CreateWindowAsync(new BrowserWindowOptions(), loadUrl);
        }

        /// <summary>
        /// Creates the window asynchronous.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="loadUrl">The load URL.</param>
        /// <returns></returns>
        public Task<BrowserWindow> CreateWindowAsync(BrowserWindowOptions options, string loadUrl = "/")
        {
            var taskCompletionSource = new TaskCompletionSource<BrowserWindow>(TaskCreationOptions.RunContinuationsAsynchronously);

            BridgeConnector.On<int>("BrowserWindowCreated", (id) =>
            {
                BridgeConnector.Off("BrowserWindowCreated");

                BrowserWindow browserWindow = new BrowserWindow(id);
                _browserWindows.Add(browserWindow);

                taskCompletionSource.SetResult(browserWindow);
            });

            BridgeConnector.Off("BrowserWindowClosed");
            BridgeConnector.On<int[]>("BrowserWindowClosed", (browserWindowIds) =>
            {
                for (int index = 0; index < _browserWindows.Count; index++)
                {
                    if (!browserWindowIds.Contains(_browserWindows[index].Id))
                    {
                        _browserWindows.RemoveAt(index);
                    }
                }
            });

            if(!TryParseLoadUrl(loadUrl, out loadUrl)) 
            { 
                throw new ArgumentException($"Unable to parse {loadUrl}", nameof(loadUrl));
            }

            // Workaround Windows 10 / Electron Bug
            // https://github.com/electron/electron/issues/4045
            if (isWindows10())
            {
                options.Width = options.Width + 14;
                options.Height = options.Height + 7;
            }

            if (options.X == -1 && options.Y == -1)
            {
                options.X = 0;
                options.Y = 0;

                BridgeConnector.Emit("createBrowserWindow", options, loadUrl);
            }
            else
            {
                // Workaround Windows 10 / Electron Bug
                // https://github.com/electron/electron/issues/4045
                if (isWindows10())
                {
                    options.X = options.X - 7;
                }

                var ownjsonSerializer = new JsonSerializer()
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore
                };

                BridgeConnector.Emit("createBrowserWindow", JObject.FromObject(options, ownjsonSerializer), loadUrl);
            }

            return taskCompletionSource.Task;
        }

        
        private bool TryParseLoadUrl(string loadUrl, out string parsedUrl) 
        {
            Uri BaseUri = new Uri($"http://localhost:{BridgeSettings.WebPort}");
            if (Uri.TryCreate(loadUrl, UriKind.Absolute, out var url) ||
                Uri.TryCreate(BaseUri, loadUrl, out url)) {
                var uri = new UriBuilder(url.ToString());
                parsedUrl = uri.ToString();
                return true;
            }
            parsedUrl = loadUrl;
            return false;
        }

        private bool isWindows10()
        {
            return RuntimeInformation.OSDescription.Contains("Windows 10");
        }

        /// <summary>
        /// A BrowserView can be used to embed additional web content into a BrowserWindow. 
        /// It is like a child window, except that it is positioned relative to its owning window. 
        /// It is meant to be an alternative to the webview tag.
        /// </summary>
        /// <returns></returns>
        public Task<BrowserView> CreateBrowserViewAsync()
        {
            return CreateBrowserViewAsync(new BrowserViewConstructorOptions());
        }

        /// <summary>
        /// A BrowserView can be used to embed additional web content into a BrowserWindow. 
        /// It is like a child window, except that it is positioned relative to its owning window. 
        /// It is meant to be an alternative to the webview tag.
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public Task<BrowserView> CreateBrowserViewAsync(BrowserViewConstructorOptions options)
        {
            var taskCompletionSource = new TaskCompletionSource<BrowserView>(TaskCreationOptions.RunContinuationsAsynchronously);

            BridgeConnector.On<int>("BrowserViewCreated", (id) =>
            {
                BridgeConnector.Off("BrowserViewCreated");

                BrowserView browserView = new BrowserView(id);

                _browserViews.Add(browserView);

                taskCompletionSource.SetResult(browserView);
            });

            var keepDefaultValuesSerializer = new JsonSerializer()
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Include
            };
            BridgeConnector.Emit("createBrowserView", JObject.FromObject(options, keepDefaultValuesSerializer));

            return taskCompletionSource.Task;
        }
    }
}
