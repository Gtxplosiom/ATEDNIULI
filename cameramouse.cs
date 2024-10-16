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

                                    // Reference point for comparison (nose point)
                                    var nosePoint = landmarksList[30];

                                    // Measure distance between the nose and eyebrow points
                                    bool eyebrowsRaised = false;
                                    double threshold = 40.0; // Adjust threshold for raised eyebrows detection
                                    double leftEyelidToBrowDistance = 0.0;
                                    double rightEyelidToBrowDistance = 0.0;

                                    // Draw left eyebrow and calculate distance to upper eyelid
                                    for (int i = 17; i <= 21; i++)
                                    {
                                        var eyebrowPoint = landmarksList[i];
                                        Cv2.Circle(frame, new OpenCvSharp.Point(eyebrowPoint.X, eyebrowPoint.Y), 3, Scalar.Blue, -1); // Draw blue dots for left eyebrow
                                    }

                                    // Draw right eyebrow and calculate distance to upper eyelid
                                    for (int i = 22; i <= 26; i++)
                                    {
                                        var eyebrowPoint = landmarksList[i];
                                        Cv2.Circle(frame, new OpenCvSharp.Point(eyebrowPoint.X, eyebrowPoint.Y), 3, Scalar.Green, -1); // Draw green dots for right eyebrow
                                    }

                                    // Calculate left upper eyelid to brow distance
                                    for (int i = 37; i <= 38; i++)
                                    {
                                        var leftUpperEyelid = landmarksList[i];
                                        Cv2.Circle(frame, new OpenCvSharp.Point(leftUpperEyelid.X, leftUpperEyelid.Y), 3, Scalar.Green, -1);
                                        for (int j = 17; j <= 21; j++)
                                        {
                                            var leftEyebrowPoint = landmarksList[j];
                                            leftEyelidToBrowDistance += Math.Abs(leftUpperEyelid.Y - leftEyebrowPoint.Y); // Distance between upper eyelid and eyebrow
                                        }
                                    }
                                    leftEyelidToBrowDistance /= 5; // Average distance for left side

                                    // Calculate right upper eyelid to brow distance
                                    for (int i = 43; i <= 44; i++)
                                    {
                                        var rightUpperEyelid = landmarksList[i];
                                        Cv2.Circle(frame, new OpenCvSharp.Point(rightUpperEyelid.X, rightUpperEyelid.Y), 3, Scalar.Green, -1);
                                        for (int j = 22; j <= 26; j++)
                                        {
                                            var rightEyebrowPoint = landmarksList[j];
                                            rightEyelidToBrowDistance += Math.Abs(rightUpperEyelid.Y - rightEyebrowPoint.Y); // Distance between upper eyelid and eyebrow
                                        }
                                    }
                                    rightEyelidToBrowDistance /= 5; // Average distance for right side

                                    // Update the distance text on the frame
                                    Cv2.PutText(frame, $"Eyelid to brow distance - left: {leftEyelidToBrowDistance} right: {rightEyelidToBrowDistance}", new OpenCvSharp.Point(10, 20), HersheyFonts.HersheySimplex, 1, Scalar.White, 2);

                                    // Check if the eyelid-to-eyebrow distances exceed the threshold
                                    eyebrowsRaised = leftEyelidToBrowDistance > threshold || rightEyelidToBrowDistance > threshold;

                                    // Indicate the result visually
                                    if (eyebrowsRaised)
                                    {
                                        Cv2.PutText(frame, "Eyebrows Raised", new OpenCvSharp.Point(10, 30), HersheyFonts.HersheySimplex, 1, Scalar.White, 2);
                                    }
                                    else
                                    {
                                        Cv2.PutText(frame, "Eyebrows Normal", new OpenCvSharp.Point(10, 30), HersheyFonts.HersheySimplex, 1, Scalar.White, 2);
                                    }

                                    // Draw nose point
                                    Cv2.Circle(frame, new OpenCvSharp.Point(nosePoint.X, nosePoint.Y), 4, Scalar.Red, -1);

                                    // Update target position for the mouse
                                    lock (positionLock) // Lock access to targetPosition
                                    {
                                        targetPosition = new TargetPosition(
                                            (int)((nosePoint.X - roiX) * scalingFactorX),
                                            (int)((nosePoint.Y - roiY) * scalingFactorY)
                                        );
                                    }
                                }
                                Cv2.Rectangle(frame, new OpenCvSharp.Rect(roiX, roiY, roiWidth, roiHeight), Scalar.Red, 2); // Draw ROI rectangle
                            }
                            Cv2.ImShow("Camera", frame);
                        }

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
