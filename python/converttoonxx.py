import tf2onnx
import tensorflow as tf

# Load the model
model = tf.keras.models.load_model("mobilenet.h5")

# Convert to ONNX format
onnx_model = tf2onnx.convert.from_keras(model)
onnx_model.save("mobilenet.onnx")
