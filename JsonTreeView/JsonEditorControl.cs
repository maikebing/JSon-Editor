﻿using System.Drawing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.ComponentModel;

namespace ZTn.Json.Editor.Forms
{
    public sealed partial class JsonEditorControl : UserControl
    {
        private const string DefaultFileFilters = @"json files (*.json)|*.json";

        #region >> Delegates

        private delegate void SetActionStatusDelegate(string text, bool isError);

        private delegate void SetJsonStatusDelegate(string text, bool isError);

        #endregion

        #region >> Fields

        private string internalOpenedFileName;

        private System.Timers.Timer jsonValidationTimer;

        #endregion

        #region >> Properties
        [DefaultValue(true)]
        public bool MenuBarVisible
        {
            get { return formMenuStrip.Visible; }
            set { formMenuStrip.Visible = value; }
        }
        [DefaultValue(true)]
        public bool StatusBarVisible
        {
            get { return guiStatusStrip.Visible; }
            set { guiStatusStrip.Visible = value; }
        }
        string _Json = null;
        [DefaultValue(null)]
        public string Json
        {
            get
            {
                if (jTokenTree != null && jTokenTree.IsHandleCreated && !jTokenTree.IsDisposed && !this.DesignMode)
                {
                    jTokenTree.GetJson(out _Json);
                }
                return _Json;
            }
            set
            {
                _Json = value;
                if (jTokenTree != null && jTokenTree.IsHandleCreated && !jTokenTree.IsDisposed && !this.DesignMode)
                {
                    var objx = JsonConvert.DeserializeObject(_Json);
                    if (objx != null)
                    {
                        jTokenTree.SetJson(_Json);
                    }
                }
            }
        }
        /// <summary>
        /// Accessor to file name of opened file.
        /// </summary>
        string OpenedFileName
        {
            get { return internalOpenedFileName; }
            set
            {
                internalOpenedFileName = value;
                saveToolStripMenuItem.Enabled = internalOpenedFileName != null;
                saveAsToolStripMenuItem.Enabled = internalOpenedFileName != null;
                Text = (internalOpenedFileName ?? "") + @" - Json Editor by ZTn";
            }
        }

        #endregion

        #region >> Constructor

        public JsonEditorControl()
        {
            //this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            //this.MainMenuStrip = this.formMenuStrip;

            InitializeComponent();

            jsonTypeComboBox.DataSource = Enum.GetValues(typeof(JTokenType));

            OpenedFileName = null;
            SetActionStatus(@"Empty document.", true);
            SetJsonStatus(@"", false);

            var commandLineArgs = Environment.GetCommandLineArgs();
            if (commandLineArgs.Skip(1).Any())
            {
                OpenedFileName = commandLineArgs[1];
                try
                {
                    using (var stream = new FileStream(commandLineArgs[1], FileMode.Open))
                    {
                        SetJsonSourceStream(stream, commandLineArgs[1]);
                    }
                }
                catch
                {
                    OpenedFileName = null;
                }
            }
        }

        #endregion

        #region >> Form

