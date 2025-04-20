using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CefNet;
using CefNet.Avalonia;
using System;
using System.Text.Json;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Views
{
    public partial class UserLogin : UserControl
    {
        public bool IsActive = false;
        private TPO2Callback _tPO2Callback;

        public UserLogin()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            Browser = this.FindControl<WebView>("Browser");

            if (Browser == null)
            {
                throw new InvalidOperationException("Browser control not found in XAML. Make sure it has x:Name='Browser'");
            }

            _tPO2Callback = new TPO2Callback();
            _tPO2Callback.GameProcessExited += OnGameProcessExited;

            Browser.BrowserCreated += Browser_BrowserCreated;

            // Set up the message handler for JavaScript communication
            SetupMessageHandler();
        }

        private void SetupMessageHandler()
        {
            // Create a message handler
            var jsMessageHandler = new JSMessageHandler(_tPO2Callback);

            // Inject the JavaScript bridge
            Browser.LoadingStateChange += (sender, e) =>
            {
                // Most CefNet implementations use CanGoBack, CanGoForward and IsLoading
                // If IsLoading isn't available, we can check another property
                if (e.CanGoBack || e.CanGoForward) // This indicates the page has loaded enough to have history
                {
                    // When page has loaded, inject our JavaScript bridge
                    InjectJavaScriptBridge();
                }
            };

            // Handle render process termination
            Browser.RenderProcessTerminated += (sender, e) =>
            {
                Console.WriteLine("Render process terminated. Reason: " + e.Status);
            };

            // Add JavaScript callback for receiving messages
            Browser.DocumentTitleChanged += (sender, e) =>
            {
                if (e.Title.StartsWith("js2cs:"))
                {
                    // Format is "js2cs:[JSON data]"
                    string json = e.Title.Substring(6); // Skip "js2cs:"
                    // Process the message using our handler
                    jsMessageHandler.ProcessMessage(
                        json,
                        successResponse =>
                        {
                            // Success callback - could execute JS back if needed
                        },
                        (errorCode, errorMessage) =>
                        {
                            Console.WriteLine($"Error processing message: {errorMessage}");
                        }
                    );
                }
            };
        }

        private void InjectJavaScriptBridge()
        {
            // This JavaScript creates a bridge object for communication
            string script = @"
                window.cefBridge = {
                    postMessage: function(message) {
                        // Using document.title as communication channel
                        var originalTitle = document.title;
                        document.title = 'js2cs:' + JSON.stringify(message);
                        setTimeout(function() { document.title = originalTitle; }, 50);
                    }
                };

                // Define callback object for the application to use
                window.callbackObj = {
                    showMessage: function(msg) {
                        window.cefBridge.postMessage({
                            action: 'showMessage',
                            message: msg
                        });
                    },
                    startGame: function(uniqueRoomName, realRoomName, gameId, playerId, playerName, playerCount) {
                        window.cefBridge.postMessage({
                            action: 'startGame',
                            uniqueRoomName: uniqueRoomName,
                            realRoomName: realRoomName,
                            gameId: gameId,
                            playerId: playerId,
                            playerName: playerName,
                            playerCount: playerCount
                        });
                    }
                };
                console.log('JavaScript bridge initialized');
            ";

            // Execute the script to inject the bridge
            Browser.GetMainFrame()?.ExecuteJavaScript(script, null, 0);
        }

        private void Browser_BrowserCreated(object sender, EventArgs e)
        {
            // Navigate to your URL
            Browser.Navigate("https://teknoparrot.com:3333/Home/Chat");

            // Wait for the page to load before injecting JavaScript
            Browser.Navigated += (s, args) =>
            {
                // Navigated event occurs when the main frame navigation is completed
                InjectJavaScript();
            };
        }
        private bool _javascriptInjected = false;

        private void InjectJavaScript()
        {
            // Prevent multiple injections
            if (_javascriptInjected)
                return;

            _javascriptInjected = true;

            var frame = Browser.GetMainFrame();
            if (frame != null)
            {
                frame.ExecuteJavaScript(
                    @"window.callbackObj = {
                        showMessage: function(msg) {
                            // Use CefNet's IPC mechanism to send messages to C#
                            const message = {
                                action: 'showMessage',
                                message: msg
                            };
                            window.cefSharp.postMessage('jsToCSharp', JSON.stringify(message));
                        },
                        startGame: function(uniqueRoomName, realRoomName, gameId, playerId, playerName, playerCount) {
                            const message = {
                                action: 'startGame',
                                uniqueRoomName: uniqueRoomName,
                                realRoomName: realRoomName,
                                gameId: gameId,
                                playerId: playerId,
                                playerName: playerName,
                                playerCount: playerCount
                            };
                            window.cefSharp.postMessage('jsToCSharp', JSON.stringify(message));
                        }
                    };
                    
                    // Provide a compatibility layer for existing code
                    if (!window.cefSharp) {
                        window.cefSharp = {
                            postMessage: function(channel, message) {
                                // Send a process message from renderer to browser process
                                window.cefPostMessage(channel, message);
                            }
                        };
                    }
                    
                    function onGameProcessExited() {
                        console.log('Game process exited');
                        // Add any site-specific code here
                    }",
                    "about:blank",
                    0
                );
            }
        }

        private void OnGameProcessExited()
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Execute JavaScript to notify the website
                if (Browser?.GetMainFrame() != null)
                {
                    Browser.GetMainFrame().ExecuteJavaScript(
                        "if(typeof onGameProcessExited === 'function') onGameProcessExited();",
                        "about:blank",
                        0
                    );
                }
            });
        }

        public void RefreshBrowserToStart()
        {
            if (Browser != null)
            {
                Browser.Navigate("https://teknoparrot.com:3333/Home/Chat");
            }
        }

        private void UserLogin_OnPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == IsVisibleProperty && Browser != null)
            {
                if (IsVisible)
                {
                    RefreshBrowserToStart();
                    IsActive = true;
                }
                else
                {
                    RefreshBrowserToStart();
                    Browser.Reload();
                    IsActive = false;
                }
            }
        }
    }

    // Cross-Platform implementation of TPO2Callback will go here
    // You can use the implementation I provided earlier with IGameProcess interface
}