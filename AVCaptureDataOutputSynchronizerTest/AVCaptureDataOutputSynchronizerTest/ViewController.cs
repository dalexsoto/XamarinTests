using System;
using System.Diagnostics;

using AVFoundation;
using CoreFoundation;
using CoreVideo;
using Foundation;
using UIKit;

namespace AVCaptureDataOutputSynchronizerTest {
	public partial class ViewController : UIViewController, IAVCaptureVideoDataOutputSampleBufferDelegate, IAVCaptureDepthDataOutputDelegate, IAVCaptureDataOutputSynchronizerDelegate {
		
		DispatchQueue dataOutputQueue = new DispatchQueue ("data queue");
		DispatchQueue sessionQueue = new DispatchQueue ("session queue");

		AVCaptureDevice device;
		AVCaptureSession session;
		AVCaptureDeviceInput videoDeviceInput;
		AVCaptureVideoDataOutput videoDataOutput;
		AVCapturePhotoOutput photoOutput;
		AVCaptureDepthDataOutput depthDataOutput;
		AVCaptureDataOutputSynchronizer outputSynchronizer;
		AVCaptureVideoPreviewLayer previewyLayer;

		bool setupResult;
		bool depthVisualizationEnabled;

		protected ViewController (IntPtr handle) : base (handle)
		{
			// Note: this .ctor should not contain any initialization logic.
		}

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();
			// Perform any additional setup after loading the view, typically from a nib.

			GoButton.TouchUpInside += async (sender, e) => {

				switch (AVCaptureDevice.GetAuthorizationStatus (AVAuthorizationMediaType.Video)) {
				case AVAuthorizationStatus.NotDetermined:
					sessionQueue.Suspend ();
					setupResult |= await AVCaptureDevice.RequestAccessForMediaTypeAsync (AVAuthorizationMediaType.Video);
					sessionQueue.Resume ();
					break;
				case AVAuthorizationStatus.Authorized:
					setupResult = true;
					break;
				}

				sessionQueue.DispatchAsync (ConfigureSession);
			};
		}

		void Go () {
			if (!setupResult) {
				Debug.WriteLine ("Something went wrong :'(");
				return;
			}

			var videoOrientation = videoDataOutput.ConnectionFromMediaType (AVMediaTypes.Video.GetConstant ())?.VideoOrientation;
			var videoDevicePosition = videoDeviceInput.Device.Position;

			Debug.WriteLine ("Session Started :D");
			InvokeOnMainThread (() => {
				previewyLayer = new AVCaptureVideoPreviewLayer (session);
				var layer = CameraPreview.Layer;
				previewyLayer.Frame = layer.Bounds;
				layer.AddSublayer (previewyLayer);
			});
			session.StartRunning ();
		}

		void ConfigureSession ()
		{
			if (!setupResult) {
				Debug.WriteLine ("Failed to get the right permissions.");
				return;
			}

			device = AVCaptureDevice.GetDefaultDevice (AVCaptureDeviceType.BuiltInDualCamera, AVMediaType.Video, AVCaptureDevicePosition.Back);
			if (device == null) {
				Debug.WriteLine ($"'{nameof (device)}' was null.");
				return;
			}

			videoDeviceInput = new AVCaptureDeviceInput (device, out var devInputErr);
			if (devInputErr != null)
				throw new NSErrorException (devInputErr);

			session = new AVCaptureSession ();
			session.BeginConfiguration ();
			session.SessionPreset = AVCaptureSession.PresetPhoto;

			if (!session.CanAddInput (videoDeviceInput)) {
				Debug.WriteLine ($"Could not add '{nameof (videoDeviceInput)}' to the session.");
				session.CommitConfiguration ();
				return;
			}
			session.AddInput (videoDeviceInput);

			videoDataOutput = new AVCaptureVideoDataOutput ();
			if (session.CanAddOutput (videoDataOutput)) {
				session.AddOutput (videoDataOutput);
				videoDataOutput.UncompressedVideoSetting = new AVVideoSettingsUncompressed {
					PixelFormatType = CVPixelFormatType.CV32BGRA
				};
				videoDataOutput.SetSampleBufferDelegateQueue (this, dataOutputQueue);
			} else {
				Debug.WriteLine ($"Could not add '{nameof (videoDataOutput)}' to the session.");
				session.CommitConfiguration ();
				return;
			}

			photoOutput = new AVCapturePhotoOutput ();
			if (session.CanAddOutput (photoOutput)) {
				session.AddOutput (photoOutput);
				photoOutput.IsHighResolutionCaptureEnabled = true;

				if (photoOutput.DepthDataDeliverySupported)
					photoOutput.DepthDataDeliveryEnabled = true;
				else
					Debug.WriteLine ("DepthDataDelivery is not supported in this device.");
			} else {
				Debug.WriteLine ($"Could not add '{nameof (photoOutput)}' to the session.");
				session.CommitConfiguration ();
				return;
			}

			depthDataOutput = new AVCaptureDepthDataOutput ();
			if (session.CanAddOutput (depthDataOutput)) {
				session.AddOutput (depthDataOutput);
				depthDataOutput.SetDelegate (this, dataOutputQueue);
				depthDataOutput.FilteringEnabled = false;

				var connection = depthDataOutput.ConnectionFromMediaType (AVMediaTypes.DepthData.GetConstant ());
				if (connection != null) {
					depthVisualizationEnabled = true;
					connection.Enabled = depthVisualizationEnabled;
				} else
					Debug.WriteLine ("No AVCaptureConnection for DepthData");
			} else {
				Debug.WriteLine ($"Could not add '{nameof (depthDataOutput)}' to the session.");
				session.CommitConfiguration ();
				return;
			}

			if (depthVisualizationEnabled) {
				outputSynchronizer = new AVCaptureDataOutputSynchronizer (new AVCaptureOutput [] { videoDataOutput, depthDataOutput });
				outputSynchronizer.SetDelegate (this, dataOutputQueue);
			} else {
				outputSynchronizer = null;
				Debug.WriteLine ($"'{nameof (outputSynchronizer)}' is set to 'null'.");
			}

			if (photoOutput.DepthDataDeliverySupported) {
				var frameDuration = device.ActiveDepthDataFormat?.VideoSupportedFrameRateRanges [0]?.MinFrameDuration;
				if (frameDuration.HasValue) {
					device.LockForConfiguration (out var lockerr);

					if (lockerr != null)
						throw new NSErrorException (lockerr);

					device.ActiveVideoMinFrameDuration = frameDuration.Value;
					device.UnlockForConfiguration ();
				}
			}

			session.CommitConfiguration ();
			setupResult = true;
			Go ();
		}

		public void DidOutputSynchronizedDataCollection (AVCaptureDataOutputSynchronizer synchronizer, AVCaptureSynchronizedDataCollection synchronizedDataCollection)
		{
			//if (synchronizedDataCollection.Count < 2) return;

			foreach (var dataOutput in synchronizedDataCollection) {
				if (synchronizedDataCollection.GetSynchronizedData (dataOutput) is AVCaptureSynchronizedDepthData depthData) {
					Debug.WriteLine (depthData);
					Debug.WriteLine ($"DepthDataWasDropped: {depthData.DepthDataWasDropped}");
				} else if (synchronizedDataCollection [dataOutput] is AVCaptureSynchronizedSampleBufferData bufferData) {
					Debug.WriteLine (bufferData);
					Debug.WriteLine ($"SampleBufferWasDropped: {bufferData.SampleBufferWasDropped}");
				}
			}

			Debug.WriteLine ($"Count: {synchronizedDataCollection.Count}");
			Debug.WriteLine ("");
		}
	}
}
