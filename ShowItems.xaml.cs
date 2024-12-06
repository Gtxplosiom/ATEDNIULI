using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using System;
using System.Collections.Generic;
using System.Windows.Threading;
using NetMQ;
using NetMQ.Sockets;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions.Internal;
using System.Windows.Shapes;
using System.IO;
using Microsoft.Office.Interop.Word;
using Microsoft.Office.Interop.Excel;
using Microsoft.Office.Interop.PowerPoint;
using System.Security.Policy;

namespace ATEDNIULI
{
    public partial class ShowItems : System.Windows.Window
    {
        private readonly UserGuide user_guide;
        private DispatcherTimer _tagRemovalTimer; // Timer for removing tags
        private List<System.Windows.Controls.Label> _tags; // List to store tags
        private double ScalingFactor; // Declare the scaling factor
        private AutomationElement _taskbarElement; // Cached taskbar element
        private List<ClickableItem> _clickableItems; // List to store clickable items
        private CameraMouse camera_mouse;
        private int globalCounter = 1;

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        private const int LOGPIXELSX = 88;

        private SubscriberSocket _subscriberSocket; // ZMQ Subscriber Socket

        public IWebDriver driver;

        private void StartZMQListener()
        {
            // Set up a new thread to listen for ZMQ messages
            Thread zmqThread = new Thread(new ThreadStart(ZMQListener));
            zmqThread.IsBackground = true; // Ensure it doesn't block the application from closing
            zmqThread.Start();
        }

        private void ZMQListener()
        {
            // Set up ZMQ Subscriber to listen to port 5555
            using (_subscriberSocket = new SubscriberSocket())
            {
                _subscriberSocket.Connect("tcp://localhost:5555");
                _subscriberSocket.Subscribe(""); // Subscribe to all messages

                while (true)
                {
                    // Receive message from Python
                    string message = _subscriberSocket.ReceiveFrameString();
                    Dispatcher.Invoke(() => ProcessDetectionMessage(message)); // Process in UI thread
                }
            }
        }

        // Define a delegate for the detection event
        public delegate void ItemDetectedEventHandler(bool isDetected);

        // Add an event using the delegate
        public event ItemDetectedEventHandler ItemDetected;

        public bool mouse_active = false;
        public string detected_item = "";
        private void ProcessDetectionMessage(string message)
        {
            //Console.WriteLine($"Received message: {message}");

            // Check for "no detections" message first
            if (message.Contains("no detections"))
            {
                //Console.WriteLine("No detections found.");
                DetectedItemLabel.Visibility = Visibility.Collapsed;
                DetectedItemText.Visibility = Visibility.Collapsed;
                ActionList.Visibility = Visibility.Collapsed;
                ItemDetected?.Invoke(false);
                return;
            }

            var parts = message.Split(',');
            if (parts.Length == 5)
            {
                string label = parts[0];
                int x1 = int.Parse(parts[1]);
                int y1 = int.Parse(parts[2]);
                int x2 = int.Parse(parts[3]);
                int y2 = int.Parse(parts[4]);

                DetectedItemLabel.Visibility = Visibility.Visible;
                DetectedItemText.Text = label;
                DetectedItemText.Visibility = Visibility.Visible;

                // Display numbered actions based on the detected label
                var actions = GetActionsForLabel(label);

                detected_item = label;

                if (actions != null && actions.Length > 0 && mouse_active)
                {
                    ActionList.ItemsSource = actions;
                    ActionList.Visibility = Visibility.Visible;
                }
                else
                {
                    ActionList.Visibility = Visibility.Collapsed;
                }

                ItemDetected?.Invoke(true);
            }
            else
            {
                detected_item = "";
                Console.WriteLine("Invalid detection message.");
                DetectedItemLabel.Visibility = Visibility.Collapsed;
                DetectedItemText.Visibility = Visibility.Collapsed;
                ActionList.Visibility = Visibility.Collapsed;

                ItemDetected?.Invoke(false);
            }
        }

        // Returns a numbered list of actions based on the detected item label
        private string[] GetActionsForLabel(string label)
        {
            switch (label.ToLower())
            {
                case "chrome":
                    return new string[]
                    {
                        "1. Open new tab",
                        "2. Open last visited website",
                        "3. Bookmark this page",
                        "4. Close tab",
                        "5. Open incognito window"
                    };
                case "folder":
                    return new string[]
                    {
                        "1. Open folder",
                        "2. Rename folder",
                        "3. View properties",
                        "4. Share folder"
                    };
                case "file manager":
                    return new string[]
                    {
                        "1. Open Desktop folder",
                        "2. Open Downloads folder",
                        "3. Open Documents folder",
                        "4. Open Videos folder",
                        "5. Open Pictures folder"
                    };
                case "youtube":
                    return new string[]
                    {
                        "1. Play/Pause video",
                        "2. Like video",
                        "3. Subscribe to channel",
                        "4. View comments",
                        "5. Share video link"
                    };
                case "microsoft word":
                    return new string[]
                    {
                        "1. New Document"
                    };
                case "excel":
                    return new string[]
                    {
                        "1. New Workbook"
                    };
                case "powerpoint":
                    return new string[]
                    {
                        "1. New Presentation"
                    };
                case "facebook":
                    return new string[]
                    {
                        "1. Facebook Home page",
                        "2. Facebook Watch page",
                        "3. Facebook Marketplace",
                        "4. Facebook Groups page"
                    };
                case "canva":
                    return new string[]
                    {
                        "1. Canva Home page"
                    };
                case "chatgpt":
                    return new string[]
                    {
                        "1. ChatGPT Home page"
                    };
                case "discord":
                    return new string[]
                    {
                        "1. Discord Home page"
                    };
                case "gmail":
                    return new string[]
                    {
                        "1. Gmail Inbox page",
                        "2. Gmail Starred page",
                        "3. Gmail Sent page",
                        "4. Gmail Drafts page"
                    };
                case "gmeet":
                    return new string[]
                    {
                        "1. Gmeet Home page"
                    };
                case "instagram":
                    return new string[]
                    {
                        "1. Instagram Home page",
                        "2. Instagram Explore page",
                        "3. Instagram Reels page",
                        "4. Instagram Inbox page"
                    };
                case "lazada":
                    return new string[]
                    {
                        "1. Lazada Home page"
                    };
                case "netflix":
                    return new string[]
                    {
                        "1. Netflix Home page"
                    };
                case "onedrive":
                    return new string[]
                    {
                        "1. Onedrive Home page"
                    };
                case "pinterest":
                    return new string[]
                    {
                        "1. Pinterest Home page",
                        "2. Pinterest Videos page",
                        "3. Pinterest Explore page"
                    };
                case "shopee":
                    return new string[]
                    {
                        "1. Shopee Home page"
                    };
                case "telegram":
                    return new string[]
                    {
                        "1. Telegram Home page"
                    };
                case "tiktok":
                    return new string[]
                    {
                        "1. Tiktok Home page",
                        "2. Tiktok Explore page",
                        "3. Tiktok Following page",
                        "4. Tiktok Live page"
                    };
                case "github":
                    return new string[]
                    {
                        "1. Github Home page",
                        "2. Github Issues page",
                        "3. Github Pull page",
                        "4. Github Projects page",
                        "5. Github Discussions page",
                        "6. Github Codespaces page",
                        "7. Github Explore page",
                        "8. Github Marketplace page"
                    };
                case "linkedin":
                    return new string[]
                    {
                        "1. LinkedIn Home page"
                    };
                case "reddit":
                    return new string[]
                    {
                        "1. Reddit Home page",
                        "2. Reddit Popular page",
                        "3. Reddit Communities page",
                        "4. Reddit Topics page"
                    };
                case "spotify":
                    return new string[]
                    {
                        "1. Spotify Home page"
                    };
                case "twitch":
                    return new string[]
                    {
                        "1. Twitch Home page",
                        "2. Twitch Browse page",
                        "3. Twitch Following page"
                    };
                case "wikipedia":
                    return new string[]
                    {
                        "1. Wikipedia Home page",
                        "2. Wikipedia Talk page",
                        "3. Wikipedia Contents page",
                        "4. Wikipedia Current Events page"
                    };
                case "x":
                    return new string[]
                    {
                        "1. X Home page"
                    };
                case "zoom":
                    return new string[]
                    {
                        "1. Zoom Home page",
                        "2. Zoom Profile page",
                        "3. Zoom Meetings page"
                    };

                default:
                    return null; // No actions for unrecognized labels
            }
        }

