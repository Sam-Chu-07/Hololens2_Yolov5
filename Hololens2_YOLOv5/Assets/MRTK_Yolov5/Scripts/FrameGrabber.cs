using System;
using System.Threading.Tasks;
using UnityEngine;
using System.Collections.Generic;

#if ENABLE_WINMD_SUPPORT
using Windows.Media.Capture;
using Windows.Graphics.Imaging;
using Windows.Media.Devices.Core;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
#endif

public class FrameGrabber
{
	public struct Frame
	{
#if ENABLE_WINMD_SUPPORT
		public MediaFrameReference mediaFrameReference;
#endif
	}
	
#if ENABLE_WINMD_SUPPORT
	MediaCapture mediaCapture;
	MediaFrameSource mediaFrameSource;
	MediaFrameReader mediaFrameReader;

	private Frame _lastFrame;

	public Frame LastFrame
	{
		get
		{
			lock (this)
			{
				return _lastFrame;
			}
		}
		private set
		{
			lock (this)
			{
				_lastFrame = value;
			}
		}
	}

	private DateTime _lastFrameCapturedTimestamp = DateTime.MaxValue;

	public float ElapsedTimeSinceLastFrameCaptured
	{
		get
		{
			return (float)(DateTime.Now - DateTime.MinValue).TotalMilliseconds;
		}
	}

	public bool IsValid
	{
		get
		{
			return mediaFrameReader != null;
		}
	}

	private FrameGrabber(MediaCapture mediaCapture = null, MediaFrameSource mediaFrameSource = null, MediaFrameReader mediaFrameReader = null)
	{
		this.mediaCapture = mediaCapture;
		this.mediaFrameSource = mediaFrameSource;
		this.mediaFrameReader = mediaFrameReader;

		if (this.mediaFrameReader != null)
		{
			Debug.Log("+= setting");
			this.mediaFrameReader.FrameArrived += MediaFrameReader_FrameArrived;
		}
	}

	public static async Task<FrameGrabber> CreateAsync(uint width, uint height)
	{
		MediaCapture mediaCapture = null;
		MediaFrameReader mediaFrameReader = null;

		MediaFrameSourceGroup selectedGroup = null;
		MediaFrameSourceInfo selectedSourceInfo = null;

		// Pick first color source             
		var groups = await MediaFrameSourceGroup.FindAllAsync();
		foreach (MediaFrameSourceGroup sourceGroup in groups)
		{
			foreach (MediaFrameSourceInfo sourceInfo in sourceGroup.SourceInfos)
			{
				Debug.Log($"[### DEBUG ###] id = {sourceInfo.Id}");
				if (sourceInfo.SourceKind == MediaFrameSourceKind.Color)
				{
					selectedSourceInfo = sourceInfo;
					break;
				}
			}

			if (selectedSourceInfo != null)
			{
				selectedGroup = sourceGroup;
				break;
			}
		}

		// No valid camera was found. This will happen on the emulator.
		if (selectedGroup == null || selectedSourceInfo == null)
		{
			Debug.Log("Failed to find Group and SourceInfo");
			return new FrameGrabber();
		}

		// Create settings 
		var settings = new MediaCaptureInitializationSettings
		{
			SourceGroup = selectedGroup,

			// This media capture can share streaming with other apps.
			SharingMode = MediaCaptureSharingMode.SharedReadOnly,

			// Only stream video and don't initialize audio capture devices.
			StreamingCaptureMode = StreamingCaptureMode.Video,

			// Set to CPU to ensure frames always contain CPU SoftwareBitmap images
			// instead of preferring GPU D3DSurface images.
			MemoryPreference = MediaCaptureMemoryPreference.Cpu,
		};

		// Create and initilize capture device 
		mediaCapture = new MediaCapture();

		try
		{
			await mediaCapture.InitializeAsync(settings);
		}
		catch (Exception e)
		{
			Debug.Log($"Failed to initilise mediacaptrue {e.ToString()}");
			return new FrameGrabber();
		}

		
		Debug.Log($"[### DEBUG ###] mediaCapture.FrameSources Count = {mediaCapture.FrameSources.Count}");
		string ID = "";
		foreach (KeyValuePair<string, MediaFrameSource> kvp in mediaCapture.FrameSources)
		{
				Debug.Log($"[### DEBUG ###] Key = {kvp.Key}");
				ID = kvp.Key;
				break;
		}
		
		MediaFrameSource selectedSource = mediaCapture.FrameSources[ID];
		Debug.Log("OK!");
		var subtype = MediaEncodingSubtypes.Bgra8;
		BitmapSize outputSize = new BitmapSize { Width = width, Height = height };

		// create new frame reader 
		mediaFrameReader = await mediaCapture.CreateFrameReaderAsync(selectedSource, subtype, outputSize);

		MediaFrameReaderStartStatus status = await mediaFrameReader.StartAsync();

		if (status == MediaFrameReaderStartStatus.Success)
		{
			Debug.Log("MediaFrameReaderStartStatus == Success");
			return new FrameGrabber(mediaCapture, selectedSource, mediaFrameReader);
		}
		else
		{
			Debug.Log($"MediaFrameReaderStartStatus != Success; {status}");
			return new FrameGrabber();
		}
	}

	public async Task StopFrameGrabberAsync()
	{
        if (mediaCapture != null && mediaCapture.CameraStreamState != Windows.Media.Devices.CameraStreamState.Shutdown)
        {
			Debug.Log("Close Camera!");
			await mediaFrameReader.StopAsync();
            mediaFrameReader.Dispose();
            mediaCapture.Dispose();
            mediaCapture = null;
        }
	}

	void MediaFrameReader_FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
	{
		MediaFrameReference frame = sender.TryAcquireLatestFrame();
		if (frame != null){
			LastFrame = new Frame
			{mediaFrameReference = frame};
			_lastFrameCapturedTimestamp = DateTime.Now;
		}
	}
#endif
}
