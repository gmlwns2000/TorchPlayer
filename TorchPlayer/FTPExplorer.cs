using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TorchPlayer
{
    public class FTPMediaSource
    {
        static Dictionary<string, string> Exts = new Dictionary<string, string>() {
            { ".mp4",  "video/mp4" },
            { ".mpeg", "video/mpeg" },
            { ".mkv",  "video/x-matroska" },
            { ".mov",  "video/quicktime" },
            { ".avi",  "video/x-msvideo" },
            { ".wmv",  "video/x-ms-wmv" },
            { ".mp3",  "audio/mpeg" },
            { ".flac", "audio/flac" },
            { ".wav",  "audio/wav" }
        };
        public static string GetMIMEFromFileName(string filename)
        {
            foreach (var item in Exts.Keys)
            {
                if (filename.ToLower().EndsWith(item))
                    return Exts[item];
            }
            return null;
        }

        public IRandomAccessStream RandomAccessStream { get; protected set; }
        public FTPServerAddress Server { get; set; }
        public string FilePath { get; set; }
        public FTPStream Stream { get; set; }

        public FTPMediaSource(FTPServerAddress server, string filepath)
        {
            Server = server;
            FilePath = filepath;
            Stream = new FTPStream(new Uri(Server.Address + "/" + FilePath), server.Credential);
            RandomAccessStream = new FTPRandomAccessStream(Stream);
        }
    }

    public class FTPEntry : ExplorerEntry { }

    public class FTPExplorer : Explorer
    {
        public static async Task<bool> CheckServer(FTPServerAddress address)
        {
            try
            {
                var request = (FtpWebRequest)WebRequest.Create(address.GetAddress("/"));
                request.Credentials = address.Credential;
                request.Method = WebRequestMethods.Ftp.ListDirectory;
                using (var response = request.GetResponse())
                {
                    using (var stream = response.GetResponseStream())
                    {
                        using (var reader = new StreamReader(stream))
                            reader.ReadToEnd();
                    }
                }
            }
            catch(WebException ex)
            {
                var margin = new Thickness(0, 10, 0, 0);
                var tb_address = new TextBox() 
                { 
                    Text = address.Server 
                };
                var tb_port = new TextBox() 
                { 
                    Text = address.Port.ToString(), 
                    Margin = margin 
                };
                var tb_user = new TextBox() 
                {
                    Text = address.User, 
                    Margin = margin,
                };
                var tb_password = new PasswordBox()
                {
                    PasswordRevealMode = PasswordRevealMode.Peek,
                    Password = address.Password,
                    Margin = margin,
                };

                var stackpanel = new StackPanel() { Orientation = Orientation.Vertical };
                stackpanel.Children.Add(tb_address);
                stackpanel.Children.Add(tb_port);
                stackpanel.Children.Add(tb_user);
                stackpanel.Children.Add(tb_password);

                var dialog = new ContentDialog()
                {
                    IsSecondaryButtonEnabled = true,
                    Title = ex.Message,
                    PrimaryButtonText = "Ok",
                    SecondaryButtonText = "Cancel",
                    Content = stackpanel,
                };
                
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Secondary)
                    return false;
                
                address.Server = tb_address.Text;
                address.Port = Convert.ToInt32(tb_port.Text);
                address.User = tb_user.Text;
                address.Password = tb_password.Password;

                return await CheckServer(address);
            }

            return true;
        }

        public List<FTPEntry> Files { get; set; } = new List<FTPEntry>();
        public List<FTPEntry> Dirs { get; set; } = new List<FTPEntry>();
        
        public override event EventHandler<PlaybackOpenedEventArgs> PlaybackOpened;

        FTPServerAddress server;

        public FTPExplorer(FTPServerAddress ftp)
        {
            server = ftp;
            GoToAbs(Path);
        }

        public override void Refresh()
        {
            var request = (FtpWebRequest)WebRequest.Create(server.GetAddress(Path));
            request.Credentials = server.Credential;
            request.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
            var lines = new List<string>();
            using (var response = request.GetResponse())
            {
                using (var stream = response.GetResponseStream())
                {
                    using (var reader = new StreamReader(stream))
                    {
                        while (!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();
                            if (line.Length > 3)
                                lines.Add(line);
                        }
                    }
                }
            }

            Dirs.Clear();
            Files.Clear();

            foreach (var line in lines)
            {
                //"-rw-r--r-- 1 ftp ftp        6227687 Jul 29  2019 학생증사진.psd
                var isDirectory = line[0] == 'd';
                var temp = line.Remove(0, 20);

                var fileSizeStr = temp.Substring(0, 15).TrimStart();
                var fileSize = Convert.ToInt64(fileSizeStr);
                temp = temp.Remove(0, 15);

                var date = temp.Substring(0, 13).Trim();
                temp = temp.Remove(0, 14);

                var name = temp;
                var fullname = $"{Path}/{name}";
                if (isDirectory)
                {
                    Dirs.Add(new FTPEntry()
                    {
                        IsDirectory = true,
                        IsMedia = false,
                        Command = new UICommand(() => { GoToSubDir(name); }),
                        DisplayName = name,
                        FullName = fullname,
                    });
                }
                else
                {
                    Files.Add(new FTPEntry()
                    {
                        IsDirectory = false,
                        IsMedia = FTPMediaSource.GetMIMEFromFileName(name) != null,
                        Command = new UICommand(() =>
                        {
                            var mime = FTPMediaSource.GetMIMEFromFileName(name);
                            if (mime != null)
                            {
                                PlaybackOpened?.Invoke(this, new PlaybackOpenedEventArgs()
                                {
                                    Address = server.GetAddressWithCredential(fullname),
                                    FullName = fullname,
                                    Name = name,
                                });
                            }
                        }),
                        DisplayName = name,
                        FullName = fullname,
                    });
                }
            }
            var entrylist = new List<ExplorerEntry>();
            foreach (var item in Dirs)
                entrylist.Add(item);
            foreach (var item in Files)
                entrylist.Add(item);
            ActualEntryList = entrylist;
            UpdateEntryList(entrylist);
        }
    }
}
