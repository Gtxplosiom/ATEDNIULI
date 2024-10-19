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
    private readonly cameramouse camera_mouse;
    private WaveInEvent wave_in_event;
    private DeepSpeechStream deep_speech_stream;
    private DeepSpeech deep_speech_model;
    private WebRtcVad vad;

    private bool wake_word_detected = false;
    private bool is_running;

    private string wake_word = "hello";

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

    private int switch_command_count = 0;
    private int left_command_count = 0;
    private int right_command_count = 0;
    private int up_command_count = 0;
    private int down_command_count = 0;
    private int enter_command_count = 0;

    private bool has_searched = false; // Flag to track search execution

    private string lastTypedPhrase = string.Empty;
    private bool typing_mode = false;

    private bool wake_word_required = false; // Default to requiring the wake word

    string app_directory = Directory.GetCurrentDirectory(); // possible gamiton labi pag reference hin assets kay para robust hiya ha iba iba na systems

    private RequestSocket socket;
    private SubscriberSocket pullsocket;

    private System.Timers.Timer inactivity_timer;
    private System.Timers.Timer intent_window_timer;
    private System.Timers.Timer input_timer;

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
    string scorer_path = @"assets\models\commands.scorer";
    string ww_scorer_path = @"assets\models\wake_word.scorer";

    int deepspeech_confidence = -40;

    // importante para diri mag error an memory corrupt ha deepspeech model
    private readonly object streamLock = new object();

    public LiveTranscription(ASRWindow asr_window, IntentWindow intent_window, MainWindow main_window, ShowItems show_items, cameramouse camera_mouse) // 
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
            OperatingMode = OperatingMode.VeryAggressive
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

        //DetectScreen(); ## for testing
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

    public void load_model(string model_path, string scorer_path)
    {
        asr_window.Dispatcher.Invoke(() => asr_window.AppendText("Loading model..."));
        deep_speech_model = new DeepSpeech(model_path);
        asr_window.Dispatcher.Invoke(() => asr_window.AppendText("Model loaded"));

        asr_window.Dispatcher.Invoke(() => asr_window.AppendText("Loading scorer..."));
        deep_speech_model.EnableExternalScorer(scorer_path);
        deep_speech_model.AddHotWord("hello", 7);
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

    public void DetectScreen()
    {
        show_items.ListClickableItemsInCurrentWindow();
        var clickable_items = show_items.GetClickableItems();
    }

    // transcfiption function
    public void StartTranscription()
    {
        try
        {
            load_model(model_path, scorer_path);

            wave_in_event = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 1),
                BufferMilliseconds = 400 // no this doesnt affect my problem
            };

            deep_speech_stream = deep_speech_model.CreateStream();
            is_running = true;

            wave_in_event.DataAvailable += OnDataAvailable;
            wave_in_event.RecordingStopped += OnRecordingStopped;

            asr_window.Dispatcher.Invoke(() => asr_window.AppendText("Starting microphone..."));
            wave_in_event.StartRecording();

            asr_window.Dispatcher.Invoke(() =>
            {
                asr_window.Hide();
            });
        }
        catch (Exception ex)
        {
            asr_window.Dispatcher.Invoke(() => asr_window.AppendText($"Error: {ex.Message}"));
        }
    }

    private void OnRecordingStopped(object sender, StoppedEventArgs e) // emergency stop handling
    {
        if (e.Exception != null)
        {
            asr_window.Dispatcher.Invoke(() => asr_window.AppendText($"Recording Stopped Error: {e.Exception.Message}"));
        }

        if (is_running)
        {
            StartTranscription();
        }
    }

    // timer function
    private void InitializeTimer()
    {
        inactivity_timer = new System.Timers.Timer(1500); // 1.5 seconds
        inactivity_timer.Elapsed += OnInactivityTimerElapsed;
        inactivity_timer.AutoReset = false; // Do not restart automatically

        intent_window_timer = new System.Timers.Timer(1500); // 1.5 seconds
        intent_window_timer.Elapsed += OnIntentTimerElapsed;
        intent_window_timer.AutoReset = false;

        input_timer = new System.Timers.Timer(3000); // 3 seconds
        input_timer.Elapsed += OnIntentTimerElapsed;
        input_timer.AutoReset = false;
    }

    // timer stuff kun mag timeout
    private void OnInactivityTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        try
        {
            Console.WriteLine("Inactivity detected");
            if (current_partial == previous_partial)
            {
                Console.WriteLine("Finalizing...");
                asr_window.Dispatcher.Invoke(() =>
                {
                    FinalizeStream();
                });
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


    private void OnIntentTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        intent_window.Dispatcher.Invoke(() => intent_window.Hide());
    }

    private void OnInputTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        try
        {
            asr_window.Dispatcher.Invoke(() =>
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

    // Forcefully end the stream
    private void FinalizeStream()
    {
        try
        {
            // Offload work to a background task
            Task.Run(() =>
            {
                lock (streamLock)
                {
                    string final_result_from_stream = deep_speech_model.FinishStream(deep_speech_stream);

                    // Perform socket operations in a background thread
                    socket.SendFrame(final_result_from_stream);
                    string receivedMessage = socket.ReceiveFrameString();
                    received = receivedMessage;

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
                asr_window.Dispatcher.Invoke(() =>
                {
                    if (asr_window.IsVisible)
                    {
                        try
                        {
                            intent_window.AppendText($"Intent: {received}");
                            intent_window_timer.Start();
                            asr_window.Hide();

                            wake_word_detected = false;
                            ResetCommandCounts();

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

        // Offload audio processing to avoid blocking the main thread
        Task.Run(() =>
        {
            ProcessAudioData(e.Buffer, e.BytesRecorded);
        });
    }


    private void ProcessAudioData(byte[] buffer, int bytesRecorded)
    {
        short[] short_buffer = new short[bytesRecorded / 2];
        Buffer.BlockCopy(buffer, 0, short_buffer, 0, bytesRecorded);

        // Run VAD detection asynchronously
        Task.Run(() =>
        {
            if (!vad.HasSpeech(short_buffer))
            {
                HandleNoSpeechDetected();
                return;
            }

            // Offload to DeepSpeech processing if VAD confirms speech
            Task.Run(() => ProcessSpeech(short_buffer));
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

                ResetInactivityTimer();

                if (!input_timer.Enabled)
                {
                    input_timer.Start();
                }

                // Process wake word detection or regular transcription
                if (wake_word_required)
                {
                    HandleWakeWord(partial_result);
                }
                else
                {
                    if (confidence > deepspeech_confidence)
                    {
                        // Proceed with transcription handling
                        ShowTranscription(partial_result);
                        ProcessCommand(partial_result);
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


    private void HandleWakeWord(string partial_result)
    {
        int new_click_count = CountClicks(partial_result);

        if (new_click_count > click_command_count)
        {
            SimulateMouseClicks(new_click_count);
        }

        if (partial_result.IndexOf(wake_word, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            wake_word_detected = true;
            partial_result = RemoveWakeWord(partial_result, wake_word);
            click_command_count = 0;

            // Update UI in a background task
            UpdateUI(() => main_window.SetListeningIcon(true));
            ShowTranscription(partial_result);
        }
    }

    private int CountClicks(string partial_result)
    {
        return partial_result.Split(new[] { "click" }, StringSplitOptions.None).Length - 1;
    }

    private void SimulateMouseClicks(int new_click_count)
    {
        for (int i = 0; i < new_click_count - click_command_count; i++)
        {
            SimulateMouseClick();
        }
        click_command_count = new_click_count;
    }

    private void ShowTranscription(string partial_result)
    {
        UpdateUI(() =>
        {
            main_window.SetListeningIcon(true);
            intent_window.Show();

            if (!asr_window.IsVisible)
            {
                asr_window.Show();
            }
            asr_window.AppendText($"You said: {partial_result}", true);
        });
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
    private void ResetInactivityTimer()
    {
        if (!inactivity_timer.Enabled)
        {
            inactivity_timer.Stop(); // Stop the timer first
            inactivity_timer.Start(); // Restart the timer
            Console.WriteLine("started the inactivity timer");
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

    private string RemoveWakeWord(string transcription, string wake_word) // pan remove hin wake word ha partial para diri ma output
    {
        int index = transcription.IndexOf(wake_word, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            return transcription.Remove(index, wake_word.Length).Trim();
        }
        return transcription;
    }

    // TODO - himua an tanan na commands na gamiton an HandleCommand function
    private void ProcessCommand(string transcription) // tanan hin commands naagi didi
    {
        // clicking commands
        if (string.IsNullOrEmpty(transcription)) return;

        int new_click_count = transcription.Split(new[] { "click" }, StringSplitOptions.None).Length - 1; // enable clicks buffer like multiple clicks sunod sunod
        if (new_click_count > click_command_count)
        {
            int clicks_to_perform = new_click_count - click_command_count;
            asr_window.Dispatcher.Invoke(() => asr_window.AppendText($"Performing {clicks_to_perform} click(s)...", true));
            for (int i = 0; i < clicks_to_perform; i++)
            {
                SimulateMouseClick();
            }
            click_command_count = new_click_count;
        }

        // wip typing
        if (transcription.IndexOf("start typing", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            typing_mode = true;
            // wip
        }

        // mouse control commands
        if (transcription.IndexOf("open mouse", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            OpenMouse();
        }

        // close mouse control
        if (transcription.IndexOf("close mouse", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            CloseMouse();
        }

        // type something
        if (transcription.IndexOf("type", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            TypeText(transcription);
        }

        if (transcription.StartsWith("search", StringComparison.OrdinalIgnoreCase))
        {
            string search_query = transcription.Substring("search".Length).Trim();
            if (!string.IsNullOrEmpty(search_query))
            {
                OpenBrowserWithSearch(search_query);
            }
        }

        HandleCommand("one", transcription, ref show_items_command_count, () =>
        {
            ProcessSpokenTag("1");
        });

        HandleCommand("two", transcription, ref show_items_command_count, () =>
        {
            ProcessSpokenTag("2");
        });

        HandleCommand("three", transcription, ref show_items_command_count, () =>
        {
            ProcessSpokenTag("3");
        });

        HandleCommand("four", transcription, ref show_items_command_count, () =>
        {
            ProcessSpokenTag("4");
        });

        HandleCommand("five", transcription, ref show_items_command_count, () =>
        {
            ProcessSpokenTag("5");
        });

        HandleCommand("six", transcription, ref show_items_command_count, () =>
        {
            ProcessSpokenTag("6");
        });

        HandleCommand("seven", transcription, ref show_items_command_count, () =>
        {
            ProcessSpokenTag("7");
        });

        HandleCommand("eight", transcription, ref show_items_command_count, () =>
        {
            ProcessSpokenTag("8");
        });

        HandleCommand("nine", transcription, ref show_items_command_count, () =>
        {
            ProcessSpokenTag("9");
        });

        HandleCommand("ten", transcription, ref show_items_command_count, () =>
        {
            ProcessSpokenTag("10");
        });

        // Handle other commands
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
        HandleCommand("left", transcription, ref left_command_count, () => SimulateKeyPress(Keys.Left));
        HandleCommand("right", transcription, ref right_command_count, () => SimulateKeyPress(Keys.Right));
        HandleCommand("up", transcription, ref up_command_count, () => SimulateKeyPress(Keys.Up));
        HandleCommand("down", transcription, ref down_command_count, () => SimulateKeyPress(Keys.Down));
        HandleCommand("enter", transcription, ref enter_command_count, () => SimulateKeyPress(Keys.Enter));
        HandleCommand("close application", transcription, ref close_calculator_command_count, () => CloseApp());
        HandleCommand("scroll up", transcription, ref scroll_up_command_count, () => ScrollUp(200));
        HandleCommand("scroll down", transcription, ref scroll_down_command_count, () => ScrollDown(-200));
        HandleCommand("screenshot", transcription, ref screenshot_command_count, () => ScreenShot());
        HandleCommand("open file manager", transcription, ref file_manager_command_count, () => StartProcess("explorer.exe"));
        HandleCommand("volume up", transcription, ref volume_up_command_count, () => VolumeUp());
        HandleCommand("volume down", transcription, ref volume_down_command_count, () => VolumeDown());
        HandleCommand("open settings", transcription, ref settings_command_count, () => StartProcess("ms-settings:"));
    }

    private void HandleCommand(string commandText, string transcription, ref int commandCount, Action action) // ini an pan prevent hin pan execute command like opening hin utro utro
    {
        int new_command_count = transcription.Split(new[] { commandText }, StringSplitOptions.None).Length - 1;
        if (new_command_count > commandCount)
        {
            int commands_to_perform = new_command_count - commandCount;
            asr_window.Dispatcher.Invoke(() => asr_window.AppendText($"Performing {commands_to_perform} {commandText} command(s)...", true));
            for (int i = 0; i < commands_to_perform; i++)
            {
                action.Invoke();
            }
            commandCount = new_command_count;
        }
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

                ClickItem(convertedRect); // Perform click on item
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

    private void TypeText(string text) // pan type - wip
    {
        if (string.IsNullOrEmpty(text)) return;

        string processed_text = ProcessTextForTyping(text);
        if (processed_text == lastTypedPhrase) return;

        SendKeys.SendWait(processed_text);
        lastTypedPhrase = processed_text;
        return;
    }

    private string ProcessTextForTyping(string text) // tanggal an keyword na type, tapos ig output an tanan na words following han keyword
    {
        const string keyword = "type";

        int keywordIndex = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);

        if (keywordIndex >= 0)
        {
            int startIndex = keywordIndex + keyword.Length;

            string result = text.Substring(startIndex).Trim();

            return result;
        }

        return text;
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
            asr_window.Dispatcher.Invoke(() => asr_window.AppendText($"Failed to start {processName}: {ex.Message}", true));
        }
    }

    private void CloseApp() // pag close hin currently used app/window
    {
        IntPtr handle = GetForegroundWindow();
        if (handle != IntPtr.Zero)
        {
            SendMessage(handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }
        asr_window.Dispatcher.Invoke(() => asr_window.AppendText("Closing current window...", true));
    }

    private void CloseApplication(string processName) // pag close hin app - wip
    {
        try
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                process.Kill();
            }
        }
        catch (Exception ex)
        {
            asr_window.Dispatcher.Invoke(() => asr_window.AppendText($"Error closing process {processName}: {ex.Message}"));
        }
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
            asr_window.Dispatcher.Invoke(() =>
            {
                asr_window.AppendText("Final Transcription: " + final_result);
                if (asr_window.IsVisible)
                {
                    asr_window.Hide();
                }
            });
        }
        catch (Exception ex)
        {
            asr_window.Dispatcher.Invoke(() => asr_window.AppendText($"Error finishing stream: {ex.Message}"));
        }
        finally
        {
            deep_speech_stream.Dispose();
            deep_speech_model.Dispose();
            vad.Dispose();
            asr_window.Dispatcher.Invoke(() => asr_window.AppendText("Transcription stopped."));
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
                asr_window.Dispatcher.Invoke(() =>
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
        asr_window.Dispatcher.Invoke(() =>
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