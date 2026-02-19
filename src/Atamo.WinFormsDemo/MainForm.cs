using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using Atamo.Hub;
using Atamo.SDK;

namespace Atamo.WinFormsDemo
{
    public class MainForm : Form
    {
        private readonly InMemoryHub _hub = new();
        private TextBox _eventTypeBox;
        private Button _submitBtn;
        private ListBox _progressList;

        public MainForm()
        {
            Text = "ATAMO WinForms Demo";
            Width = 400;
            Height = 300;

            _eventTypeBox = new TextBox { Left = 10, Top = 10, Width = 200, PlaceholderText = "Event Type" };
            _submitBtn = new Button { Left = 220, Top = 10, Width = 80, Text = "Submit" };
            _progressList = new ListBox { Left = 10, Top = 50, Width = 360, Height = 200 };

            Controls.Add(_eventTypeBox);
            Controls.Add(_submitBtn);
            Controls.Add(_progressList);

            _submitBtn.Click += async (s, e) => await SubmitEventAsync();

            // Register a subscriber to show progress
            _hub.RegisterSubscriberAsync(new EventKey("demo"), async progress =>
            {
                Invoke(new Action(() =>
                {
                    _progressList.Items.Add($"{progress.ActionKey}: {progress.Status} - {progress.Details}");
                }));
                await Task.CompletedTask;
            });
        }

        private async Task SubmitEventAsync()
        {
            var evtType = _eventTypeBox.Text.Trim();
            if (string.IsNullOrEmpty(evtType))
            {
                MessageBox.Show("Please enter an event type.");
                return;
            }
            var evt = new EventMessage
            {
                EventType = evtType,
                CorrelationId = string.Empty,
                Metadata = new MessageMetadata("demoTenant", "demoUser", DateTimeOffset.UtcNow),
                Payload = new Dictionary<string, object>()
            };
            await _hub.SubmitEventAsync(evt);
            _progressList.Items.Add($"Event '{evtType}' submitted.");
        }
    }
}
