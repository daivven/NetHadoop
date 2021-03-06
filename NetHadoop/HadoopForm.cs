﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using HDFS;
using System.Runtime.InteropServices;
using System.IO;
namespace NetHadoop
{
    public partial class HadoopForm : Form
    {
        #region 变量
        private log4net.ILog logger = log4net.LogManager.GetLogger("LG");

        private BackgroundWorker worker = new BackgroundWorker();
       
        public bool IsBusy { get { return worker.IsBusy; } }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode, EntryPoint = "SetWindowTheme")]
        private static extern Int32 SetWindowTheme(IntPtr hwnd, string subAppName, string subIdList);

        private List<FileStatus> floderlist = new List<FileStatus>();
        private readonly string rootPath = ConfigHelper.HdfsRoot;
        private static string CurrentPath = "/";
        #endregion


        public HadoopForm()
        {
            InitializeComponent();
            tbAddress.Size = new Size(this.Width - 200, tbAddress.Height);
            worker.WorkerSupportsCancellation = true;
            worker.WorkerReportsProgress = true;
            lbProgressTxt.Text = "";
            InitListView();
            LoadFileStatus("/");
        }

        private void Form_SizeChanged(object sender, EventArgs e)
        {
            tbAddress.Size = new Size(this.Width - 200, tbAddress.Height);
        }


        #region 公共
        //隐藏根路径
        private string HideRootName(string namePath)
        {
          return  System.IO.Path.GetFileName(namePath);
          //  return namePath;
        }

        private string MkFileSize(long fileSize)
        {
            string result = "";
            long tSize = (long)1024 * 1024 * 1024 * 1024;
            long gSize = 1024 * 1024 * 1024;
            long mSize = 1024 * 1024;

            if (fileSize >= tSize)
            {
                result = ((double)fileSize / tSize).ToString("f2") + "TB";
            }
            else if (fileSize >= gSize)
            {
                result = ((double)fileSize / gSize).ToString("f2") + "GB";
            }
            else if (fileSize >= mSize)
            {
                result = ((double)fileSize / mSize).ToString("f2") + "MB";
            }
            else if (fileSize > 1024)
            {
                result = ((double)fileSize / 1024).ToString("f2") + "KB";
            }
            else
            {
                result = fileSize + "B";
            }

            return result;
        }

        //识别文件类型
        private string MkFileType(FileStatus fs)
        {
            string typeString = "未知类型";
            if (fs.Isdir)
            {
                typeString = "文件夹";
            }
            else
            {
                string ext = System.IO.Path.GetExtension(fs.Path);
                if (!string.IsNullOrEmpty(ext))
                    typeString = ext;
            }
            return typeString;
        }


        //生成不重名文件
        private string MakeNewName(string name)
        {
            ListViewItem lvi = FindItemByName(name);
            while (lvi != null)
            {
                if (name.Contains("_"))
                {
                    int i = int.Parse(name.Substring(name.IndexOf("_") + 1)) + 1;
                    name = name.Substring(0, name.IndexOf("_")) + "_" + i;
                }
                else
                {
                    name = name + "_1";
                }
                return MakeNewName(name);
            }
            return name;
        }
        private ListViewItem FindItemByName(string name)
        {
            foreach (ListViewItem item in lvFiles.Items)
            {
                if (item.SubItems != null && item.SubItems[0] != null && item.SubItems[0].Text == name)
                {
                    return item;
                }
            }
            return null;
        }
        #endregion

        #region 列表
        //显示文件列表
        private void ShowInListView(List<FileStatus> floderlist)
        {
            List<string> fileExtensionDic = new List<string>();
            ImageList filesImageList = new ImageList();

            lvFiles.Items.Clear();
            if (floderlist.Count > 0)
            {
                int indexI = 0;
                List<ListViewItem> listBuffer = new List<ListViewItem>();
                foreach (FileStatus item in floderlist)
                {
                    string fileExtension = MkFileType(item);
                    if (!fileExtensionDic.Contains(fileExtension))
                    {
                        fileExtensionDic.Add(fileExtension);
                        filesImageList.Images.Add(
                         SystemFileHelper.GetFileIcon(fileExtension, false));
                    }

                    string fileName = HideRootName(item.Path);
                    if (fileName == ".Trash")
                        continue;
                    ListViewItem li = new ListViewItem();
                    li.ImageIndex = fileExtensionDic.IndexOf(fileExtension);
                    li.SubItems[0].Text = fileName;
                    li.SubItems.Add(new DateTime(1970, 1, 1).AddMilliseconds(item.Modification_time).AddHours(8).ToString("yyyy-MM-dd HH:mm"));
                    li.SubItems.Add(fileExtension);
                    li.SubItems.Add(item.Isdir ? "" : MkFileSize(item.Length));
                    li.Tag = item;
                    listBuffer.Add(li);

                    if (indexI++ % 1000 == 0)
                    {
                        lvFiles.Items.AddRange(listBuffer.ToArray());
                        listBuffer.Clear();
                    }
                    Application.DoEvents();
                }
                lvFiles.Items.AddRange(listBuffer.ToArray());
                lvFiles.SmallImageList = filesImageList;

                mystatusbar.ImageList = filesImageList;
                lbFileInfo.ImageIndex = 0;
                lbFileInfo.Text = floderlist.Count+" 对象";
            }
            else
            {
                lvFiles.Items.Add(new ListViewItem() { Text = "该文件夹为空。" });
            }
            
        }
        //初始化列表
        private void InitListView()
        {
            SetWindowTheme(lvFiles.Handle, "Explorer", null);
            //排序
            lvFiles.ListViewItemSorter = new ListViewColumnSorter();
            lvFiles.ColumnClick += new ColumnClickEventHandler(ListViewHelper.ListView_ColumnClick);
            lvFiles.Columns.Clear();
            lvFiles.Columns.Add("名称", 250);
            lvFiles.Columns.Add("修改日期", 130);
            lvFiles.Columns.Add("类型", 100);
            lvFiles.Columns.Add("大小", 100);
        }

        //加载列表
        private void LoadFileStatus(string path)
        {
            if (string.IsNullOrEmpty(path))
                path = "/";

            FSClient client=new FSClient();
            List<FileStatus> myList = client.GetFlolderList(ConfigHelper.HdfsRoot + path);
            if (myList != null)
            {
                //向上按钮
                btUp.Enabled = !(path == "/");
                
                //下拉选择过的路径
                if (!tbAddress.Items.Contains(path))
                {
                    tbAddress.Items.Add(path);
                }
                //更改显示，触发tbAddress_SelectedIndexChanged
                CurrentPath = path;
                tbAddress.Text = path;
                //显示数据
                ShowInListView(myList);
            }
        }
        //双击进入
        private void lvFiles_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            FileStatus fs = lvFiles.SelectedItems[0].Tag as FileStatus;
            if (fs != null && fs.Isdir)
            {
                LoadFileStatus(tbAddress.Text.Trim() + lvFiles.SelectedItems[0].Text+ "/");
            }
        }
        //回车进入
        private void lvFiles_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                FileStatus fs = lvFiles.SelectedItems[0].Tag as FileStatus;
                if (fs != null && fs.Isdir)
                {
                    LoadFileStatus(tbAddress.Text.Trim() + lvFiles.SelectedItems[0].Text + "/");
                }
            }
        }

        //重命名
        private void listView1_AfterLabelEdit(object sender, LabelEditEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Label))
            {
                ListViewItem myItem = lvFiles.Items[e.Item];
                if (myItem != null)
                {
                    ListViewItem existItem = FindItemByName(e.Label);
                    if (existItem != null)
                    {
                        MessageBox.Show("该文件夹名已存在！");
                        e.CancelEdit = true;
                    }
                    else
                    {
                        FSClient client = new FSClient();
                        FileStatus myFile=myItem.Tag as FileStatus;
                        
                        bool result = client.ReName(myFile.Path, ConfigHelper.HdfsRoot + "/" + CurrentPath + e.Label);
                        if (!result)
                        {
                            e.CancelEdit = true;
                            MessageBox.Show("文件夹重命名失败！");
                        }
                        else
                        {
                            LoadFileStatus(CurrentPath);
                        }
                    }
                }
            }
        }

        //选中 并 可编辑
        private void SelectItemEidt(string name)
        {
            ListViewItem lvi = FindItemByName(name);
            if (lvi != null)
            {
                lvi.Selected = true;
                lvi.BeginEdit();
            }
        }

        //重命名
        private void MenuItemReName_Click(object sender, EventArgs e)
        {
            if (lvFiles.SelectedItems.Count > 0)
            {
                lvFiles.SelectedItems[0].BeginEdit();
            }
        }
        #endregion

        #region 地址栏

        //向上
        private void btUp_Click(object sender, EventArgs e)
        {
            string nowPath = tbAddress.Text.Trim();

            if (nowPath.EndsWith("/"))
            {
                nowPath = nowPath.Substring(0, nowPath.Length - 1);
            }
            nowPath = nowPath.Substring(0, nowPath.LastIndexOf("/") + 1);
            LoadFileStatus(nowPath);
        }
        //选择现有路径
        private void tbAddress_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tbAddress.SelectedItem.ToString() != CurrentPath)
            {
                LoadFileStatus(tbAddress.SelectedItem.ToString());
            }
        }
        //回车
        private void tbAddress_KeyUp(object sender, KeyEventArgs e)
        {
           if (e.KeyCode == Keys.Enter)
            {
                LoadFileStatus(tbAddress.Text.Trim());
            }
        }

        #endregion


        #region 新建文件夹 删除选中
        //新建文件夹
        private void btMkdir_Click(object sender, EventArgs e)
        {

            string newName = MakeNewName("新建文件夹");

            ListViewItem li = new ListViewItem();
            li.ImageIndex = 1;
            li.SubItems[0].Text = newName;
            li.SubItems.Add(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            li.SubItems.Add("文件夹");
            li.SubItems.Add("");

            FSClient client = new FSClient();
            bool result = client.MakeDir(ConfigHelper.HdfsRoot + CurrentPath + newName);
            if (result)
            { 
                lvFiles.Items.Add(li);
                SelectItemEidt(newName);
            }
            else
            {
                MessageBox.Show("新建文件夹失败！");
            }

        }

        //删除选中
        private void btDelete_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("确定删除选中的文件或文件夹？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                == System.Windows.Forms.DialogResult.Yes)
            {
                if (lvFiles.SelectedItems.Count > 0)
                {
                    foreach (ListViewItem item in lvFiles.SelectedItems)
                    {
                        FileStatus myfile = item.Tag as FileStatus;
                        if (myfile != null)
                        {
                            FSClient client = new FSClient();
                            bool result = client.Delete(myfile.Path, true);
                            string msg = string.Format("{2}：{0} 删除{1}", Path.GetFileName(myfile.Path), result ? "成功" : "失败", myfile.Isdir ? "文件夹" : "文件");
                            lbProgressTxt.Text = msg;
                        }
                        Application.DoEvents();
                    }
                    //重新加载
                    LoadFileStatus(CurrentPath);
                }
            }
        }
        #endregion

        #region 下载
        //下载
        private void btDownLoad_Click(object sender, EventArgs e)
        {
            if (lvFiles.SelectedItems.Count > 0)
            {
                FolderBrowserDialog fbd = new FolderBrowserDialog();
                fbd.ShowNewFolderButton = true;

                //选择保存文件夹
                if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    worker.DoWork += new DoWorkEventHandler(DoDownLoadWork);
                    worker.ProgressChanged += new ProgressChangedEventHandler(ProgessChanged);
                    worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(CompleteDownLoadWork);

                    List<FileStatus> fileList = new List<FileStatus>();
                    List<string> fileNameList = new List<string>();
                    foreach (ListViewItem item in lvFiles.SelectedItems)
                    {
                        if (item.Text.Contains("."))
                            fileNameList.Add(item.Text.Split('.')[0]);
                        else
                            fileNameList.Add(item.Text);
                    }

                    foreach (ListViewItem item in lvFiles.SelectedItems)
                    {
                        FileStatus myfile = item.Tag as FileStatus;
                        if (myfile != null)
                        {
                            string filename = item.Text;
                            if (item.Text.Contains("."))
                                filename = item.Text.Split('.')[0];

                            fileList.Add(myfile);
                        }
                    }

                    worker.RunWorkerAsync(new DownloadArg() { FileList = fileList, SavePath = fbd.SelectedPath, OutFileType = 0 });

                }
            }
        }
        /// <summary>
        /// 根据订单 或右键下载
        /// </summary>
        /// <param name="pathList">key:文件名，value:主题名</param>
        /// <param name="outFamate">导出格式</param>
        /// <param name="fileType">以什么为文件名，0文件名，1主题名，2主题文件夹</param>
        public void DownLoadFile(Dictionary<string, string> pathList, string outFamate, int fileType)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.ShowNewFolderButton = true;
            fbd.Description = "请选择一个文件夹存放导出的视频文件。";

            //选择保存文件夹
            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                worker.DoWork += new DoWorkEventHandler(DoDownLoadWork);
                worker.ProgressChanged += new ProgressChangedEventHandler(ProgessChanged);
                worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(CompleteDownLoadWork);

                List<FileStatus> fileList = new List<FileStatus>();

                FSClient fs = new FSClient();
                List<string> noFound = new List<string>();
                foreach (string filePath in pathList.Keys)
                {
                    FileStatus myfile = fs.GetFileStatus(ConfigHelper.HdfsRoot + "/" + CurrentPath + "/" + filePath + outFamate);
                    if (myfile != null)
                    {
                        myfile.FileName = pathList[filePath] + outFamate;
                        fileList.Add(myfile);
                    }
                    else
                    {
                        noFound.Add(filePath);
                    }
                }
                string nofoundTitle = string.Format("以下{0}个文件没有找到，请核实：\r\n{1}\r\n===结束===", noFound.Count, string.Join("\r\n", noFound.ToArray()));
                File.WriteAllText(fbd.SelectedPath + "/NoFound.txt", nofoundTitle);

                worker.RunWorkerAsync(new DownloadArg() { FileList = fileList, SavePath = fbd.SelectedPath, OutFileType = fileType });
            }
        }

        //根据导入列表下载
        public void DownLoadFile(List<string> pathList, string outFamate)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.ShowNewFolderButton = true;
            fbd.Description = "请选择一个文件夹存放导出的视频文件。";

            //选择保存文件夹
            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                worker.DoWork += new DoWorkEventHandler(DoDownLoadWork);
                worker.ProgressChanged += new ProgressChangedEventHandler(ProgessChanged);
                worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(CompleteDownLoadWork);

                List<FileStatus> fileList = new List<FileStatus>();

                FSClient fs = new FSClient();
                List<string> noFound = new List<string>();

                foreach (string filePath in pathList)
                {
                    FileStatus myfile = fs.GetFileStatus(ConfigHelper.HdfsRoot + "/" + CurrentPath + "/" + filePath + outFamate);
                    if (myfile != null)
                    {
                        fileList.Add(myfile);
                    }
                    else
                    {
                        noFound.Add(filePath);
                    }
                }
                string nofoundTitle = string.Format("以下{0}个文件没有找到，请核实：\r\n{1}\r\n===结束===", noFound.Count, string.Join("\r\n", noFound.ToArray()));
                File.WriteAllText(fbd.SelectedPath + "/NoFound.txt", nofoundTitle);

                worker.RunWorkerAsync(new DownloadArg() { FileList = fileList, SavePath = fbd.SelectedPath, OutFileType = 0 });
            }
        }

        private void DoDownLoadWork(object sender, DoWorkEventArgs e)
        {
            DownloadArg da = e.Argument as DownloadArg;

            FSClient fs = new FSClient();

            fs.MutDownLoad(worker, da.SavePath, da.FileList, da.OutFileType);
            e.Result = "'" + da.SavePath + "',共找到" + da.FileList.Count + "个文件";

        }
        private void CompleteDownLoadWork(object sender, RunWorkerCompletedEventArgs e)
        {
            worker.DoWork -= new DoWorkEventHandler(DoDownLoadWork);
            worker.ProgressChanged -= new ProgressChangedEventHandler(ProgessChanged);
            worker.RunWorkerCompleted -= new RunWorkerCompletedEventHandler(CompleteDownLoadWork);

            MessageBox.Show("下载完成！视频文件存放路径：" + e.Result + ",如果存在没有找到的视频请查看NoFound.txt文件！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        #endregion

        #region 上传
        private string[] GetFiles()
        {
            string[] fileNames;
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.RestoreDirectory = true;
            openFileDialog1.Multiselect = true;
            openFileDialog1.CheckFileExists = false;
            try
            {
                DialogResult result = openFileDialog1.ShowDialog();
                if (result == DialogResult.OK && openFileDialog1.FileNames.Length < 101)
                {
                    return fileNames = openFileDialog1.FileNames;
                }
                else if (result == DialogResult.Cancel)
                {
                    return null;
                }
                else
                {
                    MessageBox.Show("您选择的文件太多了，请使用上传文件夹！");
                    return null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("错误: 读取文件失败。 " + ex.Message);
                return null;
            }
        }

        private UploadArg GetFilesInFolder()
        {
            UploadArg ua = new UploadArg();
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            //folderBrowserDialog.RootFolder = System.Environment.SpecialFolder.Desktop;
            DialogResult results = folderBrowserDialog.ShowDialog();
            if (results == DialogResult.OK)
            {
                try
                {
                    string pathName = folderBrowserDialog.SelectedPath;
                    GetFilesFromDic(pathName, ua.FilePathList);
                    ua.LocalFileRoot = pathName;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("错误: 读取文件失败。 " + ex.Message);
                }
            }
            return ua;

        }

        private void GetFilesFromDic(string rootPath, List<string> filesPath)
        {
            DirectoryInfo dir = new DirectoryInfo(rootPath);
            if (dir.Exists)
            {
                FileInfo[] theFiles = dir.GetFiles();
                foreach (FileInfo item in theFiles)
                {
                    filesPath.Add(item.FullName);
                }
                DirectoryInfo[] theDirc = dir.GetDirectories();

                foreach (DirectoryInfo item in theDirc)
                {
                    GetFilesFromDic(item.FullName, filesPath);
                }
            }
        }

        //点击 上传文件夹
        private void MenuItemUploadFloder_Click(object sender, EventArgs e)
        {
            UploadArg fileNames = GetFilesInFolder();
            if (fileNames.FilePathList.Count > 0)
            {

                worker.DoWork += new DoWorkEventHandler(DoUploadWork);
                worker.ProgressChanged += new ProgressChangedEventHandler(ProgessChanged);
                worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(CompleteUploadWork);

                worker.RunWorkerAsync(fileNames);
            }
        }

        //点击 上传文件
        private void btUpload_Click(object sender, EventArgs e)
        {
            string[] fileNames = GetFiles();
            if (fileNames != null && fileNames.Length > 0)
            {

                worker.DoWork += new DoWorkEventHandler(DoUploadWork);
                worker.ProgressChanged += new ProgressChangedEventHandler(ProgessChanged);
                worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(CompleteUploadWork);

                worker.RunWorkerAsync(new UploadArg() { FilePathList = new List<string>(fileNames), LocalFileRoot = null });
            }

        }

        //上传动作
        private void DoUploadWork(object sender, DoWorkEventArgs e)
        {
            UploadArg fileNames = e.Argument as UploadArg;

            FSClient fs = new FSClient();

            fs.MutUpload(worker, fileNames.FilePathList, ConfigHelper.HdfsRoot + CurrentPath, fileNames.LocalFileRoot);

        }

        //上传结束
        private void CompleteUploadWork(object sender, RunWorkerCompletedEventArgs e)
        {
            LoadFileStatus(CurrentPath);
            worker.DoWork -= new DoWorkEventHandler(DoUploadWork);
            worker.ProgressChanged -= new ProgressChangedEventHandler(ProgessChanged);
            worker.RunWorkerCompleted -= new RunWorkerCompletedEventHandler(CompleteUploadWork);


        }
        //显示上传进度
        private void ProgessChanged(object sender, ProgressChangedEventArgs e)
        {

            lbProgressBar.Value = e.ProgressPercentage;

            ProgressState ps = e.UserState as ProgressState;
            if (ps != null)
            {
                if (ps.CurrentCount != 0)
                {
                    lbCurrentCount.Text = ps.CurrentCount.ToString();
                }
                if (ps.totalCount != 0)
                {
                    lbTotalCount.Text = ps.totalCount.ToString();
                }
                if (ps.CurrentTitle != null)
                {
                   lbProgressTxt.Text = ps.CurrentTitle;
                }
            }
        }


        #endregion

        #region 剪切 粘贴
        //剪切板
        private List<string> pasterTemp = new List<string>();
        //剪切
        private void MenuItemCut_Click(object sender, EventArgs e)
        {
            if (lvFiles.SelectedItems.Count > 0)
            {
                pasterTemp.Clear();
                foreach (ListViewItem item in lvFiles.SelectedItems)
                {
                    FileStatus myfile = item.Tag as FileStatus;
                    if (myfile != null)
                    {
                        pasterTemp.Add(myfile.Path);
                    }
                }
            }
        }
        //粘贴
        private void MenuItemPaste_Click(object sender, EventArgs e)
        {
            if (pasterTemp.Count > 0)
            {
                FSClient client = new FSClient();
                List<string> noPaste = client.MoveFile(pasterTemp.ToArray(), ConfigHelper.HdfsRoot + CurrentPath);
                if (noPaste.Count > 0)
                {
                    try
                    {
                        File.WriteAllText("c:/NoPaste.txt", string.Join("\r\n", pasterTemp.ToArray()));
                    }
                    catch (Exception ee)
                    {
                        logger.Error("粘贴错误", ee);
                    }
                    MessageBox.Show("有" + pasterTemp.Count + "个文件或文件夹没有粘贴成功！请查看c:/NoPaste.txt详细！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                //清除剪切板
                pasterTemp.Clear();
                //重新加载
                LoadFileStatus(CurrentPath);
            }
        }

        #endregion

        #region 右侧文件列表 右键菜单
        //右键菜单
        private void listView1_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                listMenu.Show(lvFiles, e.X, e.Y);
                if (lvFiles.SelectedItems.Count > 0)
                {
                    FileStatus filestatu = lvFiles.SelectedItems[0].Tag as FileStatus;
                    if (filestatu != null)
                    {
                        if (filestatu.Isdir)
                        {
                            menuNewFolder.Enabled = false;
                            menuUpload.Enabled = false;
                            MenuItemUploadFloder.Enabled = false;
                            menuDelete.Enabled = true;
                            menuDownload.Enabled = false;

                            MenuItemCut.Enabled = true;
                            MenuItemPaste.Enabled = false;
                        }
                        else
                        {
                            menuNewFolder.Enabled = false;
                            menuUpload.Enabled = false;
                            MenuItemUploadFloder.Enabled = false;
                            menuDelete.Enabled = true;
                            menuDownload.Enabled = true;
                            MenuItemCut.Enabled = true;
                            MenuItemPaste.Enabled = false;
                        }
                    }
                }
                else
                {
                    menuNewFolder.Enabled = true;
                    menuUpload.Enabled = true;
                    MenuItemUploadFloder.Enabled = true;
                    menuDelete.Enabled = false;
                    menuDownload.Enabled = false;
                    MenuItemCut.Enabled = false;
                    MenuItemPaste.Enabled = pasterTemp.Count > 0;
                }
            }
        }
        //右键 新建
        private void menuNewFolder_Click(object sender, EventArgs e)
        {
            btMkdir_Click(sender, e);
        }
        //右键 刷新
        private void menuRefresh_Click(object sender, EventArgs e)
        {
            tbAddress_KeyUp(sender,new KeyEventArgs(Keys.Enter));
        }
        //右键 下载
        private void menuDownload_Click(object sender, EventArgs e)
        {
            btDownLoad_Click(sender, e);
        }
        //右键 上传
        private void menuUpload_Click(object sender, EventArgs e)
        {
            btUpload_Click(sender, e);
        }
        //右键 删除
        private void menuDelete_Click(object sender, EventArgs e)
        {
            btDelete_Click(sender, e);
        }
        #endregion
    }
    #region 辅助类
    internal class DownloadArg
    {
        public DownloadArg()
        {
            FileList = new List<FileStatus>();
        }
        public List<FileStatus> FileList { get; set; }
        public string SavePath { get; set; }
        /// <summary>
        /// 以什么为文件名，0文件名，1主题名，2主题文件夹
        /// </summary>
        public int OutFileType { get; set; }
    }

    internal class UploadArg
    {
        public UploadArg()
        {
            FilePathList = new List<string>();
        }
        public List<string> FilePathList { get; set; }
        public string LocalFileRoot { get; set; }
    }

    #endregion
}
