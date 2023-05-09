using Mediapipe.Unity.CoordinateSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BleMain;
using Stopwatch = System.Diagnostics.Stopwatch;
using System;

namespace Mediapipe.Unity.Tutorial
{
    public struct index
    {
        public float x;

        public float y;
    }
    public class Handtracking : MonoBehaviour
    {
        private index[] fingertop = new index[6];
        private index[] fingerbottom = new index[6];

        [SerializeField] private TextAsset _configAsset;
        [SerializeField] private RawImage _screen;
        [SerializeField] private int _width;
        [SerializeField] private int _height;
        [SerializeField] private int _fps;
        [SerializeField] private MultiHandLandmarkListAnnotationController _multiHandLandmarksAnnotationController;

        public Demo ble;
        private CalculatorGraph _graph;
        private ResourceManager _resourceManager;

        private WebCamTexture _webCamTexture;
        private Texture2D _inputTexture;
        private Color32[] _inputPixelData;
        private Texture2D _outputTexture;
        private Color32[] _outputPixelData;

        private IEnumerator Start()
        {
            if (WebCamTexture.devices.Length == 0)
            {
                throw new System.Exception("Web Camera devices are not found");
            }
            var webCamDevice = WebCamTexture.devices[0];
            _webCamTexture = new WebCamTexture(webCamDevice.name, _width, _height, _fps);
            _webCamTexture.Play();

            yield return new WaitUntil(() => _webCamTexture.width > 16);

            _screen.rectTransform.sizeDelta = new Vector2(_width, _height);

            _inputTexture = new Texture2D(_width, _height, TextureFormat.RGBA32, false);
            _inputPixelData = new Color32[_width * _height];
            _outputTexture = new Texture2D(_width, _height, TextureFormat.RGBA32, false);
            _outputPixelData = new Color32[_width * _height];

            _screen.texture = _outputTexture;

            _resourceManager = new StreamingAssetsResourceManager();
            yield return _resourceManager.PrepareAssetAsync("hand_landmark_full.bytes");
            yield return _resourceManager.PrepareAssetAsync("hand_landmark_lite.bytes");
            yield return _resourceManager.PrepareAssetAsync("hand_recrop.bytes");

            var stopwatch = new Stopwatch();

            _graph = new CalculatorGraph(_configAsset.text);
            var outputVideoStream = new OutputStream<ImageFramePacket, ImageFrame>(_graph, "output_video");
            var multiHandLandmarksStream = new OutputStream<NormalizedLandmarkListVectorPacket, List<NormalizedLandmarkList>>(_graph, "landmarks");
            outputVideoStream.StartPolling().AssertOk();
            multiHandLandmarksStream.StartPolling().AssertOk();
            _graph.StartRun().AssertOk();
            stopwatch.Start();

            var screenRect = _screen.GetComponent<RectTransform>().rect;

            while (true)
            {
                _inputTexture.SetPixels32(_webCamTexture.GetPixels32(_inputPixelData));
                var imageFrame = new ImageFrame(ImageFormat.Types.Format.Srgba, _width, _height, _width * 4, _inputTexture.GetRawTextureData<byte>());
                var currentTimestamp = stopwatch.ElapsedTicks / (System.TimeSpan.TicksPerMillisecond / 1000);
                _graph.AddPacketToInputStream("input_video", new ImageFramePacket(imageFrame, new Timestamp(currentTimestamp))).AssertOk();

                yield return new WaitForEndOfFrame();

                if (outputVideoStream.TryGetNext(out var outputVideo))
                {
                    if (outputVideo.TryReadPixelData(_outputPixelData))
                    {
                        _outputTexture.SetPixels32(_outputPixelData);
                        _outputTexture.Apply();
                    }
                }
                if (multiHandLandmarksStream.TryGetNext(out var value3))
                {
                    _multiHandLandmarksAnnotationController.DrawNow(value3);
                    if (value3 == null || value3.Count <= 0)
                    {
                        continue;
                    }
                    foreach (NormalizedLandmarkList item in value3)
                    {
                        for (int i = 0; i <= 20; i++)
                        {
                            NormalizedLandmark normalizedLandmark = item.Landmark[i];
                            if (i == 4 || i == 8 || i == 12 || i == 16 || i == 20)
                            {
                                fingertop[i / 4].x = normalizedLandmark.X;
                                fingertop[i / 4].y = normalizedLandmark.Y;
                            }
                            if (i == 3 || i == 5 || i == 9 || i == 13 || i == 17)
                            {
                                fingertop[i / 4 + 1].x = normalizedLandmark.X;
                                fingerbottom[i / 4 + 1].y = normalizedLandmark.Y;
                            }
                        }
                        if (fingertop[1].x > fingerbottom[1].x)
                        {
                            ble.Send("#001P2500T2000!");
                        }
                        if (fingertop[1].x < fingerbottom[1].x)
                        {
                            ble.Send("#001P0500T2000!");
                        }
                        MonoBehaviour.print($"{fingertop[1].x}" + $"{fingerbottom[1].x}");
                        for (int j = 4; j <= 5; j++)
                        {
                            if (fingertop[j].y > fingerbottom[j].y)
                            {
                                ble.Send("#00" + Convert.ToString(j) + "P2500T2000!");
                            }
                            if (fingertop[j].y < fingerbottom[j].y)
                            {
                                ble.Send("#00" + Convert.ToString(j) + "P0500T2000!");
                            }
                            MonoBehaviour.print($"{fingertop[j].y}" + $"{fingerbottom[j].y}");
                        }
                        for (int k = 2; k <= 3; k++)
                        {
                            if (fingertop[k].y > fingerbottom[k].y)
                            {
                                ble.Send("#00" + Convert.ToString(k) + "P0500T2000!");
                            }
                            if (fingertop[k].y < fingerbottom[k].y)
                            {
                                ble.Send("#00" + Convert.ToString(k) + "P2500T2000!");
                            }
                            MonoBehaviour.print($"{fingertop[k].y}" + $"{fingerbottom[k].y}");
                        }
                        yield return new WaitForSeconds(0.1f);
                    }
                }
                else
                {
                    _multiHandLandmarksAnnotationController.DrawNow(null);
                }
            }
        }

        private void OnDestroy()
        {
            if (_webCamTexture != null)
            {
                _webCamTexture.Stop();
            }

            if (_graph != null)
            {
                try
                {
                    _graph.CloseInputStream("input_video").AssertOk();
                    _graph.WaitUntilDone().AssertOk();
                }
                finally
                {

                    _graph.Dispose();
                }
            }
        }
    }
}