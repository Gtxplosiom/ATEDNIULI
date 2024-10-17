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

        public struct TargetPosition // Define a struct to hold the target position
        {
            public int X { get; set; }
            public int Y { get; set; }

            public TargetPosition(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        private bool isRunning = false;
        private Thread cameraThread;
        private Thread mouseThread;
        private VideoCapture capture;

        // Store the target mouse position
        private TargetPosition targetPosition;
        private readonly object positionLock = new object(); // Lock object for thread safety

        // Declare webcamWidth and webcamHeight as class-level fields
        private int webcamWidth = 640;
        private int webcamHeight = 480;

        public void StartCameraMouse()
        {
            isRunning = true;

            cameraThread = new Thread(CameraLoop);
            cameraThread.IsBackground = true;
            cameraThread.Start();

            mouseThread = new Thread(MouseMovementLoop);
            mouseThread.IsBackground = true;
            mouseThread.Start();
        }

        public void StopCameraMouse()
        {
            isRunning = false;
            cameraThread?.Join(); // Wait for the camera thread to finish
            mouseThread?.Join();   // Wait for the mouse thread to finish

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

        private Point previousNosePosition = new Point(0, 0);
        private bool isFirstFrame = true; // To handle the very first frame where there's no previous data

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
                double roiPercentage = 0.05;
                int roiWidth = (int)(webcamWidth * roiPercentage);
                int roiHeight = (int)(webcamHeight * roiPercentage);

                // Set initial ROI position
                int roiX = (webcamWidth - roiWidth) / 2;
                int roiY = (webcamHeight - roiHeight) / 2;

                double scalingFactorX = screenWidth / (double)roiWidth;
                double scalingFactorY = screenHeight / (double)roiHeight;

                try
                {
                    while (isRunning)
                    {
                        using (var frame = new Mat()) // Ensure Mat is disposed properly
                        {
                            capture.Read(frame);

                            if (frame.Empty())
                            {
                                Console.WriteLine("Error: Failed to grab frame.");
                                break;
                            }

                            Cv2.Resize(frame, frame, new OpenCvSharp.Size(webcamWidth, webcamHeight));
                            Cv2.Flip(frame, frame, FlipMode.Y);

                            using (var gray = new Mat()) // Ensure Mat is disposed properly
                            {
                                Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);

                                // Load the image into Dlib
                                using (var dlibImage = Dlib.LoadImageData<byte>(gray.Data, (uint)gray.Width, (uint)gray.Height, (uint)gray.Width))
                                {
                                    DlibDotNet.Rectangle[] faces = detector.Operator(dlibImage);

                                    foreach (var face in faces)
                                    {
                                        var landmarks = predictor.Detect(dlibImage, face);
                                        var landmarksList = new List<Point>();
                                        for (int i = 0; i < (int)landmarks.Parts; i++)
                                        {
                                            landmarksList.Add(new Point(landmarks.GetPart((uint)i).X, landmarks.GetPart((uint)i).Y));
                                        }

                                        // Draw landmarks and process the nose position
                                        ProcessLandmarks(frame, landmarksList, ref roiX, ref roiY, roiWidth, roiHeight, scalingFactorX, scalingFactorY);
                                    }

                                    Cv2.ImShow("Camera", frame);
                                } // dlibImage is disposed here
                            } // gray Mat is disposed here
                        } // frame Mat is disposed here

                        if (Cv2.WaitKey(1) == 27) // Exit if 'ESC' is pressed
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in CameraLoop: {ex.Message}");
                }
            }
        }

        private void ProcessLandmarks(Mat frame, List<Point> landmarksList, ref int roiX, ref int roiY, int roiWidth, int roiHeight, double scalingFactorX, double scalingFactorY)
        {
            var targetNosePoint = landmarksList[30];

            if (isFirstFrame)
            {
                previousNosePosition = targetNosePoint;
                isFirstFrame = false;
            }

            int steps = 10;
            double smoothingFactor = 0.5;

            for (int i = 0; i <= steps; i++)
            {
                int smoothedNoseX = (int)(previousNosePosition.X + (targetNosePoint.X - previousNosePosition.X) * (i / (double)steps) * (1 - smoothingFactor));
                int smoothedNoseY = (int)(previousNosePosition.Y + (targetNosePoint.Y - previousNosePosition.Y) * (i / (double)steps) * (1 - smoothingFactor));

                var smoothedNosePoint = new Point(smoothedNoseX, smoothedNoseY);
                Cv2.Circle(frame, new OpenCvSharp.Point(smoothedNosePoint.X, smoothedNosePoint.Y), 4, Scalar.Red, -1);

                UpdateRoi(smoothedNosePoint, ref roiX, ref roiY, roiWidth, roiHeight);
                UpdateTargetPosition(smoothedNosePoint, roiX, roiY, roiWidth, roiHeight, scalingFactorX, scalingFactorY);

                previousNosePosition = smoothedNosePoint;
            }

            Cv2.Rectangle(frame, new OpenCvSharp.Rect(roiX, roiY, roiWidth, roiHeight), Scalar.Red, 2);
        }

        private void UpdateRoi(Point smoothedNosePoint, ref int roiX, ref int roiY, int roiWidth, int roiHeight)
        {
            int edgeThreshold = 0;

            if (smoothedNosePoint.X < roiX + edgeThreshold)
            {
                roiX = Clamp(roiX - (roiX + edgeThreshold - smoothedNosePoint.X), 0, webcamWidth - roiWidth);
            }
            else if (smoothedNosePoint.X > roiX + roiWidth - edgeThreshold)
            {
                roiX = Clamp(roiX + (smoothedNosePoint.X - (roiX + roiWidth - edgeThreshold)), 0, webcamWidth - roiWidth);
            }

            if (smoothedNosePoint.Y < roiY + edgeThreshold)
            {
                roiY = Clamp(roiY - (roiY + edgeThreshold - smoothedNosePoint.Y), 0, webcamHeight - roiHeight);
            }
            else if (smoothedNosePoint.Y > roiY + roiHeight - edgeThreshold)
            {
                roiY = Clamp(roiY + (smoothedNosePoint.Y - (roiY + roiHeight - edgeThreshold)), 0, webcamHeight - roiHeight);
            }
        }

        private void UpdateTargetPosition(Point smoothedNosePoint, int roiX, int roiY, int roiWidth, int roiHeight, double scalingFactorX, double scalingFactorY)
        {
            if (smoothedNosePoint.X >= roiX && smoothedNosePoint.X <= roiX + roiWidth &&
                smoothedNosePoint.Y >= roiY && smoothedNosePoint.Y <= roiY + roiHeight)
            {
                lock (positionLock)
                {
                    targetPosition = new TargetPosition(
                        (int)((smoothedNosePoint.X - roiX) * scalingFactorX),
                        (int)((smoothedNosePoint.Y - roiY) * scalingFactorY)
                    );
                }
            }
        }

        private void MouseMovementLoop()
        {
            while (isRunning)
            {
                // Get the current target position
                TargetPosition currentTargetPosition;
                lock (positionLock) // Lock access to targetPosition
                {
                    currentTargetPosition = targetPosition;
                }
                SmoothMoveTo(currentTargetPosition.X, currentTargetPosition.Y);
                Thread.Sleep(50); // Adjust the delay to manage the mouse movement frequency
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

        private int Clamp(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int nIndex);
    }
}