        private void ActionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ActionList.SelectedItem != null)
            {
                // Parse the selected action number
                int actionNumber = int.Parse(ActionList.SelectedItem.ToString().Split('.')[0]);
                System.Threading.Tasks.Task.Run(() => ExecuteAction(detected_item, actionNumber));
            }
        }

        // Map action numbers to action methods
        // Dynamically execute actions based on the label and selected action number
        public void ExecuteAction(string label, int actionNumber)
        {
            switch (label.ToLower())
            {
                case "chrome":
                    ExecuteChromeAction(actionNumber);
                    break;

                case "folder":
                    ExecuteFolderAction(actionNumber);
                    break;

                case "file manager":
                    ExecuteFileManagerAction(actionNumber);
                    break;

                case "youtube":
                    ExecuteYouTubeAction(actionNumber);
                    break;

                case "microsoft word":
                    ExecuteWordAction(actionNumber);
                    break;

                case "powerpoint":
                    ExecutePowerPointAction(actionNumber);
                    break;

                case "excel":
                    ExecuteExcelAction(actionNumber);
                    break;

                case "facebook":
                    ExecuteFacebookAction(actionNumber);
                    break;

                case "messenger":
                    ExecuteMessengerAction(actionNumber);
                    break;

                case "canva":
                    ExecuteCanvaAction(actionNumber);
                    break;

                case "chatgpt":
                    ExecuteChatGPTAction(actionNumber);
                    break;

                case "discord":
                    ExecuteDiscordAction(actionNumber);
                    break;

                case "gmail":
                    ExecuteGmailAction(actionNumber);
                    break;

                case "gmeet":
                    ExecuteGmeetAction(actionNumber);
                    break;

                case "instagram":
                    ExecuteInstagramAction(actionNumber);
                    break;

                case "lazada":
                    ExecuteLazadaAction(actionNumber);
                    break;

                case "netflix":
                    ExecuteNetflixAction(actionNumber);
                    break;

                case "onedrive":
                    ExecuteOnedriveAction(actionNumber);
                    break;

                case "pinterest":
                    ExecutePinterestAction(actionNumber);
                    break;

                case "shopee":
                    ExecuteShopeeAction(actionNumber);
                    break;

                case "telegram":
                    ExecuteTelegramAction(actionNumber);
                    break;

                case "tiktok":
                    ExecuteTiktokAction(actionNumber);
                    break;

                case "github":
                    ExecuteGithubAction(actionNumber);
                    break;

                case "linkedin":
                    ExecuteLinkedinAction(actionNumber);
                    break;

                case "reddit":
                    ExecuteRedditAction(actionNumber);
                    break;

                case "spotify":
                    ExecuteSpotifyAction(actionNumber);
                    break;

                case "twitch":
                    ExecuteTwitchAction(actionNumber);
                    break;

                case "wikipedia":
                    ExecuteWikipediaAction(actionNumber);
                    break;

                case "x":
                    ExecuteXAction(actionNumber);
                    break;

                case "zoom":
                    ExecuteZoomAction(actionNumber);
                    break;

                default:
                    Console.WriteLine($"No actions available for label: {label}");
                    break;
            }
        }

        private void ExecuteFacebookAction(int actionNumber)
        {
            var actionUrls = new Dictionary<int, string>
            {
                { 1, "https://www.facebook.com/" },
                { 2, "https://www.facebook.com/watch/?ref=tab" },
                { 3, "https://www.facebook.com/marketplace/?ref=app_tab" },
                { 4, "https://www.facebook.com/groups/feed/" }
            };

            if (actionUrls.TryGetValue(actionNumber, out var url))
            {
                OpenUrl(url);
            }
            else
            {
                Console.WriteLine("Action not recognized for Chrome.");
            }
        }

        private void OpenUrl(string url)
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                OpenChrome();
                driver.Navigate().GoToUrl(url);
            });
        }

        private void ExecuteMessengerAction(int actionNumber)
        {
            if (actionNumber == 1)
            {
                System.Threading.Tasks.Task.Run(() =>
                {
                    OpenChrome();
                    driver.Navigate().GoToUrl("https://www.messenger.com/");
                });
            }
        }

        private void ExecuteCanvaAction(int actionNumber)
        { 
            if (actionNumber == 1)
            {
                System.Threading.Tasks.Task.Run(() =>
                {
                    OpenChrome();
                    driver.Navigate().GoToUrl("https://www.canva.com/en_ph/");
                });
            }
        }

        private void ExecuteChatGPTAction(int actionNumber)
        {
            if (actionNumber == 1)
            {
                System.Threading.Tasks.Task.Run(() =>
                {
                    OpenChrome();
                    driver.Navigate().GoToUrl("https://chatgpt.com/");
                });
            }
        }

        private void ExecuteDiscordAction(int actionNumber)
        {
            if (actionNumber == 1)
            {
                System.Threading.Tasks.Task.Run(() =>
                {
                    OpenChrome();
                    driver.Navigate().GoToUrl("https://discord.com/download");
                });
            }
        }

        private void ExecuteGmailAction(int actionNumber)
        {
            var actionUrls = new Dictionary<int, string>
            {
                { 1, "https://mail.google.com/mail/u/0/#inbox" },
                { 2, "https://mail.google.com/mail/u/0/#starred" },
                { 3, "https://mail.google.com/mail/u/0/#sent" },
                { 4, "https://mail.google.com/mail/u/0/#drafts" }
            };

            if (actionUrls.TryGetValue(actionNumber, out var url))
            {
                OpenUrl(url);
            }
            else
            {
                Console.WriteLine("Action not recognized for Chrome.");
            }
        }

        private void ExecuteGmeetAction(int actionNumber)
        {
            var actionUrls = new Dictionary<int, string>
            {
                { 1, "https://meet.google.com/landing" }
            };

            if (actionUrls.TryGetValue(actionNumber, out var url))
            {
                OpenUrl(url);
            }
            else
            {
                Console.WriteLine("Action not recognized for Chrome.");
            }
        }

        private void ExecuteInstagramAction(int actionNumber)
        {
            var actionUrls = new Dictionary<int, string>
            {
                { 1, "https://www.instagram.com/" },
                { 2, "https://www.instagram.com/explore/" },
                { 3, "https://www.instagram.com/reels/" },
                { 4, "https://www.instagram.com/direct/inbox/" }
            };

            if (actionUrls.TryGetValue(actionNumber, out var url))
            {
                OpenUrl(url);
            }
            else
            {
                Console.WriteLine("Action not recognized for Chrome.");
            }
        }

        private void ExecuteLazadaAction(int actionNumber)
        {
            var actionUrls = new Dictionary<int, string>
            {
                { 1, "https://www.lazada.com.ph/" }
            };

            if (actionUrls.TryGetValue(actionNumber, out var url))
            {
                OpenUrl(url);
            }
            else
            {
                Console.WriteLine("Action not recognized for Chrome.");
            }
        }

        private void ExecuteNetflixAction(int actionNumber)
        {
            var actionUrls = new Dictionary<int, string>
            {
                { 1, "https://www.netflix.com/ph-en/" }
            };

            if (actionUrls.TryGetValue(actionNumber, out var url))
            {
                OpenUrl(url);
            }
            else
            {
                Console.WriteLine("Action not recognized for Chrome.");
            }
        }

        private void ExecuteOnedriveAction(int actionNumber)
        {
            var actionUrls = new Dictionary<int, string>
            {
                { 1, "https://www.microsoft.com/en-us/microsoft-365/onedrive/online-cloud-storage" }
            };

            if (actionUrls.TryGetValue(actionNumber, out var url))
            {
                OpenUrl(url);
            }
            else
            {
                Console.WriteLine("Action not recognized for Chrome.");
            }
        }

        private void ExecutePinterestAction(int actionNumber)
        {
            var actionUrls = new Dictionary<int, string>
            {
                { 1, "https://www.pinterest.com/" },
                { 2, "https://www.pinterest.com/videos/"},
                { 3, "https://www.pinterest.com/ideas/"}
            };

            if (actionUrls.TryGetValue(actionNumber, out var url))
            {
                OpenUrl(url);
            }
            else
            {
                Console.WriteLine("Action not recognized for Chrome.");
            }
        }

        private void ExecuteShopeeAction(int actionNumber)
        {
            var actionUrls = new Dictionary<int, string>
            {
                { 1, "https://shopee.ph/" }
            };

            if (actionUrls.TryGetValue(actionNumber, out var url))
            {
                OpenUrl(url);
            }
            else
            {
                Console.WriteLine("Action not recognized for Chrome.");
            }
        }

        private void ExecuteTelegramAction(int actionNumber)
        {
            var actionUrls = new Dictionary<int, string>
            {
                { 1, "https://web.telegram.org/" }
            };

            if (actionUrls.TryGetValue(actionNumber, out var url))
            {
                OpenUrl(url);
            }
            else
            {
                Console.WriteLine("Action not recognized for Chrome.");
            }
        }

        private void ExecuteTiktokAction(int actionNumber)
        {
            var actionUrls = new Dictionary<int, string>
            {
                { 1, "https://www.tiktok.com/en/" },
                { 2, "https://www.tiktok.com/explore" },
                { 3, "https://www.tiktok.com/following" },
                { 4, "https://www.tiktok.com/live" }
            };

            if (actionUrls.TryGetValue(actionNumber, out var url))
            {
                OpenUrl(url);
            }
            else
            {
                Console.WriteLine("Action not recognized for Chrome.");
            }
        }

        private void ExecuteGithubAction(int actionNumber)
        {
            var actionUrls = new Dictionary<int, string>
            {
                { 1, "https://github.com/" },
                { 2, "https://github.com/issues" },
                { 3, "https://github.com/pulls" },
                { 4, "https://github.com/projects" },
                { 5, "https://github.com/discussions" },
                { 6, "https://github.com/codespaces" },
                { 7, "https://github.com/explore" },
                { 8, "https://github.com/marketplace" }
            };

            if (actionUrls.TryGetValue(actionNumber, out var url))
            {
                OpenUrl(url);
            }
            else
            {
                Console.WriteLine("Action not recognized for Chrome.");
            }
        }

        private void ExecuteLinkedinAction(int actionNumber)
        {
            var actionUrls = new Dictionary<int, string>
            {
                { 1, "https://www.linkedin.com/" }
            };

            if (actionUrls.TryGetValue(actionNumber, out var url))
            {
                OpenUrl(url);
            }
            else
            {
                Console.WriteLine("Action not recognized for Chrome.");
            }
        }

        private void ExecuteRedditAction(int actionNumber)
        {
            var actionUrls = new Dictionary<int, string>
            {
                { 1, "https://www.reddit.com/?feed=home" },
                { 2, "https://www.reddit.com/r/popular/" },
                { 3, "https://www.reddit.com/best/communities/1/" },
                { 3, "https://www.reddit.com/topics/a-1/" }
            };

            if (actionUrls.TryGetValue(actionNumber, out var url))
            {
                OpenUrl(url);
            }
            else
            {
                Console.WriteLine("Action not recognized for Chrome.");
            }
        }

        private void ExecuteSpotifyAction(int actionNumber)
        {
            var actionUrls = new Dictionary<int, string>
            {
                { 1, "https://open.spotify.com/" }
            };

            if (actionUrls.TryGetValue(actionNumber, out var url))
            {
                OpenUrl(url);
            }
            else
            {
                Console.WriteLine("Action not recognized for Chrome.");
            }
        }

        private void ExecuteTwitchAction(int actionNumber)
        {
            var actionUrls = new Dictionary<int, string>
            {
                { 1, "https://www.twitch.tv/" },
                { 2, "https://www.twitch.tv/directory" },
                { 3, "https://www.twitch.tv/directory/following" }
            };

            if (actionUrls.TryGetValue(actionNumber, out var url))
            {
                OpenUrl(url);
            }
            else
            {
                Console.WriteLine("Action not recognized for Chrome.");
            }
        }

        private void ExecuteWikipediaAction(int actionNumber)
        {
            var actionUrls = new Dictionary<int, string>
            {
                { 1, "https://en.wikipedia.org/wiki/Main_Page" },
                { 2, "https://en.wikipedia.org/wiki/Talk:Main_Page" },
                { 3, "https://en.wikipedia.org/wiki/Wikipedia:Contents" },
                { 4, "https://en.wikipedia.org/wiki/Portal:Current_events" }
            };

            if (actionUrls.TryGetValue(actionNumber, out var url))
            {
                OpenUrl(url);
            }
            else
            {
                Console.WriteLine("Action not recognized for Chrome.");
            }
        }

        private void ExecuteXAction(int actionNumber)
        {
            var actionUrls = new Dictionary<int, string>
            {
                { 1, "https://x.com/i/flow/single_sign_on" }
            };

            if (actionUrls.TryGetValue(actionNumber, out var url))
            {
                OpenUrl(url);
            }
            else
            {
                Console.WriteLine("Action not recognized for Chrome.");
            }
        }

        private void ExecuteZoomAction(int actionNumber)
        {
            var actionUrls = new Dictionary<int, string>
            {
                { 1, "https://us05web.zoom.us/myhome" },
                { 2, "https://us05web.zoom.us/profile" },
                { 3, "https://us05web.zoom.us/meeting" }
            };

            if (actionUrls.TryGetValue(actionNumber, out var url))
            {
                OpenUrl(url);
            }
            else
            {
                Console.WriteLine("Action not recognized for Chrome.");
            }
        }

        private Microsoft.Office.Interop.Word.Application wordApp;

        // Define methods for each label's actions
        private void ExecuteWordAction(int actionNumber)
        {
            wordApp = new Microsoft.Office.Interop.Word.Application { Visible = true };
            switch (actionNumber)
            {
                case 1: CreateNewDocument(); break;
                default: Console.WriteLine("Action not recognized for Chrome."); break;
            }
        }

        private void CreateNewDocument()
        {
            try
            {
                Document newDoc = wordApp.Documents.Add();
                Console.WriteLine("New Word document created.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating new document: {ex.Message}");
            }
        }

        private Microsoft.Office.Interop.Excel.Application excelApp;

        private void ExecuteExcelAction(int actionNumber)
        {
            excelApp = new Microsoft.Office.Interop.Excel.Application { Visible = true };

            switch (actionNumber)
            {
                case 1: CreateNewWorkbook(); break;
                default: Console.WriteLine("Action not recognized for Chrome."); break;
            }
        }

        private void CreateNewWorkbook()
        {
            try
            {
                Workbook newWorkbook = excelApp.Workbooks.Add();
                Console.WriteLine("New Excel workbook created.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating new workbook: {ex.Message}");
            }
        }

        private Microsoft.Office.Interop.PowerPoint.Application powerpointApp;

        private void ExecutePowerPointAction(int actionNumber)
        {
            powerpointApp = new Microsoft.Office.Interop.PowerPoint.Application { Visible = Microsoft.Office.Core.MsoTriState.msoTrue };

            switch (actionNumber)
            {
                case 1: CreateNewPresentation(); break;
                default: Console.WriteLine("Action not recognized for Chrome."); break;
            }
        }

        private void CreateNewPresentation()
        {
            try
            {
                Presentation newPresentation = powerpointApp.Presentations.Add(Microsoft.Office.Core.MsoTriState.msoTrue);
                Console.WriteLine("New PowerPoint presentation created.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating new presentation: {ex.Message}");
            }
        }

        private void ExecuteChromeAction(int actionNumber)
        {
            switch (actionNumber)
            {
                case 1: OpenNewTab(); break;
                case 2: OpenLastVisitedWebsite(); break;
                case 3: BookmarkPage(); break;
                case 4: CloseTab(); break;
                case 5: OpenIncognitoWindow(); break;
                default: Console.WriteLine("Action not recognized for Chrome."); break;
            }
        }

        private void ExecuteFolderAction(int actionNumber)
        {
            switch (actionNumber)
            {
                //case 1: OpenFolder(); break;
                //case 2: RenameFolder(); break;
                //case 3: ViewFolderProperties(); break;
                //case 4: ShareFolder(); break;
                default: Console.WriteLine("Action not recognized for Folder."); break;
            }
        }

        private void ExecuteFileManagerAction(int actionNumber)
        {
            switch (actionNumber)
            {
                case 1: OpenDesktopFolder(); break;
                case 2: OpenDownloadsFolder(); break;
                case 3: OpenDocumentsFolder(); break;
                case 4: OpenVideosFolder(); break;
                case 5: OpenPicturesFolder(); break;
                default: Console.WriteLine("Action not recognized for File Manager."); break;
            }
        }

        private void ExecuteYouTubeAction(int actionNumber)
        {
            switch (actionNumber)
            {
                //case 1: PlayPauseVideo(); break;
                //case 2: LikeVideo(); break;
                //case 3: SubscribeToChannel(); break;
                //case 4: ViewComments(); break;
                //case 5: ShareVideoLink(); break;
                default: Console.WriteLine("Action not recognized for YouTube."); break;
            }
        }

        private void OpenDesktopFolder()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            OpenFolderPath(path);
        }

        private void OpenDownloadsFolder()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads";
            OpenFolderPath(path);
        }

        private void OpenDocumentsFolder()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            OpenFolderPath(path);
        }

        private void OpenVideosFolder()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            OpenFolderPath(path);
        }

        private void OpenPicturesFolder()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            OpenFolderPath(path);
        }

        private void OpenFolderPath(string folderPath)
        {
            try
            {
                if (Directory.Exists(folderPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", folderPath);
                }
                else
                {
                    Console.WriteLine($"The folder path does not exist: {folderPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening folder: {ex.Message}");
            }
        }


        public void OpenChrome()
        {
            ChromeOptions options = new ChromeOptions();

            options.AddExcludedArgument("enable-automation");

            driver = new ChromeDriver(options);
        }

        private void OpenNewTab()
        {
            driver = new ChromeDriver();
            Console.WriteLine("Opening a new tab in Chrome...");
            driver.Navigate().GoToUrl("chrome://newtab");
        }

        private void OpenLastVisitedWebsite()
        {
            Console.WriteLine("Opening the last visited website...");
            if (driver == null)
            {
                Console.WriteLine("No chrome window yet");
            }
            else
            {
                driver.Navigate().Back(); // This simulates going back to the last page
            }
        }

        private void BookmarkPage()
        {
            if (driver == null)
            {
                Console.WriteLine("No chrome window yet");
            }
            else
            {
                Console.WriteLine("Bookmarking the current page...");
                // You can execute JavaScript to trigger the bookmark dialog in Chrome
                ((IJavaScriptExecutor)driver).ExecuteScript("document.execCommand('AddBookmark');");
            }
        }

        private void CloseTab()
        {
            if (driver == null)
            {
                Console.WriteLine("No chrome window yet");
            }
            else
            {
                Console.WriteLine("Closing the current tab...");
                driver.Close(); // Closes the current tab
            }
        }

        private void OpenIncognitoWindow()
        {
            Console.WriteLine("Opening an incognito window...");
            var options = new ChromeOptions();
            options.AddArgument("--incognito");
            driver = new ChromeDriver(options); // Open a new incognito window
        }

        private double GetScalingFactor()
        {
            IntPtr hdc = GetDC(IntPtr.Zero); // Get the device context for the entire screen
            int dpiX = GetDeviceCaps(hdc, LOGPIXELSX); // Get the horizontal DPI
            return dpiX / 96.0; // Standard DPI is 96 (1x scaling)
        }

        public ShowItems()
        {
            InitializeComponent();
            camera_mouse = new CameraMouse();
            _tags = new List<System.Windows.Controls.Label>(); // Initialize the tag list
            ScalingFactor = GetScalingFactor();
            Show();
            StartZMQListener();
            TrackMouse();
            user_guide = new UserGuide();
        }

        private static (int X, int Y) GetMousePosition()
        {
            // Get the current mouse position using Cursor.Position
            var mousePosition = System.Windows.Forms.Cursor.Position;
            return (mousePosition.X, mousePosition.Y);
        }

        public double lastDirectionX = 0;
        public double lastDirectionY = 0;

        public double lastSpeed = 0;

        // TrackMouse method to update label position and direction arrow
        private void TrackMouse()
        {
            Thread trackingThread = new Thread(() =>
            {
                while (true)
                {
                    var currentMousePosition = GetMousePosition();

                    // Get the screen width and height
                    int screenWidth = (int)System.Windows.SystemParameters.PrimaryScreenWidth;
                    int screenHeight = (int)System.Windows.SystemParameters.PrimaryScreenHeight;

                    // Default offset
                    int offsetX = 15;  // You can adjust this for more fine-grained positioning
                    int offsetY = 50;

                    // Check if the label would exceed the screen boundaries
                    if (currentMousePosition.Y - offsetY < 0) // Top side
                    {
                        offsetY = -30; // Move the label below
                    }
                    else if (currentMousePosition.Y + offsetY > screenHeight) // Bottom side
                    {
                        offsetY = 50; // Move the label above
                    }

                    // Adjust label position based on mouse position and offset
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        Canvas.SetLeft(MouseActionLabel, currentMousePosition.X - offsetX);
                        Canvas.SetTop(MouseActionLabel, currentMousePosition.Y - offsetY);

                        // Draw arrow on transparent canvas
                        DrawArrowOnCanvas(new System.Windows.Point(currentMousePosition.X, currentMousePosition.Y), lastDirectionX, lastDirectionY, lastSpeed);
                    });

                    Thread.Sleep(50); // Sleep for 50 milliseconds
                }
            });

            trackingThread.IsBackground = true;
            trackingThread.Start();
        }

        public void ClearArrowDrawings()
        {
            Console.WriteLine($"Clearing {arrowElements.Count} arrow elements.");
            foreach (var element in arrowElements)
            {
                OverlayCanvas.Children.Remove(element);
            }
            arrowElements.Clear();
        }

        // Method to draw arrow on the transparent canvas
        // List to track arrow elements
        public static List<UIElement> arrowElements = new List<UIElement>();

        private void DrawArrowOnCanvas(System.Windows.Point mousePosition, double directionX, double directionY, double speed)
        {
            // Clear only the previous arrow visuals
            foreach (var element in arrowElements)
            {
                OverlayCanvas.Children.Remove(element);
            }
            arrowElements.Clear();

            // Normalize direction
            double magnitude = Math.Sqrt(directionX * directionX + directionY * directionY);
            if (magnitude > 0)
            {
                directionX /= magnitude;
                directionY /= magnitude;
            }

            // Stretch factor based on speed (the higher the speed, the longer the arrowhead)
            double stretchFactor = Math.Min(1 + speed * 0.1, 3); // Adjust the scaling factor as needed, capped at 3

            // Adjust arrow so it starts ahead of the cursor
            double cursorOffset = 30; // Distance between the cursor and the start of the arrow
            System.Windows.Point arrowStart = new System.Windows.Point(
                mousePosition.X + directionX * cursorOffset,  // Move the start ahead of the cursor
                mousePosition.Y + directionY * cursorOffset);

            // Calculate the stretched arrowhead points
            double arrowHeadLength = 5 * stretchFactor; // Base length of arrowhead stretched by speed
            System.Windows.Point arrowEnd = new System.Windows.Point(
                arrowStart.X + directionX * arrowHeadLength,
                arrowStart.Y + directionY * arrowHeadLength);

            // Draw stretched arrowhead (triangle)
            Polygon arrowHead = new Polygon
            {
                Points = new PointCollection
        {
            arrowEnd,
            new System.Windows.Point(arrowEnd.X - directionY * arrowHeadLength / 2 - directionX * arrowHeadLength / 2,
                      arrowEnd.Y + directionX * arrowHeadLength / 2 - directionY * arrowHeadLength / 2),
            new System.Windows.Point(arrowEnd.X + directionY * arrowHeadLength / 2 - directionX * arrowHeadLength / 2,
                      arrowEnd.Y - directionX * arrowHeadLength / 2 - directionY * arrowHeadLength / 2)
        },
                Fill = Brushes.Yellow
            };

            // Add the new arrowhead to the canvas
            OverlayCanvas.Children.Add(arrowHead);
            arrowElements.Add(arrowHead); // Keep track of the arrowhead
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Set the window size to cover the entire screen
            var primaryScreenWidth = SystemParameters.PrimaryScreenWidth;
            var primaryScreenHeight = SystemParameters.PrimaryScreenHeight;

            this.Left = 0;
            this.Top = 0;
            this.Width = primaryScreenWidth;
            this.Height = primaryScreenHeight;

            OverlayCanvas.Width = this.Width;
            OverlayCanvas.Height = this.Height;

            // To prevent the window from being focusable
            this.IsHitTestVisible = false;
        }

        public class ClickableItem
        {
            public string Name { get; set; }
            public Rect BoundingRectangle { get; set; } // This holds the coordinates
        }

        private CancellationTokenSource _cancellationTokenSource;

        public void ListClickableItemsInCurrentWindow()
        {
            // Reset clickable items and tags
            _clickableItems = new List<ClickableItem>();
            foreach (var tag in _tags)
            {
                OverlayCanvas.Children.Remove(tag);
            }
            _tags.Clear();
            detected = false;

            // Ensure the method runs on the UI thread
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                Dispatcher.Invoke(() => ListClickableItemsInCurrentWindow());
                return;
            }

            // Dispose of the previous token source to avoid memory leaks
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            // Start processing clickable items with a new token
            System.Threading.Tasks.Task.Run(() => ProcessClickableItems(_cancellationTokenSource.Token));
        }

        private async System.Threading.Tasks.Task ProcessClickableItems(CancellationToken token)
        {
            AutomationElement currentWindow = null;
            try
            {
                // Throttle the execution to prevent overlap
                await System.Threading.Tasks.Task.Delay(200, token);

                if (token.IsCancellationRequested)
                    return; // Early exit if the token was canceled

                IntPtr windowHandle = GetForegroundWindow();
                IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null);
                //IntPtr userguideHandle = user_guide.GetWindowHandle();

                if (windowHandle == IntPtr.Zero)
                {
                    Console.WriteLine("Currently not focused in a window, processing desktop and taskbar items.");
                    // Skip current window scanning and continue with the rest
                }
                else
                {
                    currentWindow = AutomationElement.FromHandle(windowHandle);
                }

                // Proceed to get desktop window and its elements as before
                AutomationElement desktop = AutomationElement.RootElement.FindFirst(
                    TreeScope.Children,
                    new PropertyCondition(AutomationElement.ClassNameProperty, "Progman"));

                if (desktop != null)
                {
                    var iconCondition = new OrCondition(
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem),
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Image)
                    );

                    var desktopIcons = desktop.FindAll(TreeScope.Children, iconCondition);

                    Dispatcher.Invoke(() => ProcessClickableElements(null, null, false, desktopIcons));
                }

                if (currentWindow != null)
                {
                    string windowTitle = currentWindow.Current.Name;
                    bool isBrowser = IsBrowserWindow(windowTitle);

                    if (!isBrowser)
                    {
                        var clickableCondition = new OrCondition(
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem),  // Include TreeItem for sidebar elements
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Pane),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)// Include Pane for potential container elements
                            );

                        var clickableElements = currentWindow.FindAll(TreeScope.Descendants, clickableCondition);

                        Dispatcher.Invoke(() => ProcessClickableElements(clickableElements));
                    }
                    else if (isBrowser)
                    {
                        Console.WriteLine("Staring browser scan");
                        StartScanning(driver);
                    }
                }

                if (taskbarHandle != IntPtr.Zero)
                {
                    var taskbarElement = AutomationElement.FromHandle(taskbarHandle);

                    if (taskbarElement != null)
                    {
                        var clickableCondition = new OrCondition(
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink)
                        );

                        var taskbarItems = taskbarElement.FindAll(TreeScope.Subtree, clickableCondition);

                        Dispatcher.Invoke(() => ProcessClickableElements(taskbarItems, null));
                    }
                }

                InteractWithUserGuideWindow("UserGuide");
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Process was canceled.");
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show($"Error: {ex.Message}"));
            }
        }

        private void TagClickableElements(Rect[] boundingRects, string[] elementNames)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                for (int i = 0; i < boundingRects.Length; i++)
                {
                    try
                    {
                        var rect = boundingRects[i];

                        var tag = new System.Windows.Controls.Label
                        {
                            Content = globalCounter.ToString(),  // Display the globalCounter number
                            Background = Brushes.Yellow,
                            Foreground = Brushes.Black,
                            Padding = new Thickness(5),
                            Opacity = 0.7
                        };

                        Canvas.SetLeft(tag, rect.Left / ScalingFactor);
                        Canvas.SetTop(tag, rect.Top / ScalingFactor - 20);

                        OverlayCanvas.Children.Add(tag);

                        _tags.Add(tag);

                        // Create and store the clickable item with its adjusted bounding rectangle
                        _clickableItems.Add(new ClickableItem
                        {
                            Name = $"Item {globalCounter}",
                            BoundingRectangle = rect
                        });

                        globalCounter++;  // Increment the global counter after tagging each element
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to place a tag: {ex.Message}");
                    }
                }
            });
        }

        private Rect[] GetBoundingRectangles(AutomationElementCollection clickableElements, out string[] elementNames)
        {
            var boundingRects = new List<Rect>();
            var namesList = new List<string>();

            foreach (AutomationElement element in clickableElements)
            {
                try
                {
                    var boundingRect = element.Current.BoundingRectangle;

                    if (!boundingRect.IsEmpty)
                    {
                        boundingRects.Add(new Rect(
                            boundingRect.Left / ScalingFactor,
                            boundingRect.Top / ScalingFactor,
                            boundingRect.Width / ScalingFactor,
                            boundingRect.Height / ScalingFactor));

                        string controlName = element.Current.Name ?? "Unknown";
                        namesList.Add(controlName);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to get bounding rectangle: {ex.Message}");
                }
            }

            elementNames = namesList.ToArray();
            return boundingRects.ToArray();
        }

        public void InteractWithUserGuideWindow(string windowTitle)
        {
            try
            {
                var desktop = AutomationElement.RootElement;
                var windowCondition = new PropertyCondition(AutomationElement.NameProperty, windowTitle);

                var userGuideWindow = desktop.FindFirst(TreeScope.Children, windowCondition);

                if (userGuideWindow != null)
                {
                    Console.WriteLine("UserGuide window found!");

                    var clickableCondition = new OrCondition(
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink)
                    );

                    var clickableItems = userGuideWindow.FindAll(TreeScope.Descendants, clickableCondition);

                    var boundingRects = GetBoundingRectangles(clickableItems, out string[] clickableNames);

                    TagClickableElements(boundingRects, clickableNames);
                }
                else
                {
                    Console.WriteLine("UserGuide window not found.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Interaction Error: {ex.Message}");
            }
        }

        public void StartScanning(IWebDriver driver)
        {
            try
            {
                if (driver == null)
                {
                    Console.WriteLine("Driver is null!");
                    return;
                }

                IReadOnlyCollection<IWebElement> linkElements = driver.FindElements(By.CssSelector("a"));
                if (linkElements == null || linkElements.Count == 0)
                {
                    Console.WriteLine("No links found.");
                    return;
                }

                int linkCount = 1;
                int viewportWidth = Convert.ToInt32(((IJavaScriptExecutor)driver).ExecuteScript("return window.innerWidth;"));
                int viewportHeight = Convert.ToInt32(((IJavaScriptExecutor)driver).ExecuteScript("return window.innerHeight;"));
                var browserPosition = driver.Manage().Window.Position;

                foreach (IWebElement link in linkElements)
                {
                    try
                    {
                        if (link == null)
                        {
                            Console.WriteLine("Link is null, skipping...");
                            continue; // Skip if link is null
                        }

                        var location = link.Location;
                        var size = link.Size;

                        // Check if location or size is invalid
                        if (location == null || size == null || location.X < 0 || location.Y < 0 || size.Width <= 0 || size.Height <= 0)
                        {
                            Console.WriteLine($"Link {linkCount} has invalid location or size.");
                            continue;
                        }

                        if (IsInViewport(location.X, location.Y, size.Width, size.Height, viewportWidth, viewportHeight))
                        {
                            int adjustedX = location.X + browserPosition.X;
                            int adjustedY = location.Y + browserPosition.Y + 80;

                            // Create the bounding rectangle
                            Rect boundingRect = new Rect(adjustedX, adjustedY, size.Width, size.Height);

                            var clickableItem = new ClickableItem
                            {
                                Name = $"Link {linkCount}",
                                BoundingRectangle = boundingRect
                            };

                            // Ensure _clickableItems is initialized
                            if (_clickableItems == null)
                            {
                                Console.WriteLine("_clickableItems is null!");
                            }
                            else
                            {
                                _clickableItems.Add(clickableItem);
                            }

                            // Ensure _tags and OverlayCanvas are initialized
                            if (_tags == null)
                            {
                                Console.WriteLine("_tags list is null!");
                            }

                            if (OverlayCanvas == null)
                            {
                                Console.WriteLine("OverlayCanvas is null!");
                            }
                            else
                            {
                                // Ensure UI updates are marshaled to the UI thread using Dispatcher
                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                {
                                    // Add UI tag for visualization (as before)
                                    System.Windows.Controls.Label tag = new System.Windows.Controls.Label
                                    {
                                        Content = $"{globalCounter}",
                                        Background = Brushes.Yellow,
                                        Foreground = Brushes.Black,
                                        Padding = new Thickness(5),
                                        Opacity = 0.7
                                    };

                                    Canvas.SetLeft(tag, adjustedX);
                                    Canvas.SetTop(tag, adjustedY); // Position tag above the link
                                    OverlayCanvas.Children.Add(tag);

                                    _tags.Add(tag);
                                });
                            }
                        }

                        globalCounter++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing link {linkCount}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred during scanning: {ex.Message}");
            }
        }

        public bool detected = false;
        private void ProcessClickableElements(
    AutomationElementCollection clickableElements = null,
    AutomationElementCollection webClickables = null,
    bool isBrowser = false,
    AutomationElementCollection desktopIcons = null)
        {
            if (clickableElements != null)
            {
                foreach (AutomationElement element in clickableElements)
                {
                    if (element == null || element.Current.IsOffscreen) continue;

                    var boundingRect = element.Current.BoundingRectangle;

                    if (!boundingRect.IsEmpty)
                    {
                        Rect adjustedBoundingRect = new Rect(
                            boundingRect.Left / ScalingFactor,
                            boundingRect.Top / ScalingFactor,
                            boundingRect.Width / ScalingFactor,
                            boundingRect.Height / ScalingFactor
                        );

                        string controlName = element.Current.Name;
                        Console.WriteLine($"Clickable Item {globalCounter}: {controlName}");

                        // Wrap UI updates in Dispatcher.Invoke to ensure main thread updates
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                // Create a new label (tag) representing the clickable element
                                System.Windows.Controls.Label tag = new System.Windows.Controls.Label
                                {
                                    Content = globalCounter,
                                    Background = Brushes.Yellow,
                                    Foreground = Brushes.Black,
                                    Padding = new Thickness(5),
                                    Opacity = 0.7
                                };

                                // Set the adjusted position based on the bounding rectangle
                                Canvas.SetLeft(tag, adjustedBoundingRect.Left);
                                Canvas.SetTop(tag, adjustedBoundingRect.Top - 20);

                                OverlayCanvas.Children.Add(tag);

                                // Add the tag to the list of UI elements
                                _tags.Add(tag);

                                // Create and store the clickable item with adjusted bounding rectangle
                                _clickableItems.Add(new ClickableItem
                                {
                                    Name = controlName,
                                    BoundingRectangle = boundingRect
                                });

                                globalCounter++;  // Increment the global counter after each element
                            }
                            catch (Exception uiEx)
                            {
                                Console.WriteLine($"UI update error (clickableElements): {uiEx.Message}");
                            }
                        });

                        detected = true;
                    }
                }
            }
            else if (isBrowser)
            {
                try
                {
                    IReadOnlyCollection<IWebElement> linkElements = driver.FindElements(By.CssSelector("a"));
                    int linkCount = 1;
                    int viewportWidth = Convert.ToInt32(((IJavaScriptExecutor)driver).ExecuteScript("return window.innerWidth;"));
                    int viewportHeight = Convert.ToInt32(((IJavaScriptExecutor)driver).ExecuteScript("return window.innerHeight;"));
                    var browserPosition = driver.Manage().Window.Position;

                    foreach (IWebElement link in linkElements)
                    {
                        try
                        {
                            var location = link.Location;
                            var size = link.Size;

                            if (IsInViewport(location.X, location.Y, size.Width, size.Height, viewportWidth, viewportHeight))
                            {
                                Console.WriteLine($"Link {linkCount}:");
                                Console.WriteLine($"Bounding Box (Browser Coordinates) - X: {location.X}, Y: {location.Y}, Width: {size.Width}, Height: {size.Height}");
                                int adjustedX = location.X + browserPosition.X;
                                int adjustedY = location.Y + browserPosition.Y;

                                Console.WriteLine($"Adjusted Bounding Box (Screen Coordinates) - X: {adjustedX}, Y: {adjustedY}");
                                Console.WriteLine($"Link URL: {link.GetAttribute("href")}");

                                var clickableItem = new ClickableItem
                                {
                                    Name = $"Link {linkCount}",
                                    BoundingRectangle = new Rect(adjustedX, adjustedY, size.Width, size.Height)
                                };

                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                {
                                    try
                                    {
                                        System.Windows.Controls.Label tag = new System.Windows.Controls.Label
                                        {
                                            Content = $"Link {globalCounter}",
                                            Background = Brushes.Yellow,
                                            Foreground = Brushes.Black,
                                            Padding = new Thickness(5),
                                            Opacity = 0.7
                                        };

                                        Canvas.SetLeft(tag, adjustedX);
                                        Canvas.SetTop(tag, adjustedY - 20);
                                        OverlayCanvas.Children.Add(tag);

                                        _tags.Add(tag);
                                        _clickableItems.Add(clickableItem);

                                        globalCounter++;
                                    }
                                    catch (Exception linkUiEx)
                                    {
                                        Console.WriteLine($"UI update error for browser links: {linkUiEx.Message}");
                                    }
                                });

                                linkCount++;
                            }
                        }
                        catch (Exception linkElementEx)
                        {
                            Console.WriteLine($"Error processing link: {linkElementEx.Message}");
                        }
                    }
                }
                catch (Exception browserEx)
                {
                    Console.WriteLine($"An error occurred during scanning links: {browserEx.Message}");
                }
                detected = true;
            }
            else if (desktopIcons != null)
            {
                foreach (AutomationElement element in desktopIcons)
                {
                    if (element == null || element.Current.IsOffscreen) continue;

                    var boundingRect = element.Current.BoundingRectangle;

                    if (!boundingRect.IsEmpty)
                    {
                        string controlName = element.Current.Name;

                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                System.Windows.Controls.Label tag = new System.Windows.Controls.Label
                                {
                                    Content = globalCounter,
                                    Background = Brushes.Yellow,
                                    Foreground = Brushes.Black,
                                    Padding = new Thickness(5),
                                    Opacity = 0.7
                                };

                                Canvas.SetLeft(tag, boundingRect.Left);
                                Canvas.SetTop(tag, boundingRect.Top - 20);

                                OverlayCanvas.Children.Add(tag);
                                _tags.Add(tag);

                                _clickableItems.Add(new ClickableItem
                                {
                                    Name = controlName,
                                    BoundingRectangle = boundingRect
                                });

                                globalCounter++;
                            }
                            catch (Exception desktopUiEx)
                            {
                                Console.WriteLine($"UI update error for desktop icons: {desktopUiEx.Message}");
                            }
                        });

                        detected = true;
                    }
                }
            }
            else
            {
                Console.WriteLine("No icons detected.");
            }
        }

        // Method to check if the current window is a browser
        private bool IsBrowserWindow(string windowTitle)
        {
            bool isBrowser = windowTitle.Contains("Chrome") ||
                             windowTitle.Contains("Firefox") ||
                             windowTitle.Contains("Edge") ||
                             windowTitle.Contains("Internet Explorer");

            // Debug output to verify window title and browser detection
            Console.WriteLine($"Window Title: {windowTitle}, Is Browser: {isBrowser}");
            return isBrowser;
        }

        public void ListTaskbarItems()
        {
            // Initialize the list of clickable items on the taskbar
            var taskbarItems = new List<ClickableItem>();

            try
            {
                // Find the handle of the taskbar
                IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null);

                if (taskbarHandle != IntPtr.Zero)
                {
                    var taskbarElement = AutomationElement.FromHandle(taskbarHandle);

                    if (taskbarElement != null)
                    {
                        var clickableCondition = new OrCondition(
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink)
                        );

                        var clickableElements = taskbarElement.FindAll(TreeScope.Subtree, clickableCondition);

                        foreach (AutomationElement element in clickableElements)
                        {
                            if (!element.Current.IsOffscreen)
                            {
                                var boundingRect = element.Current.BoundingRectangle;

                                if (!boundingRect.IsEmpty)
                                {
                                    // Adjust the bounding rectangle coordinates for scaling
                                    Rect adjustedBoundingRect = new Rect(
                                        boundingRect.Left / ScalingFactor,
                                        boundingRect.Top / ScalingFactor,
                                        boundingRect.Width / ScalingFactor,
                                        boundingRect.Height / ScalingFactor
                                    );

                                    string controlName = element.Current.Name;
                                    Console.WriteLine($"Taskbar Item {globalCounter}: {controlName}");

                                    // Create and store the taskbar item
                                    taskbarItems.Add(new ClickableItem
                                    {
                                        Name = controlName,
                                        BoundingRectangle = boundingRect
                                    });

                                    // Create a label (tag) for the taskbar item
                                    System.Windows.Controls.Label tag = new System.Windows.Controls.Label
                                    {
                                        Content = globalCounter, // Prefix with 'T' for taskbar items
                                        Background = Brushes.Green,
                                        Foreground = Brushes.White,
                                        Padding = new Thickness(5),
                                        Opacity = 0.7
                                    };

                                    // Set the adjusted position based on the bounding rectangle
                                    Canvas.SetLeft(tag, adjustedBoundingRect.Left);
                                    Canvas.SetTop(tag, adjustedBoundingRect.Top - 20); // Position above the bounding box
                                    OverlayCanvas.Children.Add(tag);

                                    // Add the tag to the list
                                    _tags.Add(tag);

                                    globalCounter++;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error listing taskbar items: {ex.Message}");
            }
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            // Immediately deactivate the window to prevent it from getting focus
            this.Hide();
            this.Show();
        }

        public List<ClickableItem> GetClickableItems()
        {
            if (_clickableItems != null)
            {
                return _clickableItems;
            }
            return null;
        }

        private void StartTagRemovalTimer()
        {
            // Initialize the timer if it's not already initialized
            if (_tagRemovalTimer == null)
            {
                _tagRemovalTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(10) // Set timer for 10 seconds
                };
                _tagRemovalTimer.Tick += RemoveTags; // Attach the event handler
            }

            _tagRemovalTimer.Start(); // Start the timer
        }

        private void RemoveTags(object sender, EventArgs e)
        {
            // Stop the timer
            _tagRemovalTimer.Stop();

            // Remove tags from the overlay canvas
            foreach (var tag in _tags)
            {
                OverlayCanvas.Children.Remove(tag);
            }

            _tags.Clear(); // Clear the list of tags

            globalCounter = 1;
        }

        public void RemoveTagsNoTimer()
        {
            Dispatcher.Invoke(() =>
            {
                // Remove tags from the overlay canvas
                foreach (var tag in _tags)
                {
                    OverlayCanvas.Children.Remove(tag);
                }

                _tags.Clear(); // Clear the list of tags

                globalCounter = 1;
            });
        }

        private static bool IsInViewport(int x, int y, int width, int height, int viewportWidth, int viewportHeight)
        {
            return x + width > 0 && x < viewportWidth && y + height > 0 && y < viewportHeight;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    }
}