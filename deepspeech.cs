using DeepSpeechClient.Models;
using DeepSpeechClient;
using NAudio.Wave;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WebRtcVadSharp;
using System.IO;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Input;
using CoreAudio;
using ATEDNIULI;
using System.Data;
using Python.Runtime;
using System.Text;
using NetMQ;
using NetMQ.Sockets;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Threading;
using OpenCvSharp;
using static ATEDNIULI.ShowItems;
using System.Linq;

class LiveTranscription
{
    private readonly ASRWindow asr_window;
    private readonly IntentWindow intent_window;
    private readonly MainWindow main_window;
    private readonly ShowItems show_items;
    private readonly CameraMouse camera_mouse;
    private WaveInEvent wave_in_event;
    private DeepSpeechStream deep_speech_stream;
    private DeepSpeech deep_speech_model;
    private WebRtcVad vad;

    private bool wake_word_detected = false;
    private bool is_running;

    private string wake_word = "thermal";

    private int click_command_count = 0;
    private int calculator_command_count = 0;
    private int notepad_command_count = 0;
    private int close_window_command_count = 0;
    private int chrome_command_count = 0;
    private int word_command_count = 0;
    private int excel_command_count = 0;
    private int powerpoint_command_count = 0;
    private int edge_command_count = 0;
    private int file_manager_command_count = 0;
    private int settings_command_count = 0;
    private int type_command_count = 0;
    private int close_calculator_command_count = 0;
    private int close_app_command_count = 0;
    private int scroll_up_command_count = 0;
    private int scroll_down_command_count = 0;
    private int screenshot_command_count = 0;
    private int volume_up_command_count = 0;
    private int volume_down_command_count = 0;
    private int search_command_count = 0;
    private int show_items_command_count = 0;
    private int execute_number_command_count = 0;

    private int switch_command_count = 0;
    private int left_command_count = 0;
    private int right_command_count = 0;
    private int up_command_count = 0;
    private int down_command_count = 0;
    private int enter_command_count = 0;

    private bool has_searched = false; // Flag to track search execution

    private string lastTypedPhrase = string.Empty;
    private bool typing_mode = false;

    private bool wake_word_required = true; // Default to requiring the wake word

    string app_directory = Directory.GetCurrentDirectory(); // possible gamiton labi pag reference hin assets kay para robust hiya ha iba iba na systems

    private RequestSocket socket;
    private SubscriberSocket pullsocket;
    private const int inactivity_timeout = 3000;
    private const int intent_window_timeout = 1500;

    private System.Timers.Timer inactivity_timer;
    private System.Timers.Timer intent_window_timer;
    private System.Timers.Timer input_timer;
    private System.Timers.Timer wake_word_timer;
    private System.Timers.Timer debounce_timer;
    
    private const int debounce_timeout = 1000; // Delay in milliseconds
    private const int input_timeout = 7000;
    private const int wake_word_timeout = 5000; // 10 seconds timeout for no wake word detection

    // pag store han current result and previous
    private string previous_partial = string.Empty;
    private string current_partial = string.Empty;

