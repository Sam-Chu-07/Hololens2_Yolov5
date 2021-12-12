using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Windows.WebCam;

using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.MixedReality.Toolkit.Utilities;

namespace HoloToolkit.Yolov5.ObjectDetection
{
	public class NetworkBehaviour : MonoBehaviour
	{
		const int BUFSIZE = (int)1e5;

		// Public fields
		public string IP = "10.10.10.164";
		public Text StatusBlock;
		

		// Private fields
		private float update;
		private bool _isRunning = false;
		
		private byte[] recvBuffer = new byte[BUFSIZE];
		private PeriodicTableLoader ObjectCreator;
		private PhotoCapture photoCaptureObject = null;
		private Resolution cameraResolution;
		private Socket sender;

		void Start()
		{         
			try
			{
				ObjectCreator = GetComponent<PeriodicTableLoader>();
				ObjectCreator.test();

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
				PhotoCapture.CreateAsync(false, OnPhotoCaptureCreated);
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

        
        void Update()
		{
			update += Time.deltaTime;
			
			if (_isRunning == true && update >= 0.5f && ObjectCreator.Lock == false)
			{
				update = 0;
				try
				{
					_isRunning = false;
					photoCaptureObject.TakePhotoAsync(OnCapturedPhotoToMemory);
				}
				catch (ArgumentNullException ane)
				{
					Debug.Log($"ArgumentNullException : {ane.ToString()}");
					StatusBlock.text = $"ArgumentNullException : {ane.ToString()}";
					_isRunning = false;
				}
				catch (SocketException se)
				{
					Debug.Log($"SocketException : {se.ToString()}");
					StatusBlock.text = $"SocketException : {se.ToString()}";
					_isRunning = false;
				}
				catch (Exception ex)
				{
					Debug.Log($"Error: {ex.Message}");
					StatusBlock.text = $"Error: {ex.Message}";
					_isRunning = false;
				}
			}
		}

		void OnPhotoCaptureCreated(PhotoCapture captureObject)
		{
			photoCaptureObject = captureObject;

			IEnumerable<Resolution> availableResolutions = PhotoCapture.SupportedResolutions;
			foreach (var res in availableResolutions)
			{
				Debug.Log("PhotoCapture Resolution: " + res.width + "x" + res.height);
			}

			cameraResolution = availableResolutions.OrderByDescending((res) => res.width * res.height).First();
			if(cameraResolution.width > 1280)
            {
				cameraResolution.width = 1504;
				cameraResolution.height = 846;
			}

			CameraParameters c = new CameraParameters();
			c.hologramOpacity = 0.0f;
			c.cameraResolutionWidth = cameraResolution.width;
			c.cameraResolutionHeight = cameraResolution.height;
			c.pixelFormat = CapturePixelFormat.BGRA32;

			captureObject.StartPhotoModeAsync(c, OnPhotoModeStarted);
		}

		private void OnPhotoModeStarted(PhotoCapture.PhotoCaptureResult result)
		{
			if (result.success)
			{
				Debug.Log("Start photo mode!");
				photoCaptureObject.TakePhotoAsync(OnCapturedPhotoToMemory);
			}
			else
			{ Debug.LogError("Unable to start photo mode!"); }
		}


		void OnCapturedPhotoToMemory(PhotoCapture.PhotoCaptureResult result, PhotoCaptureFrame photoCaptureFrame)
		{
			try
			{
				if (result.success)
				{
					var photoBuffer = new List<byte>();
					Texture2D texture = new Texture2D(cameraResolution.width, cameraResolution.height);

					photoCaptureFrame.UploadImageDataToTexture(texture);
					var byteArray = texture.EncodeToJPG();
					Debug.Log($"[### DEBUG ###] byteArray Size = {byteArray.Length}");

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
								Color = box["color"].ToObject<int>(),
								Note = box["note"].ToString(),
								X = x + width/2,
								Y = y + height/2,
							}); 
						}

						ObjectCreator.CreateElement(goodsInfo);
					}
					else
					{
						ObjectCreator.ClearElement();
					}

					Destroy(texture);
					photoCaptureFrame.Dispose();
				}
				else
				{
					Debug.Log($"[### DEBUG ###] PhotoCapture Result Fail!");
				}
			}
			catch(Exception ex)
			{
				Debug.Log($"Error in OnCapturedPhotoToMemory: {ex.Message}");
				throw;
			}

			_isRunning = true;
			//photoCaptureObject.StopPhotoModeAsync(OnStoppedPhotoMode);
		}

		void OnStoppedPhotoMode(PhotoCapture.PhotoCaptureResult result)
		{
			photoCaptureObject.Dispose();
			photoCaptureObject = null;
		}
	}
}
