using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Network
{
    /// <summary>
    /// Serves two MJPEG streams over HTTP:
    ///   http://localhost:8766/raw     → ZED camera RGB feed
    ///   http://localhost:8766/overlay → Unity game camera with skeleton overlay
    ///
    /// Attach to the TelerehabNetwork GameObject alongside TelerehabWebSocketServer.
    /// Assign OverlayCamera to the camera that renders the skeleton view.
    /// Assign ZedRawTexture if you have a direct handle to the ZED RGB texture,
    /// otherwise the raw stream mirrors the overlay.
    /// </summary>
    [DisallowMultipleComponent]
    public class TelerehabVideoStreamer : MonoBehaviour
    {
        [Header("Cameras")]
        [Tooltip("The Unity camera that renders the skeleton/angle overlay")]
        public Camera OverlayCamera;

        [Tooltip("Optional: ZED raw RGB texture. Leave null to mirror the overlay.")]
        public Texture ZedRawTexture;

        [Header("Stream Settings")]
        public int  port          = 8766;
        public int  captureWidth  = 640;
        public int  captureHeight = 360;
        [Range(10, 90)]
        public int  jpegQuality   = 60;
        [Range(5, 30)]
        public int  targetFps     = 15;

        // ── Latest JPEG bytes (written on main thread, read on HTTP threads) ──
        private byte[] _overlayJpeg;
        private byte[] _rawJpeg;
        private readonly object _overlayLock = new();
        private readonly object _rawLock     = new();

        // ── Render textures ───────────────────────────────────────────────────
        private RenderTexture _overlayRT;
        private Texture2D     _overlayTex;
        private Texture2D     _rawTex;

        // ── HTTP server ───────────────────────────────────────────────────────
        private HttpListener         _listener;
        private CancellationTokenSource _cts;

        private void Start()
        {
            // Create render targets
            _overlayRT  = new RenderTexture(captureWidth, captureHeight, 24, RenderTextureFormat.ARGB32);
            _overlayTex = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
            _rawTex     = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);

            StartCoroutine(CaptureLoop());

            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
            try
            {
                _listener.Start();
                Debug.Log($"[VideoStreamer] MJPEG server on http://localhost:{port}/");
                Task.Run(() => ServeLoop(_cts.Token));
            }
            catch (Exception e)
            {
                Debug.LogError($"[VideoStreamer] Failed to start: {e.Message}");
            }
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _listener?.Stop();
            StopAllCoroutines();

            if (_overlayRT  != null) Destroy(_overlayRT);
            if (_overlayTex != null) Destroy(_overlayTex);
            if (_rawTex     != null) Destroy(_rawTex);
        }

        // ── Main-thread frame capture ─────────────────────────────────────────
        private IEnumerator CaptureLoop()
        {
            var wait = new WaitForSeconds(1f / targetFps);
            while (true)
            {
                yield return wait;
                yield return new WaitForEndOfFrame();
                CaptureOverlay();
                CaptureRaw();
            }
        }

        private void CaptureOverlay()
        {
            if (OverlayCamera == null) return;

            var prevTarget = OverlayCamera.targetTexture;
            OverlayCamera.targetTexture = _overlayRT;
            OverlayCamera.Render();
            OverlayCamera.targetTexture = prevTarget;

            RenderTexture.active = _overlayRT;
            _overlayTex.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
            _overlayTex.Apply();
            RenderTexture.active = null;

            byte[] jpeg = _overlayTex.EncodeToJPG(jpegQuality);
            lock (_overlayLock) { _overlayJpeg = jpeg; }
        }

        private void CaptureRaw()
        {
            // Use ZED raw texture if available, otherwise mirror overlay
            Texture src = ZedRawTexture;

            if (src == null)
            {
                // Mirror overlay
                lock (_rawLock)
                lock (_overlayLock)
                {
                    _rawJpeg = _overlayJpeg;
                }
                return;
            }

            // Blit ZED texture to a Texture2D
            RenderTexture tmp = RenderTexture.GetTemporary(captureWidth, captureHeight, 0);
            Graphics.Blit(src, tmp);
            RenderTexture.active = tmp;
            _rawTex.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
            _rawTex.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(tmp);

            byte[] jpeg = _rawTex.EncodeToJPG(jpegQuality);
            lock (_rawLock) { _rawJpeg = jpeg; }
        }

        // ── HTTP serve loop (background thread) ───────────────────────────────
        private async Task ServeLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener.IsListening)
            {
                try
                {
                    var ctx  = await _listener.GetContextAsync();
                    var path = ctx.Request.Url?.AbsolutePath ?? "/";
                    _ = Task.Run(() => StreamMjpeg(ctx, path, ct), ct);
                }
                catch (Exception e) when (!ct.IsCancellationRequested)
                {
                    Debug.LogWarning($"[VideoStreamer] {e.Message}");
                }
            }
        }

        private async Task StreamMjpeg(HttpListenerContext ctx, string path, CancellationToken ct)
        {
            const string boundary = "TelerehabFrame";

            ctx.Response.ContentType      = $"multipart/x-mixed-replace;boundary={boundary}";
            ctx.Response.StatusCode       = 200;
            ctx.Response.SendChunked      = true;
            ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");
            ctx.Response.AddHeader("Cache-Control", "no-cache, no-store");
            ctx.Response.AddHeader("Connection", "keep-alive");

            bool useRaw = path.Contains("raw");
            var  stream = ctx.Response.OutputStream;
            var  delay  = TimeSpan.FromMilliseconds(1000.0 / targetFps);

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    byte[] jpeg;
                    if (useRaw)
                        lock (_rawLock) { jpeg = _rawJpeg; }
                    else
                        lock (_overlayLock) { jpeg = _overlayJpeg; }

                    if (jpeg == null || jpeg.Length == 0)
                    {
                        await Task.Delay(100, ct);
                        continue;
                    }

                    // Write MJPEG part
                    string header = $"--{boundary}\r\nContent-Type: image/jpeg\r\nContent-Length: {jpeg.Length}\r\n\r\n";
                    byte[] headerBytes = Encoding.ASCII.GetBytes(header);

                    await stream.WriteAsync(headerBytes, 0, headerBytes.Length, ct);
                    await stream.WriteAsync(jpeg,        0, jpeg.Length,        ct);

                    byte[] crlf = Encoding.ASCII.GetBytes("\r\n");
                    await stream.WriteAsync(crlf, 0, crlf.Length, ct);
                    await stream.FlushAsync(ct);

                    await Task.Delay(delay, ct);
                }
            }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                Debug.Log($"[VideoStreamer] Client disconnected: {e.Message}");
            }
            finally
            {
                try { ctx.Response.Close(); } catch { }
            }
        }
    }
}
