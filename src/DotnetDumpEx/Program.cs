using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NStack;
using Terminal.Gui;

namespace DotnetDumpEx
{
    class Program
    {
        static void Main(string[] args)
        {
            Application.Init();

            var app = Toplevel.Create();

            //var bottomPanel = new FrameView(title: null)
            //{
            //    X = 0,
            //    Y = Pos.Bottom(app) - 4,
            //    Height = 4,
            //    Width = Dim.Fill()
            //};

            var rightPanel = new FrameView(title: null)
            {
                X = Pos.Right(app) - 25,
                Y = 1,
                Height = Dim.Fill() - 1,
                Width = 25
            };

            app.Add(rightPanel);

            var leftPanel = new FrameView(title: null)
            {
                X = 0,
                Y = 1,
                Height = Dim.Fill() - 1,
                Width = Dim.Fill() - 25
            };

            //app.Add(topPanel, bottomPanel);
            app.Add(leftPanel);

            var input = new InputField
            {
                Height = 1,
                Width = Dim.Fill(),
                X = 0,
                Y = Pos.Bottom(app) - 1,
            };

            var textView = new OutputTextView
            {
                Height = Dim.Fill(),
                Width = Dim.Fill(),
                X = 0,
                Y = 0
            };
            textView.Id = "TextView";

            leftPanel.Add(textView);

            //leftPanel.Add(textView);

            app.Add(input);


            var process = Process.Start(new ProcessStartInfo
            {
                FileName = @"C:\Users\kevin\.dotnet\tools\dotnet-dump.exe",
                Arguments = @"analyze E:\CoreConsoleApp1.exe_200514_121537.dmp",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            });

            Task.Run(() =>
            {
                while (true)
                {
                    var line = process.StandardOutput.ReadLine();

                    Debug.WriteLine("Appending " + line);

                    if (line != null)
                    {
                        Application.MainLoop.Invoke(() =>
                        {
                            textView.Append(line);
                            // textView.ScrollTo(int.MaxValue);
                        });
                    }
                }
            });

            input.NewCommand += c =>
            {
                process.StandardInput.WriteLine(c);
            };

            Application.Run(app);

            //process.OutputDataReceived += Process_OutputDataReceived;

        }

        private static void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine("> " + e.Data);
        }
    }

    public class InputField : TextField
    {
        public event Action<string> NewCommand;

        public InputField() : base(string.Empty)
        {
        }

        public InputField(string text) : base(text)
        {
        }

        public InputField(ustring text) : base(text)
        {
        }

        public InputField(int x, int y, int w, ustring text) : base(x, y, w, text)
        {
        }

        public override bool ProcessKey(KeyEvent kb)
        {
            if (kb.Key == Key.Enter)
            {
                var text = Text.ToString();
                Text = string.Empty;
                NewCommand?.Invoke(text);
                return true;
            }

            return base.ProcessKey(kb);
        }
    }
}
