import pyautogui
import numpy as np
import cv2
import threading
import time
from ultralytics import YOLO

# Load the YOLO model
model = YOLO('official-train-1-best-640.pt')

# Get screen dimensions
screen_width, screen_height = pyautogui.size()

# Calculate width and height for 36 quadrants (6 columns and 6 rows)
quadrant_width = screen_width // 6
quadrant_height = screen_height // 6

last_inferred_quadrant = None
bounding_boxes = []
bounding_boxes_lock = threading.Lock()

def get_current_quadrant(x, y):
    # Calculate the column and row for the current mouse position
    col = min(5, x // quadrant_width)  # Ensure col is within bounds (0 to 5)
    row = min(5, y // quadrant_height)  # Ensure row is within bounds (0 to 5)
    return row * 6 + col  # Return the quadrant index (0 to 35)

def screenshot_quadrant(quadrant):
    col = quadrant % 6
    row = quadrant // 6
    left = max(0, col * quadrant_width - 10)  # Add overlap
    top = max(0, row * quadrant_height - 10)   # Add overlap
    width = min(screen_width, quadrant_width + 20)  # Add overlap
    height = min(screen_height, quadrant_height + 20)  # Add overlap
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
    
    # Limit the number of bounding boxes to a maximum of 100
    with bounding_boxes_lock:
        bounding_boxes = new_bounding_boxes[:100]  # Only keep the first 100 boxes

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

        with bounding_boxes_lock:
            for (x1, y1, x2, y2, label) in bounding_boxes:
                if x1 < x < x2 and y1 < y < y2:
                    print(f"Hovering over: {label} at [{x1}, {y1}, {x2}, {y2}]")

        time.sleep(0.1)

# Start the mouse polling thread
mouse_polling_thread = threading.Thread(target=check_mouse_position)
mouse_polling_thread.start()
