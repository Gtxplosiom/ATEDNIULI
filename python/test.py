import cv2
import numpy as np
import pyautogui
from ultralytics import YOLO
import mss
import time
import threading

# Load YOLOv8 model using the ultralytics library
model = YOLO("official-train-1-best-640.pt")  # Load YOLOv8 model
model.conf = 0.7  # Set the confidence threshold (70%) directly in the model

# Set up mouse callback for tracking
screen_width, screen_height = pyautogui.size()  # Get screen dimensions
roi_size = 60  # Size of the area where detection happens

# Function to track the mouse cursor
def get_mouse_position():
    x, y = pyautogui.position()
    return x, y

# Screen capture setup using mss
previous_detections = []  # To track detections from previous frames

# Function for capturing the screen
def capture_screen(sct, roi_size, screen_width, screen_height):
    # Get the current mouse position
    mouse_x, mouse_y = get_mouse_position()

    # Define a square ROI area around the mouse position
    roi_top_left = (mouse_x - roi_size // 2, mouse_y - roi_size // 2)
    roi_bottom_right = (mouse_x + roi_size // 2, mouse_y + roi_size // 2)

    # Ensure the ROI doesn't go out of screen bounds
    roi_top_left = (max(0, roi_top_left[0]), max(0, roi_top_left[1]))
    roi_bottom_right = (min(screen_width, roi_bottom_right[0]), min(screen_height, roi_bottom_right[1]))

    # Capture the screen in the defined ROI area
    monitor = {"top": roi_top_left[1], "left": roi_top_left[0], "width": roi_size, "height": roi_size}
    screen = np.array(sct.grab(monitor))

    # Convert the captured screen to BGR (mss captures in RGB format)
    screen_bgr = cv2.cvtColor(screen, cv2.COLOR_RGB2BGR)
    
    return screen_bgr

# Function to perform detection
def process_detection(screen_bgr):
    # Convert BGR to RGB for YOLO
    screen_rgb = cv2.cvtColor(screen_bgr, cv2.COLOR_BGR2RGB)

    # Run YOLOv8 detection on the captured screen area (ROI)
    results = model(screen_rgb)

    current_detections = []  # List to hold current frame detections
    detected_any = False  # Flag to check if any detection was made

    # Draw detections on the ROI if confidence is above threshold
    for result in results[0].boxes:
        confidence = result.conf[0]  # Get the confidence score
        if confidence >= 0.7:  # Filter by confidence
            # Extract the bounding box coordinates
            x1, y1, x2, y2 = map(int, result.xyxy[0])  # Get box coordinates (top-left, bottom-right)
            class_id = int(result.cls[0])  # Get the class ID
            label = model.names[class_id]  # Get the label (class name)

            # Check if this detection is new (not repeated from previous frame)
            detection = (label, confidence)  # Simplified to label and confidence tuple
            if detection not in previous_detections:
                current_detections.append(detection)
                detected_any = True
                cv2.rectangle(screen_rgb, (x1, y1), (x2, y2), (0, 255, 0), 2)  # Green box
                cv2.putText(screen_rgb, f"{label} {confidence:.2f}", (x1, y1 - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 255, 0), 2)
                print(f"Detection: {label} {confidence:.2f}")  # Output simplified message

    # If no detections in current frame, print "No Detection"
    if not detected_any:
        print("No Detection")

    return screen_rgb, current_detections

# Setup and initialize mss
with mss.mss() as sct:
    while True:
        # Capture the screen in a separate thread
        screen_bgr = capture_screen(sct, roi_size, screen_width, screen_height)

        # Process the captured screen for detections
        screen_rgb, current_detections = process_detection(screen_bgr)

        # Update the previous detections to current detections
        previous_detections = current_detections

        # Display the screen area with detection in a window
        cv2.imshow("Live Detection", screen_rgb)

        # Exit on 'q' key press
        if cv2.waitKey(1) & 0xFF == ord('q'):
            break

        # Add a slight delay (100ms) between each frame
        time.sleep(0.5)

# Clean up
cv2.destroyAllWindows()
