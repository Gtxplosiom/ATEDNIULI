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
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Drawing;

namespace ATEDNIULI
{
    public partial class CameraMouse : INotifyPropertyChanged
    {
        public CameraMouse()
        {
            InitializeComponent();
            DataContext = this;

            // Initialize the timer
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(3); // Set the duration (3 seconds)
            timer.Tick += Timer_Tick;

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
        private static int initialPrecisionRadius = 150; // Initial size of the precision area
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

        private System.Windows.Rect collapsedArea;
        private System.Windows.Window invisibleWindow;

        private DispatcherTimer timer;
        private double originalLeft;
        private double originalTop;

        private BitmapSource ConvertMat;

        private void PositionWindow()
        {
            // Get the dimensions of the primary screen
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;

            // Set the window position to the top right corner
            this.Left = screenWidth - this.Width; // Set Left position
            this.Top = 0; // Set Top position

            originalLeft = this.Left;
            originalTop = this.Top;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // Stop the timer
            timer.Stop();

            // Return the window to its original position
            this.Left = originalLeft;
            this.Top = originalTop;
        }

        public void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {

            // Get the dimensions of the primary screen
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;

            // Set the window position to the top right corner
            this.Left = screenWidth; // Set Left position
            this.Top = 0; // Set Top position

            timer.Start();
        }

        private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {

        }

        public void StartCameraMouse()
        {
            isRunning = true;

            InitializeKalmanFilter();

            baselineHorizontalDistance = 0;
            baselineVerticalDistance = 0;
            isDefaultMouthSet = false;

            cameraThread = new Thread(CameraLoop);
            cameraThread.IsBackground = true;
            cameraThread.Start();
        }

        public void StopCameraMouse()
        {
            isRunning = false;

            baselineHorizontalDistance = 0;
            baselineVerticalDistance = 0;
            isDefaultMouthSet = false;

            // Signal threads to stop and wait for them to finish
            cameraThread?.Join(500); // Give threads a maximum of 500ms to finish gracefully
            mouseThread?.Join(500);

            Cv2.DestroyAllWindows();

            ClosePreview();
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            // Immediately deactivate the window to prevent it from getting focus
            this.Hide();
            this.Show();
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

                int roiX = (webcamWidth - roiWidth) / 2;
                int roiY = (webcamHeight - roiHeight) / 2;

                double scalingFactorX = screenWidth / (double)roiWidth;
                double scalingFactorY = screenHeight / (double)roiHeight;

                var frame = new Mat();
                var gray = new Mat();
                var landmarksList = new List<Point>();

                try
                {
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

                        try
                        {
                            using (var dlibImage = Dlib.LoadImageData<byte>(gray.Data, (uint)gray.Width, (uint)gray.Height, (uint)gray.Width))
                            {
                                var faces = detector.Operator(dlibImage);

                                foreach (var face in faces)
                                {
                                    try
                                    {
                                        var landmarks = predictor.Detect(dlibImage, face);
                                        landmarksList.Clear(); // Clear for reuse
                                        for (int i = 0; i < (int)landmarks.Parts; i++)
                                        {
                                            landmarksList.Add(new Point(landmarks.GetPart((uint)i).X, landmarks.GetPart((uint)i).Y));
                                        }

                                        ProcessLandmarks(frame, landmarksList, ref roiX, ref roiY, roiWidth, roiHeight, scalingFactorX, scalingFactorY);
                                    }
                                    catch (Exception landmarkEx)
                                    {
                                        Console.WriteLine($"Error processing landmarks: {landmarkEx.Message}");
                                    }
                                }
                            }
                        }
                        catch (Exception dlibEx)
                        {
                            Console.WriteLine($"Error loading Dlib image: {dlibEx.Message}");
                        }

                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (this.Visibility == Visibility.Collapsed)
                            {
                                this.Visibility = Visibility.Visible;
                            }

                            CameraImageSource = ConvertMatToBitmapSource(frame);
                        }));

                        if (Cv2.WaitKey(1) == 27)
                        {
                            break;
                        }

