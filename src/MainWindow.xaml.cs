using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using System.Windows.Media;
using Microsoft.Win32;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace AudioDecrypt
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        // 解密文件列表
        private ObservableCollection<DecryptFileInfo> _decryptFiles = new ObservableCollection<DecryptFileInfo>();
        // 支持的文件扩展名
        private readonly string[] _supportedExtensions = { ".ncm", ".kgm", ".kgma", ".vpr" };
        // 解密任务是否正在运行
        private bool _isDecrypting = false;
        // 动画计时器
        private DispatcherTimer _animationTimer;
        // 上次解密时间
        private DateTime _lastDecryptTime;

        public MainWindow()
        {
            InitializeComponent();
            
            // 设置数据源
            dgDecryptList.ItemsSource = _decryptFiles;
            
            // 初始化输出目录为"我的音乐"文件夹
            string myMusicPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            txtOutputDir.Text = myMusicPath;
            
            // 初始化动画计时器
            _animationTimer = new DispatcherTimer();
            _animationTimer.Interval = TimeSpan.FromMilliseconds(100);
            _animationTimer.Tick += AnimationTimer_Tick;
            
            // 初始化状态文本
            txtStatus.Text = "准备就绪";
            
            UpdateUI();
        }

        /// <summary>
        /// 动画计时器Tick事件
        /// </summary>
        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            // 在此添加动画更新逻辑，例如进度条脉冲动画等
            int dots = (int)((DateTime.Now - _lastDecryptTime).TotalSeconds % 4);
            string statusText = "解密中";
            for (int i = 0; i < dots; i++) statusText += ".";
            txtStatus.Text = statusText;
        }

        /// <summary>
        /// 选择文件按钮点击事件
        /// </summary>
        private void btnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            if (_isDecrypting)
            {
                ShowMessage("正在解密中，请等待完成后再操作", "提示", MessageBoxImage.Information);
                return;
            }

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "支持的音乐文件|*.ncm;*.kgm;*.kgma;*.vpr|所有文件|*.*",
                Multiselect = true,
                Title = "选择要解密的音乐文件"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                bool filesAdded = false;
                
                foreach (string filePath in openFileDialog.FileNames)
                {
                    if (AddFileToList(filePath))
                        filesAdded = true;
                }
                
                // 显示第一个选择的文件路径
                if (openFileDialog.FileNames.Length > 0)
                {
                    txtFilePath.Text = openFileDialog.FileNames[0];
                    if (openFileDialog.FileNames.Length > 1)
                    {
                        txtFilePath.Text += $" (共选择了 {openFileDialog.FileNames.Length} 个文件)";
                    }
                }
                
                UpdateUI();
                
                // 添加文件成功的提示
                if (filesAdded)
                {
                    txtStatus.Text = $"已添加 {openFileDialog.FileNames.Length} 个文件，准备就绪";
                    AnimateProgressBar();
                }
            }
        }

        /// <summary>
        /// 选择文件夹按钮点击事件
        /// </summary>
        private void btnSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_isDecrypting)
            {
                ShowMessage("正在解密中，请等待完成后再操作", "提示", MessageBoxImage.Information);
                return;
            }

            FolderBrowserDialog folderBrowser = new FolderBrowserDialog
            {
                Description = "选择包含加密音乐文件的文件夹",
                ShowNewFolderButton = false
            };

            if (folderBrowser.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string folderPath = folderBrowser.SelectedPath;
                txtFilePath.Text = folderPath;  // 更新文本框显示选择的文件夹路径
                
                string[] files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => _supportedExtensions.Contains(Path.GetExtension(f).ToLower()))
                    .ToArray();

                int validFilesAdded = 0;
                foreach (string filePath in files)
                {
                    if (AddFileToList(filePath))
                        validFilesAdded++;
                }
                
                if (files.Length > 0)
                {
                    txtFilePath.Text += $" (找到 {files.Length} 个文件)";
                    if (validFilesAdded > 0)
                    {
                        txtStatus.Text = $"已添加 {validFilesAdded} 个文件，准备就绪";
                        AnimateProgressBar();
                    }
                }
                else
                {
                    txtFilePath.Text += " (未找到支持的音乐文件)";
                    txtStatus.Text = "未找到支持的音乐文件";
                }
                
                UpdateUI();
            }
        }

        /// <summary>
        /// 选择输出目录按钮点击事件
        /// </summary>
        private void btnSelectOutputDir_Click(object sender, RoutedEventArgs e)
        {
            if (_isDecrypting)
            {
                ShowMessage("正在解密中，请等待完成后再操作", "提示", MessageBoxImage.Information);
                return;
            }

            FolderBrowserDialog folderBrowser = new FolderBrowserDialog
            {
                Description = "选择解密音乐文件的输出文件夹",
                ShowNewFolderButton = true
            };

            if (!string.IsNullOrEmpty(txtOutputDir.Text))
            {
                folderBrowser.SelectedPath = txtOutputDir.Text;
            }

            if (folderBrowser.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtOutputDir.Text = folderBrowser.SelectedPath;
                
                // 如果选择了文件但开始解密按钮还未启用，启用它
                if (_decryptFiles.Count > 0)
                {
                    btnStartDecrypt.IsEnabled = true;
                }
                
                txtStatus.Text = $"输出目录已设置: {Path.GetFileName(folderBrowser.SelectedPath)}";
            }
        }

        /// <summary>
        /// 开始解密按钮点击事件
        /// </summary>
        private async void btnStartDecrypt_Click(object sender, RoutedEventArgs e)
        {
            if (_isDecrypting || _decryptFiles.Count == 0)
            {
                return;
            }

            try
            {
                _isDecrypting = true;
                _lastDecryptTime = DateTime.Now;
                UpdateUI();

                // 获取输出目录
                string outputDir = txtOutputDir.Text;
                if (string.IsNullOrEmpty(outputDir))
                {
                    outputDir = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
                    txtOutputDir.Text = outputDir;
                }

                // 确保输出目录存在
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // 更新进度条
                progressDecryption.Minimum = 0;
                progressDecryption.Maximum = _decryptFiles.Count;
                progressDecryption.Value = 0;
                txtStatus.Text = "正在解密...";
                
                // 启动动画计时器
                _animationTimer.Start();

                // 开始解密任务
                await Task.Run(() => DecryptFiles(outputDir));
                
                // 停止动画计时器
                _animationTimer.Stop();

                txtStatus.Text = $"解密完成，共处理 {_decryptFiles.Count} 个文件，输出到 {Path.GetFileName(outputDir)}";
                
                // 显示成功解密的动画效果
                AnimateDecryptSuccess();
                
                // 显示完成消息
                ShowMessage($"成功解密 {_decryptFiles.Count} 个文件！\n输出目录: {outputDir}", "操作成功", MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _animationTimer.Stop();
                ShowMessage($"解密过程中发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "解密出错";
            }
            finally
            {
                _isDecrypting = false;
                UpdateUI();
            }
        }

        /// <summary>
        /// 清空列表按钮点击事件
        /// </summary>
        private void btnClearList_Click(object sender, RoutedEventArgs e)
        {
            if (_isDecrypting)
            {
                ShowMessage("正在解密中，请等待完成后再操作", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_decryptFiles.Count > 0)
            {
                _decryptFiles.Clear();
                txtFilePath.Text = string.Empty;
                txtStatus.Text = "列表已清空，请添加文件";
                UpdateUI();
                
                // 清空后显示动画
                AnimateProgressBar(true);
            }
        }

        /// <summary>
        /// 解密文件
        /// </summary>
        private void DecryptFiles(string outputDir)
        {
            int successCount = 0;
            int errorCount = 0;
            
            for (int i = 0; i < _decryptFiles.Count; i++)
            {
                DecryptFileInfo fileInfo = _decryptFiles[i];
                
                // 更新状态，所有文件重新标记为处理中
                UpdateFileStatus(i, "处理中...");

                try
                {
                    // 创建解密配置
                    MusicDecryptConfig config = new MusicDecryptConfig
                    {
                        FilePath = fileInfo.FilePath,
                        OutputDir = outputDir,
                        EnableCloudKey = false
                    };

                    // 创建解密器
                    MusicDecryptorBase decryptor = MusicDecryptorFactory.Create(fileInfo.FilePath);
                    
                    // 执行解密
                    decryptor.Decrypt(config);
                    
                    // 更新状态为成功
                    UpdateFileStatus(i, "成功");
                    
                    // 查找输出文件并更新
                    string baseFileName = Path.GetFileNameWithoutExtension(fileInfo.FilePath);
                    string[] possibleExtensions = { ".mp3", ".flac" };
                    
                    foreach (string ext in possibleExtensions)
                    {
                        string potentialFile = Path.Combine(outputDir, baseFileName + ext);
                        if (File.Exists(potentialFile))
                        {
                            // 更新输出文件路径
                            UpdateOutputFile(i, potentialFile);
                            break;
                        }
                    }
                    
                    successCount++;
                }
                catch (Exception ex)
                {
                    // 更新状态为错误
                    UpdateFileStatus(i, "错误");
                    
                    // 输出错误消息到控制台
                    Console.WriteLine($"解密文件 {fileInfo.FileName} 失败: {ex.Message}");
                    errorCount++;
                }
                
                // 更新进度条
                UpdateProgress(i + 1);
            }
            
            // 更新最终状态显示
            Dispatcher.Invoke(() => {
                txtStatus.Text = $"解密完成: {successCount} 成功, {errorCount} 失败";
            });
        }
        
        /// <summary>
        /// 更新输出文件路径
        /// </summary>
        private void UpdateOutputFile(int index, string outputFile)
        {
            Dispatcher.Invoke(() =>
            {
                if (index >= 0 && index < _decryptFiles.Count)
                {
                    _decryptFiles[index].OutputFile = Path.GetFileName(outputFile);
                    dgDecryptList.Items.Refresh();
                }
            });
        }

        /// <summary>
        /// 添加文件到列表
        /// </summary>
        private bool AddFileToList(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;
                
            string ext = Path.GetExtension(filePath).ToLower();
            if (!_supportedExtensions.Contains(ext))
                return false;
                
            // 检查文件是否已在列表中
            if (_decryptFiles.Any(f => f.FilePath == filePath))
                return false;
                
            // 获取文件类型
            string fileType = ext.TrimStart('.').ToUpper();
            
            // 创建解密文件信息对象
            DecryptFileInfo fileInfo = new DecryptFileInfo
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                Status = "等待中",
                FileType = fileType,
                OutputFile = "待解密"
            };
            
            // 添加到列表
            _decryptFiles.Add(fileInfo);
            
            return true;
        }

        /// <summary>
        /// 更新进度条
        /// </summary>
        private void UpdateProgress(int value)
        {
            Dispatcher.Invoke(() =>
            {
                progressDecryption.Value = value;
            });
        }

        /// <summary>
        /// 更新文件状态
        /// </summary>
        private void UpdateFileStatus(int index, string status)
        {
            Dispatcher.Invoke(() =>
            {
                if (index >= 0 && index < _decryptFiles.Count)
                {
                    _decryptFiles[index].Status = status;
                    dgDecryptList.Items.Refresh();
                }
            });
        }

        /// <summary>
        /// 更新UI状态
        /// </summary>
        private void UpdateUI()
        {
            btnSelectFile.IsEnabled = !_isDecrypting;
            btnSelectFolder.IsEnabled = !_isDecrypting;
            btnSelectOutputDir.IsEnabled = !_isDecrypting;
            btnClearList.IsEnabled = !_isDecrypting && _decryptFiles.Count > 0;
            
            // 只有在未解密且有文件时才能启用解密按钮
            btnStartDecrypt.IsEnabled = !_isDecrypting && _decryptFiles.Count > 0;
            
            // 更新解密状态显示
            if (_isDecrypting)
            {
                progressDecryption.IsIndeterminate = true;
            }
            else
            {
                progressDecryption.IsIndeterminate = false;
            }
        }
        
        /// <summary>
        /// 显示消息
        /// </summary>
        private void ShowMessage(string message, string title, MessageBoxImage icon)
        {
            System.Windows.MessageBox.Show(this, message, title, MessageBoxButton.OK, icon);
        }
        
        /// <summary>
        /// 显示消息
        /// </summary>
        private void ShowMessage(string message, string title, MessageBoxButton button, MessageBoxImage icon)
        {
            System.Windows.MessageBox.Show(this, message, title, button, icon);
        }
        
        /// <summary>
        /// 进度条动画
        /// </summary>
        private void AnimateProgressBar(bool reset = false)
        {
            DoubleAnimation animation = new DoubleAnimation();
            
            if (reset)
            {
                animation.From = progressDecryption.Value;
                animation.To = 0;
            }
            else
            {
                animation.From = 0;
                animation.To = 100;
            }
            
            animation.Duration = TimeSpan.FromSeconds(0.5);
            animation.AutoReverse = true;
            
            progressDecryption.BeginAnimation(System.Windows.Controls.ProgressBar.ValueProperty, animation);
            
            // 0.5秒后重置进度条
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (s, args) =>
            {
                progressDecryption.BeginAnimation(System.Windows.Controls.ProgressBar.ValueProperty, null);
                progressDecryption.Value = 0;
                timer.Stop();
            };
            timer.Start();
        }
        
        /// <summary>
        /// 解密成功动画
        /// </summary>
        private void AnimateDecryptSuccess()
        {
            // 创建绿色闪烁效果
            SolidColorBrush originalBrush = progressDecryption.Foreground as SolidColorBrush;
            
            ColorAnimation colorAnimation = new ColorAnimation();
            // 修复类型转换
            Color successColor = Colors.Green;
            try 
            {
                successColor = (Color)FindResource("SuccessColor");
            }
            catch
            {
                // 如果无法获取资源，使用预定义的绿色
                successColor = Color.FromRgb(76, 175, 80);
            }
            
            colorAnimation.From = successColor;
            colorAnimation.To = Colors.White;
            colorAnimation.Duration = TimeSpan.FromSeconds(0.3);
            colorAnimation.AutoReverse = true;
            colorAnimation.RepeatBehavior = new RepeatBehavior(3);
            
            SolidColorBrush animationBrush = new SolidColorBrush(successColor);
            progressDecryption.Foreground = animationBrush;
            animationBrush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation);
            
            // 进度条动画
            DoubleAnimation animation = new DoubleAnimation();
            animation.From = 0;
            animation.To = progressDecryption.Maximum;
            animation.Duration = TimeSpan.FromSeconds(0.5);
            
            progressDecryption.BeginAnimation(System.Windows.Controls.ProgressBar.ValueProperty, animation);
            
            // 恢复原始颜色
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(2);
            timer.Tick += (s, args) =>
            {
                progressDecryption.Foreground = originalBrush;
                timer.Stop();
            };
            timer.Start();
        }

        /// <summary>
        /// 拖放入口事件
        /// </summary>
        private void Window_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            // 检查是否正在解密
            if (_isDecrypting)
            {
                e.Effects = System.Windows.DragDropEffects.None;
                e.Handled = true;
                return;
            }
            
            // 检查是否包含文件
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                // 获取拖拽的文件
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                
                // 检查文件是否有效
                bool hasValidFiles = false;
                foreach (string file in files)
                {
                    if (File.Exists(file) && _supportedExtensions.Contains(Path.GetExtension(file).ToLower()))
                    {
                        hasValidFiles = true;
                        break;
                    }
                    else if (Directory.Exists(file))
                    {
                        // 如果是文件夹，检查是否包含有效文件
                        string[] dirFiles = Directory.GetFiles(file, "*.*", SearchOption.AllDirectories)
                            .Where(f => _supportedExtensions.Contains(Path.GetExtension(f).ToLower()))
                            .ToArray();
                        
                        if (dirFiles.Length > 0)
                        {
                            hasValidFiles = true;
                            break;
                        }
                    }
                }
                
                if (hasValidFiles)
                {
                    e.Effects = System.Windows.DragDropEffects.Copy;
                    // 显示拖放遮罩
                    dropOverlay.Visibility = Visibility.Visible;
                }
                else
                {
                    e.Effects = System.Windows.DragDropEffects.None;
                }
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
            
            e.Handled = true;
        }
        
        /// <summary>
        /// 拖放完成事件
        /// </summary>
        private void Window_Drop(object sender, System.Windows.DragEventArgs e)
        {
            // 隐藏拖放遮罩
            dropOverlay.Visibility = Visibility.Collapsed;
            
            // 检查是否正在解密
            if (_isDecrypting)
            {
                ShowMessage("正在解密中，请等待完成后再操作", "提示", MessageBoxImage.Information);
                return;
            }
            
            // 检查是否包含文件
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                // 获取拖拽的文件
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                
                int validFilesAdded = 0;
                int totalFilesFound = 0;
                
                foreach (string path in files)
                {
                    if (File.Exists(path))
                    {
                        // 添加单个文件
                        if (AddFileToList(path))
                        {
                            validFilesAdded++;
                            totalFilesFound++;
                        }
                    }
                    else if (Directory.Exists(path))
                    {
                        // 如果是文件夹，添加所有支持的文件
                        string[] dirFiles = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                            .Where(f => _supportedExtensions.Contains(Path.GetExtension(f).ToLower()))
                            .ToArray();
                        
                        totalFilesFound += dirFiles.Length;
                        
                        foreach (string file in dirFiles)
                        {
                            if (AddFileToList(file))
                            {
                                validFilesAdded++;
                            }
                        }
                    }
                }
                
                // 更新UI
                UpdateUI();
                
                // 添加文件成功的提示
                if (validFilesAdded > 0)
                {
                    // 显示第一个文件路径
                    if (files.Length == 1)
                    {
                        if (File.Exists(files[0]))
                        {
                            txtFilePath.Text = files[0];
                        }
                        else
                        {
                            txtFilePath.Text = files[0] + $" (找到 {totalFilesFound} 个文件)";
                        }
                    }
                    else
                    {
                        txtFilePath.Text = $"拖放了 {files.Length} 个项目 (找到 {totalFilesFound} 个文件)";
                    }
                    
                    txtStatus.Text = $"已添加 {validFilesAdded} 个文件，准备就绪";
                    AnimateProgressBar();
                    
                    // 播放添加成功的动画
                    AnimateDropSuccess();
                }
                else if (totalFilesFound == 0)
                {
                    txtStatus.Text = "未找到支持的音乐文件";
                }
            }
        }
        
        /// <summary>
        /// 拖放成功动画
        /// </summary>
        private void AnimateDropSuccess()
        {
            // 创建闪烁效果
            ColorAnimation colorAnimation = new ColorAnimation();
            
            // 修复类型转换
            Color primaryColor = Colors.Blue;
            Color backgroundColor = Colors.White;
            try 
            {
                primaryColor = (Color)FindResource("PrimaryColor");
                backgroundColor = (Color)FindResource("BackgroundColor");
            }
            catch
            {
                // 如果无法获取资源，使用预定义的蓝色和白色
                primaryColor = Color.FromRgb(61, 132, 245);
                backgroundColor = Color.FromRgb(240, 247, 255);
            }
            
            colorAnimation.From = primaryColor;
            colorAnimation.To = backgroundColor;
            colorAnimation.Duration = TimeSpan.FromSeconds(0.3);
            colorAnimation.AutoReverse = true;
            
            // 应用到DataGrid边框
            SolidColorBrush borderBrush = new SolidColorBrush(primaryColor);
            dgDecryptList.BorderBrush = borderBrush;
            borderBrush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation);
            
            // 1秒后重置边框颜色
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (s, args) =>
            {
                // 使用预定义的边框颜色
                dgDecryptList.BorderBrush = new SolidColorBrush(Color.FromRgb(220, 230, 240)); // BorderColor
                timer.Stop();
            };
            timer.Start();
        }

        /// <summary>
        /// 关于按钮点击事件
        /// </summary>
        private void btnAbout_Click(object sender, RoutedEventArgs e)
        {
            // 显示关于窗口
            aboutOverlay.Visibility = Visibility.Visible;
            
            // 添加动画效果
            ScaleTransform scale = new ScaleTransform(0.8, 0.8);
            aboutOverlay.RenderTransform = scale;
            
            DoubleAnimation scaleXAnimation = new DoubleAnimation();
            scaleXAnimation.From = 0.8;
            scaleXAnimation.To = 1.0;
            scaleXAnimation.Duration = TimeSpan.FromSeconds(0.2);
            
            DoubleAnimation scaleYAnimation = new DoubleAnimation();
            scaleYAnimation.From = 0.8;
            scaleYAnimation.To = 1.0;
            scaleYAnimation.Duration = TimeSpan.FromSeconds(0.2);
            
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnimation);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnimation);
        }
        
        /// <summary>
        /// 关闭关于窗口按钮点击事件
        /// </summary>
        private void btnCloseAbout_Click(object sender, RoutedEventArgs e)
        {
            // 隐藏关于窗口
            aboutOverlay.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// 解密文件信息
    /// </summary>
    public class DecryptFileInfo
    {
        public string FilePath { get; set; } = "";
        public string FileName { get; set; } = "";
        public string Status { get; set; } = "";
        public string FileType { get; set; } = "";
        public string OutputFile { get; set; } = "";
    }
} 