using UnityEngine;
using Vuplex.WebView;

public class WebViewCommunication : MonoBehaviour
{
    public CanvasWebViewPrefab webViewPrefab; // Assign the Vuplex WebView prefab in the Inspector

    async void Start()
    {
        // Wait for the WebView to initialize
        await webViewPrefab.WaitUntilInitialized();

        // Ensure JavaScript is enabled

        // Subscribe to the MessageEmitted event
        webViewPrefab.WebView.MessageEmitted += OnMessageEmitted;

        // Load the HTML page
        webViewPrefab.WebView.LoadHtml(@"
            <!DOCTYPE html>
            <html>
            <head>
                <title>Vuplex Test</title>
            </head>
            <body>
                <h1>Vuplex WebView Test</h1>
                <p>Click the buttons to get width or height from Unity:</p>
                  <button onclick='getDimensionsFromUnity()'>Get Dimensions</button>
                <button onclick='getWidthFromUnity()'>Get Width</button>
                <button onclick='getHeightFromUnity()'>Get Height</button>
                <p id='response'>Waiting for Unity response...</p>
                <script>
                    if (window.vuplex) {
                        addMessageListener();
                    } else {
                        window.addEventListener('vuplexready', addMessageListener);
                    }

                    function addMessageListener() {
                        console.log('Vuplex is ready. Adding message listener.');
                        window.vuplex.addEventListener('message', function(event) {
                            console.log('Message received from Unity:', event.data);
                            document.getElementById('response').innerText = event.data;
                        });
                    }

                    function getDimensionsFromUnity() {
                        console.log('Sending message to Unity: GetDimensions');
                        window.vuplex.postMessage('GetDimensions');
                    }

                    function getWidthFromUnity() {
                        console.log('Sending message to Unity: GetWidth');
                        window.vuplex.postMessage('GetWidth');
                    }

                    function getHeightFromUnity() {
                        console.log('Sending message to Unity: GetHeight');
                        window.vuplex.postMessage('GetHeight');
                    }
                </script>
            </body>
            </html>
        ");
    }

    private void OnMessageEmitted(object sender, EventArgs<string> eventArgs)
    {
        Debug.Log($"Message received from JavaScript: {eventArgs.Value}");

        if (eventArgs.Value == "GetDimensions")
        {
            Debug.Log("Processing GetDimensions in Unity.");

            // Prepare the response
            string width = Screen.width.ToString();
            string height = Screen.height.ToString();
            string response = $"{{ \"width\": \"{width}\", \"height\": \"{height}\" }}";

            Debug.Log($"Sending response to JavaScript: {response}");
            webViewPrefab.WebView.PostMessage(response);
        }
        else if (eventArgs.Value == "GetWidth")
        {
            Debug.Log("Processing GetWidth in Unity.");
            int width = Screen.width;

            Debug.Log($"Sending width to JavaScript: {width}");
            webViewPrefab.WebView.PostMessage(width.ToString()); // Send as plain string
        }
        else if (eventArgs.Value == "GetHeight")
        {
            Debug.Log("Processing GetHeight in Unity.");
            int height = Screen.height;

            Debug.Log($"Sending height to JavaScript: {height}");
            webViewPrefab.WebView.PostMessage(height.ToString()); // Send as plain string
        }
        else
        {
            Debug.LogWarning($"Unexpected message: {eventArgs.Value}");
        }
    }
}
