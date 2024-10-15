import pandas as pd
import numpy as np
import tensorflow as tf
from tensorflow.keras.preprocessing.text import Tokenizer
from tensorflow.keras.preprocessing.sequence import pad_sequences
from tensorflow.keras.models import load_model
import pickle
import re
import zmq
import asyncio

class IntentRecognition:
    def __init__(self):
        self.tokenizer = None
        self.max_len = None
        self.intent_labels = None
        self.model = load_model("assets/models/intent_model_1/my_model.keras")
        self.prep_data()
        
        self.context = zmq.Context()
        self.socket = self.context.socket(zmq.REP)
        self.socket.bind("tcp://localhost:6969")
        
        # Send a "ready" message once the setup is complete
        notify_socket = self.context.socket(zmq.PUSH)
        notify_socket.connect("tcp://localhost:6970")
        notify_socket.send_string("Intent recognition model loaded")

    def prep_data(self):
        with open('assets/models/intent_model_1/tokenizer.pickle', 'rb') as handle:
            self.tokenizer = pickle.load(handle)
        with open('assets/models/intent_model_1/max_len.pickle', 'rb') as handle:
            self.max_len = pickle.load(handle)
        with open('assets/models/intent_model_1/intent_labels.pickle', 'rb') as handle:
            self.intent_labels = pickle.load(handle)

    def predict_intent(self, sample_input):
        if len(sample_input) != 0:
            try:
                normalized_input = self.normalize(sample_input)
                
                sample_seq = self.tokenizer.texts_to_sequences([normalized_input])
                
                padded_sample = pad_sequences(sample_seq, maxlen=self.max_len, padding='post')

                predicted_intent = self.model.predict(padded_sample)
                predicted_intent_idx = np.argmax(predicted_intent)
                predicted_intent_label = self.intent_labels[predicted_intent_idx]

                normalized_intent = self.normalize(predicted_intent_label)
                return normalized_intent
            except Exception as e:
                return "Error processing the input."
        else:
            return None
    
    def normalize(self, text):
        text = text.lower()
        text = re.sub(r'[^a-zA-Z0-9\s]', '', text)
        text = text.strip()
        return text
    
    async def get_response(self):
        while True:
            try:
                message = self.socket.recv_string()
                response = self.predict_intent(message)
                self.socket.send_string(response)
            except Exception as e:
                self.socket.send_string("Unexpected error occurred.")
            await asyncio.sleep(1)

if __name__ == "__main__":
    intent_recognition = IntentRecognition()
    asyncio.run(intent_recognition.get_response())
