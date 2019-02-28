using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace tesseract_train
{
    public partial class frmMain : Form
    {
        private string _tessacertPath;
        private string _tessacertExe;

        private List<FeTrain> _trainList;

        public frmMain()
        {
            InitializeComponent();
            pnlC.Controls.Clear();
            _trainList = new List<FeTrain>();
            LoadCurrentIni();
        }

        private void LoadCurrentIni()
        {
            tbTesseractPath.Text = @"E:\tesseract-ocr\";
            tbTrainImagePath.Text = Application.StartupPath;
            tbLang.Text = "ts";
            tbFont.Text = "yzm";

            _tessacertPath = tbTesseractPath.Text;
            _tessacertExe = Path.Combine(_tessacertPath, "tesseract.exe");
        }

        private void btnMakeBox_Click(object sender, EventArgs e)
        {
            /* 读取指定目录下tif文件，合并到一个文件  dict<file,idx>
             * 生成原始的box文件
             * 读取人工识别文件 file
             * file idx     List<fileInfo>
             *      List<small> 本次识别的
             *      List<small> 人工识别的
             * small
             *      string      识别的字符
             *      left        第四象限坐标
             *      bottom
             *      right
             *      top
             */
            string imagePath = tbTrainImagePath.Text.Trim();
            string lang = tbLang.Text.Trim();
            string font = tbFont.Text.Trim();

            string tmpBoxPath = Path.Combine(imagePath, FeConst.DIR_TEMP_BOX);
            string tmpImgPath = Path.Combine(imagePath, FeConst.DIR_TEMP_IMG);
            string tmpTrainPath = Path.Combine(imagePath, FeConst.DIR_TEMP_TRAIN);
            if(!Directory.Exists(tmpBoxPath)) Directory.CreateDirectory(tmpBoxPath);
            if(!Directory.Exists(tmpImgPath)) Directory.CreateDirectory(tmpImgPath);
            if(!Directory.Exists(tmpTrainPath)) Directory.CreateDirectory(tmpTrainPath);

            Dictionary<int, FeTrain> dictIdxTrain = new Dictionary<int, FeTrain>();
            Dictionary<string, FeTrain> dictFileTrain = new Dictionary<string, FeTrain>();
            List<string> fileList = new List<string>();

            _trainList.Clear();

            //读取图片列表
            DirectoryInfo dir = new DirectoryInfo(imagePath);
            int idx = 0;
            foreach (var fi in dir.GetFiles())
            { 
                string ext = fi.Extension.ToLower();
                if ((ext == ".tiff") || (ext == ".tif"))
                {
                    FeTrain train = new FeTrain();
                    train.Idx = idx;
                    train.FullName = fi.FullName;
                    
                    dictIdxTrain.Add(idx, train);
                    dictFileTrain.Add(fi.Name, train);
                    fileList.Add(fi.FullName);
                    _trainList.Add(train);
                    idx++;
                }
            }
            
            //合并tiff
            string mergeFile = string.Format("{0}.{1}.exp0.tif", lang, font);
            MergeTiff(fileList, Path.Combine(tmpBoxPath, mergeFile));
 
            //生成box
            string args;
            string trainFile = Path.Combine(_tessacertPath, "tessdata", lang + ".traineddata");
            if (File.Exists(trainFile))
                args = string.Format("{0}.{1}.exp0.tif {0}.{1}.exp0 -l {0} -psm 7 batch.nochop makebox", lang, font);
            else
                args = string.Format("{0}.{1}.exp0.tif {0}.{1}.exp0 -psm 7 batch.nochop makebox", lang, font);
            ProcessBat(_tessacertExe, tmpBoxPath, args);
            
            //box ==> FeTrain --> boxList
            string boxFile = Path.Combine(tmpBoxPath, string.Format("{0}.{1}.exp0.box", lang, font));
            using (FileStream fs = new FileStream(boxFile, FileMode.Open))
            {
                StreamReader sr = new StreamReader(fs, Encoding.Default);
                string lineStr = string.Empty;
                int priorIdx = -1;
                int smallIdx = 0;
                while ((lineStr = sr.ReadLine()) != null)
                {
                    lineStr = lineStr.Trim();
                    string[] arr = lineStr.Split(' ');
                    if (arr.Length == 6)
                    {
                        int tmpIdx = int.Parse(arr[5]);
                        if (tmpIdx == priorIdx)
                        {
                            smallIdx++;
                        }
                        else
                        {
                            smallIdx = 0;
                        }
                        FeTrain train;
                        if (dictIdxTrain.TryGetValue(tmpIdx, out train))
                        {
                            train.PutBox(smallIdx, arr[0], int.Parse(arr[1]), int.Parse(arr[2]), int.Parse(arr[3]), int.Parse(arr[4]));
                        }
                        priorIdx = tmpIdx;
                    }
                }
                sr.Close();
            }

            //mark.txt ==> FeTrain --> markList
            string markFile = Path.Combine(imagePath, FeConst.FILE_MARK);
            using (FileStream fs = new FileStream(markFile, FileMode.OpenOrCreate))
            {
                StreamReader sr = new StreamReader(fs, Encoding.Default);
                string lineStr = string.Empty;
                while((lineStr=sr.ReadLine())!=null)
                {
                    string[] arr = lineStr.Split(',');  //char left bottom right top filename smallidx
                    if (arr.Length != 7) continue;
                    string fileName = arr[5];
                    int smallIdx = int.Parse(arr[6]);
                    FeTrain train;
                    if (dictFileTrain.TryGetValue(fileName, out train))
                    {
                        train.PutMark(smallIdx, arr[0], int.Parse(arr[1]), int.Parse(arr[2]), int.Parse(arr[3]), int.Parse(arr[4]));
                    }
                }
            }

            //根据dictTrain建立界面
            int sameTrainCount = 0;
            int markCount = 0;
            int sameCount = 0;
            pnlC.Controls.Clear();

            pnlC.Visible = false;
            pnlC.SuspendLayout();

            int totalH = 0;
            int totalW = 100 + 160 * 5;
            foreach (var item in dictIdxTrain)
            {
                FeTrain train = item.Value;
                markCount += train.MarkCount;
                sameCount += train.SameCount;
                if ((train.MarkCount == train.BoxCount) && (train.MarkCount == train.SameCount))
                    sameTrainCount++;
                train.ShowView(pnlC);
                totalH += 120;
            }
            pnlC.Location = new System.Drawing.Point(0, 0);
            pnlC.Size = new System.Drawing.Size(totalW, totalH);
            pnlC.ResumeLayout(false);
            pnlC.PerformLayout();
            pnlC.Visible = true;

            this.Text = string.Format("{0}/{1}={2:N2}  {3}/{4}={5:N2}", sameTrainCount, dictIdxTrain.Count, sameTrainCount * 1.0 / dictIdxTrain.Count, sameCount, markCount, sameCount * 1.0 / markCount);
        }

        private void btnSaveMark_Click(object sender, EventArgs e)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var train in _trainList)
            {
                foreach (var item in train.SmallMarkList)
                {
                    FeSmall mark = item.Value;
                    string line = mark.GetMarkLine(train.FileName);
                    if (line == "") continue;
                    sb.AppendLine(line);
                }
            }
            SaveFile(sb, FeConst.FILE_MARK);
        }

        private void btnTrain_Click(object sender, EventArgs e)
        {
            /*
             * mark.txt+tmpImg ==> ts.yzm.exp0.tif 合并
             * box
             * mark.txt 修正box
             * unicharset_extractor ts.yzm.exp0.box
             * shapeclustering -F font_properties -U unicharset ts.yzm.exp0.tr
             * mftraining -F font_properties -U unicharset -O ts.unicharset ts.yzm.exp0.tr
             * cntraining ts.yzm.exp0.tr
             * rename normproto ts.normproto
             * rename inttemp ts.inttemp
             * rename pffmtable ts.pffmtable
             * rename shapetable ts.shapetable
             * combine_tessdata ts
             * copy ts.traineddata E:\tesseract-ocr\tessdata
             */
            string imagePath = tbTrainImagePath.Text;
            string lang = tbLang.Text.Trim();
            string font = tbFont.Text.Trim();

            string tmpBoxPath = Path.Combine(imagePath, FeConst.DIR_TEMP_BOX);
            string tmpImgPath = Path.Combine(imagePath, FeConst.DIR_TEMP_IMG);
            string tmpTrainPath = Path.Combine(imagePath, FeConst.DIR_TEMP_TRAIN);

            Dictionary<int, FeSmall> smallList = new Dictionary<int, FeSmall>();
            List<string> fileList = new List<string>();
            int idx=0;
            string markFile = Path.Combine(imagePath, FeConst.FILE_MARK);
            using (FileStream fs = new FileStream(markFile, FileMode.OpenOrCreate))
            {
                StreamReader sr = new StreamReader(fs, Encoding.Default);
                string lineStr = string.Empty;
                while ((lineStr = sr.ReadLine()) != null)
                {
                    string[] arr = lineStr.Split(',');  //char left bottom right top filename smallidx
                    if (arr.Length != 7) continue;
                    string fileName = arr[5];
                    string fiName = Path.GetFileNameWithoutExtension(fileName);
                    string fiExt = Path.GetExtension(fileName);
                    int smallIdx = int.Parse(arr[6]);
                    FeSmall small = new FeSmall();
                    small.Idx = idx;
                    small.FileName = string.Format("{0}_{1}{2}", fiName, smallIdx, fiExt);
                    small.Value = arr[0];
                    smallList.Add(idx, small);
                    fileList.Add(Path.Combine(tmpImgPath, small.FileName));
                    idx++;
                }
            }

            //合并tiff

            string mergeFile = string.Format("{0}.{1}.exp0.tif", lang, font);
            MergeTiff(fileList, Path.Combine(tmpTrainPath, mergeFile));

            //生成box
            string args;
            string trainFile = Path.Combine(_tessacertPath, "tessdata", lang + ".traineddata");
            if (File.Exists(trainFile))
                args = string.Format("{0}.{1}.exp0.tif {0}.{1}.exp0 -l {0} -psm 10 batch.nochop makebox", lang, font);
            else
                args = string.Format("{0}.{1}.exp0.tif {0}.{1}.exp0 -psm 10 batch.nochop makebox", lang, font);
            ProcessBat(_tessacertExe, tmpTrainPath, args);
            
            //mark.txt 修正 box
            StringBuilder sb = new StringBuilder();
            string boxFile = Path.Combine(tmpTrainPath, string.Format("{0}.{1}.exp0.box", lang, font));
            using (FileStream fs = new FileStream(boxFile, FileMode.Open))
            {
                StreamReader sr = new StreamReader(fs, Encoding.Default);
                string lineStr = string.Empty;
                while ((lineStr = sr.ReadLine()) != null)
                {
                    lineStr = lineStr.Trim();
                    string[] arr = lineStr.Split(' ');
                    if (arr.Length == 6)
                    {
                        int tmpIdx = int.Parse(arr[5]);
                        FeSmall small;
                        if (smallList.TryGetValue(tmpIdx, out small))
                        { 
                            //修正
                            if (arr[0] != small.Value)
                            {
                                arr[0] = small.Value;
                                lineStr = string.Format("{0} {1} {2} {3} {4} {5}", arr[0], arr[1], arr[2], arr[3], arr[4], arr[5]);
                            }
                        }
                    }
                    sb.AppendLine(lineStr);
                }
                sr.Close();
            }
            SaveFile(sb, boxFile);

            if (File.Exists(trainFile))
                args = string.Format("{0}.{1}.exp0.tif {0}.{1}.exp0 -l {0} -psm 10 nobatch box.train", lang, font);
            else
                args = string.Format("{0}.{1}.exp0.tif {0}.{1}.exp0 -psm 10 nobatch box.train", lang, font);
            ProcessBat(_tessacertExe, tmpTrainPath, args);

            //unicharset_extractor ts.yzm.exp0.box
            string cmdExe = Path.Combine(_tessacertPath, "unicharset_extractor.exe");
            args = string.Format("{0}.{1}.exp0.box", lang, font);
            ProcessBat(cmdExe, tmpTrainPath, args);

            //echo ts 0 0 0 0 0 >> font_properties
            sb = new StringBuilder();
            sb.AppendLine(string.Format("{0} 0 0 0 0 0", font));
            SaveFile(sb, Path.Combine(tmpTrainPath, "font_properties"));

            //shapeclustering -F font_properties -U unicharset ts.yzm.exp0.tr
            cmdExe = Path.Combine(_tessacertPath, "shapeclustering.exe");
            args = string.Format("-F font_properties -U unicharset {0}.{1}.exp0.tr", lang, font);
            ProcessBat(cmdExe, tmpTrainPath, args);

            //mftraining -F font_properties -U unicharset -O ts.unicharset ts.yzm.exp0.tr
            cmdExe = Path.Combine(_tessacertPath, "mftraining.exe");
            args = string.Format("-F font_properties -U unicharset -O {0}.unicharset {0}.{1}.exp0.tr", lang, font);
            ProcessBat(cmdExe, tmpTrainPath, args);

            //cntraining ts.yzm.exp0.tr
            cmdExe = Path.Combine(_tessacertPath, "cntraining.exe");
            args = string.Format("{0}.{1}.exp0.tr", lang, font);
            ProcessBat(cmdExe, tmpTrainPath, args);

            string tmpFile;
            FileInfo fi;

            tmpFile = Path.Combine(tmpTrainPath, string.Format("{0}.normproto", lang));
            if (File.Exists(tmpFile)) File.Delete(tmpFile);
            tmpFile = Path.Combine(tmpTrainPath, string.Format("{0}.inttemp", lang));
            if (File.Exists(tmpFile)) File.Delete(tmpFile);
            tmpFile = Path.Combine(tmpTrainPath, string.Format("{0}.pffmtable", lang));
            if (File.Exists(tmpFile)) File.Delete(tmpFile);
            tmpFile = Path.Combine(tmpTrainPath, string.Format("{0}.shapetable", lang));
            if (File.Exists(tmpFile)) File.Delete(tmpFile);

            tmpFile = Path.Combine(tmpTrainPath, "normproto");
            fi = new FileInfo(tmpFile);
            fi.MoveTo(Path.Combine(tmpTrainPath, string.Format("{0}.normproto", lang)));
            tmpFile = Path.Combine(tmpTrainPath, "inttemp");
            fi = new FileInfo(tmpFile);
            fi.MoveTo(Path.Combine(tmpTrainPath, string.Format("{0}.inttemp", lang)));
            tmpFile = Path.Combine(tmpTrainPath, "pffmtable");
            fi = new FileInfo(tmpFile);
            fi.MoveTo(Path.Combine(tmpTrainPath, string.Format("{0}.pffmtable", lang)));
            tmpFile = Path.Combine(tmpTrainPath, "shapetable");
            fi = new FileInfo(tmpFile);
            fi.MoveTo(Path.Combine(tmpTrainPath, string.Format("{0}.shapetable", lang)));

            //combine_tessdata ts
            cmdExe = Path.Combine(_tessacertPath, "combine_tessdata.exe");
            args = string.Format("{0}", lang);
            ProcessBat(cmdExe, tmpTrainPath, args);

            string tmpTrainDataFile = string.Format("{0}.traineddata", lang);
            tmpFile = Path.Combine(tmpTrainPath, tmpTrainDataFile);
            fi = new FileInfo(tmpFile);        
            fi.CopyTo(Path.Combine(_tessacertPath, "tessdata", tmpTrainDataFile),true);
            /*
            string _fullName = @"E:\tesseract-train\tesseract-train\tesseract-train\bin\Debug\1.tiff";
            string fileName = Path.GetFileName(_fullName);
            string path = Path.GetDirectoryName(_fullName);
            string ext = Path.GetExtension(fileName);
            string name = Path.GetFileNameWithoutExtension(fileName);
            this.Text = string.Format("name={0},ext={1},path={2}", name, ext, path);
            */
        }

        private void ProcessBat(string exe, string work, string args)
        {
            Process p = new Process();
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.FileName = exe;
            p.StartInfo.WorkingDirectory = work;
            p.StartInfo.Arguments = args;
            //p.StartInfo.RedirectStandardError = true;
            //p.StartInfo.RedirectStandardInput = true;
            //p.StartInfo.RedirectStandardOutput = true;
            p.Start();
            p.WaitForExit();
            p.Close();
        }
        private void MergeTiff(List<string> tiffList, string mergeFile)
        {
            /*
            List<string> fileList = new List<string>();
            fileList.Add(@"E:\temp\3_1.tif");
            fileList.Add(@"E:\temp\3_2.tif");
            fileList.Add(@"E:\temp\3_3.tif");
            MergeTiff(fileList, @"E:\temp\3.tif");
             */
            if (tiffList.Count <= 0) return;
            if (tiffList.Count == 1)
            {
                File.Copy(tiffList[0], mergeFile);
                return;
            }
            ImageCodecInfo info = ImageCodecInfo.GetImageEncoders().First(ie => ie.MimeType == "image/tiff");
            EncoderParameters ep = new EncoderParameters(2);
            ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.MultiFrame);
            ep.Param[1] = new EncoderParameter(System.Drawing.Imaging.Encoder.Compression, (long)EncoderValue.CompressionNone);
            using (Image mergeImg = Image.FromFile(tiffList[0]))
            {
                mergeImg.Save(mergeFile, info, ep);
                ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.FrameDimensionPage);
                for (int i = 1; i < tiffList.Count; i++)
                {
                    using (Image img = Image.FromFile(tiffList[i]))
                    {
                        mergeImg.SaveAdd(img, ep);
                    }
                }
                ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.Flush);
                mergeImg.SaveAdd(ep);
            }
        }
        private void SaveFile(StringBuilder sb, string fileName)
        {
            using (FileStream fs = File.Open(fileName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
            {
                fs.Seek(0, SeekOrigin.Begin);
                byte[] data = Encoding.Default.GetBytes(sb.ToString());
                fs.Write(data, 0, data.Length);
                fs.Flush();
                fs.Close();
            }
        }
    }
    public class FeConst
    {
        public const string DIR_TEMP_BOX = "TempBox";
        public const string DIR_TEMP_IMG = "TempImg";
        public const string DIR_TEMP_TRAIN = "TempTrain";
        public const string FILE_MARK = "mark.txt";
    }
    public class FeTrain
    {
        private int _idx;
        private string _fullName;   //全路径文件名
        private string _fileName;   //文件名（不包括路径）
        private string _fiPath;
        private string _fiName;       
        private string _fiExt;
        private string _width;
        private string _height;
        private bool _isHide;       //false=当前
        private Dictionary<int, FeSmall> _smallBoxList;
        private Dictionary<int, FeSmall> _smallMarkList; //smallIdx, FeSmall
        private PictureBox _pictureBox;
        private Label _lblFileName;

        private int _boxCount;
        private int _markCount;
        private int _sameCount;
        
        public FeTrain(int charNumber = 5)
        {
            _smallBoxList = new Dictionary<int, FeSmall>();
            _smallMarkList = new Dictionary<int, FeSmall>();
            CapacityBox(charNumber);
            CapacityMark(charNumber);
            _pictureBox = new System.Windows.Forms.PictureBox();
            _pictureBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            _pictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            _lblFileName = new System.Windows.Forms.Label();
            _lblFileName.AutoSize = true;
            _lblFileName.Font = new System.Drawing.Font("Arial Narrow", 7.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            _boxCount = 0;
            _markCount = 0;
            _sameCount = 0;
        }
        public int Idx
        {
            get { return _idx; }
            set { _idx = value; }
        }
        public string FullName
        {
            get { return _fullName; }
            set 
            {
                _fullName = value;
                _fileName = Path.GetFileName(_fullName);
                _fiPath = Path.GetDirectoryName(_fullName);
                _fiName = Path.GetFileNameWithoutExtension(_fileName);
                _fiExt = Path.GetExtension(_fileName);
            }
        }
        public string FileName
        {
            get { return _fileName; }
        }
        public string FiPath
        {
            get { return _fiPath; }
        }
        public string FiName
        {
            get { return _fiName; }
        }
        public string FiExt
        {
            get { return _fiExt; }
        }
        public Dictionary<int, FeSmall> SmallBoxList
        {
            get { return _smallBoxList; }
        }
        public Dictionary<int, FeSmall> SmallMarkList
        {
            get { return _smallMarkList; }
        }
        public int BoxCount
        {
            get { return _boxCount; }
        }
        public int MarkCount
        {
            get { return _markCount; }
        }
        public int SameCount
        {
            get { return _sameCount; }
        }

        private void CapacityBox(int newSize)
        {
            int idx = _smallBoxList.Count;
            for (int i = idx; i < newSize; i++)
            {
                FeSmall small = new FeSmall();
                small.Idx = i;
                _smallBoxList.Add(i, small);
            }
        }
        private void CapacityMark(int newIdx)
        {
            int idx = _smallMarkList.Count;
            for (int i = idx; i < newIdx; i++)
            {
                FeSmall small = new FeSmall();
                small.Idx = i;
                _smallMarkList.Add(i, small);
            }
        }
        public void PutBox(int smallIdx, string value, int left, int bottom, int right, int top)
        {
            if (smallIdx >= _smallBoxList.Count) CapacityBox(smallIdx+1);
            FeSmall small = _smallBoxList[smallIdx];
            small.IsEmpty = false;
            small.Idx = smallIdx;
            small.Value = value;
            small.Left = left;
            small.Bottom = bottom;
            small.Right = right;
            small.Top = top;
            _boxCount++;
        }
        public void PutMark(int smallIdx, string value, int left, int bottom, int right, int top)
        {
            if (smallIdx >= _smallMarkList.Count) CapacityMark(smallIdx+1);
            FeSmall small = _smallMarkList[smallIdx];
            small.IsEmpty = false;
            small.Idx = smallIdx;
            small.Value = value;
            small.Left = left;
            small.Bottom = bottom;
            small.Right = right;
            small.Top = top;
            _markCount++;
            if (_smallBoxList[smallIdx].Value == value)
            {
                small.IsSame = true;
                _sameCount++;
                _smallBoxList[smallIdx].IsSame = true;
            } 
        }
        public void ShowView(Panel container)
        {
            const int BLOCK_HEIGHT = 60;
            const int PADDING_LEFT = 10;
            const int PADDING_TOP = 10;
            int baseTop = _idx * BLOCK_HEIGHT;

            _pictureBox.Location = new System.Drawing.Point(PADDING_LEFT, baseTop + PADDING_TOP);
            _pictureBox.Image = Image.FromFile(_fullName);
            container.Controls.Add(_pictureBox);

            _lblFileName.Location = new System.Drawing.Point(PADDING_LEFT, baseTop + PADDING_TOP + _pictureBox.Image.Height);
            _lblFileName.Text = _fileName;
            container.Controls.Add(_lblFileName);

            int offsetX = 100;
            int offsetY = baseTop + PADDING_TOP;
            foreach (var item in _smallBoxList)
            {
                item.Value.DoDoubleClick = this.ProcBoxDoubleClick;
                item.Value.PictureBoxBackColor = System.Drawing.Color.LightSteelBlue; 
                item.Value.CutImage(_pictureBox.Image);
                if (item.Value.IsSame)
                {
                    item.Value.TextBoxBackColor = System.Drawing.Color.LightSteelBlue; 
                }
                item.Value.ShowView(offsetX, offsetY, container);
            }
            offsetY += 27;
            foreach (var item in _smallMarkList)
            {
                string smallFileName = string.Format("{0}_{1}{2}", _fiName, item.Value.Idx, _fiExt);
                string smallFullName = Path.Combine(_fiPath, FeConst.DIR_TEMP_IMG, smallFileName);
                item.Value.DoDoubleClick = this.ProcMarkDoubleClick;
                item.Value.LoadImage(smallFullName);
                if (item.Value.IsSame)
                {
                    item.Value.TextBoxBackColor = System.Drawing.Color.LightSteelBlue;
                }
                else
                {
                    if (!item.Value.IsEmpty)
                    {
                        item.Value.TextBoxBackColor = System.Drawing.Color.Plum;
                        _smallBoxList[item.Value.Idx].TextBoxBackColor = System.Drawing.Color.Plum;
                    }
                }
                item.Value.ShowView(offsetX, offsetY, container);
            }
        }

        private void ProcBoxDoubleClick(FeSmall box)
        {
            FeSmall mark;
            if (_smallMarkList.TryGetValue(box.Idx, out mark))
            {
                string smallFileName = string.Format("{0}_{1}{2}", _fiName, box.Idx, _fiExt);
                string smallFullName = Path.Combine(_fiPath, FeConst.DIR_TEMP_IMG, smallFileName);
                mark.GetFromSmall(box);
                mark.SaveImage(smallFullName);
            }
            
        }
        private void ProcMarkDoubleClick(FeSmall mark)
        {
            mark.FullEmpty();
        }
    }
    public class FeSmall
    {
        private bool _isEmpty;
        private int _idx;       //small idx
        private string _value;  //识别的字符串
        private int _left;      //第一象限坐标
        private int _bottom;
        private int _right;
        private int _top;

        private PictureBox _pictureBox;
        private TextBox _textBox;

        private Action<FeSmall> _doDoubleClick;
        private Bitmap _picBmp;
        private string _fileName;

        private bool _isSame;
        
        public FeSmall()
        {
            _isEmpty = true;
            InitComponent();
        }

        public void InitComponent()
        {
            _pictureBox = new System.Windows.Forms.PictureBox();
            _pictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            _textBox = new System.Windows.Forms.TextBox();
            _textBox.Font = new System.Drawing.Font("Arial", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            _textBox.DoubleClick += new System.EventHandler(TextBox_DoubleClick);
        }
        public bool IsEmpty
        {
            get { return _isEmpty; }
            set { _isEmpty = value; }
        }
        public int Idx
        {
            get { return _idx; }
            set { _idx = value; }
        }
        public string Value
        {
            get { return _value; }
            set { _value = value; }
        }
        public int Left
        {
            get { return _left; }
            set { _left = value; }
        }
        public int Bottom
        {
            get { return _bottom; }
            set { _bottom = value; }
        }
        public int Right
        {
            get { return _right; }
            set { _right = value; }
        }
        public int Top
        {
            get { return _top; }
            set { _top = value; }
        }
        public Color PictureBoxBackColor
        {
            get { return _pictureBox.BackColor; }
            set { _pictureBox.BackColor = value; }
        }
        public Color TextBoxBackColor
        {
            get { return _textBox.BackColor; }
            set { _textBox.BackColor = value; }
        }
        public Action<FeSmall> DoDoubleClick
        {
            get { return _doDoubleClick; }
            set { _doDoubleClick = value; }
        }
        public string FileName
        {
            get { return _fileName; }
            set { _fileName = value; }
        }
        public bool IsSame
        {
            get { return _isSame; }
            set { _isSame = value; }
        }

        public void FullEmpty()
        {
            _isEmpty = true;
            _pictureBox.Image = null;
            _textBox.Text = "";
        }
        public void CutImage(Image img)
        { 
            int mw = img.Width;
            int mh = img.Height;
            Bitmap bmpRaw;
            using (MemoryStream msRaw = new MemoryStream())
            {
                img.Save(msRaw, ImageFormat.Bmp);
                bmpRaw = new Bitmap(msRaw);
                Rectangle rect = ToQuadrant4(mw, mh);
                if (!rect.IsEmpty)
                {
                    _picBmp = bmpRaw.Clone(rect, PixelFormat.DontCare);
                    using (MemoryStream msCut = new MemoryStream())
                    {
                        _picBmp.Save(msCut, ImageFormat.Tiff);
                        //bmpCut.Save("aa.tif",ImageFormat.Tiff);
                        _pictureBox.Image = Image.FromStream(msCut);
                    }
                }
            }
        }

        public void GetFromSmall(FeSmall small)
        {
            _picBmp = small._picBmp.Clone(new Rectangle(0, 0, small._picBmp.Width, small._picBmp.Height), PixelFormat.DontCare);
            using (MemoryStream ms = new MemoryStream())
            {
                _picBmp.Save(ms, ImageFormat.Tiff);
                _pictureBox.Image = Image.FromStream(ms);
            }
            _textBox.Text = small._textBox.Text;
            _isEmpty = false;
            _value = small._value;
            _left = small.Left;
            _bottom = small._bottom;
            _right = small._right;
            _top = small._top;
        }
        public void SaveImage(string fullFile)
        {
            if (_picBmp != null)
            {
                _picBmp.Save(fullFile, ImageFormat.Tiff);
            }
        }
        public void LoadImage(string fullFile)
        {
            if (File.Exists(fullFile))
            {
                using (Bitmap bmp = new Bitmap(fullFile))
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        bmp.Save(ms, ImageFormat.Tiff);
                        _pictureBox.Image = Image.FromStream(ms);
                    }
                }
            }
            else
                _pictureBox.Image = null;
        }
        public string GetMarkLine(string rawFile)
        {
            if (_isEmpty) return "";
            string tmpStr = _textBox.Text.Trim();
            string[] arr = tmpStr.Split('=');
            if (arr.Length != 2) return "";
            _value = arr[0];
            return string.Format("{0},{1},{2},{3},{4},{5},{6}", _value, _left, _bottom, _right, _top, rawFile, _idx);
        }

        public Rectangle ToQuadrant4(int mw, int mh)
        {
            int x = _left;
            int y = mh - _top;
            int w = _right - _left;
            int h = _top - _bottom;
            if ((x >= 0) && (y >= 0) && ((x + w) <= mw) && ((y + h) <= mh) && (h != 0) && (w != 0))
                return new Rectangle(x, y, w, h);
            else
                return new Rectangle();
        }

        public void ShowView(int x, int y, Panel container)
        {
            const int SMALL_WIDTH = 160;
            const int PICTURE_WIDTH = 20;
            int offsetX = x + _idx * SMALL_WIDTH;
            int offsetY = y;
            _pictureBox.Location = new System.Drawing.Point(offsetX, offsetY);
            _pictureBox.Size = new System.Drawing.Size(PICTURE_WIDTH, 21);
            //picBox.Image = Image.FromFile(_fullName);

            offsetX = offsetX + PICTURE_WIDTH + 5;
            _textBox.Location = new System.Drawing.Point(offsetX, offsetY);
            _textBox.Size = new System.Drawing.Size(120, 21);
            _textBox.Text = _isEmpty ? "" : string.Format("{0}={1} {2} {3} {4}", _value, _left, _bottom, _right, _top);

            container.Controls.Add(_pictureBox);
            container.Controls.Add(_textBox);
        }
        private void TextBox_DoubleClick(object sender, EventArgs e)
        {
            if (_doDoubleClick != null)
            {
                _doDoubleClick(this);
            }
        }
    }

    [DataContract]
    public class Student
    {
        [DataMember]
        public int ID { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public int Age { get; set; }

        [DataMember]
        public string Sex { get; set; }
    }
}
/*
Student std1 = new Student() { ID = 1, Name = "wgf", Sex = "男", Age = 99 };
DataContractJsonSerializer json1 = new DataContractJsonSerializer(typeof(Student));
MemoryStream ms1 = new MemoryStream();
json1.WriteObject(ms1, std1);
ms1.Position = 0;
//byte[] bytes = new byte[ms1.Length];
//ms1.Read(bytes, 0, (int)ms1.Length);
StreamReader sr = new StreamReader(ms1, Encoding.UTF8);
using (FileStream stream = File.Open("a1.txt", FileMode.Create, FileAccess.ReadWrite))
{
    string s = "";
    byte[] bs = s.ToArray<byte>();
    //byte[] bytes = sr.ReadToEnd().ToArray
    //stream.Write(bytes, 0, (int)bytes.Length);
}
StreamReader sr = new StreamReader(ms1, Encoding.UTF8);
string str = sr.ReadToEnd();
sr.Close();
ms1.Close();
MemoryStream ms2 = new MemoryStream(Encoding.UTF8.GetBytes(str));
DataContractJsonSerializer json2 = new DataContractJsonSerializer(typeof(Student));
Student std2 = (Student)json2.ReadObject(ms2);
this.Text = std2.Name;
*/