        /// <inheritdoc />
        /// <remarks>
        /// Optimization aiming to reduce flickering on large documents (successfully).
        /// Source: http://stackoverflow.com/a/89125/1774251
        /// </remarks>
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;    // Turn on WS_EX_COMPOSITED
                return cp;
            }
        }

        #endregion

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = @"json files (*.json)|*.json|All files (*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                using (var stream = openFileDialog.OpenFile())
                {
                    SetJsonSourceStream(stream, openFileDialog.FileName);
                }
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (OpenedFileName == null)
            {
                return;
            }

            try
            {
                using (var stream = new FileStream(OpenedFileName, FileMode.Open))
                {
                    jTokenTree.GetJson(stream);
                }
            }
            catch
            {
                MessageBox.Show(this, $"An error occured when saving file as \"{OpenedFileName}\".", @"Save As...");

                OpenedFileName = null;
                SetActionStatus(@"Document NOT saved.", true);

                return;
            }

            SetActionStatus(@"Document successfully saved.", false);
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = DefaultFileFilters,
                FilterIndex = 1,
                RestoreDirectory = true
            };

            if (saveFileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            try
            {
                OpenedFileName = saveFileDialog.FileName;
                using (var stream = saveFileDialog.OpenFile())
                {
                    if (stream.CanWrite)
                    {
                        jTokenTree.GetJson(stream);
                    }
                }
            }
            catch
            {
                MessageBox.Show(this, $"An error occured when saving file as \"{OpenedFileName}\".", @"Save As...");

                OpenedFileName = null;
                SetActionStatus(@"Document NOT saved.", true);

                return;
            }

            SetActionStatus(@"Document successfully saved.", false);
        }

        private void newJsonObjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            jTokenTree.SetJson("{}");

            saveAsToolStripMenuItem.Enabled = true;
        }

        private void newJsonArrayToolStripMenuItem_Click(object sender, EventArgs e)
        {
            jTokenTree.SetJson("[]");

            saveAsToolStripMenuItem.Enabled = true;
        }

        private void aboutJsonEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //new AboutBox().ShowDialog();
        }

        private void jsonValueTextBox_TextChanged(object sender, EventArgs e)
        {
            StartValidationTimer();
        }

        private void jsonValueTextBox_Leave(object sender, EventArgs e)
        {
            jsonValueTextBox.TextChanged -= jsonValueTextBox_TextChanged;
        }

        private void jsonValueTextBox_Enter(object sender, EventArgs e)
        {
            jsonValueTextBox.TextChanged += jsonValueTextBox_TextChanged;
        }

        private void jTokenTree_AfterSelect(object sender, JsonTreeView.AfterSelectEventArgs eventArgs)
        {
            newtonsoftJsonTypeTextBox.Text = eventArgs.TypeName;

            jsonTypeComboBox.Text = eventArgs.JTokenTypeName;

            // If jsonValueTextBox is focused then it triggers this event in the update process, so don't update it again ! (risk: infinite loop between events).
            if (!jsonValueTextBox.Focused)
            {
                jsonValueTextBox.Text = eventArgs.GetJsonString();
            }
        }

        private void SetJsonSourceStream(Stream stream, string fileName)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            OpenedFileName = fileName;

            try
            {
                jTokenTree.SetJson(stream);
            }
            catch
            {
                MessageBox.Show(this, $"An error occured when reading \"{OpenedFileName}\"", @"Open...");

                OpenedFileName = null;
                SetActionStatus(@"Document NOT loaded.", true);

                return;
            }

            SetActionStatus(@"Document successfully loaded.", false);
            saveAsToolStripMenuItem.Enabled = true;
        }

        private void SetActionStatus(string text, bool isError)
        {
            if (InvokeRequired)
            {
                Invoke(new SetActionStatusDelegate(SetActionStatus), text, isError);
                return;
            }

            actionStatusLabel.Text = text;
            actionStatusLabel.ForeColor = isError ? Color.OrangeRed : Color.Black;
        }

        private void SetJsonStatus(string text, bool isError)
        {
            if (InvokeRequired)
            {
                Invoke(new SetJsonStatusDelegate(SetActionStatus), text, isError);
                return;
            }

            jsonStatusLabel.Text = text;
            jsonStatusLabel.ForeColor = isError ? Color.OrangeRed : Color.Black;
        }

        private void StartValidationTimer()
        {
            jsonValidationTimer?.Stop();

            jsonValidationTimer = new System.Timers.Timer(250);

            jsonValidationTimer.Elapsed += (o, args) =>
            {
                jsonValidationTimer.Stop();

                jTokenTree.Invoke(new Action(JsonValidationTimerHandler));
            };

            jsonValidationTimer.Start();
        }

        private void JsonValidationTimerHandler()
        {
            try
            {
                jTokenTree.UpdateSelected(jsonValueTextBox.Text);

                SetJsonStatus("Json format validated.", false);
            }
            catch (JsonReaderException exception)
            {
                SetJsonStatus(
                    $"INVALID Json format at (line {exception.LineNumber}, position {exception.LinePosition})",
                    true);
            }
            catch
            {
                SetJsonStatus("INVALID Json format", true);
            }
        }
    }
}
