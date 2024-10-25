using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using OpenCvSharp;
using DlibDotNet;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.ComponentModel;
using System.Windows;

namespace ATEDNIULI
{
    public partial class CameraMouse : INotifyPropertyChanged
    {
        public CameraMouse()
        {
            InitializeComponent();
            DataContext = this;

            PositionWindow();
        }

        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out Point point);

        // dlls kanan mouse variables
        [DllImport("user32.dll", SetLastError = true)]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;

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

        private BitmapSource _cameraImageSource;

        public BitmapSource CameraImageSource
        {
            get => _cameraImageSource;
            set
            {
                if (_cameraImageSource != value)
                {
                    _cameraImageSource = value;
                    OnPropertyChanged(nameof(CameraImageSource));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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

        // precision mode stuff
        private static int initialPrecisionRadius = 500; // Initial size of the precision area
        private static int reducedPrecisionRadius = 50; // Reduced size of the precision area
        private double precisionFactor = 1.0;
        private static DateTime precisionStartTime;
        private static DateTime transitionPrecision;
        private static DateTime reductionStartTime;
        private static bool newPrecisionArea = false;
        private static bool preparingPrecision = false;
        private static bool precisionActivated = false;
        private static bool isRadiusReduced = false; // To track if the radius has been reduced
        private static (int X, int Y) lastMousePosition;

        private BitmapSource ConvertMat;

        private void PositionWindow()
        {
            // Get the dimensions of the primary screen
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;

            // Set the window position to the top right corner
            this.Left = screenWidth - this.Width; // Set Left position
            this.Top = 0; // Set Top position
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

            ClosePreview();
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

                    Task.Run(() => PrecisionMode());

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

                            Dispatcher.Invoke(() =>
                            {
                                if (this.Visibility == Visibility.Collapsed)
                                {
                                    this.Visibility = Visibility.Visible;
                                }
                                CameraImageSource = ConvertMatToBitmapSource(frame);
                            });
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

        private BitmapSource ConvertMatToBitmapSource(Mat mat)
        {
            return BitmapSource.Create(
                mat.Width,
                mat.Height,
                96,
                96,
                PixelFormats.Bgr24,
                null,
                (IntPtr)mat.Data,                // Cast mat.Data to IntPtr
                (int)(mat.Step() * mat.Height),   // Explicitly cast long to int for buffer size
                (int)mat.Step()                   // Explicitly cast long to int for stride
            );
        }

        private void ClosePreview()
        {
            Dispatcher.Invoke(() =>
            {
                this.Visibility = Visibility.Collapsed;
            });
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
                //mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
            }
            else
            {
                //mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
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

            double smoothingFactor = 0.6; // Smoothing factor for low-pass filter

            for (int i = 0; i <= steps; i++)
            {
                int newX = 0;
                int newY = 0;

                // Start transitioning to precision mode if necessary
                if (preparingPrecision == true)
                {
                    // Only set transition time once when transitioning begins
                    if (transitionPrecision == default)
                    {
                        transitionPrecision = DateTime.Now; // Set when transitioning begins
                    }

                    double elapsedTime = (DateTime.Now - transitionPrecision).TotalMilliseconds;

                    if (elapsedTime <= 1500)
                    {
                        precisionFactor = 1.0 - (0.95 * (elapsedTime / 1500.0)); // Linear transition
                    }
                    else
                    {
                        precisionFactor = 0.1; // Cap at 10% after 2 seconds
                    }
                }

                // Calculate smoothed movement
                newX = (int)(startX + deltaX * (i / (double)steps) * (1 - smoothingFactor) * precisionFactor);
                newY = (int)(startY + deltaY * (i / (double)steps) * (1 - smoothingFactor) * precisionFactor);
                SetCursorPos(newX, newY);

                Thread.Sleep(duration / steps); // Wait between steps
            }
        }


        private void PrecisionMode()
        {
            int currentPrecisionRadius = 0;
            double distanceToTarget;

            while (true)
            {
                // Get the current mouse position
                var currentMousePosition = GetMousePosition();

                // Check if we are already in precision mode
                if (newPrecisionArea)
                {
                    // Use the last known position as the target when in precision mode
                    distanceToTarget = CalculateDistance(currentMousePosition, lastMousePosition);
                }
                else
                {
                    // If not in precision mode, use the current position as the new target
                    currentPrecisionRadius = initialPrecisionRadius;
                    lastMousePosition = currentMousePosition;
                    distanceToTarget = 0; // No distance calculation needed when just entering
                }

                // Check if the cursor is in the precision area
                if (distanceToTarget < currentPrecisionRadius)
                {
                    if (!newPrecisionArea)
                    {
                        precisionStartTime = DateTime.Now; // Start time for entering precision mode
                        newPrecisionArea = true;
                        Console.WriteLine("Updated Precision Area");
                    }
                    else
                    {
                        Console.WriteLine("Begining to stabilize");
                        if ((DateTime.Now - precisionStartTime).TotalMilliseconds >= 1500)
                        {
                            Console.WriteLine("Transitioning Precision Sensitivity");

                            preparingPrecision = true;
                        }
                    }
                }
                else
                {
                    if (newPrecisionArea)
                    {
                        newPrecisionArea = false; // Exit precision mode
                        preparingPrecision = false;
                        precisionActivated = false;
                        precisionFactor = 1.0;
                        isRadiusReduced = false; // Reset the radius reduction flag
                        reductionStartTime = default; // Reset the reduction start time
                        transitionPrecision = default;
                        lastMousePosition = (0, 0); // Optionally reset last mouse position
                        currentPrecisionRadius = initialPrecisionRadius; // Reset to initial radius
                        Console.WriteLine("Exited Precision Area.");
                    }
                }

                Thread.Sleep(100); // Delay to avoid excessive CPU usage
            }
        }

        private static (int X, int Y) GetMousePosition()
        {
            // Get the current mouse position using Cursor.Position
            var mousePosition = System.Windows.Forms.Cursor.Position;
            return (mousePosition.X, mousePosition.Y);
        }

        private static double CalculateDistance((int X, int Y) point1, (int X, int Y) point2)
        {
            return Math.Sqrt(Math.Pow(point1.X - point2.X, 2) + Math.Pow(point1.Y - point2.Y, 2));
        }

        private int Clamp(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int nIndex);
    }
}
