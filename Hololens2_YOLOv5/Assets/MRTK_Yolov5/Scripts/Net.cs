using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.MixedReality.Toolkit.Utilities;

#if ENABLE_WINMD_SUPPORT
using Windows.Media;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
using Windows.Security.Cryptography;
#endif


namespace HoloToolkit.Yolov5.ObjectDetection
{
    public class Net : MonoBehaviour
    {
        const int BUFSIZE = (int)1e5;

        // Public fields
        public string IP = "10.10.10.164";
        public Text StatusBlock;


        // Private fields
        private float update;
        private bool _isRunning = false;

        private FrameGrabber frameGrabber;
        private byte[] recvBuffer = new byte[BUFSIZE];
        private PeriodicTableLoader ObjectCreator;
        private Socket sender;

        async void Start()
        {
            try
            {
                ObjectCreator = GetComponent<PeriodicTableLoader>();
                ObjectCreator.test();
                StatusBlock.text = "Running";

                // Run processing loop in separate parallel Task, get the latest frame
                try
                {
                    IPAddress ipAddress = IPAddress.Parse(IP);
                    IPEndPoint remoteEP = new IPEndPoint(ipAddress, 11000);
                    Debug.Log($"[### DEBUG ###] Client IP : {ipAddress}");

                    // Create a TCP/IP  socket.
                    sender = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    sender.Connect(remoteEP);
                    Debug.Log($"[### DEBUG ###] Socket connected to {sender.RemoteEndPoint.ToString()}");
                }
                catch (Exception ex)
                {
                    StatusBlock.text = $"Connect to Server Error: {ex.Message}";
                    Debug.Log($"Connect to Server Error: {ex.Message}");
                }


                // Start Photo Capture and send it to Server
#if ENABLE_WINMD_SUPPORT
                frameGrabber = await FrameGrabber.CreateAsync(1504, 846);
#endif
                _isRunning = true;
                Debug.Log("Start Capture");
            }
            catch (Exception ex)
            {
                StatusBlock.text = $"Error init: {ex.Message}";
                Debug.LogError($"[### ERROR ###] Failed to start model inference: {ex}");
            }
        }

        private void OnDestroy()
        {
            _isRunning = false;
        }


        async void Update()
        {
            update += Time.deltaTime;
            StatusBlock.text = "     " + _isRunning;
            if (_isRunning == true && update >= 1.0f && ObjectCreator.Lock == false)
            {
                update = 0;
                _isRunning = false;
#if ENABLE_WINMD_SUPPORT
                var lastFrame = frameGrabber.LastFrame;
                if (lastFrame.mediaFrameReference != null)
                {
                    try
                    {
                        using (var videoFrame = lastFrame.mediaFrameReference.VideoMediaFrame.GetVideoFrame())
                        {
                            if (videoFrame != null && videoFrame.SoftwareBitmap != null)
                            {
                                var byteArray = await toByteArray(videoFrame.SoftwareBitmap);
                                Debug.Log($"[### DEBUG ###] byteArray Size = {byteArray.Length}");
                                ObjectPrediction(byteArray);
                            }
                            else
                            {   Debug.Log("videoFrame or SoftwareBitmap = null");}
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Log($"[### Deebug ###] Update Error: {ex.Message}");
                        //_isRunning = false;
                        //return;
                    }
                }
                else
                {   Debug.Log("lastFrame.mediaFrameReference = null");}
#endif
                _isRunning = true;
            }
        }

#if ENABLE_WINMD_SUPPORT
        public async Task<byte[]> toByteArray(SoftwareBitmap sftBitmap_c)
        {
            SoftwareBitmap sftBitmap = SoftwareBitmap.Convert(sftBitmap_c, BitmapPixelFormat.Bgra8);
            InMemoryRandomAccessStream  mss =  new InMemoryRandomAccessStream();
            Windows.Graphics.Imaging.BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, mss);
        
            encoder.SetSoftwareBitmap(sftBitmap);
            await encoder.FlushAsync();

            IBuffer bufferr = new Windows.Storage.Streams.Buffer((uint)mss.Size);
            await mss.ReadAsync(bufferr, (uint)mss.Size, InputStreamOptions.None);
        
            DataReader dataReader = DataReader.FromBuffer(bufferr);
            byte[] bytes = new byte[bufferr.Length];
            dataReader.ReadBytes(bytes);
            return bytes;
        }
#endif
        void ObjectPrediction(byte[] byteArray)
        {
            try
            {
                int bytesSent = sender.Send(byteArray);
                int bytesRec = sender.Receive(recvBuffer);

                var response = Encoding.UTF8.GetString(recvBuffer, 0, bytesRec);
                if (response == "Error")
                    return;

                JObject results = JObject.Parse(response);
                if (results != null && results["BoxNum"].ToObject<int>() > 0)
                {
                    List<GoodsData> goodsInfo = new List<GoodsData>();
                    foreach (var box in results["Bbox"])
                    {
                        var x = box["x"].ToObject<float>();
                        var y = box["y"].ToObject<float>();
                        var width = box["width"].ToObject<float>();
                        var height = box["height"].ToObject<float>();
                        var confidence = box["confidence"].ToObject<float>();
                        var classs_Eng = box["class"].ToString();

                        goodsInfo.Add(new GoodsData
                        {
                            Name = box["name"].ToString(),
                            Symbol = box["symbol"].ToString(),
                            Price = box["price"].ToObject<int>(),
                            Manufacturer = box["manufacturer"].ToString(),
                            Ingredients = box["ingredient"].ToString(),
                            Calorie = box["calorie"].ToObject<float>(),
                            Note = box["note"].ToString(),
                            X = x + width / 2,
                            Y = y + height / 2,
                        });
                    }

                    Debug.Log("5");
                    ObjectCreator.CreateElement(goodsInfo);
                }
                else
                {
                    Debug.Log("7");
                    ObjectCreator.ClearElement();
                }
            }
            catch (ArgumentNullException ane)
            {
                Debug.Log($"[### Debug ###] ArgumentNullException : {ane.ToString()}");
                throw;
            }
            catch (SocketException se)
            {
                Debug.Log($" [### Debug ###] SocketException : {se.ToString()}");
                throw;
            }
            catch (Exception ex)
            {
                Debug.Log($"[### Debug ###] Error: {ex.Message} ");
                throw;
            }
            Debug.Log("9");
        }
    }
}
