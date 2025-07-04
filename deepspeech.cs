﻿using DeepSpeechClient.Models;
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
using System.Threading;
using OpenCvSharp;
using static ATEDNIULI.ShowItems;
using System.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.Logger;
using static System.Net.Mime.MediaTypeNames;
using FastText.NetWrapper;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using OpenCvSharp.Internal.Vectors;
using System.Management;
using Newtonsoft.Json.Linq;
using System.Net.Http;

class LiveTranscription
{
    private readonly ASRWindow asr_window;
    private readonly IntentWindow intent_window;
    private readonly MainWindow main_window;
    private readonly ShowItems show_items;
    private readonly CameraMouse camera_mouse;
    private readonly HelpWindow help_window;
    private readonly SettingsWindow settings_window;
    private readonly UserGuide user_guide;

    private WaveInEvent wave_in_event;
    private DeepSpeechStream deep_speech_stream;
    private DeepSpeech deep_speech_model;
    private WebRtcVad vad;

    private FastTextWrapper intent_model;

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
    private int window_actions_count = 0;
    private int brightness_command_count = 0;
    private int bookmark_command_count = 0;

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
    private System.Timers.Timer wake_word_timer;
    private System.Timers.Timer debounce_timer;
    private System.Timers.Timer typing_appear_timer;
    private System.Timers.Timer searching_appear_timer;
    private System.Timers.Timer bookmarks_appear_timer;
    private System.Timers.Timer reset_typing_stream_timer;

    private const int debounce_timeout = 1000; // Delay in milliseconds
    private const int wake_word_timeout = 5000; // 10 seconds timeout for no wake word detection
    private const int typing_appear = 2000;
    private const int searching_appear = 2000;
    private const int bookmarks_appear = 2000;
    private const int reset_typing_timeout = 7000;

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
    private const byte VK_CONTROL = 0x11;
    private const byte VK_TAB = 0x09;
    private const byte VK_SHIFT = 0x10;
    private const byte VK_T = 0x54;

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

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    // waray waray
    //string model_path = @"assets\models\waray_1.pbmm";
    //string scorer_path = @"assets\models\waray_dnc.scorer";

    // english
    string model_path = @"assets\models\delta12.pbmm";
    string commands_scorer = @"assets\models\official_commands_scorer.scorer";
    string wake_word_scorer = @"assets\models\ww_numbers.scorer";
    string one_ten_scorer = @"assets\models\1-10.scorer";
    string intent_model_path = @"assets\models\intent_model_bigrams.bin";

    int deepspeech_confidence = -100;

    // importante para diri mag error an memory corrupt ha deepspeech model
    private readonly object streamLock = new object();

    private IWebDriver driver;

    private string GetPythonExecutablePath()
    {
        // Get the user's home directory
        string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Construct the Python executable path
        string python12Path = Path.Combine(userHome, @"AppData\Local\Programs\Python\Python312\python.exe");

        // Check if the Python executable exists
        if (File.Exists(python12Path))
        {
            return python12Path;
        }
        else
        {
            Console.WriteLine("Python is not installed at the expected path.");
            return null;
        }
    }

    private string GetPythonScriptPath(string scriptName)
    {
        // Get the user's home directory
        string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        // Get the current directory
        string currentDirectory = Environment.CurrentDirectory;

        // Construct the script path
        //string scriptPath = Path.Combine(userHome, @"Desktop\Capstone\ATEDNIULI\edn-app\ATEDNIULI\python", scriptName);
        string scriptPathRelease = Path.Combine(currentDirectory, @"python", scriptName);

        // Check if the script exists
        if (File.Exists(scriptPathRelease))
        {
            //return scriptPath;
            return scriptPathRelease;
        }
        else
        {
            Console.WriteLine($"Script {scriptName} is not found at the expected path.");
            return null;
        }
    }

    public class AppConfig
    {
        public AppSettings AppSettings { get; set; }

        public static AppConfig Load(string filePath)
        {
            try
            {
                string fullPath = Path.GetFullPath(filePath);
                //Console.WriteLine($"Attempting to load configuration from: {fullPath}");

                if (!File.Exists(fullPath))
                    throw new FileNotFoundException($"Configuration file not found at: {fullPath}");

                var json = File.ReadAllText(fullPath);
                //Console.WriteLine("Configuration file successfully read.");
                return JsonConvert.DeserializeObject<AppConfig>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load configuration from {filePath}. Error: {ex.Message}");
                return null;
            }
        }
    }

    public class AppSettings
    {
        public bool IsFirstTime { get; set; }
        public int MouseSpeed { get; set; }
    }

    public int mouse_speed = 0;

    public void UpdateConfiguration(AppConfig config, string filePath)
    {
        try
        {
            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            Console.WriteLine($"Serialized JSON: {json}");

            File.WriteAllText(filePath, json);
            Console.WriteLine("Configuration updated successfully.");

            // Read back to verify
            var writtenContent = File.ReadAllText(filePath);
            Console.WriteLine($"File Content After Update:\n{writtenContent}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to update configuration: {ex.Message}");
        }
    }

    private AppConfig config;

