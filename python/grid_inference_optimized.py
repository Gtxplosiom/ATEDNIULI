import pyautogui
import numpy as np
import cv2
import threading
import time
from ultralytics import YOLO

# Load the YOLOv8 model using the ultralytics package
model = YOLO('official-train-1-best-640.pt')

# Get screen resolution
screen_width, screen_height = pyautogui.size()

# Divide the screen into 6 quadrants (3 horizontal, 2 vertical)
quadrant_width = screen_width // 3
quadrant_height = screen_height // 2

# Create a variable to store the last quadrant inferred
last_inferred_quadrant = None
bounding_boxes = []

# Lock to ensure thread-safe access
bounding_boxes_lock = threading.Lock()

def get_current_quadrant(x, y):
    """Return the current quadrant based on mouse coordinates."""
    col = x // quadrant_width
    row = y // quadrant_height
    return row * 3 + col  # Returns a number between 0 and 5 (6 quadrants)

def screenshot_quadrant(quadrant):
    """Take a screenshot of the specific quadrant."""
    col = quadrant % 3
    row = quadrant // 3
    left = col * quadrant_width
    top = row * quadrant_height
    width = quadrant_width
    height = quadrant_height
    screenshot = pyautogui.screenshot(region=(left, top, width, height))
    return np.array(screenshot), left, top

def perform_inference_on_quadrant(quadrant):
    """Perform YOLOv8 inference on the specific quadrant."""
    global bounding_boxes
    image, left_offset, top_offset = screenshot_quadrant(quadrant)
    image = cv2.cvtColor(image, cv2.COLOR_RGB2BGR)
    results = model(image)
    new_bounding_boxes = []
    
    # Get bounding boxes in relative to original screen size
    for result in results:
        for box in result.boxes.xyxy:  # xyxy format of YOLO
            x1, y1, x2, y2 = map(int, box[:4])
            x1 += left_offset
            y1 += top_offset
            x2 += left_offset
            y2 += top_offset
            label = model.names[int(result.boxes.cls[0])]
            new_bounding_boxes.append((x1, y1, x2, y2, label))
    
    # Update bounding boxes thread-safely
    with bounding_boxes_lock:
        bounding_boxes = new_bounding_boxes

def run_inference_thread(quadrant):
    """Run YOLOv8 inference on a separate thread."""
    perform_inference_on_quadrant(quadrant)
    print(f"Inference performed on quadrant {quadrant}")

def check_mouse_position():
    """Poll the mouse position at intervals and trigger inference as needed."""
    global last_inferred_quadrant
    while True:
        x, y = pyautogui.position()  # Get current mouse position
        current_quadrant = get_current_quadrant(x, y)
        
        # If mouse moves to a new quadrant, run inference in a new thread
        if current_quadrant != last_inferred_quadrant:
            last_inferred_quadrant = current_quadrant
            inference_thread_obj = threading.Thread(target=run_inference_thread, args=(current_quadrant,))
            inference_thread_obj.start()

        # Check if the mouse is hovering over any bounding boxes
        with bounding_boxes_lock:
            for (x1, y1, x2, y2, label) in bounding_boxes:
                if x1 < x < x2 and y1 < y < y2:
                    print(f"Hovering over: {label} at [{x1}, {y1}, {x2}, {y2}]")

        time.sleep(0.1)  # Poll every 100 ms

# Start the mouse position polling in a separate thread
mouse_polling_thread = threading.Thread(target=check_mouse_position)
mouse_polling_thread.start()