    //keyboard dlls didi
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);
    private const int KEYEVENTF_KEYUP = 0x0002;

    //mga keys ig map didi

    private const byte VK_LWIN = 0x5B;
    private const byte VK_SNAPSHOT = 0x2C;

    // dlls kanan mouse variables
    [DllImport("user32.dll", SetLastError = true)]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;

    // dlls kanan pag get current window
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    // dlls pan close current window
    [DllImport("user32.dll")]
    private static extern bool SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    private const uint WM_CLOSE = 0x0010;

    //pan audio control ha windows
    private static MMDeviceEnumerator deviceEnumerator = new MMDeviceEnumerator();
    private static MMDevice device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

    // waray waray
    //string model_path = @"assets\models\waray_1.pbmm";
    //string scorer_path = @"assets\models\waray_dnc.scorer";

    // english
    string model_path = @"assets\models\delta15.pbmm";
    string commands_scorer = @"assets\models\commands.scorer";
    string typing_scorer = @"assets\models\sherwoodschool_plus_commands.scorer";

    int deepspeech_confidence = -50;

    // importante para diri mag error an memory corrupt ha deepspeech model
    private readonly object streamLock = new object();

    public LiveTranscription(ASRWindow asr_window, IntentWindow intent_window, MainWindow main_window, ShowItems show_items, CameraMouse camera_mouse) // 
    {
        this.asr_window = asr_window ?? throw new ArgumentNullException(nameof(asr_window));
        this.intent_window = intent_window ?? throw new ArgumentNullException(nameof(intent_window));
        this.main_window = main_window ?? throw new ArgumentNullException(nameof(main_window));
        this.show_items = show_items ?? throw new ArgumentNullException(nameof(show_items));
        this.camera_mouse = camera_mouse ?? throw new ArgumentNullException(nameof(camera_mouse));
        vad = new WebRtcVad
        {
            SampleRate = WebRtcVadSharp.SampleRate.Is16kHz,
            FrameLength = WebRtcVadSharp.FrameLength.Is20ms,
            OperatingMode = OperatingMode.Aggressive
        };

        // initialize python
        // initialize intent recognition
        Task.Run(() =>
        {
            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = @"C:\Users\super.admin\AppData\Local\Programs\Python\Python312\python.exe",
                Arguments = "C:\\Users\\super.admin\\Desktop\\Capstone\\ATEDNIULI\\edn-app\\ATEDNIULI\\python\\intent.py",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                EnvironmentVariables =
                    {
                        { "PYTHONIOENCODING", "utf-8:replace" }
                    }
            };

            using (Process process = Process.Start(start))
            {
                // Optionally read the output of the Python script
                using (var reader = process.StandardOutput)
                {
                    string result = reader.ReadToEnd();
                    Console.WriteLine(result);
                }

                // Optionally read the error output of the Python script
                using (var errorReader = process.StandardError)
                {
                    string error = errorReader.ReadToEnd();
                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine($"Error: {error}");
                    }
                }
            }
        });

        Task.Run(() =>
        {
            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = @"C:\Users\super.admin\AppData\Local\Programs\Python\Python312\python.exe",
                Arguments = "C:\\Users\\super.admin\\Desktop\\Capstone\\ATEDNIULI\\edn-app\\ATEDNIULI\\python\\grid_inference_optimized.py",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                //CreateNoWindow = true, // Prevents the console window from appearing
                EnvironmentVariables =
                {
                    { "PYTHONIOENCODING", "utf-8:replace" }
                }
            };
            using (Process process = Process.Start(start))
            {
                // Optionally read the output of the Python script
                using (var reader = process.StandardOutput)
                {
                    string result = reader.ReadToEnd();
                    Console.WriteLine(result);
                }

                // Optionally read the error output of the Python script
                using (var errorReader = process.StandardError)
                {
                    string error = errorReader.ReadToEnd();
                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine($"Error: {error}");
                    }
                }
            }
        });

        // pag communicate ha python code ha fld nga ma wait anay bago mag load an python bago mag continue ha rest of the program
        using (var notifySocketIR = new PullSocket("tcp://localhost:6970"))
        {
            Console.WriteLine("Waiting for the model to be ready...");
            string readyMessage = notifySocketIR.ReceiveFrameString();
            Console.WriteLine(readyMessage); // Print the "ready" message
        }

        // pan run hin code
        InitializeIntentSocket();

        // timers
        InitializeTimer();

        show_items.ItemDetected += CheckDetected;
    }

    // zmq stuffs
    private void InitializeIntentSocket()
    {
        if (socket != null && socket.IsDisposed == false)
        {
            socket.Close();  // Close the existing socket
            socket.Dispose(); // Release resources associated with the socket
        }

        // Create a new socket instance
        socket = new RequestSocket("tcp://localhost:6969");
    }

    private bool itemDetected = false;
    private void CheckDetected(bool isDetected)
    {
        if (isDetected)
        {
            Console.WriteLine("An item has been detected.");
            itemDetected = true;
        }
        else
        {
            Console.WriteLine("No item detected.");
            itemDetected = false;
        }
    }

    public class DetectionResult
    {
        public int TileX { get; set; }
        public int TileY { get; set; }
        public List<Detection> Detections { get; set; }
    }

    public class Detection
    {
        public int X { get; set; }          // x coordinate
        public int Y { get; set; }          // y coordinate
        public string ClassName { get; set; } // class name
    }

    public bool showed_detected = false;
    public void DetectScreen()
    {
        if (showed_detected == false)
        {
            show_items.ListClickableItemsInCurrentWindow();
            var clickable_items = show_items.GetClickableItems();

            if (clickable_items != null)
            {
                showed_detected = true;
                UpdateUI(() => main_window.HighlightOD(showed_detected));
            }
        }
    }

    public void load_model(string model_path, string scorer_path)
    {
        UpdateUI(() => asr_window.AppendText("Loading model..."));
        deep_speech_model = new DeepSpeech(model_path);
        UpdateUI(() => asr_window.AppendText("Model loaded"));

        UpdateUI(() => asr_window.AppendText("Loading scorer..."));
        deep_speech_model.EnableExternalScorer(scorer_path);
    }

    public void SwitchScorer(string scorerPath)
    {
        if (deep_speech_model == null) return;

        lock (streamLock)
        {
            try
            {
                // Disable the current scorer if active
                deep_speech_model.DisableExternalScorer();

                // Enable the new scorer
                deep_speech_model.EnableExternalScorer(scorerPath);
                Console.WriteLine($"Switching scorer to: {scorerPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }

    // Fields for stream rate limiting
    private DateTime last_stream_finalize_time = DateTime.MinValue;
    private const int stream_finalize_cooldown = 5000; // 5 seconds cooldown between finalizations

    // transcfiption function
    public void StartTranscription()
    {
        try
        {
            load_model(model_path, commands_scorer);

            wave_in_event = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 1),
                BufferMilliseconds = 500 // no this doesnt affect my problem
            };

            deep_speech_stream = deep_speech_model.CreateStream();
            is_running = true;

            wave_in_event.DataAvailable += OnDataAvailable;
            wave_in_event.RecordingStopped += OnRecordingStopped;

            UpdateUI(() => asr_window.AppendText("Starting microphone..."));
            wave_in_event.StartRecording();

            UpdateUI(() =>
            {
                asr_window.HideWithFadeOut();
            });
        }
        catch (Exception ex)
        {
            UpdateUI(() => asr_window.AppendText($"Error: {ex.Message}"));
        }
    }

    private void OnRecordingStopped(object sender, StoppedEventArgs e) // emergency stop handling
    {
        if (e.Exception != null)
        {
            UpdateUI(() => asr_window.AppendText($"Recording Stopped Error: {e.Exception.Message}"));
        }

        if (is_running)
        {
            StartTranscription();
        }
    }

    // timer function
    private void InitializeTimer()
    {
        inactivity_timer = new System.Timers.Timer(inactivity_timeout); // 3 seconds
        inactivity_timer.Elapsed += OnInactivityTimerElapsed;
        inactivity_timer.AutoReset = false; // Do not restart automatically

        intent_window_timer = new System.Timers.Timer(intent_window_timeout); // 3 seconds
        intent_window_timer.Elapsed += OnIntentTimerElapsed;
        intent_window_timer.AutoReset = false;

        input_timer = new System.Timers.Timer(input_timeout); // 7 seconds
        input_timer.Elapsed += OnIntentTimerElapsed;
        input_timer.AutoReset = false;

        // Initialize wake word reset timer
        wake_word_timer = new System.Timers.Timer(wake_word_timeout);
        wake_word_timer.Elapsed += OnWakeWordTimeout;
        wake_word_timer.AutoReset = false; // Timer will only trigger once

        debounce_timer = new System.Timers.Timer(debounce_timeout);
        debounce_timer.Elapsed += (sender, e) =>
        {
            isDebounceElapsed = true; // Set the flag when the timer elapses
            debounce_timer.Stop(); // Stop the timer
        };
        debounce_timer.AutoReset = false; // Ensure the timer only runs once
    }
    private bool isDebounceElapsed = false;

    // timer stuff kun mag timeout
    private void OnInactivityTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        try
        {
            Console.WriteLine("Inactivity detected");
            if (current_partial == previous_partial)
            {
                Console.WriteLine("Finalizing...");
                UpdateUI(() => FinalizeStream());
            }
            else
            {
                Console.WriteLine("Restarting...");
                previous_partial = current_partial; // Update previous partial
                inactivity_timer.Start(); // Restart timer for further inactivity
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in OnInactivityTimerElapsed: {ex.Message}");
        }
    }

    private void OnWakeWordTimeout(object sender, System.Timers.ElapsedEventArgs e)
    {
        // No wake word detected within timeout, finalize and reset stream
        inactivity_timer.Stop();
        Console.WriteLine("Wake word not detected within timeout, resetting stream.");
        FinalizeStream();
    }

    private void OnIntentTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        //UpdateUI(() => intent_window.Hide());
    }

    private void OnInputTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        try
        {
            Console.WriteLine("Input is too long resetting stream");
            inactivity_timer.Stop();
            UpdateUI(() =>
            {
                FinalizeStream();
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in Input Timer: {ex.Message}");
        }
    }

    private string received = "";

    private string send_to_intent = "";
    // Forcefully end the stream
    List<string> numberStrings = new List<string>
        {
            "one", "two", "three", "four ", "five", "six ", "seven ", "eight ", "nine ", "ten",
            "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen",
            "eighteen", "nineteen", "twenty", "twenty one", "twenty two", "twenty three",
            "twenty four", "twenty five", "twenty six", "twenty seven", "twenty eight",
            "twenty nine", "thirty", "thirty one", "thirty two", "thirty three",
            "thirty four", "thirty five", "thirty six", "thirty seven", "thirty eight",
            "thirty nine", "forty", "forty one", "forty two", "forty three",
            "forty four", "forty five", "forty six", "forty seven", "forty eight",
            "forty nine", "fifty"
        };
    private void FinalizeStream()
    {
        try
        {
            // Check if cooldown period has passed
            if ((DateTime.Now - last_stream_finalize_time).TotalMilliseconds < stream_finalize_cooldown)
            {
                Console.WriteLine("Stream finalization cooldown active, skipping finalization.");
                return; // Skip finalization if cooldown is active
            }

            last_stream_finalize_time = DateTime.Now;
            // Offload work to a background task
            Task.Run(() =>
            {
                lock (streamLock)
                {
                    string final_result_from_stream = deep_speech_model.FinishStream(deep_speech_stream);

                    send_to_intent = RemoveWakeWord(final_result_from_stream, wake_word);

                    // Perform socket operations in a background thread
                    socket.SendFrame(send_to_intent);
                    string receivedMessage = socket.ReceiveFrameString();
                    received = receivedMessage;

                    if (wake_word_detected && !commandExecuted)
                    {
                        UpdateUI(() => show_items.NotificationLabel.Content = $"Executing: {received}");
                        // Check for specific commands
                        if (received == "OpenChrome")
                        {
                            StartProcess("chrome");
                        }
                        else if (received == "OpenWord")
                        {
                            StartProcess("winword");
                        }
                        else if (received == "OpenExcel")
                        {
                            StartProcess("excel");
                        }
                        else if (received == "OpenPowerpoint")
                        {
                            StartProcess("powerpnt");
                        }
                        else if (received == "ScreenShot")
                        {
                            ScreenShot();
                        }
                        else if (received == "CloseApp")
                        {
                            CloseApp();
                        }
                        else if (received == "OpenExplorer")
                        {
                            StartProcess("explorer");
                        }
                        else if (received == "OpenSettings")
                        {
                            StartProcess("ms-settings:");
                        }
                        else if (received == "OpenNotepad")
                        {
                            StartProcess("notepad");
                        }
                        else if (received == "VolumeUp")
                        {
                            VolumeUp();
                        }
                        else if (received == "VolumeDown")
                        {
                            VolumeDown();
                        }
                        else if (received == "MouseControl")
                        {
                            OpenMouse();
                        }
                        else if (received == "MouseControlOff")
                        {
                            CloseMouse();
                        }
                        else if (received == "ShowItems")
                        {
                            DetectScreen();
                        }
                    }
                    else if (!wake_word_detected && !commandExecuted)
                    {
                        if (showed_detected && !itemDetected)
                        {
                            // Iterate through the predefined number strings
                            for (int number_index = 0; number_index < numberStrings.Count; number_index++)
                            {
                                string number = numberStrings[number_index]; // Get the current number as a string

                                // Here we handle the command for each number string
                                HandleCommand(number, final_result_from_stream, ref execute_number_command_count, () =>
                                {
                                    ProcessSpokenTag($"{number_index + 1}"); // Use number_index + 1 if you want to represent 1-based index
                                });
                            }
                        }
                    }

                    commandExecuted = false;
                    wake_word_detected = false;
                    ResetCommandCounts();

                    // Dispose and reset the stream
                    deep_speech_stream.Dispose();
                    deep_speech_stream = deep_speech_model.CreateStream();
                }
            }).ContinueWith(t =>
            {
                // Check if an exception was thrown in the Task
                if (t.Exception != null)
                {
                    Console.WriteLine($"Error finalizing stream: {t.Exception.InnerException.Message}");
                    // Handle exceptions (log them, show error messages, etc.)
                    return;
                }

                // Update the UI on the UI thread
                UpdateUI(() =>
                {
                    if (asr_window.IsVisible)
                    {
                        try
                        {
                            intent_window.AppendText($"Intent: {received}");
                            intent_window_timer.Start();
                            main_window.UpdateListeningIcon(false);
                            asr_window.HideWithFadeOut();

                            Console.WriteLine("Stream finalized successfully.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error updating UI: {ex.Message}");
                        }
                    }
                });
            }, TaskScheduler.FromCurrentSynchronizationContext()); // Ensure the continuation runs on the UI thread context
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in FinalizeStream: {ex.Message}");
        }
    }

    private void OnDataAvailable(object sender, WaveInEventArgs e)
    {
        if (!is_running || e.BytesRecorded <= 0)
        {
            UpdateUI(() => asr_window.AppendText("No audio data recorded."));
            return;
        }

        // timers start everytime audio is detedted

        StartInactivityTimer();

        StartWakeWordTimer();

        StartInputTimer();

        // Offload audio processing to avoid blocking the main thread
        Task.Run(() =>
        {
            ProcessAudioData(e.Buffer, e.BytesRecorded);
        });
    }

    private bool is_stream_ready = true;
    private void ProcessAudioData(byte[] buffer, int bytesRecorded)
    {
        if (deep_speech_stream == null || !is_stream_ready) return; // Avoid feeding if stream is not ready

        short[] short_buffer = new short[bytesRecorded / 2];
        Buffer.BlockCopy(buffer, 0, short_buffer, 0, bytesRecorded);

        Task.Run(() =>
        {
            lock (streamLock)
            {
                if (vad.HasSpeech(short_buffer))
                {
                    is_stream_ready = false; // Block further feeds during processing
                    
                    ProcessSpeech(short_buffer);

                    is_stream_ready = true; // Ready for next audio feed
                }
                else
                {
                    HandleNoSpeechDetected(); // Finalizes stream if no speech is detected
                }
            }
        });
    }

    private void ProcessSpeech(short[] short_buffer)
    {
        // Lock only around the critical section to minimize delay
        lock (streamLock)
        {
            try
            {
                // Feed audio to the model
                deep_speech_model.FeedAudioContent(deep_speech_stream, short_buffer, (uint)short_buffer.Length);

                // Get intermediate decoding with metadata (confidence values)
                var metadata = deep_speech_model.IntermediateDecodeWithMetadata(deep_speech_stream, 1);

                if (metadata == null || metadata.Transcripts.Length == 0) return;

                var partial_result = metadata.Transcripts[0].Tokens.Select(t => t.Text).Aggregate((a, b) => a + b).Trim();
                float confidence = (float)metadata.Transcripts[0].Confidence;

                if (string.IsNullOrEmpty(partial_result)) return;

                current_partial = partial_result;

                // Process wake word detection or regular transcription
                if (wake_word_required)
                {
                    HandleWakeWord(partial_result, confidence);
                }
                else
                {
                    if (confidence > deepspeech_confidence)
                    {
                        // Proceed with transcription handling
                        ShowTranscription(partial_result);
                        //ProcessCommand(partial_result);
                    }
                    else
                    {
                        // Handle low confidence case (e.g., log, retry, prompt user)
                        Console.WriteLine("Low confidence in transcription. Please repeat.");
                    }

                    // Optionally log or display the confidence score
                    Console.WriteLine($"Confidence: {confidence}");
                }
            }
            catch (AccessViolationException ex)
            {
                Console.WriteLine($"AccessViolationException: {ex.Message}");
                deep_speech_model.FinishStream(deep_speech_stream);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General Exception: {ex.Message}");
            }
        }
    }

    private void HandleWakeWord(string partial_result, double confidence)
    {
        if (itemDetected && confidence > deepspeech_confidence)
        {
            for (int number_index = 0; number_index < numberStrings.Count; number_index++)
            {
                string number = numberStrings[number_index]; // Get the current number as a string

                // Here we handle the command for each number string
                HandleCommand(number, partial_result, ref execute_number_command_count, () =>
                {
                    show_items.ExecuteAction(number_index + 1); // Use number_index + 1 if you want to represent 1-based index
                });
            }
        }

        if (partial_result.Contains(wake_word))
        {
            if (partial_result.IndexOf(wake_word, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                wake_word_detected = true;

                if (wake_word_timer.Enabled)
                {
                    wake_word_timer.Close();
                }
                
                partial_result = RemoveWakeWord(partial_result, wake_word);

                ShowTranscription(partial_result);
                ProcessCommand(partial_result);
            }
        }
    }

    private void ShowTranscription(string partial_result)
    {
        UpdateUI(() => main_window.UpdateListeningIcon(true));

        UpdateUI(() =>
        {
            try
            {
                if (!asr_window.IsVisible)
                {
                    asr_window.ShowWithFadeIn();
                }
                asr_window.AppendText($"You said: {partial_result}", true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return;
            }
        });

        //UpdateUI(() => intent_window.Show());
    }

    private void HandleNoSpeechDetected()
    {
        UpdateUI(() =>
        {
            if (!asr_window.IsVisible) return;

            FinalizeStream(); // Finalize the stream when no speech is detected
        });
    }

    // Reset inactivity timer and setup event to process commands after inactivity
    private void StartInactivityTimer()
    {
        if (!inactivity_timer.Enabled)
        {
            inactivity_timer.Stop(); // Stop the timer first
            inactivity_timer.Start(); // Restart the timer
            Console.WriteLine("started the inactivity timer");
        }
    }

    private void StartWakeWordTimer()
    {
        if (!wake_word_timer.Enabled)
        {
            wake_word_timer.Stop(); // Stop the timer first
            wake_word_timer.Start(); // Restart the timer
            Console.WriteLine("started the wake word timer");
        }
    }

    private void StartInputTimer()
    {
        if (!input_timer.Enabled)
        {
            input_timer.Stop(); // Stop the timer first
            input_timer.Start(); // Restart the timer
            Console.WriteLine("started the input timer");
        }
    }

    // Helper method to update UI on the main thread
    private void UpdateUI(Action action)
    {
        if (main_window.Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            main_window.Dispatcher.Invoke(action);
        }
    }

    public static void VolumeUp(float amount = 0.1f) // Adjust amount as needed
    {
        float newVolume = Math.Min(device.AudioEndpointVolume.MasterVolumeLevelScalar + amount, 1.0f);
        device.AudioEndpointVolume.MasterVolumeLevelScalar = newVolume;
    }

    public static void VolumeDown(float amount = 0.1f) // Adjust amount as needed
    {
        float newVolume = Math.Max(device.AudioEndpointVolume.MasterVolumeLevelScalar - amount, 0.0f);
        device.AudioEndpointVolume.MasterVolumeLevelScalar = newVolume;
    }

    private string RemoveWakeWord(string transcription, string wake_word)
    {
        // Find the index of the wake word
        int index = transcription.IndexOf(wake_word, StringComparison.OrdinalIgnoreCase);

        // If the wake word is found, remove everything up to and including the wake word
        if (index >= 0)
        {
            // Calculate the starting index of the text after the wake word
            int startAfterWakeWord = index + wake_word.Length;

            // Return the remaining text after the wake word, trimmed of any leading/trailing whitespace
            return transcription.Substring(startAfterWakeWord).Trim();
        }

        // If the wake word is not found, return the original transcription
        return transcription;
    }


    // TODO - himua an tanan na commands na gamiton an HandleCommand function
    private bool commandExecuted = false;
    private void ProcessCommand(string transcription) // tanan hin commands naagi didi
    {
        if (string.IsNullOrEmpty(transcription)) return;

        // mouse control commands
        if (transcription.IndexOf("open mouse", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            UpdateUI(() => show_items.NotificationLabel.Content = "Opening Mouse");
            OpenMouse();
            commandExecuted = true;
            UpdateUI(() => FinalizeStream());
        }

        if (transcription.IndexOf("close mouse", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            UpdateUI(() => show_items.NotificationLabel.Content = "Closing Mouse");
            CloseMouse();
            commandExecuted = true;
            UpdateUI(() => FinalizeStream());
        }

        // type something
        //if (transcription.IndexOf("type", StringComparison.OrdinalIgnoreCase) >= 0)
        //{
        //    SwitchScorer(typing_scorer);
        //    UpdateUI(() => FinalizeStream());
        //    UpdateUI(() => asr_window.Show());
        //}

        if (transcription.StartsWith("search", StringComparison.OrdinalIgnoreCase))
        {
            string search_query = transcription.Substring("search".Length).Trim();
            if (!string.IsNullOrEmpty(search_query))
            {
                OpenBrowserWithSearch(search_query);
                return; // Exit after processing this command
            }
        }

        HandleCommand("open calculator", transcription, ref calculator_command_count, () => StartProcess("calc"));
        HandleCommand("show items", transcription, ref show_items_command_count, () => DetectScreen());
        HandleCommand("stop showing", transcription, ref show_items_command_count, () => CloseShowItemsWindow());
        HandleCommand("open notepad", transcription, ref notepad_command_count, () => StartProcess("notepad"));
        HandleCommand("close window", transcription, ref close_window_command_count, () => SimulateKeyPress(Keys.ControlKey)); // Customize as needed
        HandleCommand("open chrome", transcription, ref chrome_command_count, () => StartProcess("chrome"));
        HandleCommand("open edge", transcription, ref edge_command_count, () => StartProcess("msedge"));
        HandleCommand("open word", transcription, ref word_command_count, () => StartProcess("winword"));
        HandleCommand("open excel", transcription, ref excel_command_count, () => StartProcess("excel"));
        HandleCommand("open powerpoint", transcription, ref powerpoint_command_count, () => StartProcess("powerpnt"));
        HandleCommand("open file manager", transcription, ref file_manager_command_count, () => StartProcess("explorer"));
        HandleCommand("switch", transcription, ref switch_command_count, () => SimulateKeyPress(Keys.Tab));

        //HandleCommand("left", transcription, ref left_command_count, () => SimulateKeyPress(Keys.Left));
        //HandleCommand("right", transcription, ref right_command_count, () => SimulateKeyPress(Keys.Right));
        //HandleCommand("up", transcription, ref up_command_count, () => SimulateKeyPress(Keys.Up));
        //HandleCommand("down", transcription, ref down_command_count, () => SimulateKeyPress(Keys.Down));

        HandleCommand("enter", transcription, ref enter_command_count, () => SimulateKeyPress(Keys.Enter));
        HandleCommand("close application", transcription, ref close_calculator_command_count, () => CloseApp());
        HandleCommand("scroll up", transcription, ref scroll_up_command_count, () => ScrollUp(200));
        HandleCommand("scroll down", transcription, ref scroll_down_command_count, () => ScrollDown(-200));
        HandleCommand("screenshot", transcription, ref screenshot_command_count, () => ScreenShot());
        HandleCommand("volume up", transcription, ref volume_up_command_count, () => VolumeUp());
        HandleCommand("volume down", transcription, ref volume_down_command_count, () => VolumeDown());
        HandleCommand("open settings", transcription, ref settings_command_count, () => StartProcess("ms-settings:"));
    }

    private bool HandleCommand(string commandPhrase, string transcription, ref int commandCount, Action action)
    {
        if (transcription.IndexOf(commandPhrase, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            commandCount++;
            action();
            commandExecuted = true;

            UpdateUI(() => show_items.NotificationLabel.Content = $"Executed: {commandPhrase}");
            UpdateUI(() => FinalizeStream());

            return true; // Indicate that a command was executed
        }
        return false;
    }

    private void ProcessSpokenTag(string spokenTag)
    {
        var clickableItems = show_items.GetClickableItems();

        if (clickableItems == null || clickableItems.Count == 0)
        {
            Console.WriteLine("No clickable items found.");
            return; // Exit early since there are no items to process
        }

        if (int.TryParse(spokenTag, out int tagNumber))
        {
            clickableItems = show_items.GetClickableItems();
            if (tagNumber > 0 && tagNumber <= clickableItems.Count)
            {
                var selectedItem = clickableItems[tagNumber - 1]; // 0-based index

                // Convert System.Windows.Rect to OpenCvSharp.Rect
                var convertedRect = ConvertToOpenCvRect(selectedItem.BoundingRectangle);

                UpdateUI(() => show_items.NotificationLabel.Content = $"Clicking {tagNumber}");
                ClickItem(convertedRect); // Perform click on item

                show_items.RemoveTagsNoTimer();
                showed_detected = false;
                UpdateUI(() => main_window.HighlightOD(showed_detected));
            }
            else
            {
                Console.WriteLine("Invalid tag number");
            }
        }
        else
        {
            Console.WriteLine("Could not recognize a valid tag number");
        }
    }

    // Method to convert System.Windows.Rect to OpenCvSharp.Rect
    private OpenCvSharp.Rect ConvertToOpenCvRect(System.Windows.Rect rect)
    {
        return new OpenCvSharp.Rect((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
    }

    private void ClickItem(Rect boundingRect)
    {
        // Simulate a mouse click at the center of the bounding rectangle
        double x = boundingRect.Left + boundingRect.Width / 2;
        double y = boundingRect.Top + boundingRect.Height / 2;

        // Use the method to simulate the mouse click (you may need to implement this)
        ClickMouseAt(x, y);
    }

    private void ClickMouseAt(double x, double y)
    {
        // Simulate the mouse movement and click event at the specified coordinates
        System.Windows.Forms.Cursor.Position = new System.Drawing.Point((int)x, (int)y);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
    }

    private ShowItems showItemsWindow; // Field to hold the window reference

    private void CloseShowItemsWindow()
    {
        // Check if the window is currently open and close it
        if (showItemsWindow != null && showItemsWindow.IsVisible)
        {
            showItemsWindow.Close();
            showItemsWindow = null; // Reset the reference after closing
        }
    }

    public static void ScrollUp(int steps = 120) // Adjust steps as needed
    {
        mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)steps, 0);
    }

    public static void ScrollDown(int steps = 120) // Adjust steps as needed
    {
        mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)-steps, 0);
    }

    public static void ScreenShot()
    {
        keybd_event(VK_LWIN, 0, 0, 0);
        keybd_event(VK_SNAPSHOT, 0, 0, 0);
        keybd_event(VK_SNAPSHOT, 0, KEYEVENTF_KEYUP, 0);
        keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, 0);
    }

    public void OpenBrowserWithSearch(string query)
    {
        if (!has_searched)
        {
            string url = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            has_searched = true;
        }
    }

    private void SimulateMouseClick() // pan setup hin click
    {
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
    }

    private void SimulateKeyPress(Keys key) // pan enable hin keys ha keyboard
    {
        string keyString;

        switch (key)
        {
            case Keys.Enter:
                keyString = "{ENTER}";
                break;
            case Keys.Tab:
                keyString = "{TAB}";
                break;
            case Keys.Escape:
                keyString = "{ESC}";
                break;
            case Keys.Up:
                keyString = "{UP}";
                break;
            case Keys.Down:
                keyString = "{DOWN}";
                break;
            case Keys.Left:
                keyString = "{LEFT}";
                break;
            case Keys.Right:
                keyString = "{RIGHT}";
                break;
            default:
                keyString = key.ToString();
                break;
        }

        SendKeys.SendWait(keyString);
    }

    private void StartProcess(string processName) // pag open hin app
    {
        try
        {
            Process.Start(processName);
        }
        catch (Exception ex)
        {
            UpdateUI(() => asr_window.AppendText($"Failed to start {processName}: {ex.Message}", true));
        }
    }

    private void CloseApp() // pag close hin currently used app/window
    {
        IntPtr handle = GetForegroundWindow();
        if (handle != IntPtr.Zero)
        {
            SendMessage(handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }
        UpdateUI(() => asr_window.AppendText("Closing current window...", true));
    }

    private void ResetCommandCounts() // pan reset hin mga command count, kailangan command count for now kay para diri na execute an mga commands pa utro utro kay an partial transcription
    {
        click_command_count = 0;
        calculator_command_count = 0;
        notepad_command_count = 0;
        close_window_command_count = 0;
        chrome_command_count = 0;
        word_command_count = 0;
        excel_command_count = 0;
        powerpoint_command_count = 0;
        edge_command_count = 0;
        file_manager_command_count = 0;
        settings_command_count = 0;
        type_command_count = 0;
        close_calculator_command_count = 0;
        close_app_command_count = 0;
        scroll_up_command_count = 0;
        scroll_down_command_count = 0;
        screenshot_command_count = 0;
        volume_up_command_count = 0;
        volume_down_command_count = 0;
        search_command_count = 0;
        show_items_command_count = 0;
        execute_number_command_count = 0;

        switch_command_count = 0;
        left_command_count = 0;
        right_command_count = 0;
        up_command_count = 0;
        down_command_count = 0;
        enter_command_count = 0;

        has_searched = false;
    }

    public void StopTranscription() // pan emergency la ini
    {
        if (!is_running) return;

        is_running = false;
        wave_in_event.StopRecording();
        wave_in_event.Dispose();

        try
        {
            string final_result = deep_speech_model.FinishStream(deep_speech_stream);
            UpdateUI(() =>
            {
                asr_window.AppendText("Final Transcription: " + final_result);
                if (asr_window.IsVisible)
                {
                    asr_window.HideWithFadeOut();
                }
            });
        }
        catch (Exception ex)
        {
            UpdateUI(() => asr_window.AppendText($"Error finishing stream: {ex.Message}"));
        }
        finally
        {
            deep_speech_stream.Dispose();
            deep_speech_model.Dispose();
            vad.Dispose();
            UpdateUI(() => asr_window.AppendText("Transcription stopped."));
        }
    }

    private bool camera_mouse_opened = false;
    private void OpenMouse()
    {
        if (!camera_mouse_opened)
        {
            camera_mouse_opened = true;
            Task.Run(() =>
            {
                // Assuming you have a method StartCameraMouse in your cameramouse class
                camera_mouse.StartCameraMouse();
                UpdateUI(() =>
                {
                    asr_window.AppendText("Camera mouse activated.");
                });
            });
        }
    }

    // TODO - ig improve pa ini kay medyo clunky pa an pag close and open
    private void CloseMouse()
    {
        camera_mouse_opened = false;
        // Assuming you have a method StopCameraMouse in your cameramouse class to stop the mouse functionality
        camera_mouse.StopCameraMouse(); // Implement this in cameramouse.cs
        UpdateUI(() =>
        {
            asr_window.AppendText("Camera mouse deactivated.");
        });
    }

    public bool IsActiveWindow(string applicationName)
    {
        IntPtr handle = GetForegroundWindow();
        if (handle == IntPtr.Zero) return false;

        System.Text.StringBuilder windowText = new System.Text.StringBuilder(256);
        GetWindowText(handle, windowText, windowText.Capacity);

        return windowText.ToString().ToLower().Contains(applicationName.ToLower());
    }
}

// TODO - ig aapply pa an intent recognition ngan yolo functionalities
// TODO - REFACTOR THIS ALL! to fix crashes and instability.