    public LiveTranscription(ASRWindow asr_window, IntentWindow intent_window, MainWindow main_window, UserGuide user_guide, ShowItems show_items, CameraMouse camera_mouse, HelpWindow help_window, SettingsWindow settings_window) // 
    {
        this.asr_window = asr_window ?? throw new ArgumentNullException(nameof(asr_window));
        this.intent_window = intent_window ?? throw new ArgumentNullException(nameof(intent_window));
        this.main_window = main_window ?? throw new ArgumentNullException(nameof(main_window));
        this.show_items = show_items ?? throw new ArgumentNullException(nameof(show_items));
        this.camera_mouse = camera_mouse ?? throw new ArgumentNullException(nameof(camera_mouse));
        this.help_window = help_window ?? throw new ArgumentNullException(nameof(help_window));
        this.settings_window = settings_window ?? throw new ArgumentNullException(nameof(settings_window));
        this.user_guide = user_guide ?? throw new ArgumentNullException(nameof(user_guide));

        config = AppConfig.Load("appsettings.json");
        bool isFirstTime = config.AppSettings.IsFirstTime;
        int MouseSpeed = config.AppSettings.MouseSpeed;

        mouse_speed = MouseSpeed;

        vad = new WebRtcVad
        {
            SampleRate = WebRtcVadSharp.SampleRate.Is16kHz,
            FrameLength = WebRtcVadSharp.FrameLength.Is20ms,
            OperatingMode = OperatingMode.VeryAggressive
        };

        string pythonExecutablePath = GetPythonExecutablePath();

        string gridInferenceScriptPath = GetPythonScriptPath("od.py");

        Task.Run(() =>
        {
            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = pythonExecutablePath,
                Arguments = gridInferenceScriptPath,
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

        InitializeTimer();

        InitializeWhisper();

        show_items.ItemDetected += CheckDetected;

        UpdateMouseActionLabel();

        this.help_window = help_window;
        this.settings_window = settings_window;

        if (isFirstTime)
        {
            OpenTutorial();
        }

        camera_mouse.mouse_speed_multiplier = MouseSpeed;
        settings_window.mouseSpeed = MouseSpeed;

        //settings_window.Show();
    }

    private void OpenTutorial()
    {
        config.AppSettings.IsFirstTime = false; // Update
        Console.WriteLine($"IsFirstTime: {config.AppSettings.IsFirstTime}");
        UpdateConfiguration(config, "appsettings.json");

        UpdateUI(() => user_guide.Show());
        in_tutorial = true;
        UpdateUI(() => FinalizeStream());
    }

    private void updateMouseSpeed(int speed)
    {
        config.AppSettings.MouseSpeed = speed;
        mouse_speed = speed;
        camera_mouse.mouse_speed_multiplier = speed;
        UpdateConfiguration(config, "appsettings.json");
    }

    public string action = "none";

    private void UpdateMouseActionLabel()
    {
        Thread labelthread = new Thread(() =>
        {
            while (true)
            {
                //if (in_tutorial)
                //{
                //    Console.WriteLine(user_guide.ReturnState());
                //}

                action = camera_mouse.action;

                mouse_steady = camera_mouse.mouse_steady;

                show_items.lastDirectionX = camera_mouse.lastDirectionX;
                show_items.lastDirectionY = camera_mouse.lastDirectionY;

                show_items.lastSpeed = camera_mouse.lastSpeed;

                config = AppConfig.Load("appsettings.json");
                camera_mouse.mouse_speed_multiplier = config.AppSettings.MouseSpeed;
                Console.WriteLine(camera_mouse.mouse_speed_multiplier);

                show_items.Dispatcher.Invoke(() =>
                {
                    show_items.MouseActionLabel.Content = action;  // Update label content when action changes

                    if (action != "none")
                    {
                        show_items.MouseActionLabel.Visibility = System.Windows.Visibility.Visible;
                    }
                    else
                    {
                        show_items.MouseActionLabel.Visibility = System.Windows.Visibility.Collapsed;
                    }
                });

                Thread.Sleep(50); // Sleep for 50 milliseconds
            }
        });

        labelthread.IsBackground = true;
        labelthread.Start();
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
            //Console.WriteLine("No item detected.");
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
    public bool number_clicked = true;
    private bool showed_state4 = false;
    public void DetectScreen()
    {
        var tutorial_state = user_guide.ReturnState();

        if (!mouse_activated)
        {
            if (in_tutorial && tutorial_state == "state4")
            {
                Console.WriteLine(tutorial_state);
                if (!showed_state4)
                {
                    Console.WriteLine("trying to show items in tutorial.....");
                    UpdateUI(() => TutorialStuff(tutorial_state, "e"));
                    UpdateUI(() => TutorialStuff(tutorial_state, "next"));
                    showed_state4 = true;
                }
            }

            show_items.RemoveTagsNoTimer();
            UpdateUI(() => asr_window.HideWithFadeOut());
            wake_word_detected = false;

            showed_detected = false;

            if (showed_detected == false)
            {
                show_items.ListClickableItemsInCurrentWindow();
                var clickable_items = show_items.GetClickableItems();
            }

            if (number_clicked)
            {
                showed_detected = true;
                StartTranscription();
                UpdateUI(() => main_window.HighlightODIcon(showed_detected));
                number_clicked = false;
            }
        }
        else
        {
            UpdateUI(() => show_items.NotificationLabel.Content = "Turn off mouse first");
        }
        
    }

    public void load_model(string model_path, string scorer_path, string intent_model_path)
    {
        UpdateUI(() => asr_window.AppendText("Loading model..."));
        deep_speech_model = new DeepSpeech(model_path);
        UpdateUI(() => asr_window.AppendText("Model loaded"));

        UpdateUI(() => asr_window.AppendText("Loading scorer..."));
        deep_speech_model.EnableExternalScorer(scorer_path);
        deep_speech_model.AddHotWord("thermal", 5);

        UpdateUI(() => asr_window.AppendText("Loading intent model..."));
        intent_model = new FastTextWrapper();
        intent_model.LoadModel(intent_model_path);
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
    private const int RequiredDurationMs = 5000; // 5 seconds of audio
    private const int FrameSize = 320; // 20ms frame at 16kHz for 16-bit mono PCM
    private List<byte> typingBuffer = new List<byte>(); // To accumulate audio data
    private bool isProcessing = false; // Flag to manage processing state

    private Queue<byte[]> audioQueue = new Queue<byte[]>(); // Queue for accumulated audio buffers
    private bool isProcessingQueue = false; // Flag to manage queue processing state

    private bool app_start = false;
    public void StartTranscription()
    {
        try
        {
            DisposePreviousResources();

            if (!typing_mode && !showed_detected && !search_mode && !shown_bookmarks)
            {
                if (!app_start)
                {
                    load_model(model_path, wake_word_scorer, intent_model_path);
                    app_start = true;
                }
                else
                {
                    SwitchScorer(wake_word_scorer);
                    switched = false;
                    wake_word_detected = false;
                }

                wave_in_event = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(16000, 1),
                    BufferMilliseconds = 500
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
            else if (typing_mode && !showed_detected && !search_mode && !shown_bookmarks)
            {
                Console.WriteLine("Switching to typingmode");

                wave_in_event = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(16000, 1) // 16kHz mono 
                };

                wave_in_event.DataAvailable += (s, e) =>
                {
                    int availableFrames = e.BytesRecorded / FrameSize;

                    for (int i = 0; i < availableFrames; i++)
                    {
                        byte[] frame = new byte[FrameSize];
                        Array.Copy(e.Buffer, i * FrameSize, frame, 0, FrameSize);
                        typingBuffer.AddRange(frame); // Continuously add frame to the buffer
                    }

                    // If there's enough data, enqueue for transcription and clear the buffer
                    if (typingBuffer.Count >= RequiredDurationMs * wave_in_event.WaveFormat.AverageBytesPerSecond / 1000)
                    {
                        byte[] audioToProcess = typingBuffer.ToArray();
                        typingBuffer.Clear(); // Reset the buffer for the next round

                        audioQueue.Enqueue(audioToProcess); // Enqueue buffer for processing
                        ProcessAudioQueue(); // Start processing the queue if not already
                    }
                };

                wave_in_event.StartRecording();
            }
            else if (showed_detected && !typing_mode && !search_mode && !shown_bookmarks)
            {
                Console.WriteLine("listening for numbers...");

                wave_in_event = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(16000, 1) // 16kHz mono 
                };

                wave_in_event.DataAvailable += (s, e) =>
                {
                    int availableFrames = e.BytesRecorded / FrameSize;

                    for (int i = 0; i < availableFrames; i++)
                    {
                        byte[] frame = new byte[FrameSize];
                        Array.Copy(e.Buffer, i * FrameSize, frame, 0, FrameSize);
                        typingBuffer.AddRange(frame); // Continuously add frame to the buffer
                    }

                    // If there's enough data, enqueue for transcription and clear the buffer
                    if (typingBuffer.Count >= RequiredDurationMs * wave_in_event.WaveFormat.AverageBytesPerSecond / 1000)
                    {
                        byte[] audioToProcess = typingBuffer.ToArray();
                        typingBuffer.Clear(); // Reset the buffer for the next round

                        audioQueue.Enqueue(audioToProcess); // Enqueue buffer for processing
                        ProcessAudioQueue(); // Start processing the queue if not already
                    }
                };

                wave_in_event.StartRecording();
            }
            else if (search_mode && !showed_detected && !typing_mode && !shown_bookmarks)
            {
                Console.WriteLine("What are you searching...");

                wave_in_event = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(16000, 1) // 16kHz mono 
                };

                wave_in_event.DataAvailable += (s, e) =>
                {
                    int availableFrames = e.BytesRecorded / FrameSize;

                    for (int i = 0; i < availableFrames; i++)
                    {
                        byte[] frame = new byte[FrameSize];
                        Array.Copy(e.Buffer, i * FrameSize, frame, 0, FrameSize);
                        typingBuffer.AddRange(frame); // Continuously add frame to the buffer
                    }

                    // If there's enough data, enqueue for transcription and clear the buffer
                    if (typingBuffer.Count >= RequiredDurationMs * wave_in_event.WaveFormat.AverageBytesPerSecond / 1000)
                    {
                        byte[] audioToProcess = typingBuffer.ToArray();
                        typingBuffer.Clear(); // Reset the buffer for the next round

                        audioQueue.Enqueue(audioToProcess); // Enqueue buffer for processing
                        ProcessAudioQueue(); // Start processing the queue if not already
                    }
                };

                wave_in_event.StartRecording();
            }
            else if (shown_bookmarks && !showed_detected && !typing_mode && !search_mode)
            {
                Console.WriteLine("choose bookmark...");

                wave_in_event = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(16000, 1) // 16kHz mono 
                };

                wave_in_event.DataAvailable += (s, e) =>
                {
                    int availableFrames = e.BytesRecorded / FrameSize;

                    for (int i = 0; i < availableFrames; i++)
                    {
                        byte[] frame = new byte[FrameSize];
                        Array.Copy(e.Buffer, i * FrameSize, frame, 0, FrameSize);
                        typingBuffer.AddRange(frame); // Continuously add frame to the buffer
                    }

                    // If there's enough data, enqueue for transcription and clear the buffer
                    if (typingBuffer.Count >= RequiredDurationMs * wave_in_event.WaveFormat.AverageBytesPerSecond / 1000)
                    {
                        byte[] audioToProcess = typingBuffer.ToArray();
                        typingBuffer.Clear(); // Reset the buffer for the next round

                        audioQueue.Enqueue(audioToProcess); // Enqueue buffer for processing
                        ProcessAudioQueue(); // Start processing the queue if not already
                    }
                };

                wave_in_event.StartRecording();
            }
        }
        catch (Exception ex)
        {
            UpdateUI(() => asr_window.AppendText($"Error: {ex.Message}"));
        }
    }

    private bool search_mode = false;

    private void DisposePreviousResources()
    {
        // Stop recording and dispose resources
        if (wave_in_event != null)
        {
            // Unsubscribe from events first
            wave_in_event.DataAvailable -= OnDataAvailable;
            wave_in_event.RecordingStopped -= OnRecordingStopped;

            // Stop recording
            wave_in_event.StopRecording();

            // Dispose of the WaveInEvent instance
            wave_in_event.Dispose();
            wave_in_event = null;
        }

        // Dispose of the deep speech stream if it exists
        if (deep_speech_stream != null)
        {
            deep_speech_stream.Dispose();
            deep_speech_stream = null;
        }

        // Clear buffers
        typingBuffer.Clear();
    }

    private async Task TranscribeAudioBufferAsync(byte[] buffer, int length)
    {
        // Check if already processing; if so, skip this audio buffer
        if (isProcessing) return;

        // Set processing flag to true
        isProcessing = true;

        const int minDurationMs = 1020; // Minimum duration Whisper expects in milliseconds
        int minByteCount = wave_in_event.WaveFormat.AverageBytesPerSecond * minDurationMs / 1000;

        // If buffer length is less than required minimum, add padding
        if (length < minByteCount)
        {
            var paddedBuffer = new byte[minByteCount];
            Array.Copy(buffer, paddedBuffer, length); // Copy the original audio data
                                                      // Remaining bytes in paddedBuffer are already initialized to zero (silence)
            buffer = paddedBuffer;
            length = minByteCount;
        }

        using (var processor = whisperFactory.CreateBuilder()
            .WithLanguage("en")
            .Build())
        {
            using (var memoryStream = new MemoryStream())
            {
                WriteWavHeader(memoryStream, length, wave_in_event.WaveFormat);
                memoryStream.Write(buffer, 0, length); // Write the padded audio data

                memoryStream.Position = 0; // Set the position back to the beginning
                var results = processor.ProcessAsync(memoryStream);
                await ProcessResultsAsync(results);
            }
        }

        // Reset processing flag after transcription completes
        isProcessing = false;
    }

    private async void ProcessAudioQueue()
    {
        if (isProcessingQueue || audioQueue.Count == 0) return;

        isProcessingQueue = true;

        while (audioQueue.Count > 0)
        {
            var audioBuffer = audioQueue.Dequeue();

            try
            {
                await TranscribeAudioBufferAsync(audioBuffer, audioBuffer.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during transcription: {ex.Message}");
            }
        }

        isProcessingQueue = false;
    }

    private async Task ProcessResultsAsync(IAsyncEnumerable<SegmentData> results)
    {
        if (!showed_detected && !search_mode && !shown_bookmarks)
        {
            UpdateUI(() => asr_window.OutputTextBox.Text = "Result: "); // Clear previous results

            var resultList = new List<SegmentData>();
            var enumerator = results.GetAsyncEnumerator();

            try
            {
                while (await enumerator.MoveNextAsync())
                {
                    resultList.Add(enumerator.Current);
                }
            }
            finally
            {
                await enumerator.DisposeAsync();
            }

            // Append results to the TextBox
            foreach (var result in resultList)
            {
                UpdateUI(() => asr_window.OutputTextBox.Text += $"{result.Text}\n");
                TypeText(result.Text);
            }
        }
        else if (search_mode && !showed_detected && !shown_bookmarks)
        {
            string queryText = "";
            var resultList = new List<SegmentData>();
            var enumerator = results.GetAsyncEnumerator();

            try
            {
                while (await enumerator.MoveNextAsync())
                {
                    resultList.Add(enumerator.Current);
                }
            }
            finally
            {
                await enumerator.DisposeAsync();
            }

            foreach (var result in resultList)
            {
                string cleanedText = result.Text
                    .Replace(".", " ")
                    .Replace(",", " ")
                    .Replace("!", " ")
                    .Replace("?", " ")
                    .ToLower(); // Clean up and standardize to lowercase

                // Remove content inside brackets and parentheses
                cleanedText = Regex.Replace(cleanedText, @"[\[\(].*?[\]\)]", string.Empty);

                UpdateUI(() => asr_window.AppendText(cleanedText));

                if (cleanedText.Contains("stop showing"))
                {
                    search_mode = false;
                    StartTranscription();
                }

                if (cleanedText.Contains("search now"))
                {
                    UpdateUI(() => queryText = asr_window.OutputTextBox.Text.Replace("search now", "").Trim());
                    UpdateUI(() => OpenBrowserWithSearch(queryText));
                    search_mode = false;
                    StartTranscription();
                }
                else if (cleanedText.Contains("stop searching"))
                {
                    search_mode = false;
                    StartTranscription();
                }
            }
        }
        else if (shown_bookmarks && !search_mode && !showed_detected)
        {
            var resultList = new List<SegmentData>();
            var enumerator = results.GetAsyncEnumerator();

            try
            {
                while (await enumerator.MoveNextAsync())
                {
                    resultList.Add(enumerator.Current);
                }
            }
            finally
            {
                await enumerator.DisposeAsync();
            }

            // List of words to replace with numbers
            var wordToNumberMap = new Dictionary<string, string>
            {
                { "one", "1" },
                { "two", "2" },
                { "too", "2" },
                { "do", "2" },
                { "three", "3" },
                { "four", "4" },
                { "five", "5" },
                { "six", "6" },
                { "seven", "7" },
                { "eight", "8" },
                { "ate", "8" },
                { "nine", "9" },
                { "ten", "10" },
                { "ben", "10" },
                { "and", "10" },
                { "then", "10" }
            };

            // Append results to the TextBox
            foreach (var result in resultList)
            {
                string cleanedText = result.Text
                    .Replace(".", " ")
                    .Replace(",", " ")
                    .Replace("!", " ")
                    .Replace("?", " ")
                    .ToLower(); // Clean up and standardize to lowercase

                // Remove content inside brackets and parentheses
                cleanedText = Regex.Replace(cleanedText, @"[\[\(].*?[\]\)]", string.Empty);

                // Replace words with numbers using the dictionary
                foreach (var word in wordToNumberMap)
                {
                    // Use word boundaries to replace only whole words
                    cleanedText = Regex.Replace(cleanedText, @"\b" + Regex.Escape(word.Key) + @"\b", word.Value);
                }

                // Check if the text contains "stop showing"
                if (cleanedText.Contains("hide bookmarks"))
                {
                    shown_bookmarks = false;
                    StartTranscription();
                }

                // Use a regular expression to find all numbers in the cleaned text
                var numberMatches = Regex.Matches(cleanedText, @"\d+");

                foreach (var match in numberMatches)
                {
                    // Convert the matched number string to an integer
                    if (int.TryParse(match.ToString(), out int number))
                    {
                        // Handle the command for each number found in cleanedText
                        HandleCommand(number.ToString(), cleanedText, ref execute_number_command_count, () =>
                        {
                            CheckAndLaunchBookmark(number); // Process the parsed number
                            UpdateUI(() => show_items.BookmarkList.Visibility = System.Windows.Visibility.Collapsed);
                            shown_bookmarks = false;
                            StartTranscription();
                        });
                    }
                }
            }
        }
        else
        {
            var resultList = new List<SegmentData>();
            var enumerator = results.GetAsyncEnumerator();

            try
            {
                while (await enumerator.MoveNextAsync())
                {
                    resultList.Add(enumerator.Current);
                }
            }
            finally
            {
                await enumerator.DisposeAsync();
            }

            // List of words to replace with numbers
            var wordToNumberMap = new Dictionary<string, string>
            {
                { "one", "1" },
                { "two", "2" },
                { "too", "2" },
                { "do", "2" },
                { "three", "3" },
                { "four", "4" },
                { "five", "5" },
                { "six", "6" },
                { "seven", "7" },
                { "eight", "8" },
                { "ate", "8" },
                { "nine", "9" },
                { "ten", "10" },
                { "ben", "10" },
                { "and", "10" },
                { "then", "10" }
            };

            // Append results to the TextBox
            foreach (var result in resultList)
            {
                string cleanedText = result.Text
                    .Replace(".", " ")
                    .Replace(",", " ")
                    .Replace("!", " ")
                    .Replace("?", " ")
                    .ToLower(); // Clean up and standardize to lowercase

                // Remove content inside brackets and parentheses
                cleanedText = Regex.Replace(cleanedText, @"[\[\(].*?[\]\)]", string.Empty);

                // Replace words with numbers using the dictionary
                foreach (var word in wordToNumberMap)
                {
                    // Use word boundaries to replace only whole words
                    cleanedText = Regex.Replace(cleanedText, @"\b" + Regex.Escape(word.Key) + @"\b", word.Value);
                }

                // Check if the text contains "stop showing"
                if (cleanedText.Contains("stop showing"))
                {
                    number_clicked = true;
                    showed_detected = false;
                    show_items.RemoveTagsNoTimer();
                    StartTranscription();
                    UpdateUI(() => main_window.HighlightODIcon(showed_detected));
                }

                // Use a regular expression to find all numbers in the cleaned text
                var numberMatches = Regex.Matches(cleanedText, @"\d+");

                foreach (var match in numberMatches)
                {
                    // Convert the matched number string to an integer
                    if (int.TryParse(match.ToString(), out int number))
                    {
                        // Handle the command for each number found in cleanedText
                        HandleCommand(number.ToString(), cleanedText, ref execute_number_command_count, () =>
                        {
                            ProcessSpokenTag(number.ToString()); // Process the parsed number
                        });
                    }
                }
            }
        }
    }

    private void TypeText(string text)
    {
        Task.Run(() =>
        {
            text = text.ToLower();

        if (Regex.IsMatch(text, @"[\[\(].*?[\]\)]"))
        {
            text = Regex.Replace(text, @"\[[^\]]*\]|\([^\)]*\)", string.Empty);
        }

        if (text.Contains("clear "))
        {
            // Use a regex to search for "clear" followed by a number anywhere in the sentence
            var match = Regex.Match(text, @"clear (\d+)", RegexOptions.IgnoreCase);

            if (match.Success && int.TryParse(match.Groups[1].Value, out int numToClear) && numToClear > 0)
            {
                for (int i = 0; i < numToClear; i++)
                {
                    SendKeys.SendWait("{BACKSPACE}");
                    Thread.Sleep(50);  // Adjust delay as needed
                }

                UpdateUI(() => asr_window.OutputTextBox.Text = "clearing...");
                return;
            }
        }

        if (text.Contains("stop typing") || text.Contains("Stop typing") || text.Contains("disable typing") || text.Contains("deactivate typing"))
        {
            typing_mode = false;
            wave_in_event.StopRecording();
            StartTranscription();
            UpdateUI(() => show_items.NotificationLabel.Content = "Stopped typing...");
            UpdateUI(() => main_window.HighlightTypingIcon(typing_mode));
            UpdateUI(() => {
                show_items.TypingActions.Visibility = System.Windows.Visibility.Collapsed;
            });
            actions = null;
            return;
        }
        else
        {
            // Split text into words to handle "capital" functionality
            var words = text.Split(' ');
            var processedWords = new List<string>();

            for (int i = 0; i < words.Length; i++)
            {
                if (words[i] == "capital" && i + 1 < words.Length)
                {
                    // Capitalize the following word
                    processedWords.Add(char.ToUpper(words[i + 1][0]) + words[i + 1].Substring(1));
                    i++; // Skip the next word since it's already processed
                }
                else
                {
                    processedWords.Add(words[i]);
                }
            }

            // Join processed words back into a single string and filter out unwanted characters
            string normalized_result = string.Join(" ", processedWords);
            normalized_result = new string(normalized_result.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray());

            var quotation = '"';
            // Apply replacements for punctuation words
            normalized_result = normalized_result.Replace("thermal 1", ".")
                                                    .Replace("thermal 2", ",")
                                                    .Replace("thermal 3", "?")
                                                    .Replace("thermal 4", "!")
                                                    .Replace("thermal 5", "@")
                                                    .Replace("thermal 6", "(")
                                                    .Replace("thermal 7", ")")
                                                    .Replace("thermal 8", ";")
                                                    .Replace("thermal 9", ":")
                                                    .Replace("thermal 10", "'")
                                                    .Replace("thermal 11", $"{quotation}")
                                                    .Replace("thermal 12", "-")
                                                    .Replace("thermal 13", "_")
                                                    .Replace("thermal 14", "/")
                                                    .Replace("thermal 15", "#")
                                                    .Replace("thermal 16", "$")
                                                    .Replace("thermal 17", "+")
                                                    .Replace("thermal 18", "-")
                                                    .Replace("thermal 19", "*")
                                                    .Replace("thermal 20", "%")
                                                    .Replace("thermal 21", "=")
                                                    .Replace("new paragraph", "\n\n")
                                                    .Replace("indent", "\t");

            // Send each character for typing
            foreach (char c in normalized_result)
            {
                SendKeys.SendWait(c.ToString());
                Thread.Sleep(50); // Adjust delay as needed
            }
            SendKeys.SendWait(" ");

            UpdateUI(() => asr_window.OutputTextBox.Text = "");
        }
        });
    }

    private void WriteWavHeader(Stream stream, int dataLength, WaveFormat format)
    {
        int sampleRate = format.SampleRate;
        short channels = (short)format.Channels;
        short bitsPerSample = (short)format.BitsPerSample;
        int byteRate = sampleRate * channels * bitsPerSample / 8;
        short blockAlign = (short)(channels * bitsPerSample / 8);

        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            writer.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"));
            writer.Write(36 + dataLength);
            writer.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.UTF8.GetBytes("fmt "));
            writer.Write(16); // Subchunk1Size for PCM
            writer.Write((short)1); // AudioFormat for PCM
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);
            writer.Write(System.Text.Encoding.UTF8.GetBytes("data"));
            writer.Write(dataLength);
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

    private void OnRecordingStopped(object sender, NAudio.Wave.StoppedEventArgs e) // emergency stop handling
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

        //intent_window_timer = new System.Timers.Timer(intent_window_timeout); // 3 seconds
        //intent_window_timer.Elapsed += OnIntentTimerElapsed;
        //intent_window_timer.AutoReset = false;

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

        typing_appear_timer = new System.Timers.Timer(typing_appear);
        typing_appear_timer.Elapsed += TypingMode;
        typing_appear_timer.AutoReset = false; // Timer will only trigger once

        searching_appear_timer = new System.Timers.Timer(searching_appear);
        searching_appear_timer.Elapsed += SearchingMode;
        searching_appear_timer.AutoReset = false; // Timer will only trigger once

        bookmarks_appear_timer = new System.Timers.Timer(bookmarks_appear);
        bookmarks_appear_timer.Elapsed += BookmarksShown;
        bookmarks_appear_timer.AutoReset = false; // Timer will only trigger once

        reset_typing_stream_timer = new System.Timers.Timer(reset_typing_timeout);
        reset_typing_stream_timer.Elapsed += OnResetTypingTimerElapsed;
        reset_typing_stream_timer.AutoReset = false; // Timer will only trigger once
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

    private void OnResetTypingTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        lock (streamLock)
        {
            if (deep_speech_stream != null)
            {
                deep_speech_stream.Dispose();
            }
            deep_speech_stream = deep_speech_model.CreateStream();
            Console.WriteLine("Stream reset successfully.");
            is_stream_ready = true;
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

            if (!wake_word_detected)
            {
                deep_speech_stream.Dispose();
                deep_speech_stream = deep_speech_model.CreateStream();
                return;
            }
            else
            {
                commandExecuted = false;
                wake_word_detected = false;
                switched_state = false;
                ResetCommandCounts();
            }

            Task.Run(() =>
            {
                SwitchScorer(wake_word_scorer);
                switched = false;

                lock (streamLock)
                {
                    string final_result_from_stream = deep_speech_model.FinishStream(deep_speech_stream);

                    // Dispose and reset the stream
                    deep_speech_stream.Dispose();
                    deep_speech_stream = deep_speech_model.CreateStream();
                }
            }).ContinueWith(t =>
            {
                //if 
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
                    if (asr_window.IsVisible && !typing_mode)
                    {
                        try
                        {
                            //intent_window.AppendText($"Intent: {received}");
                            //intent_window_timer.Start();
                            main_window.UpdateListeningIcon(false);
                            asr_window.HideWithFadeOut();

                            Console.WriteLine("Stream finalized successfully.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error updating UI: {ex.Message}");
                        }
                    }

                    Console.WriteLine("Stream finalized successfully.");
                });

            }, TaskScheduler.FromCurrentSynchronizationContext()); // Ensure the continuation runs on the UI thread context
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in FinalizeStream: {ex.Message}");
        }
    }

    private void ProcessIntent(string send_to_intent)
    {
        var prediction = intent_model.PredictSingle(send_to_intent);
        var intent = prediction.Label.ToString();
        var confidence = prediction.Probability;

        Console.WriteLine($"{intent} : {confidence}");

        if (wake_word_detected && !commandExecuted && confidence > 0.9)
        {
            UpdateUI(() => show_items.NotificationLabel.Content = $"Executing: {received}");
            // Check for specific commands
            if (intent == "__label__open_chrome")
            {
                StartProcess("chrome");
            }
            else if (intent == "__label__open_word")
            {
                StartProcess("winword");
            }
            else if (intent == "__label__open_excel")
            {
                StartProcess("excel");
            }
            else if (intent == "__label__open_powerpoint")
            {
                StartProcess("powerpnt");
            }
            //else if (intent == "__label__screen_shot")
            //{
            //    ScreenShot();
            //}
            else if (intent == "__label__close_app")
            {
                CloseApp();
            }
            else if (intent == "__label__open_explorer")
            {
                StartProcess("explorer");
            }
            else if (intent == "__label__open_settings")
            {
                StartProcess("ms-settings:");
            }
            else if (intent == "__label__open_notepad")
            {
                StartProcess("notepad");
            }
            else if (intent == "__label__volume_up")
            {
                VolumeUp();
            }
            else if (intent == "__label__volume_down")
            {
                VolumeDown();
            }
            else if (intent == "__label__mouse_control_on")
            {
                OpenMouse();
                mouse_activated = true;
            }
            else if (intent == "__label__mouse_control_off")
            {
                CloseMouse();
                mouse_activated = false;
            }
            else if (intent == "__label__show_items")
            {
                DetectScreen();
            }
            else if (intent == "__label__stop_showing")
            {
                RemoveTags();
            }
            else if (intent == "__label__start_typing")
            {
                if (!typing_mode)
                {
                    UpdateUI(() => FinalizeStream());
                    wave_in_event.StopRecording();
                    typing_appear_timer.Start();
                    audioBuffer.SetLength(0);
                }
                else
                {
                    Console.WriteLine("already in typing mode");
                }
            }
            UpdateUI(() => FinalizeStream());
        }
    }

    private MemoryStream audioBuffer = new MemoryStream();
    private void OnDataAvailable(object sender, WaveInEventArgs e)
    {
        if (!is_running || e.BytesRecorded <= 0)
        {
            UpdateUI(() => asr_window.AppendText("No audio data recorded."));
            return;
        }

        // timers start everytime audio is detedted

        if (!typing_mode)
        {
            StartInactivityTimer();

            StartWakeWordTimer();
        }

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
                    if (confidence > 100)
                    {
                        ShowTranscription(partial_result);
                        ProcessCommand(partial_result);
                    }
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

    private bool mouse_steady = false;
    private bool switched = false;
    private string detected_item = "";
    private bool state1_done = false;
    private bool state6_done = false;

    private void HandleWakeWord(string partial_result, double confidence)
    {
        if (itemDetected && confidence > -100 && mouse_activated)
        {
            for (int number_index = 0; number_index < numberStrings.Count; number_index++)
            {
                string number = numberStrings[number_index]; // Get the current number as a string

                detected_item = show_items.detected_item;

                // Here we handle the command for each number string
                HandleCommand(number, partial_result, ref execute_number_command_count, () =>
                {
                    var tutorial_state = user_guide.ReturnState();

                    if (in_tutorial && tutorial_state == "state6")
                    {
                        Console.WriteLine(tutorial_state);
                        if (!state6_done)
                        {
                            Console.WriteLine("trying to show items in tutorial.....");
                            UpdateUI(() => TutorialStuff(tutorial_state, "d"));
                            UpdateUI(() => TutorialStuff(tutorial_state, "e"));
                            state6_done = true;
                        }
                    }

                    show_items.ExecuteAction(detected_item, number_index + 1); // Use number_index + 1 if you want to represent 1-based index
                    deep_speech_model.FreeStream(deep_speech_stream);
                    deep_speech_stream.Dispose();
                    deep_speech_stream = deep_speech_model.CreateStream();
                });
            }
        }

        if (partial_result.Contains(wake_word) && !wake_word_detected && confidence > -100)
        {
            var tutorial_state = user_guide.ReturnState();
            
            if (in_tutorial && tutorial_state == "state0")
            {
                switched_state = true;
                UpdateUI(() => user_guide.SwitchState("next"));
                deep_speech_stream.Dispose();
                deep_speech_stream = deep_speech_model.CreateStream();
                return;
            }
            else if (in_tutorial && tutorial_state == "state1" && !state1_done)
            {
                UpdateUI(() => TutorialStuff(tutorial_state, "e"));
                UpdateUI(() => TutorialStuff(tutorial_state, "f"));
                state1_done = true;
                wake_word_detected = true;
            }
            else
            {
                wake_word_detected = true;
            }
        }

        if (wake_word_detected)
        {
            if (wake_word_timer.Enabled)
            {
                wake_word_timer.Close();
            }

            if (!switched)
            {
                SwitchScorer(commands_scorer);
                deep_speech_model.FreeStream(deep_speech_stream);
                deep_speech_stream.Dispose();
                deep_speech_stream = deep_speech_model.CreateStream();
                switched = true;
            }

            partial_result = RemoveWakeWord(partial_result, wake_word);

            ShowTranscription(partial_result);
            ProcessCommand(partial_result);

            if (!typing_mode)
            {
                // Measure word count before processing intent
                int wordCount = partial_result.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;

                if (wordCount > 1) // Adjust condition based on your requirement (e.g., > 1 for multiple words)
                {
                    send_to_intent = partial_result;

                    Task.Run(() => ProcessIntent(send_to_intent));
                }
            }
        }

    }

    private void TutorialStuff(string tutorial_state, string letter)
    {
        switch (tutorial_state)
        {
            case "state1":
                user_guide.UpdateTextBlocks(letter);
                break;
            case "state2":
                user_guide.UpdateTextBlocks(letter);
                break;
            case "state3":
                user_guide.UpdateTextBlocks(letter);
                break;
            case "state4":
                user_guide.UpdateTextBlocks(letter);
                break;
            case "state5":
                user_guide.UpdateTextBlocks(letter);
                break;
            case "state6":
                user_guide.UpdateTextBlocks(letter);
                break;
            case "state7":
                user_guide.UpdateTextBlocks(letter);
                break;
            case "state8":
                user_guide.UpdateTextBlocks(letter);
                break;
        }
    }

    // WHISPER STUFF
    private WhisperFactory whisperFactory;
    private static async Task DownloadModel(string fileName, GgmlType ggmlType)
    {
        Console.WriteLine($"Downloading Model {fileName}");
        using (var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(ggmlType))
        using (var fileWriter = File.OpenWrite(fileName))
        {
            await modelStream.CopyToAsync(fileWriter);
        }
    }
    private async void InitializeWhisper()
    {
        var ggmlType = GgmlType.Base;
        var modelFileName = "ggml-base.bin";

        // Check if the model file exists; if not, download it
        if (!File.Exists(modelFileName))
        {
            await DownloadModel(modelFileName, ggmlType);
        }

        // Create the whisper factory object to create the processor object
        whisperFactory = WhisperFactory.FromPath(modelFileName);
    }

    private void ShowTranscription(string partial_result)
    {
        if (!typing_mode)
        {
            UpdateUI(() => main_window.UpdateListeningIcon(true));

            UpdateUI(() =>
            {
                try
                {
                    if (!asr_window.IsVisible && wake_word_detected)
                    {
                        asr_window.ShowWithFadeIn(false);
                    }
                    asr_window.AppendText($"You said: {partial_result}", true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    return;
                }
            });
        }
        else
        {
            UpdateUI(() => asr_window.AppendText($"Typing: {partial_result}", true));
        }
        //UpdateUI(() => intent_window.Show());
    }

    private void HandleNoSpeechDetected()
    {
        UpdateUI(() =>
        {
            if (!asr_window.IsVisible) return;

            UpdateUI(() => FinalizeStream()); // Finalize the stream when no speech is detected
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

    public static void VolumeUp(float amount = 0.5f) // Adjust amount as needed
    {
        float newVolume = Math.Min(device.AudioEndpointVolume.MasterVolumeLevelScalar + amount, 1.0f);
        device.AudioEndpointVolume.MasterVolumeLevelScalar = newVolume;
    }

    public static void VolumeDown(float amount = 0.5f) // Adjust amount as needed
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

    private bool mouse_activated = false;
    // TODO - himua an tanan na commands na gamiton an HandleCommand function
    private bool commandExecuted = false;
    private bool switched_state = false;
    private bool in_tutorial = false;
    private bool opened_word = false;
    private bool typing_tutorial = false;
    private bool search_state8 = false;
    private void ProcessCommand(string transcription) // tanan hin commands naagi didi
    {
        if (string.IsNullOrEmpty(transcription)) return;

        // mouse control commands
        if (transcription.IndexOf("activate mouse", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            UpdateUI(() => show_items.NotificationLabel.Content = "Opening Mouse");
            mouse_activated = true;
            OpenMouse();
            commandExecuted = true;
            UpdateUI(() => FinalizeStream());
        }

        if (transcription.IndexOf("deactivate mouse", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            UpdateUI(() => show_items.NotificationLabel.Content = "Closing Mouse");
            mouse_activated = false;
            CloseMouse();
            commandExecuted = true;
            UpdateUI(() => FinalizeStream());
        }

        if (transcription.IndexOf("start typing", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (!typing_mode)
            {
                UpdateUI(() => FinalizeStream());
                wave_in_event.StopRecording();
                typing_appear_timer.Start();
                audioBuffer.SetLength(0);
            }
            else
            {
                Console.WriteLine("already in typing mode");
            }
        }

        if (transcription.IndexOf("close", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var tutorial_state = user_guide.ReturnState();

            if (in_tutorial && tutorial_state == "state8")
            {
                UpdateUI(() => show_items.NotificationLabel.Content = "Closing tutorial...");
                UpdateUI(() => user_guide.Hide());
                in_tutorial = false;
                UpdateUI(() => FinalizeStream());
            }
        }

        if (transcription.IndexOf("show bookmarks", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (!shown_bookmarks)
            {

                UpdateUI(() => FinalizeStream());
                wave_in_event.StopRecording();
                bookmarks_appear_timer.Start();
                audioBuffer.SetLength(0);
            }
            else
            {
                Console.WriteLine("already in searching mode");
            }
        }

        if (transcription.IndexOf("search", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (!search_mode)
            {
                var tutorial_state = user_guide.ReturnState();

                if (in_tutorial && tutorial_state == "state8")
                {
                    Console.WriteLine(tutorial_state);
                    if (!search_state8)
                    {
                        Console.WriteLine("trying to access search mode in tutorial.....");
                        UpdateUI(() => TutorialStuff(tutorial_state, "e"));
                        UpdateUI(() => TutorialStuff(tutorial_state, "f"));
                        search_state8 = true;
                    }
                }

                UpdateUI(() => FinalizeStream());
                wave_in_event.StopRecording();
                searching_appear_timer.Start();
                audioBuffer.SetLength(0);
            }
            else
            {
                Console.WriteLine("already in searching mode");
            }
        }

        if (transcription.IndexOf("show items", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            DetectScreen();
            UpdateUI(() => FinalizeStream());
        }

        if (transcription.IndexOf("open help", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            UpdateUI(() => help_window.Show());
            UpdateUI(() => FinalizeStream());
        }

        if (transcription.IndexOf("close help", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            UpdateUI(() => help_window.Hide());
            UpdateUI(() => FinalizeStream());
        }

        if (transcription.IndexOf("open open", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            OpenTutorial();
        }

        if (transcription.IndexOf("next", StringComparison.OrdinalIgnoreCase) >= 0 && !switched_state)
        {
            switched_state = true;
            UpdateUI(() => user_guide.SwitchState("next"));
            UpdateUI(() => FinalizeStream());
        }

        if (transcription.IndexOf("previous", StringComparison.OrdinalIgnoreCase) >= 0 && !switched_state)
        {
            switched_state = true;
            UpdateUI(() => user_guide.SwitchState("previous"));
            UpdateUI(() => FinalizeStream());
        }

        if (transcription.IndexOf("play music", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            try
            {
                // Open the Windows Media Player app
                System.Diagnostics.Process.Start("ms-player:");

                UpdateUI(() => FinalizeStream());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open the Media Player app: {ex.Message}");
            }
        }

        HandleCommand("open calculator", transcription, ref calculator_command_count, () => StartProcess("calc"));
        HandleCommand("stop showing", transcription, ref show_items_command_count, () => RemoveTags());
        HandleCommand("open notepad", transcription, ref notepad_command_count, () => StartProcess("notepad"));
        HandleCommand("close window", transcription, ref close_window_command_count, () => SimulateKeyPress(System.Windows.Forms.Keys.ControlKey)); // Customize as needed
        HandleCommand("open chrome", transcription, ref chrome_command_count, () => show_items.OpenChrome());
        HandleCommand("open youtube", transcription, ref chrome_command_count, () =>
        {
            show_items.OpenYouTube();
            UpdateUI(() => FinalizeStream());
        });

        HandleCommand("open facebook", transcription, ref chrome_command_count, () =>
        {
            show_items.OpenFaceBook();
            UpdateUI(() => FinalizeStream());
        });

        HandleCommand("open messenger", transcription, ref chrome_command_count, () =>
        {
            show_items.OpenMessenger();
            UpdateUI(() => FinalizeStream());
        });

        HandleCommand("open canva", transcription, ref chrome_command_count, () =>
        {
            show_items.OpenCanva();
            UpdateUI(() => FinalizeStream());
        });

        HandleCommand("open discord", transcription, ref chrome_command_count, () =>
        {
            show_items.OpenDiscord();
            UpdateUI(() => FinalizeStream());
        });

        HandleCommand("open gmail", transcription, ref chrome_command_count, () =>
        {
            show_items.OpenGmail();
            UpdateUI(() => FinalizeStream());
        });

        HandleCommand("open gmeet", transcription, ref chrome_command_count, () =>
        {
            show_items.OpenGmeet();
            UpdateUI(() => FinalizeStream());
        });

        HandleCommand("open instagram", transcription, ref chrome_command_count, () =>
        {
            show_items.OpenInstagram();
            UpdateUI(() => FinalizeStream());
        });

        HandleCommand("open lazada", transcription, ref chrome_command_count, () =>
        {
            show_items.OpenLazada();
            UpdateUI(() => FinalizeStream());
        });

        HandleCommand("open netflix", transcription, ref chrome_command_count, () =>
        {
            show_items.OpenNetflix();
            UpdateUI(() => FinalizeStream());
        });

        HandleCommand("open onedrive", transcription, ref chrome_command_count, () =>
        {
            show_items.OpenOnedrive();
            UpdateUI(() => FinalizeStream());
        });

        HandleCommand("open pinterest", transcription, ref chrome_command_count, () =>
        {
            show_items.OpenPinterest();
            UpdateUI(() => FinalizeStream());
        });

        HandleCommand("open shopee", transcription, ref chrome_command_count, () =>
        {
            show_items.OpenShopee();
            UpdateUI(() => FinalizeStream());
        });

        HandleCommand("open telegram", transcription, ref chrome_command_count, () =>
        {
            show_items.OpenTelegram();
            UpdateUI(() => FinalizeStream());
        });

        HandleCommand("open tiktok", transcription, ref chrome_command_count, () =>
        {
            show_items.OpenTiktok();
            UpdateUI(() => FinalizeStream());
        });

        HandleCommand("open github", transcription, ref chrome_command_count, () =>
        {
            show_items.OpenGithub();
            UpdateUI(() => FinalizeStream());
        });

        HandleCommand("open reddit", transcription, ref chrome_command_count, () =>
        {
            show_items.OpenReddit();
            UpdateUI(() => FinalizeStream());
        });

        HandleCommand("open spotify", transcription, ref chrome_command_count, () =>
        {
            show_items.OpenSpotify();
            UpdateUI(() => FinalizeStream());
        });

        HandleCommand("open twitch", transcription, ref chrome_command_count, () =>
        {
            show_items.OpenTwitch();
            UpdateUI(() => FinalizeStream());
        });

        HandleCommand("open wikipedia", transcription, ref chrome_command_count, () =>
        {
            show_items.OpenWikipedia();
            UpdateUI(() => FinalizeStream());
        });

        HandleCommand("open x", transcription, ref chrome_command_count, () =>
        {
            show_items.OpenX();
            UpdateUI(() => FinalizeStream());
        });

        HandleCommand("open zoom", transcription, ref chrome_command_count, () =>
        {
            show_items.OpenZoom();
            UpdateUI(() => FinalizeStream());
        });

        HandleCommand("maximize", transcription, ref window_actions_count, () => MaximizeWindow());
        HandleCommand("minimize", transcription, ref window_actions_count, () => MinimizeWindow());
        HandleCommand("increase brightness", transcription, ref brightness_command_count, () => IncreaseBrightness());
        HandleCommand("decrease brightness", transcription, ref brightness_command_count, () => DecreaseBrightness());
        HandleCommand("open edge", transcription, ref edge_command_count, () => StartProcess("msedge"));
        HandleCommand("open word", transcription, ref word_command_count, () => StartProcess("winword"));
        HandleCommand("open excel", transcription, ref excel_command_count, () => StartProcess("excel"));
        HandleCommand("open powerpoint", transcription, ref powerpoint_command_count, () => StartProcess("powerpnt"));
        HandleCommand("open file manager", transcription, ref file_manager_command_count, () => StartProcess("explorer"));
        HandleCommand("switch", transcription, ref switch_command_count, () => SimulateKeyPress(System.Windows.Forms.Keys.Tab));
        HandleCommand("enter", transcription, ref enter_command_count, () => SimulateKeyPress(System.Windows.Forms.Keys.Enter));
        HandleCommand("close application", transcription, ref close_calculator_command_count, () => CloseApp());
        HandleCommand("scroll up", transcription, ref scroll_up_command_count, () => ScrollUp(200));
        HandleCommand("scroll down", transcription, ref scroll_down_command_count, () => ScrollDown(-200));
        HandleCommand("screenshot", transcription, ref screenshot_command_count, () => ScreenShot());
        HandleCommand("volume up", transcription, ref volume_up_command_count, () => VolumeUp());
        HandleCommand("volume down", transcription, ref volume_down_command_count, () => VolumeDown());
        HandleCommand("open settings", transcription, ref settings_command_count, () => UpdateUI(() => settings_window.Show()));
        HandleCommand("close settings", transcription, ref settings_command_count, () => UpdateUI(() => settings_window.Hide()));
        HandleCommand("add bookmark", transcription, ref bookmark_command_count, () => CreateBookmark());
        HandleCommand("next tab", transcription, ref bookmark_command_count, () => NextTab());
        HandleCommand("previous tab", transcription, ref bookmark_command_count, () => PreviousTab());
        HandleCommand("new tab", transcription, ref bookmark_command_count, () => NewTab());
    }

    private static string bookmarksFile = "assets/bookmarks.txt";
    private static List<string> bookmarks = new List<string>();

    private void CreateBookmark()
    {
        string url = GetActiveChromeURL();
        if (!string.IsNullOrEmpty(url))
        {
            StoreBookmark(url);
            Console.WriteLine($"Stored URL: {url}");
        }
        else
        {
            Console.WriteLine("No active Chrome URL detected.");
        }

        UpdateUI(() => FinalizeStream());
    }

    private void ShowBookmarks() // for adjustment for show items
    {
        UpdateUI(() =>
        {
            if (File.Exists(bookmarksFile))
            {
                show_items.BookmarkList.Items.Clear();  // Clear existing items

                foreach (string line in File.ReadLines(bookmarksFile))
                {
                    show_items.BookmarkList.Items.Add(line);
                }

                show_items.BookmarkList.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                Console.WriteLine("No bookmarks saved yet.");
            }
        });
    }

    private void CheckAndLaunchBookmark(int bookmarkNumber)
    {
        if (bookmarkNumber < 1 || bookmarkNumber > 10)
        {
            Console.WriteLine("Invalid bookmark number.");
            return;
        }

        LaunchBookmark(bookmarkNumber);
    }


    private void LaunchBookmark(int index)
    {
        if (!File.Exists(bookmarksFile))
        {
            Console.WriteLine("No bookmarks file found.");
            return;
        }

        var lines = File.ReadAllLines(bookmarksFile);

        if (index >= 1 && index <= lines.Length)
        {
            var line = lines[index - 1];

            try
            {
                // Split the line into parts by " - " and get the last part (URL)
                var parts = line.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length >= 3)
                {
                    string url = parts[2];  // URL is the third part
                    Console.WriteLine($"Opening {url} in a new tab...");

                    // Use OpenUrl to launch the bookmark in the existing Chrome instance
                    show_items.OpenUrl(url);
                }
                else
                {
                    Console.WriteLine("Malformed bookmark entry.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to open URL: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine($"No bookmark found at position {index}");
        }
    }

    public static void NewTab()
    {
        try
        {
            // Simulate pressing Ctrl + T to open a new tab
            keybd_event(VK_CONTROL, 0, 0, 0);  // Press Ctrl
            keybd_event(VK_T, 0, 0, 0);        // Press T
            keybd_event(VK_T, 0, KEYEVENTF_KEYUP, 0);          // Release T
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);    // Release Ctrl

            Console.WriteLine("New tab opened.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to open a new tab: {ex.Message}");
        }
    }

    public static void NextTab() // can be improved so selenium will switch focus everytime the tab is switched
    {
        // Simulate Ctrl + Tab
        keybd_event(VK_CONTROL, 0, 0, 0); // Press Ctrl
        keybd_event(VK_TAB, 0, 0, 0);     // Press Tab
        keybd_event(VK_TAB, 0, KEYEVENTF_KEYUP, 0); // Release Tab
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0); // Release Ctrl
    }

    public static void PreviousTab()
    {
        // Simulate Ctrl + Shift + Tab
        keybd_event(VK_CONTROL, 0, 0, 0); // Press Ctrl
        keybd_event(VK_SHIFT, 0, 0, 0);   // Press Shift
        keybd_event(VK_TAB, 0, 0, 0);     // Press Tab
        keybd_event(VK_TAB, 0, KEYEVENTF_KEYUP, 0); // Release Tab
        keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, 0); // Release Shift
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0); // Release Ctrl
    }

    static string GetActiveChromeURL()
    {
        try
        {
            using (HttpClient client = new HttpClient())
            {
                string chromeDebugUrl = "http://localhost:9222/json";
                string json = client.GetStringAsync(chromeDebugUrl).Result;

                // Parse the JSON response
                JArray tabs = JArray.Parse(json);

                foreach (var tab in tabs)
                {
                    // Check for the active and visible tab with a non-extension URL
                    if (tab["type"]?.ToString() == "page" &&
                        tab["url"] != null &&
                        !tab["url"].ToString().StartsWith("chrome-extension://") &&
                        tab["url"].ToString().Length > 0 &&
                        tab["title"]?.ToString().Length > 0 &&
                        tab["title"]?.ToString() != "New Tab")
                    {
                        return tab["url"].ToString();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching URL: {ex.Message}");
        }

        return string.Empty;
    }

    private static void StoreBookmark(string url)
    {
        try
        {
            int nextNumber = 1;

            // Check if the file already exists and find the highest number
            if (File.Exists(bookmarksFile))
            {
                var lines = File.ReadAllLines(bookmarksFile);

                if (lines.Length > 0)
                {
                    // Get the last line and parse its number
                    var lastLine = lines.LastOrDefault();
                    if (lastLine != null)
                    {
                        var parts = lastLine.Split(new[] { '-' }, 2);
                        if (parts.Length == 2 && int.TryParse(parts[0].Trim().Replace("Bookmark ", ""), out int lastNumber))
                        {
                            nextNumber = lastNumber + 1;
                        }
                    }
                }
            }

            // Append the new bookmark with a sequential number
            string newBookmark = $"Bookmark {nextNumber} - {DateTime.Now.ToLongTimeString()} - {url}";
            File.AppendAllText(bookmarksFile, $"{newBookmark}{Environment.NewLine}");

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to store bookmark: {ex.Message}");
        }
    }

    private static void ListBookmarks()
    {
        if (File.Exists(bookmarksFile))
        {
            Console.WriteLine("--- Stored Bookmarks ---");
            foreach (string line in File.ReadLines(bookmarksFile))
            {
                Console.WriteLine(line);
            }
        }
        else
        {
            Console.WriteLine("No bookmarks saved yet.");
        }
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

    private string[] actions = null;
    private void TypingMode(object sender, System.Timers.ElapsedEventArgs e)
    {
        var tutorial_state = user_guide.ReturnState();

        if (in_tutorial && tutorial_state == "state7")
        {
            Console.WriteLine(tutorial_state);
            if (!typing_tutorial)
            {
                Console.WriteLine("trying to show items in tutorial.....");
                UpdateUI(() => TutorialStuff(tutorial_state, "d"));
                UpdateUI(() => TutorialStuff(tutorial_state, "e"));
                UpdateUI(() => TutorialStuff(tutorial_state, "f"));
                typing_tutorial = true;
            }
        }

        typing_mode = true;

        UpdateUI(() => main_window.HighlightTypingIcon(typing_mode));
        UpdateUI(() => asr_window.ShowWithFadeIn(true));

        UpdateUI(() =>
        {
            show_items.TypingActions.Visibility = System.Windows.Visibility.Visible;
        });

        StartTranscription();

        UpdateUI(() => show_items.NotificationLabel.Content = "Now Typing...");

        if (typing_mode)
        {
            wake_word_timer.Stop();
            intent_window_timer.Stop();
        }
    }

    public class WindowActions
    {
        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        private const uint INPUT_KEYBOARD = 1;
        private const ushort VK_LWIN = 0x5B; // Left Windows Key
        private const ushort VK_UP = 0x26;  // Up Arrow Key
        private const ushort VK_DOWN = 0x28; // Down Arrow Key
        private const uint KEYEVENTF_KEYUP = 0x0002; // Key release flag

        public void MaximizeActiveWindow()
        {
            SendKeyCombo(VK_LWIN, VK_UP);
        }

        public void MinimizeActiveWindow()
        {
            SendKeyCombo(VK_LWIN, VK_DOWN);
        }

        private void SendKeyCombo(ushort modifierKey, ushort actionKey)
        {
            INPUT[] inputs = new INPUT[4];

            // Press Modifier Key (Windows Key)
            inputs[0] = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT { wVk = modifierKey }
                }
            };

            // Press Action Key (Up/Down Arrow)
            inputs[1] = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT { wVk = actionKey }
                }
            };

            // Release Action Key
            inputs[2] = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT { wVk = actionKey, dwFlags = KEYEVENTF_KEYUP }
                }
            };

            // Release Modifier Key
            inputs[3] = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT { wVk = modifierKey, dwFlags = KEYEVENTF_KEYUP }
                }
            };

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }
    }

    // Usage Example
    private void MaximizeWindow()
    {
        var actions = new WindowActions();
        actions.MaximizeActiveWindow();
    }

    private void MinimizeWindow()
    {
        var actions = new WindowActions();
        actions.MinimizeActiveWindow();
    }

    public class BrightnessController
    {
        // Increase brightness by a specified step
        public void IncreaseBrightness(int step = 20)
        {
            AdjustBrightness(step);
        }

        // Decrease brightness by a specified step
        public void DecreaseBrightness(int step = 20)
        {
            AdjustBrightness(-step);
        }

        private void AdjustBrightness(int step)
        {
            try
            {
                // Get the current brightness
                int currentBrightness = GetCurrentBrightness();

                // Calculate the new brightness level
                int newBrightness = Clamp(currentBrightness + step, 0, 100);

                // Set the brightness to the new level
                SetBrightness(newBrightness);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adjusting brightness: {ex.Message}");
            }
        }

        private int GetCurrentBrightness()
        {
            int brightness = 0;

            // Query WMI for the current brightness
            using (var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    brightness = Convert.ToInt32(obj["CurrentBrightness"]);
                    break;
                }
            }

            return brightness;
        }

        private void SetBrightness(int brightness)
        {
            // Use WMI to set the brightness
            using (var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightnessMethods"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    obj.InvokeMethod("WmiSetBrightness", new object[] { UInt32.MaxValue, brightness });
                }
            }
        }

        // Clamp method for compatibility with .NET Framework
        private int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }

    public static class MathExtensions
    {
        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }

    private BrightnessController brightnessController = new BrightnessController();

    private void IncreaseBrightness()
    {
        brightnessController.IncreaseBrightness(); // Increases brightness by the default 10%
    }

    private void DecreaseBrightness()
    {
        brightnessController.DecreaseBrightness(); // Decreases brightness by the default 10%
    }

    private void SearchingMode(object sender, System.Timers.ElapsedEventArgs e)
    {
        search_mode = true;

        UpdateUI(() => asr_window.ShowWithFadeIn(true));

        StartTranscription();

        UpdateUI(() => show_items.NotificationLabel.Content = "Now Searching...");

        if (search_mode)
        {
            wake_word_timer.Stop();
            intent_window_timer.Stop();
        }
    }

    private bool shown_bookmarks = false;
    private void BookmarksShown(object sender, System.Timers.ElapsedEventArgs e)
    {
        UpdateUI(() => ShowBookmarks());

        shown_bookmarks = true;

        UpdateUI(() => asr_window.ShowWithFadeIn(true));

        StartTranscription();

        UpdateUI(() => show_items.NotificationLabel.Content = "Choose Bookmark...");

        if (shown_bookmarks)
        {
            wake_word_timer.Stop();
            intent_window_timer.Stop();
        }
    }

    private void ProcessSpokenTag(string spokenTag)
    {
        Console.WriteLine($"Processing spoken tag: {spokenTag}");  // Add this line for debugging
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

                show_items.RemoveTagsNoTimer();

                UpdateUI(() => show_items.NotificationLabel.Content = $"Clicking {tagNumber}");
                ClickItem(convertedRect); // Perform click on item

                showed_detected = false;
                number_clicked = true;
                StartTranscription();
                UpdateUI(() => main_window.HighlightODIcon(showed_detected));
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

    private bool clicked_state4 = false;

    private void ClickItem(Rect boundingRect)
    {
        var tutorial_state = user_guide.ReturnState();

        if (in_tutorial && tutorial_state == "state4")
        {
            Console.WriteLine(tutorial_state);
            if (!clicked_state4)
            {
                Console.WriteLine("trying to click item in tutorial.....");
                UpdateUI(() => TutorialStuff(tutorial_state, "f"));
                clicked_state4 = true;
            }
        }
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
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
    }

    private ShowItems showItemsWindow; // Field to hold the window reference

    private void RemoveTags()
    {
        show_items.RemoveTagsNoTimer();
        showed_detected = false;
        UpdateUI(() => main_window.HighlightODIcon(showed_detected));
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
        Console.WriteLine("Opening Google search in Chrome...");

        // Encode the query to ensure special characters are handled correctly
        string encodedQuery = Uri.EscapeDataString(query);

        // Construct the Google search URL with the encoded query
        string googleSearchUrl = $"https://www.google.com/search?q={encodedQuery}";

        show_items.OpenUrl(googleSearchUrl);

        // Navigate to the constructed URL
        show_items.driver.Navigate().GoToUrl(googleSearchUrl);
    }

    public void OpenChrome()
    {
        driver = new ChromeDriver();
        Console.WriteLine("Opening Chrome...");
    }

    private void SimulateMouseClick() // pan setup hin click
    {
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
    }

    private void SimulateKeyPress(System.Windows.Forms.Keys key) // pan enable hin keys ha keyboard
    {
        string keyString;

        switch (key)
        {
            case System.Windows.Forms.Keys.Enter:
                keyString = "{ENTER}";
                break;
            case System.Windows.Forms.Keys.Tab:
                keyString = "{TAB}";
                break;
            case System.Windows.Forms.Keys.Escape:
                keyString = "{ESC}";
                break;
            case System.Windows.Forms.Keys.Up:
                keyString = "{UP}";
                break;
            case System.Windows.Forms.Keys.Down:
                keyString = "{DOWN}";
                break;
            case System.Windows.Forms.Keys.Left:
                keyString = "{LEFT}";
                break;
            case System.Windows.Forms.Keys.Right:
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
        var tutorial_state = user_guide.ReturnState();

        try
        {
            if (in_tutorial && tutorial_state == "state2")
            {
                Console.WriteLine(tutorial_state);
                if (!opened_word)
                {
                    Console.WriteLine("trying to open word in tutorial.....");
                    UpdateUI(() => TutorialStuff(tutorial_state, "e"));
                    opened_word = true;
                } 
            }
            if (in_tutorial && tutorial_state == "state3")
            {
                Console.WriteLine(tutorial_state);
                if (!opened_word)
                {
                    Console.WriteLine("trying to open word in tutorial.....");
                    UpdateUI(() => TutorialStuff(tutorial_state, "e"));
                    opened_word = true;
                }
            }

            Process.Start(processName);
        }
        catch (Exception ex)
        {
            UpdateUI(() => asr_window.AppendText($"Failed to start {processName}: {ex.Message}", true));
        }
    }

    private void CloseApp() // close the currently used app/window
    {
        var tutorial_state = user_guide.ReturnState();

        if (in_tutorial && tutorial_state == "state2")
        {
            Console.WriteLine(tutorial_state);
            if (opened_word)
            {
                Console.WriteLine("trying to open word in tutorial.....");
                UpdateUI(() => TutorialStuff(tutorial_state, "f"));
                UpdateUI(() => TutorialStuff(tutorial_state, "g"));
                opened_word = false;
            }     
        }
        if (in_tutorial && tutorial_state == "state3")
        {
            Console.WriteLine(tutorial_state);
            if (opened_word)
            {
                Console.WriteLine("trying to open word in tutorial.....");
                UpdateUI(() => TutorialStuff(tutorial_state, "f"));
                opened_word = false;
            }
        }

        IntPtr handle = GetForegroundWindow();
        if (handle != IntPtr.Zero)
        {
            PostMessage(handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
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
        window_actions_count = 0;
        brightness_command_count = 0;
        bookmark_command_count = 0;

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
                    wake_word_detected = false;
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
    private bool mouse_state5 = false;
    private void OpenMouse()
    {
        var tutorial_state = user_guide.ReturnState();

        if (in_tutorial && tutorial_state == "state5")
        {
            Console.WriteLine(tutorial_state);
            if (!mouse_state5)
            {
                Console.WriteLine("trying to show items in tutorial.....");
                UpdateUI(() => TutorialStuff(tutorial_state, "e"));
                UpdateUI(() => TutorialStuff(tutorial_state, "f"));
                UpdateUI(() => TutorialStuff(tutorial_state, "g"));
                UpdateUI(() => TutorialStuff(tutorial_state, "next"));
                mouse_state5 = true;
            }
        }

        if (!camera_mouse_opened)
        {
            camera_mouse_opened = true;
            show_items.mouse_active = true;

            Task.Run(() =>
            {
                // Assuming you have a method StartCameraMouse in your cameramouse class
                camera_mouse.StartCameraMouse();
                UpdateUI(() =>
                {
                    show_items.NotificationLabel.Content = "Camera mouse activated.";
                });
            });
        }
    }

    // TODO - ig improve pa ini kay medyo clunky pa an pag close and open
    private void CloseMouse()
    {
        camera_mouse_opened = false;
        show_items.mouse_active = false;

        UpdateUI(() =>
        {
            show_items.ClearArrowDrawings();

            show_items.NotificationLabel.Content = "Camera mouse deactivated.";
        });

        camera_mouse.StopCameraMouse(); // Implement this in cameramouse.cs
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