import cv2
import numpy as np
import pyautogui
from ultralytics import YOLO
import mss
import time
import threading
import zmq
import os

# Load the YOLO model
model_path = os.path.join(os.getcwd(), 'assets', 'models', 'official-train-1-best-640.pt')
model = YOLO(model_path)

# Set up ZMQ for sending detections to C#
context = zmq.Context()
socket = context.socket(zmq.PUB)  # Publisher socket
socket.bind("tcp://*:5555")  # Binding on port 5555

# Get screen dimensions
screen_width, screen_height = pyautogui.size()

# Define size for the region of interest (ROI)
roi_size = 60  # Size of the area around the mouse for detection

# Function to track the mouse cursor
def get_mouse_position():
    x, y = pyautogui.position()
    return x, y

# Function to capture the screen
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

            # Send the detection details to C# app via ZMQ
            message = f"{label},{x1},{y1},{x2},{y2}"
            socket.send_string(message)  # Send the message to C#
            print(f"Detection: {label} at [{x1}, {y1}, {x2}, {y2}] with confidence {confidence:.2f}")  # Output simplified message

            detected_any = True

    # If no detections in current frame, send a "no detections" message
    if not detected_any:
        socket.send_string("no detections")

    return screen_rgb

# Setup and initialize mss
with mss.mss() as sct:
    while True:
        # Capture the screen in a separate thread
        screen_bgr = capture_screen(sct, roi_size, screen_width, screen_height)

        # Process the captured screen for detections
        screen_rgb = process_detection(screen_bgr)

        # Display the screen area with detection in a window
        # cv2.imshow("Live Detection", screen_rgb)

        # Exit on 'q' key press
        if cv2.waitKey(1) & 0xFF == ord('q'):
            break

        # Add a slight delay (100ms) between each frame
        time.sleep(0.5)

# Clean up
cv2.destroyAllWindows()
