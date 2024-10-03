using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using OpenCvSharp;
using DlibDotNet;

namespace ATEDNIULI
{
    internal class cameramouse
    {
        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out Point point);

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int X;
            public int Y;

            public Point(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        private volatile bool isRunning = false;
        private Thread cameraThread;
        private VideoCapture capture;

        public void StartCameraMouse()
        {
            isRunning = true;
            cameraThread = new Thread(CameraLoop);
            cameraThread.IsBackground = true;
            cameraThread.Start();
        }

        public void StopCameraMouse()
        {
            isRunning = false;
            cameraThread?.Join(); // Wait for the thread to finish

            Cv2.DestroyAllWindows(); // Close any OpenCV windows
            capture?.Release(); // Release the webcam if it's still open
            capture = null; // Clear reference to avoid using old instance
        }

        private bool IsCameraAvailable(int index)
        {
            using (var testCapture = new VideoCapture(index))
            {
                return testCapture.IsOpened();
            }
        }

        private void CameraLoop()
        {
            using (var detector = Dlib.GetFrontalFaceDetector())
            using (var predictor = ShapePredictor.Deserialize("assets/models/shape_predictor_68_face_landmarks.dat"))
            {
                capture = IsCameraAvailable(1) ? new VideoCapture(1) : new VideoCapture(0);

                if (!capture.IsOpened())
                {
                    Console.WriteLine("Error: Could not open webcam.");
                    return;
                }

                int screenWidth = GetSystemMetrics(0);
                int screenHeight = GetSystemMetrics(1);
                int webcamWidth = 640;
                int webcamHeight = 480;
                double roiPercentage = 0.20;
                int roiWidth = (int)(webcamWidth * roiPercentage);
                int roiHeight = (int)(webcamHeight * roiPercentage);
                int roiX = (webcamWidth - roiWidth) / 2;
                int roiY = (webcamHeight - roiHeight) / 2;

                double scalingFactorX = screenWidth / (double)roiWidth;
                double scalingFactorY = screenHeight / (double)roiHeight;

                try
                {
                    while (isRunning)
                    {
                        using (var frame = new Mat())
                        {
                            capture.Read(frame);

                            if (frame.Empty())
                            {
                                Console.WriteLine("Error: Failed to grab frame.");
                                break;
                            }

                            Cv2.Flip(frame, frame, FlipMode.Y);

                            using (var gray = new Mat())
                            {
                                Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
                                var dlibImage = Dlib.LoadImageData<byte>(gray.Data, (uint)gray.Width, (uint)gray.Height, (uint)gray.Width);
                                DlibDotNet.Rectangle[] faces = detector.Operator(dlibImage);

                                foreach (var face in faces)
                                {
                                    var landmarks = predictor.Detect(dlibImage, face);
                                    var landmarksList = new List<Point>();
                                    for (int i = 0; i < (int)landmarks.Parts; i++)
                                    {
                                        landmarksList.Add(new Point(landmarks.GetPart((uint)i).X, landmarks.GetPart((uint)i).Y));
                                    }

                                    // Draw nose point
                                    var nosePoint = landmarksList[30];
                                    Cv2.Circle(frame, new OpenCvSharp.Point(nosePoint.X, nosePoint.Y), 4, Scalar.Red, -1);

                                    // Move cursor based on nose position
                                    int targetX = (int)((nosePoint.X - roiX) * scalingFactorX);
                                    int targetY = (int)((nosePoint.Y - roiY) * scalingFactorY);
                                    SmoothMoveTo(targetX, targetY);
                                }
                                Cv2.Rectangle(frame, new OpenCvSharp.Point(roiX, roiY), new OpenCvSharp.Point(roiX + roiWidth, roiY + roiHeight), new Scalar(0, 255, 0), 2);
                            }

                            Cv2.ImShow("preview", frame);

                            // Break the loop if 'q' is pressed
                            if (Cv2.WaitKey(1) == 'q')
                            {
                                StopCameraMouse();
                            }
                        }
                    }
                }
                finally
                {
                    Cv2.DestroyAllWindows();
                    capture?.Release();
                }
            }
        }

        private void SmoothMoveTo(int targetX, int targetY, int duration = 100, int steps = 10)
        {
            GetCursorPos(out Point currentPos);
            int startX = currentPos.X;
            int startY = currentPos.Y;

            double deltaX = targetX - startX;
            double deltaY = targetY - startY;

            // Use a low-pass filter to smooth the movement
            double smoothingFactor = 0.5;

            for (int i = 0; i <= steps; i++)
            {
                int newX = (int)(startX + deltaX * (i / (double)steps) * (1 - smoothingFactor));
                int newY = (int)(startY + deltaY * (i / (double)steps) * (1 - smoothingFactor));
                SetCursorPos(newX, newY);
                Thread.Sleep(duration / steps); // Wait between steps
            }
        }

        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int smIndex);
    }
}
