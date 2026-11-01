using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace BatchAudioExtract
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        private Label lblFf;
        private Button btnFf;
        private ListBox list;
        private Label lblCount;
        private ComboBox combo;
        private CheckBox fastChk;
        private TextBox txtOut;
        private Button btnStart;
        private Label lblStatus;
        private ProgressBar prog;
        private RichTextBox log;

        private List<string> _files = new List<string>();
        private bool _running;
        private bool _cancel;
        private string _ffmpeg;

        private readonly string[] _exts = { "mp3", "wav", "flac", "m4a", "aac", "ogg" };
        private readonly string[] _extLabels = { "MP3  (.mp3)", "WAV  (.wav)", "FLAC (.flac)", "M4A  (.m4a)", "AAC  (.aac)", "OGG  (.ogg)" };
        private readonly string[] _videoExts = { ".mp4", ".mkv", ".avi", ".mov", ".flv", ".wmv", ".ts", ".m4v", ".webm", ".rmvb", ".mpg", ".mpeg", ".3gp" };

        public MainForm()
        {
            string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
            string[] candidates =
            {
                Path.Combine(exeDir, "ffmpeg", "ffmpeg.exe"),
                Path.Combine(exeDir, "ffmpeg.exe"),
                @"E:\软件安装\VideoCaptioner\resource\bin\ffmpeg.exe"
            };
            _ffmpeg = candidates.FirstOrDefault(File.Exists) ?? "";

            InitUI();
        }

        private void InitUI()
        {
            Text = "批量提取音频  v1.1";
            ClientSize = new Size(760, 600);
            MinimumSize = new Size(680, 540);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 9);

            int y = 12, pad = 12;

            lblFf = new Label { AutoSize = true, Location = new Point(pad, y + 5) };
            RefreshFfLabel();
            btnFf = new Button { Text = "手动指定", AutoSize = true, Location = new Point(700, y), Height = 26 };
            btnFf.Click += (s, e) => PickFfmpeg();
            Controls.Add(lblFf);
            Controls.Add(btnFf);
            y += 34;

            var btnAdd = new Button { Text = "添加视频文件…", AutoSize = true, Location = new Point(pad, y), Height = 28 };
            var btnFolder = new Button { Text = "添加整个文件夹…", AutoSize = true, Location = new Point(140, y), Height = 28 };
            var btnClear = new Button { Text = "清空", AutoSize = true, Location = new Point(280, y), Height = 28 };
            lblCount = new Label { Text = "文件数: 0", AutoSize = true, Location = new Point(360, y + 6), ForeColor = Color.Gray };
            btnAdd.Click += (s, e) => AddFiles();
            btnFolder.Click += (s, e) => AddFolder();
            btnClear.Click += (s, e) => { _files.Clear(); list.Items.Clear(); SetCount(); };
            Controls.Add(btnAdd); Controls.Add(btnFolder); Controls.Add(btnClear); Controls.Add(lblCount);
            y += 36;

            list = new ListBox { Location = new Point(pad, y), Size = new Size(736, 200), ScrollAlwaysVisible = true };
            Controls.Add(list);
            y += 208;

            var lFmt = new Label { Text = "格式:", AutoSize = true, Location = new Point(pad, y + 5) };
            combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(60, y), Width = 140 };
            combo.Items.AddRange(_extLabels);
            combo.SelectedIndex = 0;
            fastChk = new CheckBox { Text = "⚡ 极速模式(直接复制音轨,秒出)", AutoSize = true, Location = new Point(210, y + 5) };
            Controls.Add(lFmt); Controls.Add(combo); Controls.Add(fastChk);
            y += 34;

            var lOut = new Label { Text = "输出到(留空=视频同目录):", AutoSize = true, Location = new Point(pad, y + 5) };
            txtOut = new TextBox { Location = new Point(100, y), Width = 600 };
            var btnOut = new Button { Text = "…", Location = new Point(708, y), Size = new Size(28, 26) };
            btnOut.Click += (s, e) => { var d = new FolderBrowserDialog(); if (d.ShowDialog() == DialogResult.OK) txtOut.Text = d.SelectedPath; };
            Controls.Add(lOut); Controls.Add(txtOut); Controls.Add(btnOut);
            y += 38;

            btnStart = new Button { Text = "开始提取", AutoSize = true, Location = new Point(pad, y), Height = 32, BackColor = Color.FromArgb(225, 245, 225) };
            lblStatus = new Label { Text = "就绪", AutoSize = true, Location = new Point(120, y + 8), ForeColor = Color.FromArgb(60, 60, 60) };
            btnStart.Click += (s, e) => StartStop();
            Controls.Add(btnStart); Controls.Add(lblStatus);
            y += 40;

            prog = new ProgressBar { Location = new Point(pad, y), Size = new Size(736, 20), Minimum = 0, Maximum = 100, Value = 0 };
            Controls.Add(prog);
            y += 30;

            log = new RichTextBox { Location = new Point(pad, y), Size = new Size(736, 150), ReadOnly = true, BackColor = Color.FromArgb(250, 250, 250) };
            Controls.Add(log);

            FormClosing += (s, e) => { _cancel = true; };

            AllowDrop = true;
            DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            DragDrop += (s, e) =>
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var f in files)
                {
                    if (Directory.Exists(f))
                    {
                        foreach (var sf in Directory.EnumerateFiles(f, "*.*", SearchOption.AllDirectories)
                            .Where(p => _videoExts.Contains(Path.GetExtension(p).ToLowerInvariant())))
                            if (!_files.Contains(sf)) { _files.Add(sf); list.Items.Add(sf); }
                    }
                    else if (_videoExts.Contains(Path.GetExtension(f).ToLowerInvariant()) && !_files.Contains(f))
                    {
                        _files.Add(f); list.Items.Add(f);
                    }
                }
                SetCount();
            };
        }

        private void RefreshFfLabel()
        {
            if (File.Exists(_ffmpeg))
            {
                lblFf.Text = "✓ ffmpeg: " + _ffmpeg;
                lblFf.ForeColor = Color.ForestGreen;
            }
            else
            {
                lblFf.Text = "✗ 未找到 ffmpeg，请点击右侧手动指定";
                lblFf.ForeColor = Color.Crimson;
            }
        }

        private void PickFfmpeg()
        {
            var ofd = new OpenFileDialog { Title = "选择 ffmpeg.exe", Filter = "ffmpeg|ffmpeg.exe|所有文件|*.*" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                _ffmpeg = ofd.FileName;
                RefreshFfLabel();
            }
        }

        private void AddFiles()
        {
            var ofd = new OpenFileDialog
            {
                Multiselect = true,
                Title = "选择视频文件",
                Filter = "视频文件|*.mp4;*.mkv;*.avi;*.mov;*.flv;*.wmv;*.ts;*.m4v;*.webm;*.rmvb;*.mpg;*.mpeg;*.3gp|所有文件|*.*"
            };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                foreach (var f in ofd.FileNames) { if (!_files.Contains(f)) { _files.Add(f); list.Items.Add(f); } }
                SetCount();
            }
        }

        private void AddFolder()
        {
            var ffd = new FolderBrowserDialog();
            if (ffd.ShowDialog() == DialogResult.OK)
            {
                foreach (var f in Directory.EnumerateFiles(ffd.SelectedPath, "*.*", SearchOption.AllDirectories)
                    .Where(p => _videoExts.Contains(Path.GetExtension(p).ToLowerInvariant())))
                {
                    if (!_files.Contains(f)) { _files.Add(f); list.Items.Add(f); }
                }
                SetCount();
            }
        }

        private void SetCount() { lblCount.Text = "文件数: " + _files.Count; }

        private void AddLog(string msg)
        {
            if (log.IsHandleCreated)
            {
                log.BeginInvoke((Action)(() =>
                {
                    log.AppendText(msg + "\r\n");
                    log.SelectionStart = log.TextLength;
                    log.ScrollToCaret();
                }));
            }
        }

        private string GetExt() { return _exts[combo.SelectedIndex]; }

        private void StartStop()
        {
            if (_running) { _cancel = true; btnStart.Text = "正在停止…"; return; }

            if (!File.Exists(_ffmpeg))
            {
                MessageBox.Show("未找到 ffmpeg，请先手动指定 ffmpeg.exe", "缺少 ffmpeg");
                return;
            }
            if (_files.Count == 0)
            {
                MessageBox.Show("请先添加要提取的视频文件", "提示");
                return;
            }

            _running = true;
            _cancel = false;
            btnStart.Text = "停止";
            btnStart.BackColor = Color.FromArgb(255, 220, 220);
            prog.Value = 0;
            log.Clear();
            string outDir = txtOut.Text.Trim();
            bool fast = fastChk.Checked;
            string ext = GetExt();

            var th = new Thread(() => Worker(_files.ToList(), outDir, ext, fast));
            th.IsBackground = true;
            th.Start();
        }

        private void Worker(List<string> files, string outDir, string ext, bool fast)
        {
            string[] enc;
            if (fast)
            {
                enc = new[] { "-c:a", "copy" };
                ext = "m4a";
            }
            else
            {
                switch (ext)
                {
                    case "mp3": enc = new[] { "-c:a", "libmp3lame", "-q:a", "2" }; break;
                    case "wav": enc = new[] { "-c:a", "pcm_s16le" }; break;
                    case "flac": enc = new[] { "-c:a", "flac" }; break;
                    case "m4a": enc = new[] { "-c:a", "aac", "-b:a", "192k" }; break;
                    case "aac": enc = new[] { "-c:a", "aac", "-b:a", "192k" }; break;
                    case "ogg": enc = new[] { "-c:a", "libvorbis", "-q:a", "4" }; break;
                    default: enc = new[] { "-c:a", "libmp3lame", "-q:a", "2" }; break;
                }
            }

            AddLog(string.Format("开始提取 {0} 个文件 [{1}]", files.Count, fast ? "极速-直接复制音轨" : ext.ToUpper()));
            int total = files.Count, done = 0;

            foreach (var f in files)
            {
                if (_cancel) break;
                string baseName = Path.GetFileNameWithoutExtension(f);
                string outPath;
                if (string.IsNullOrWhiteSpace(outDir))
                    outPath = Path.Combine(Path.GetDirectoryName(f), baseName + "." + ext);
                else
                    outPath = Path.Combine(outDir, baseName + "." + ext);
                SetStatus("正在处理: " + baseName);

                var psi = new ProcessStartInfo
                {
                    FileName = _ffmpeg,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };
                var args = new List<string>();
                args.Add("-y");
                args.Add("-i");
                args.Add(f);
                args.Add("-vn");
                args.AddRange(enc);
                args.Add(outPath);
                psi.Arguments = string.Join(" ", args.Select(a => "\"" + a.Replace("\"", "\\\"") + "\""));

                try
                {
                    using (var p = Process.Start(psi))
                    {
                        string err = p.StandardError.ReadToEnd();
                        if (!p.WaitForExit(3600000)) { p.Kill(); }
                        if (p.ExitCode == 0 && File.Exists(outPath))
                            AddLog("[完成] " + baseName + "." + ext);
                        else
                        {
                            string line = "未知错误";
                            foreach (var ln in err.Split('\n'))
                            {
                                string t = ln.Trim();
                                if (t.Contains("rror") || t.Contains("nvalid") || t.Contains("ailed")) { line = t; break; }
                            }
                            AddLog("[失败] " + baseName + " : " + line);
                        }
                    }
                }
                catch (Exception ex) { AddLog("[失败] " + baseName + " : " + ex.Message); }

                done++;
                prog.Invoke((Action)(() => prog.Value = Math.Min((int)(done * 100.0 / total), 100)));
                Application.DoEvents();
            }

            _running = false;
            btnStart.Invoke((Action)(() =>
            {
                btnStart.Text = "开始提取";
                btnStart.BackColor = Color.FromArgb(225, 245, 225);
                lblStatus.Text = "完成: " + done + " / " + total;
            }));
            AddLog("全部处理完毕。");
        }

        private void SetStatus(string s)
        {
            if (lblStatus.IsHandleCreated)
                lblStatus.BeginInvoke((Action)(() => lblStatus.Text = s));
        }
    }
}
