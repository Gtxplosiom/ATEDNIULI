import tensorflow as tf
from tensorflow.keras.layers import Conv2D, BatchNormalization, Activation, MaxPool2D, Conv2DTranspose, Concatenate, Input
from tensorflow.keras.models import Model
from tensorflow.keras.applications import MobileNetV2
import numpy as np
from tensorflow.keras.preprocessing.image import load_img, img_to_array
import matplotlib.pyplot as plt

print("TF Version: ", tf.__version__)

def conv_block(inputs, num_filters):
    x = Conv2D(num_filters, 3, padding="same")(inputs)
    x = BatchNormalization()(x)
    x = Activation("relu")(x)

    x = Conv2D(num_filters, 3, padding="same")(x)
    x = BatchNormalization()(x)
    x = Activation("relu")(x)

    return x

def decoder_block(inputs, skip, num_filters):
    x = Conv2DTranspose(num_filters, (2, 2), strides=2, padding="same")(inputs)
    x = Concatenate()([x, skip])
    x = conv_block(x, num_filters)

    return x

def build_mobilenetv2_unet(input_shape):    ## (512, 512, 3)
    """ Input """
    inputs = Input(shape=input_shape)

    """ Pre-trained MobileNetV2 """
    encoder = MobileNetV2(include_top=False, weights="imagenet",
        input_tensor=inputs, alpha=1.4)

    """ Encoder """
    s1 = encoder.get_layer("input_1").output                ## (512 x 512)
    s2 = encoder.get_layer("block_1_expand_relu").output    ## (256 x 256)
    s3 = encoder.get_layer("block_3_expand_relu").output    ## (128 x 128)
    s4 = encoder.get_layer("block_6_expand_relu").output    ## (64 x 64)

    """ Bridge """
    b1 = encoder.get_layer("block_13_expand_relu").output   ## (32 x 32)

    """ Decoder """
    d1 = decoder_block(b1, s4, 512)                         ## (64 x 64)
    d2 = decoder_block(d1, s3, 256)                         ## (128 x 128)
    d3 = decoder_block(d2, s2, 128)                         ## (256 x 256)
    d4 = decoder_block(d3, s1, 64)                          ## (512 x 512)

    """ Output """
    outputs = Conv2D(1, 1, padding="same", activation="sigmoid")(d4)

    model = Model(inputs, outputs, name="MobileNetV2_U-Net")
    return model

def preprocess_image(image_path, target_size=(512, 512)):
    """Load an image and preprocess it for the U-Net model."""
    image = load_img(image_path, target_size=target_size)  # Resize to target size
    image_array = img_to_array(image) / 255.0  # Normalize to [0, 1]
    image_array = np.expand_dims(image_array, axis=0)  # Add batch dimension
    return image_array, image

def detect_white_parts(binary_mask, threshold=0.5):
    """Detect the white regions (presence of the object) in the binary mask."""
    # Count the number of white pixels in the binary mask
    white_pixel_count = np.sum(binary_mask == 1)
    total_pixels = binary_mask.size  # Total number of pixels in the binary mask

    # Calculate the percentage of white pixels
    white_percentage = (white_pixel_count / total_pixels) * 100
    print(f"White Pixel Count: {white_pixel_count} / Total Pixels: {total_pixels}")
    print(f"White Pixel Percentage: {white_percentage:.2f}%")

    # If more than a certain percentage of the mask is white, we consider it detected
    if white_percentage > threshold:
        print("Object Detected: White areas present in the mask.")
    else:
        print("No Object Detected: No significant white areas in the mask.")

if __name__ == "__main__":
    # model = build_mobilenetv2_unet((512, 512, 3))
    # model.save("mobilenet.h5")

    model = tf.keras.models.load_model("mobilenet.h5")

    image_path = "sample_lines.png"  # Example image path
    input_array, original_image = preprocess_image(image_path)

    # Step 2: Perform inference
    predictions = model.predict(input_array)

    # Step 3: Postprocess the predictions
    predicted_mask = predictions[0, :, :, 0]  # Remove batch and channel dimensions
    binary_mask = (predicted_mask > 0.60).astype(np.uint8)  # Threshold the mask

    # Step 4: Visualize the results
    plt.figure(figsize=(10, 5))

    # Original Image
    plt.subplot(1, 3, 1)
    plt.title("Original Image")
    plt.imshow(original_image)
    plt.axis("off")

    # Predicted Mask (Soft)
    plt.subplot(1, 3, 2)
    plt.title("Predicted Mask (Soft)")
    plt.imshow(predicted_mask, cmap="gray")
    plt.axis("off")

    # Predicted Mask (Binary)
    plt.subplot(1, 3, 3)
    plt.title("Predicted Mask (Binary)")
    plt.imshow(binary_mask, cmap="gray")
    plt.axis("off")

    # Detect presence of white areas in the binary mask
    detect_white_parts(binary_mask, threshold=0.5)

    plt.tight_layout()
    plt.show()