                        // Optional: Trigger garbage collection to release memory periodically (for debugging only)
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in CameraLoop: {ex.Message}");
                }
                finally
                {
                    capture?.Release();
                    frame?.Dispose();
                    gray?.Dispose();
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

        private double baselineHorizontalDistance = 0;
        private double baselineVerticalDistance = 0;
        private bool isDefaultMouthSet = false; // Flag to track if the neutral expression is captured
        private Point currentMousePosition = new Point(0, 0);

        private void ProcessLandmarks(Mat frame, List<Point> landmarksList, ref int roiX, ref int roiY, int roiWidth, int roiHeight, double scalingFactorX, double scalingFactorY)
        {
            var screenWidth = (int)SystemParameters.PrimaryScreenWidth;
            var screenHeight = (int)SystemParameters.PrimaryScreenHeight;

            // Step 1: Define key landmarks (using the face edge points)
            var chinPoint = landmarksList[8];   // Chin
            var leftCheekPoint = landmarksList[0];  // Left edge of the face
            var rightCheekPoint = landmarksList[16]; // Right edge of the face

            // Calculate the approximate center of the face edge
            roiX = (chinPoint.X + leftCheekPoint.X + rightCheekPoint.X) / 3;
            roiY = (chinPoint.Y + leftCheekPoint.Y + rightCheekPoint.Y) / 3;

            // Inner and Outer Circle Radii
            int innerCircleRadius = 10; // Neutral area
            int outerCircleRadius = 50; // Max movement area

            // Step 2: Define the nose landmark for movement calculation
            var targetNosePoint = landmarksList[30];

            // Calculate distance between the nose and the center of the outer circle
            double distanceFromCenter = Math.Sqrt(Math.Pow(targetNosePoint.X - roiX, 2) + Math.Pow(targetNosePoint.Y - roiY, 2));

            // Step 3: If the nose is inside the inner circle, don't move the cursor (neutral area)
            if (distanceFromCenter <= innerCircleRadius)
            {
                return; // No movement if inside the inner circle
            }

            // Step 4: Calculate the direction of movement
            double moveX = targetNosePoint.X - roiX; // X direction of the nose from the center
            double moveY = targetNosePoint.Y - roiY; // Y direction of the nose from the center

            // Normalize the direction vector
            double magnitude = Math.Sqrt(moveX * moveX + moveY * moveY);
            if (magnitude > 0)
            {
                moveX /= magnitude;
                moveY /= magnitude;
            }

            // Step 5: Calculate the distance from the nose to the inner circle edge
            double distanceToInnerCircleEdge = distanceFromCenter - innerCircleRadius;

            // Step 6: Scale the speed based on the distance from the inner circle edge
            double speed = Math.Min(distanceToInnerCircleEdge, 25); // Cap the speed to a reasonable limit (e.g., 20)

            // Step 7: Gradually move the mouse in the direction of the nose
            double incrementX = (moveX * speed) * 2;
            double incrementY = (moveY * speed) * 2;

            // Update the current mouse position
            currentMousePosition.X += (int)incrementX;
            currentMousePosition.Y += (int)incrementY;

            // Ensure the mouse stays within the screen boundaries
            currentMousePosition.X = Clamp(currentMousePosition.X, 0, screenWidth);
            currentMousePosition.Y = Clamp(currentMousePosition.Y, 0, screenHeight);

            // Update the mouse cursor position
            Task.Run(() => SmoothMoveTo(currentMousePosition.X, currentMousePosition.Y));

            // Visualize the inner and outer circles
            Cv2.Circle(frame, new OpenCvSharp.Point(roiX, roiY), outerCircleRadius, Scalar.Blue, 2); // Outer circle
            Cv2.Circle(frame, new OpenCvSharp.Point(roiX, roiY), innerCircleRadius, Scalar.Green, 2); // Inner circle
            Cv2.Circle(frame, new OpenCvSharp.Point(targetNosePoint.X, targetNosePoint.Y), 5, Scalar.Red, -1); // Nose point

            // Visualize the line representing the distance from the nose to the inner circle edge
            // Visualize the line representing the distance from the nose to the inner circle edge
            double innerCircleEdgeX = roiX + innerCircleRadius * moveX;
            double innerCircleEdgeY = roiY + innerCircleRadius * moveY;
            OpenCvSharp.Point innerCircleEdgePoint = new OpenCvSharp.Point(innerCircleEdgeX, innerCircleEdgeY);

            // Draw the line from the edge of the inner circle to the nose point
            Cv2.Line(frame, new OpenCvSharp.Point(innerCircleEdgeX, innerCircleEdgeY),
                     new OpenCvSharp.Point(targetNosePoint.X, targetNosePoint.Y), Scalar.Yellow, 2);

            // Visualize the face edge points
            Cv2.Circle(frame, new OpenCvSharp.Point(chinPoint.X, chinPoint.Y), 5, Scalar.Magenta, -1); // Chin point
            Cv2.Circle(frame, new OpenCvSharp.Point(leftCheekPoint.X, leftCheekPoint.Y), 5, Scalar.Cyan, -1); // Left cheek
            Cv2.Circle(frame, new OpenCvSharp.Point(rightCheekPoint.X, rightCheekPoint.Y), 5, Scalar.Cyan, -1); // Right cheek
        }

        private void SmoothMoveTo(int targetX, int targetY, int duration = 100, int steps = 10)
        {
            // Null check for Kalman filters
            if (kalmanFilterX == null || kalmanFilterY == null)
            {
                throw new InvalidOperationException("Kalman filter not initialized. Call InitializeKalmanFilter first.");
            }

            GetCursorPos(out Point currentPos);
            int startX = currentPos.X;
            int startY = currentPos.Y;

            double deltaX = targetX - startX;
            double deltaY = targetY - startY;

            double smoothingFactor = 0.6; // Smoothing factor for low-pass filter

            for (int i = 0; i <= steps; i++)
            {
                // Prediction step for Kalman filter (using last known state)
                Mat predictedX = kalmanFilterX.Predict();
                Mat predictedY = kalmanFilterY.Predict();

                // Correct the prediction with the actual target position (the measurement)
                Mat measurementX = new Mat(2, 1, MatType.CV_32F);
                measurementX.Set<float>(0, targetX);
                measurementX.Set<float>(1, 0); // Assuming you want to ignore the second measurement

                Mat measurementY = new Mat(2, 1, MatType.CV_32F);
                measurementY.Set<float>(0, targetY);
                measurementY.Set<float>(1, 0); // Assuming you want to ignore the second measurement

                kalmanFilterX.Correct(measurementX);
                kalmanFilterY.Correct(measurementY);

                // Get smoothed position
                float smoothedX = kalmanFilterX.StatePost.Get<float>(0);
                float smoothedY = kalmanFilterY.StatePost.Get<float>(0);

                //// If transitioning to precision mode, adjust precisionFactor
                //if (preparingPrecision == true)
                //{
                //    // Only set transition time once when transitioning begins
                //    if (transitionPrecision == null)
                //    {
                //        transitionPrecision = DateTime.Now; // Set when transitioning begins
                //    }

                //    double elapsedTime = (DateTime.Now - transitionPrecision).TotalMilliseconds;

                //    if (elapsedTime <= 1500)
                //    {
                //        precisionFactor = 1.0 - (0.95 * (elapsedTime / 1500.0)); // Linear transition
                //    }
                //    else
                //    {
                //        precisionFactor = 0.1; // Cap at 10% after 2 seconds
                //    }
                //}

                // Calculate smoothed movement with adjusted precision factor
                smoothedX = (float)(startX + deltaX * (i / (double)steps) * (1 - smoothingFactor) * precisionFactor);
                smoothedY = (float)(startY + deltaY * (i / (double)steps) * (1 - smoothingFactor) * precisionFactor);

                // Set the cursor position
                SetCursorPos((int)smoothedX, (int)smoothedY);

                Thread.Sleep(duration / steps); // Wait between steps
            }
        }


        private KalmanFilter kalmanFilterX;
        private KalmanFilter kalmanFilterY;

        private void InitializeKalmanFilter()
        {
            // Kalman filter for X and Y coordinates
            kalmanFilterX = new KalmanFilter(4, 2, 0);  // State: [x, dx] (position and velocity), Measurement: [x]
            kalmanFilterY = new KalmanFilter(4, 2, 0);  // State: [y, dy] (position and velocity), Measurement: [y]

            // Create and populate transition matrix (A) - assuming constant velocity model
            kalmanFilterX.TransitionMatrix = new Mat(4, 4, MatType.CV_32F);
            kalmanFilterX.TransitionMatrix.Set<float>(0, 0, 1);
            kalmanFilterX.TransitionMatrix.Set<float>(0, 2, 1);
            kalmanFilterX.TransitionMatrix.Set<float>(1, 1, 1);
            kalmanFilterX.TransitionMatrix.Set<float>(1, 3, 1);
            kalmanFilterX.TransitionMatrix.Set<float>(2, 2, 1);
            kalmanFilterX.TransitionMatrix.Set<float>(3, 3, 1);

            kalmanFilterY.TransitionMatrix = new Mat(4, 4, MatType.CV_32F);
            kalmanFilterY.TransitionMatrix.Set<float>(0, 0, 1);
            kalmanFilterY.TransitionMatrix.Set<float>(0, 2, 1);
            kalmanFilterY.TransitionMatrix.Set<float>(1, 1, 1);
            kalmanFilterY.TransitionMatrix.Set<float>(1, 3, 1);
            kalmanFilterY.TransitionMatrix.Set<float>(2, 2, 1);
            kalmanFilterY.TransitionMatrix.Set<float>(3, 3, 1);

            // Measurement matrix (H)
            kalmanFilterX.MeasurementMatrix = new Mat(2, 4, MatType.CV_32F);
            kalmanFilterX.MeasurementMatrix.Set<float>(0, 0, 1);
            kalmanFilterX.MeasurementMatrix.Set<float>(1, 2, 1);

            kalmanFilterY.MeasurementMatrix = new Mat(2, 4, MatType.CV_32F);
            kalmanFilterY.MeasurementMatrix.Set<float>(0, 1, 1);
            kalmanFilterY.MeasurementMatrix.Set<float>(1, 3, 1);

            // Set initial state estimate (set to initial cursor position)
            GetCursorPos(out Point currentPos);
            kalmanFilterX.StatePost = new Mat(4, 1, MatType.CV_32F);
            kalmanFilterX.StatePost.Set<float>(0, currentPos.X);
            kalmanFilterX.StatePost.Set<float>(1, 0);
            kalmanFilterX.StatePost.Set<float>(2, 0);
            kalmanFilterX.StatePost.Set<float>(3, 0);

            kalmanFilterY.StatePost = new Mat(4, 1, MatType.CV_32F);
            kalmanFilterY.StatePost.Set<float>(0, currentPos.Y);
            kalmanFilterY.StatePost.Set<float>(1, 0);
            kalmanFilterY.StatePost.Set<float>(2, 0);
            kalmanFilterY.StatePost.Set<float>(3, 0);
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