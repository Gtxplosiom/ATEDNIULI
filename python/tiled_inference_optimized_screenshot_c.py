import cv2
import numpy as np
import onnxruntime as ort
import mss  # For capturing the screen
import zmq
import json

class YOLOv8:
    def __init__(self, onnx_model, confidence_thres=0.5, iou_thres=0.5, tile_size=640, overlap=80, model_input_size=640):
        self.onnx_model = onnx_model
        self.confidence_thres = confidence_thres
        self.iou_thres = iou_thres
        self.tile_size = tile_size
        self.overlap = overlap  # Amount of overlap in pixels
        self.model_input_size = model_input_size

        # Load the class names from the classes.txt file
        with open(r"C:\Users\super.admin\Desktop\Capstone\ATEDNIULI\edn-app\ATEDNIULI\assets\classes.txt", "r") as f:
            self.classes = [line.strip() for line in f.readlines()]

        # Generate a color palette for the classes
        self.color_palette = np.random.uniform(0, 255, size=(len(self.classes), 3))

        # Setup ZeroMQ
        self.context = zmq.Context()
        self.socket = self.context.socket(zmq.PUB)  # Publish socket
        self.socket.bind("tcp://localhost:4200")  # Binding to port 4200

    def draw_detections(self, img, box, score, class_id):
        if class_id >= len(self.classes):
            print(f"Warning: Detected class_id {class_id} out of bounds.")
            return
        
        x1, y1, w, h = box
        color = self.color_palette[class_id]
        cv2.rectangle(img, (int(x1), int(y1)), (int(x1 + w), int(y1 + h)), color, 2)

        label = f"{self.classes[class_id]}: {score:.2f}"
        label_size = cv2.getTextSize(label, cv2.FONT_HERSHEY_SIMPLEX, 0.5, 1)[0]

        cv2.rectangle(img, (x1, y1 - label_size[1] - 5), (x1 + label_size[0], y1), color, cv2.FILLED)
        cv2.putText(img, label, (x1, y1 - 5), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 0, 0), 1, cv2.LINE_AA)

    def preprocess(self, img):
        if img is None or img.size == 0:
            raise ValueError("Invalid image provided.")
        
        img = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)
        img_resized = cv2.resize(img, (self.model_input_size, self.model_input_size), interpolation=cv2.INTER_LINEAR)

        image_data = np.array(img_resized) / 255.0
        image_data = np.transpose(image_data, (2, 0, 1))  # Channel first
        return np.expand_dims(image_data, axis=0).astype(np.float32)

    def postprocess(self, img, output):
        outputs = np.transpose(np.squeeze(output[0]))
        boxes, scores, class_ids = [], [], []
        detections = []  # To store the (x, y, class_name) for detected items

        h, w = img.shape[:2]
        x_factor, y_factor = w / self.model_input_size, h / self.model_input_size

        min_size = 10
        max_size = 75

        for row in outputs:
            classes_scores = row[4:]  # Assuming the first 4 elements are x, y, w, h
            max_score = np.max(classes_scores)

            if max_score >= self.confidence_thres:
                class_id = np.argmax(classes_scores)
                
                # Check if class_id is within the valid range
                if class_id >= len(self.classes):
                    print(f"Warning: Detected class_id {class_id} out of bounds (max {len(self.classes) - 1}).")
                    continue  # Skip to the next detection
                
                x, y, w, h = row[:4]
                left = int((x - w / 2) * x_factor)
                top = int((y - h / 2) * y_factor)
                width = int(w * x_factor)
                height = int(h * y_factor)

                if width >= min_size and height >= min_size and width <= max_size and height <= max_size:
                    class_ids.append(class_id)
                    scores.append(max_score)
                    boxes.append([left, top, width, height])

                    # Store only XY coordinates along with class name
                    coord = (left, top, self.classes[class_id])
                    
                    # Add unique coordinates to the list
                    if coord not in detections:
                        detections.append(coord)  # Only add if unique

        indices = cv2.dnn.NMSBoxes(boxes, scores, self.confidence_thres, self.iou_thres)

        if len(indices) > 0:
            if isinstance(indices, tuple):
                indices = indices[0]  # Flatten tuple

            for i in indices.flatten():
                self.draw_detections(img, boxes[i], scores[i], class_ids[i])

        return img, detections  # Return detections (coordinates + class name)
    
    def capture_screen(self):
        with mss.mss() as sct:
            # Capture the entire screen, including all monitors and the taskbar
            monitor = sct.monitors[0]  # Capture the full virtual screen
            screenshot = sct.grab(monitor)
            img = np.array(screenshot)  # Convert to numpy array
            return cv2.cvtColor(img, cv2.COLOR_BGRA2BGR)  # Convert from BGRA to BGR
        
    def send_detections(self, all_detections):
        # Create structured detection results
        detection_results = []
        tile_x, tile_y = 0, 0  # Replace with actual tile coordinates if needed

        # Organize detections by tile
        detection_result = {"TileX": tile_x, "TileY": tile_y, "Detections": []}
        
        for (x, y, class_name) in all_detections:
            # Only include x, y, and class_name in the detection
            detection = {
                "x": x,
                "y": y,
                "ClassName": class_name
            }
            detection_result["Detections"].append(detection)
        
        detection_results.append(detection_result)

        # Serialize the detection results to JSON format
        json_message = json.dumps(detection_results)

        print(f"json message: {json_message}")
        
        # Send the serialized JSON message over ZeroMQ
        self.socket.send_string(json_message)

    def main(self):
        session = ort.InferenceSession(self.onnx_model, providers=["CUDAExecutionProvider", "CPUExecutionProvider"])
        model_inputs = session.get_inputs()

        # Capture the current screen
        img = self.capture_screen()
        img_height, img_width = img.shape[:2]
        
        # Calculate the number of tiles with overlap
        num_tiles_x = int(np.ceil((img_width - self.overlap) / (self.tile_size - self.overlap)))
        num_tiles_y = int(np.ceil((img_height - self.overlap) / (self.tile_size - self.overlap)))
        
        output_img = np.zeros((img_height, img_width, 3), dtype=np.uint8)

        all_detections = []  # List to store all detected (x, y, class_name)

        for i in range(num_tiles_x):
            for j in range(num_tiles_y):
                x1 = int(i * (self.tile_size - self.overlap))
                y1 = int(j * (self.tile_size - self.overlap))
                x2 = min(x1 + self.tile_size, img_width)
                y2 = min(y1 + self.tile_size, img_height)

                # Ensure the tile doesn't exceed image boundaries
                tile = img[y1:y2, x1:x2]
                tile_data = self.preprocess(tile)
                outputs = session.run(None, {model_inputs[0].name: tile_data})
                detections, detections_from_tile = self.postprocess(tile, outputs)

                output_img[y1:y2, x1:x2] = detections
                
                # Extend the list with detections from this tile
                all_detections.extend(detections_from_tile)

        cv2.imwrite("Output.png", output_img)
        cv2.waitKey(0)

        self.send_detections(all_detections)

        return all_detections

if __name__ == "__main__":
    detection = YOLOv8(r"C:\Users\super.admin\Desktop\Capstone\ATEDNIULI\edn-app\ATEDNIULI\assets\models\official-train-1-best-640.onnx")
    message = detection.main()
    print(f"sent: {message}")
    cv2.destroyAllWindows()
