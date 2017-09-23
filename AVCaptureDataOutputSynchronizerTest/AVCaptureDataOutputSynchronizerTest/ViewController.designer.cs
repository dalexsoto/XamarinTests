// WARNING
//
// This file has been generated automatically by Visual Studio from the outlets and
// actions declared in your storyboard file.
// Manual changes to this file will not be maintained.
//
using Foundation;
using System;
using System.CodeDom.Compiler;

namespace AVCaptureDataOutputSynchronizerTest
{
    [Register ("ViewController")]
    partial class ViewController
    {
        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UIView CameraPreview { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UIButton GoButton { get; set; }

        void ReleaseDesignerOutlets ()
        {
            if (CameraPreview != null) {
                CameraPreview.Dispose ();
                CameraPreview = null;
            }

            if (GoButton != null) {
                GoButton.Dispose ();
                GoButton = null;
            }
        }
    }
}