import cv2
import numpy as np
import pyautogui
import mss
import time
from tensorflow.keras.models import load_model

# Load your MobileNet-based U-Net model
model = load_model("mobilenet.h5")

# Set up screen capture and ROI
screen_width, screen_height = pyautogui.size()
roi_size = 60  # Size of the area around the mouse for detection

def preprocess_frame(frame, target_size=(512, 512)):
    resized_frame = cv2.resize(frame, target_size)
    normalized_frame = resized_frame / 255.0
    input_array = np.expand_dims(normalized_frame, axis=0)
    return input_array

def detect_white_parts(binary_mask, threshold=0.25, min_area=500, aspect_ratio_threshold=0.5):
    """Detect the white regions (presence of the object) in the binary mask."""
    contours, _ = cv2.findContours(binary_mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    
    valid_detection = False
    for contour in contours:
        contour_area = cv2.contourArea(contour)
        
        if contour_area > min_area:
            # Calculate the bounding box of the contour
            x, y, w, h = cv2.boundingRect(contour)
            
            # Check the aspect ratio of the bounding box
            aspect_ratio = float(w) / h if h != 0 else 1.0
            
            # Only consider valid contours with reasonable aspect ratios
            if aspect_ratio > aspect_ratio_threshold:
                valid_detection = True
                break  # Exit loop once a valid object is found

    return valid_detection

# Add a counter to track consistency over multiple frames
detection_counter = 0
detection_threshold = 5  # Number of consecutive frames to confirm a valid detection

# Screen capture setup using mss
with mss.mss() as sct:
    while True:
        # Get the current mouse position
        mouse_x, mouse_y = pyautogui.position()

        # Define the ROI area around the mouse
        roi_top_left = (mouse_x - roi_size // 2, mouse_y - roi_size // 2)
        roi_bottom_right = (mouse_x + roi_size // 2, mouse_y + roi_size // 2)

        # Capture the screen in the defined ROI area
        monitor = {"top": max(0, roi_top_left[1]), 
                   "left": max(0, roi_top_left[0]), 
                   "width": roi_size, 
                   "height": roi_size}
        screen = np.array(sct.grab(monitor))

        # Convert the captured frame to BGR
        screen_bgr = cv2.cvtColor(screen, cv2.COLOR_RGB2BGR)

        # Preprocess the captured frame for the U-Net model
        input_array = preprocess_frame(screen_bgr)

        # Run inference using your MobileNet-based U-Net model
        predictions = model.predict(input_array)
        predicted_mask = predictions[0, :, :, 0]  # Extract the predicted mask
        
        # Dynamic thresholding based on the predicted mask's mean or median value
        dynamic_threshold = np.mean(predicted_mask)  # or np.median(predicted_mask)
        binary_mask = (predicted_mask > dynamic_threshold).astype(np.uint8)  # Threshold to binary

        # Debugging output
        print(f"Predicted Mask Sum: {binary_mask.sum()}")  # Debug sum of the binary mask

        # Check if any "icon" is detected in the ROI
        if detect_white_parts(binary_mask, threshold=0.25, min_area=500):
            detection_counter += 1
        else:
            detection_counter = 0  # Reset if no object detected
        
        if detection_counter >= detection_threshold:
            print("Object Detected: Consistent detection over several frames.")
        else:
            print("No Object Detected: No valid object found (insufficient frame consistency).")

        # Visualize the mask to understand the detection
        # Uncomment to see the mask for debugging
        cv2.imshow("Predicted Mask", binary_mask * 255)

        # Add delay to reduce CPU usage
        time.sleep(0.2)

        # Exit on 'q' key press
        if cv2.waitKey(1) & 0xFF == ord('q'):
            break

# Clean up
cv2.destroyAllWindows()
