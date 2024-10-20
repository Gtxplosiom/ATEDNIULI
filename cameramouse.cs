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

        // dlls kanan mouse variables
        [DllImport("user32.dll", SetLastError = true)]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_SHOWWINDOW = 0x0040;

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

        private void SetWindowAlwaysOnTopAndPosition(string windowName, int screenWidth, int screenHeight)
        {
            // Retrieve the window handle for the OpenCV window
            IntPtr hWnd = Cv2.GetWindowHandle(windowName);

            // Define window size and position for the right side of the screen
            int windowWidth = 640;  // Visually reduce width by 50%
            int windowHeight = 480; // Visually reduce height by 50%
            int posX = screenWidth - windowWidth; // X position for right alignment
            int posY = (screenHeight - windowHeight) / 2; // Centered vertically

            // Resize the window (display size only, not affecting the resolution)
            Cv2.ResizeWindow(windowName, windowWidth, windowHeight);

            // Set the window to always be on top and position it on the right side
            SetWindowPos(hWnd, HWND_TOPMOST, posX, posY, windowWidth, windowHeight, SWP_SHOWWINDOW);
        }

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

            // Signal threads to stop and wait for them to finish
            cameraThread?.Join(500); // Give threads a maximum of 500ms to finish gracefully
            mouseThread?.Join(500);

            Cv2.DestroyAllWindows();
            capture?.Release();
            capture = null;
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
                    var frame = new Mat();
                    var gray = new Mat();

                    while (isRunning)
                    {
                        capture.Read(frame);

                        if (frame.Empty())
                        {
                            Console.WriteLine("Error: Failed to grab frame.");
                            break;
                        }

                        Cv2.Resize(frame, frame, new OpenCvSharp.Size(webcamWidth, webcamHeight));
                        Cv2.Flip(frame, frame, FlipMode.Y);

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
                            SetWindowAlwaysOnTopAndPosition("Camera", screenWidth, screenHeight);
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

        private void ProcessLandmarks(Mat frame, List<Point> landmarksList, ref int roiX, ref int roiY, int roiWidth, int roiHeight, double scalingFactorX, double scalingFactorY)
        {
            // Step 1: Define key landmarks
            var targetNosePoint = landmarksList[30];
            var leftBrowPoint = landmarksList[19];
            var rightBrowPoint = landmarksList[24];
            var leftUpperEyelidPoint = landmarksList[37];
            var rightUpperEyelidPoint = landmarksList[44];

            // Step 2: Check for first frame
            if (isFirstFrame)
            {
                previousNosePosition = targetNosePoint;
                isFirstFrame = false;
            }

            int steps = 10;
            double smoothingFactor = 0.5;

            // Step 3: Process smoothing for nose movement
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

            // Step 4: Detect if brows are raised
            // Calculate distances between the brow and the upper eyelid for both left and right sides
            double leftBrowToEyelidDist = Math.Abs(leftBrowPoint.Y - leftUpperEyelidPoint.Y);
            double rightBrowToEyelidDist = Math.Abs(rightBrowPoint.Y - rightUpperEyelidPoint.Y);

            // Define a threshold for determining if the brow is raised (you can tweak this value based on your tests)
            double browRaiseThreshold = 35.0;

            bool isLeftBrowRaised = leftBrowToEyelidDist > browRaiseThreshold;
            bool isRightBrowRaised = rightBrowToEyelidDist > browRaiseThreshold;

            // Step 5: Draw rectangles or markers to visualize the brow status
            Cv2.Rectangle(frame, new OpenCvSharp.Rect(roiX, roiY, roiWidth, roiHeight), Scalar.Red, 2);

            Cv2.Circle(frame, new OpenCvSharp.Point(leftBrowPoint.X, leftBrowPoint.Y), 3, Scalar.Blue, -1); // Left Brow Point
            Cv2.Circle(frame, new OpenCvSharp.Point(rightBrowPoint.X, rightBrowPoint.Y), 3, Scalar.Blue, -1); // Right Brow Point
            Cv2.Circle(frame, new OpenCvSharp.Point(leftUpperEyelidPoint.X, leftUpperEyelidPoint.Y), 3, Scalar.Green, -1); // Left Upper Eyelid Point
            Cv2.Circle(frame, new OpenCvSharp.Point(rightUpperEyelidPoint.X, rightUpperEyelidPoint.Y), 3, Scalar.Green, -1); // Right Upper Eyelid Point

            // Optionally, you can draw lines to better visualize the connections
            Cv2.Line(frame, new OpenCvSharp.Point(leftBrowPoint.X, leftBrowPoint.Y), new OpenCvSharp.Point(leftUpperEyelidPoint.X, leftUpperEyelidPoint.Y), Scalar.White, 1); // Left side connection
            Cv2.Line(frame, new OpenCvSharp.Point(rightBrowPoint.X, rightBrowPoint.Y), new OpenCvSharp.Point(rightUpperEyelidPoint.X, rightUpperEyelidPoint.Y), Scalar.White, 1); // Right side connection

            // Step 6: Display if the brows are raised
            if (isLeftBrowRaised)
            {
                Cv2.PutText(frame, "Left Brow Raised", new OpenCvSharp.Point(10, 30), HersheyFonts.HersheySimplex, 1, Scalar.Green, 2);
            }

            if (isRightBrowRaised)
            {
                Cv2.PutText(frame, "Right Brow Raised", new OpenCvSharp.Point(10, 60), HersheyFonts.HersheySimplex, 1, Scalar.Green, 2);
            }

            if (isLeftBrowRaised || isRightBrowRaised)
            {
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
            }
            else
            {
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
            }
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
