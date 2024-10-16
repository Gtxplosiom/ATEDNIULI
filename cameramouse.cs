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
                int webcamWidth = 640;
                int webcamHeight = 480;
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
                        using (var frame = new Mat())
                        {
                            capture.Read(frame);

                            if (frame.Empty())
                            {
                                Console.WriteLine("Error: Failed to grab frame.");
                                break;
                            }

                            Cv2.Resize(frame, frame, new OpenCvSharp.Size(webcamWidth, webcamHeight));
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

                                    // Reference point for comparison (nose point)
                                    var targetNosePoint = landmarksList[30];

                                    if (isFirstFrame)
                                    {
                                        // On the first frame, initialize the previousNosePosition with the target position
                                        previousNosePosition = targetNosePoint;
                                        isFirstFrame = false;
                                    }

                                    // Smooth the movement using the method similar to SmoothMoveTo
                                    int steps = 10; // Number of steps for smooth transition
                                    double smoothingFactor = 0.5; // Adjust smoothing factor

                                    // Gradually interpolate from previous nose position to the new target position
                                    for (int i = 0; i <= steps; i++)
                                    {
                                        // Interpolated X and Y positions
                                        int smoothedNoseX = (int)(previousNosePosition.X + (targetNosePoint.X - previousNosePosition.X) * (i / (double)steps) * (1 - smoothingFactor));
                                        int smoothedNoseY = (int)(previousNosePosition.Y + (targetNosePoint.Y - previousNosePosition.Y) * (i / (double)steps) * (1 - smoothingFactor));

                                        // Update the smoothed position of the nose
                                        var smoothedNosePoint = new Point(smoothedNoseX, smoothedNoseY);

                                        // Draw the smoothed nose point on the frame
                                        Cv2.Circle(frame, new OpenCvSharp.Point(smoothedNosePoint.X, smoothedNosePoint.Y), 4, Scalar.Red, -1);

                                        // Adjust the ROI based on the smoothed nose point's position
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

                                        // Update target position for the mouse if the smoothed nose is within the ROI
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

                                        // Update the previous nose position with the current smoothed position
                                        previousNosePosition = smoothedNosePoint;

                                        // Draw the updated ROI rectangle
                                        Cv2.Rectangle(frame, new OpenCvSharp.Rect(roiX, roiY, roiWidth, roiHeight), Scalar.Red, 2);
                                    }

                                    Cv2.ImShow("Camera", frame);
                                }

                                if (Cv2.WaitKey(1) == 27) // Exit if 'ESC' is pressed
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in CameraLoop: {ex.Message}");
                }
            }
        }



        private int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
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

        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int smIndex);
    }
}