import pandas as pd
import numpy as np
import tensorflow as tf
from tensorflow.keras.preprocessing.text import Tokenizer
from tensorflow.keras.preprocessing.sequence import pad_sequences
from tensorflow.keras.models import load_model
import pickle
import re

class IntentRecognition:
    def __init__(self):
        self.tokenizer = None
        self.max_len = None
        self.intent_labels = None
        self.model = load_model("assets/models/intent_model_1/my_model.keras")
        self.prep_data()  # Prepare data on initialization

    def prep_data(self):
        # Load the tokenizer
        with open('assets/models/intent_model_1/tokenizer.pickle', 'rb') as handle:
            self.tokenizer = pickle.load(handle)

        # Load the max_len value used during training
        with open('assets/models/intent_model_1/max_len.pickle', 'rb') as handle:
            self.max_len = pickle.load(handle)

        # Load the intent labels
        with open('assets/models/intent_model_1/intent_labels.pickle', 'rb') as handle:
            self.intent_labels = pickle.load(handle)

    def predict_intent(self, sample_input):
        # Ensure the input is in UTF-8 format
        if isinstance(sample_input, bytes):
            sample_input = sample_input.decode('utf-8')

        print(f"Original Input: {repr(sample_input)}")  # Use repr() to show hidden characters

        normalized_input = self.normalize_intent(sample_input)
        
        # Process the input and make the prediction
        sample_seq = self.tokenizer.texts_to_sequences([normalized_input])
        padded_sample = pad_sequences(sample_seq, maxlen=self.max_len, padding='post')

        # Make the prediction
        predicted_intent = self.model.predict(padded_sample)
        predicted_intent_idx = np.argmax(predicted_intent)

        predicted_intent_label = self.intent_labels[predicted_intent_idx]

        print(f"Original Output: {repr(predicted_intent_label)}")  # Use repr() to show hidden characters

        normalized_intent = self.normalize_intent(predicted_intent_label)

        return normalized_intent
    
    def normalize_intent(self, intent):
        # Lowercase the intent label
        intent = intent.lower()
        # Remove special characters
        intent = re.sub(r'[^a-zA-Z0-9\s]', '', intent)
        # Strip whitespace
        intent = intent.strip()
        return intent
    
    def hello_world(self):
        return "Hello World!"

# Example usage (for testing in Python)
if __name__ == "__main__":
    intent_recognition = IntentRecognition()
    while True:
        sample_input = input("Enter input: ")
        predicted_intent = intent_recognition.predict_intent(sample_input)
        print(f"Predicted intent: {predicted_intent}")
