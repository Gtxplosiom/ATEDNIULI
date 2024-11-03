import zmq
import pyautogui
import numpy as np
import cv2
import threading
import time
import os
from ultralytics import YOLO

# # Load the YOLO model
# model = YOLO(r'C:\Users\super.admin\Desktop\Capstone\ATEDNIULI\edn-app\ATEDNIULI\assets\models\official-train-1-best-640.pt')

# Load the YOLO model from the current directory (RELEASE)
model_path = os.path.join(os.getcwd(), 'assets', 'models', 'official-train-1-best-640.pt')
model = YOLO(model_path)

# Set up ZMQ for sending detections to C#
context = zmq.Context()
socket = context.socket(zmq.PUB)  # Publisher socket
socket.bind("tcp://*:5555")  # Binding on port 5555

# Get screen dimensions
screen_width, screen_height = pyautogui.size()

# Calculate width and height for 36 quadrants (6 columns and 6 rows)
quadrant_width = screen_width // 6
quadrant_height = screen_height // 6

last_inferred_quadrant = None
bounding_boxes = []
bounding_boxes_lock = threading.Lock()

def get_current_quadrant(x, y):
    col = min(5, x // quadrant_width)
    row = min(5, y // quadrant_height)
    return row * 6 + col

def screenshot_quadrant(quadrant):
    col = quadrant % 6
    row = quadrant // 6
    left = max(0, col * quadrant_width - 10)
    top = max(0, row * quadrant_height - 10)
    width = min(screen_width, quadrant_width + 20)
    height = min(screen_height, quadrant_height + 20)
    screenshot = pyautogui.screenshot(region=(left, top, width, height))
    return np.array(screenshot), left, top

def perform_inference_on_quadrant(quadrant):
    global bounding_boxes
    image, left_offset, top_offset = screenshot_quadrant(quadrant)
    image = cv2.cvtColor(image, cv2.COLOR_RGB2BGR)
    results = model(image)
    new_bounding_boxes = []

    for result in results:
        for box in result.boxes.xyxy:
            x1, y1, x2, y2 = map(int, box[:4])
            x1 += left_offset
            y1 += top_offset
            x2 += left_offset
            y2 += top_offset
            label = model.names[int(result.boxes.cls[0])]
            new_bounding_boxes.append((x1, y1, x2, y2, label))
    
    with bounding_boxes_lock:
        bounding_boxes = new_bounding_boxes[:100]

def run_inference_thread(quadrant):
    perform_inference_on_quadrant(quadrant)
    print(f"Inference performed on quadrant {quadrant}")

def check_mouse_position():
    global last_inferred_quadrant
    while True:
        x, y = pyautogui.position()
        current_quadrant = get_current_quadrant(x, y)
        
        if current_quadrant != last_inferred_quadrant:
            last_inferred_quadrant = current_quadrant
            inference_thread_obj = threading.Thread(target=run_inference_thread, args=(current_quadrant,))
            inference_thread_obj.start()

        found_match = False  # Track whether a match was found

        with bounding_boxes_lock:
            for (x1, y1, x2, y2, label) in bounding_boxes:
                if x1 < x < x2 and y1 < y < y2:
                    # Send the detection to the C# app via ZMQ
                    message = f"{label},{x1},{y1},{x2},{y2}"
                    socket.send_string(message)  # Send the message to C#
                    print(f"Hovering over: {label} at [{x1}, {y1}, {x2}, {y2}]")
                    found_match = True  # A match was found, so we won't send "no detections"
                    break  # No need to check other bounding boxes once a match is found

        # If no match was found, send a "no detections" message
        if not found_match:
            socket.send_string("no detections")

        time.sleep(0.1)

mouse_polling_thread = threading.Thread(target=check_mouse_position)
mouse_polling_thread.start()
