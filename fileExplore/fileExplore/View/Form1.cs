using System;
using Nest;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Collections;
using fileExplore.Dao;
using Microsoft.VisualBasic;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using System.Text;

namespace fileExplore
{
    public partial class Form1 : Form
    {
        // constant
        string dataCheck = "dataforCheck11231asasdasdqweadaw";

        List<fileInfo> ListJson = new List<fileInfo>();

        // FileSystemWatcher
        FileSystemWatcher[] fileSystemWatchers;
        // fix duplicate change event
        static private Hashtable fileWriteTime = new Hashtable();
        //static string[] ignoreFolders = { "$RECYCLE.BIN", "\\elasticsearch\\", "\\kibana-elasticsearch\\" };
        bool isProcessRunning = false; // cái này là của process bar 
        ProgressDialog progressBar = new ProgressDialog();// cái này là của process bar 
        static fileDao dao = new fileDao();
        //list kiểm tra dã bấm vào cây hay chưa
        List<string> listPath = new List<string>();
        //biến dùng để luuw lại thông tin của parent root trước đó
        DirectoryInfo parentDirInfo;
        public Form1()
        {
            InitializeComponent();
            PopulateTreeView();
            bool checkExitsData = dao.CheckExits(dataCheck);
            if (!checkExitsData)
            {
                ListJson.Add(new fileInfo()
                {
                    name = dataCheck,
                    path = "",
                    content = "" // cái chỗ này sẽ đọc nội dung file ra nhưng chưa làm tới 
                });

                Task subThreadForGetAllFile = new Task(() => getAllFileInDriver());

                subThreadForGetAllFile.Start(); // cho tiến trình tìm file chạy 1 thread khác 
                //progressBar.ShowDialog();
            }
            else // choox này sẽ xoắ đi khi hoàn tất #################################################
            {
                txtInfo.Visible = false;
                MessageBox.Show(" data da ton taij");
                //btnSearch.Enabled = true;
            }

            this.treeViewEx.NodeMouseClick += new TreeNodeMouseClickEventHandler(this.treeViewEx_NodeMouseClick);
        }


        private void Form1_Load(object sender, EventArgs e)
        {
          
            //----------File system watcher: cập nhật thông tin khi có thay đổi file
            // get all drive in computer
            string[] drives = Environment.GetLogicalDrives();

            // filter file types
            string[] extensions = { "*.txt", "*.doc", "*.docx", "*.pdf" };

            // init fileSystemWatcher for each drive
            fileSystemWatchers = new FileSystemWatcher[drives.Length * extensions.Length];

            int i = 0;
            foreach (string strDrive in drives)
            {
                if (!Directory.Exists(strDrive))
                {
                    continue;
                }
                   

                // will be a fileSystemWatcher of each file type. B/c fileSystemWatcher don't support Filters in .Net Framework
                try
                {
                    foreach (string etx in extensions)
                    {
                        FileSystemWatcher watcher = new FileSystemWatcher(strDrive)
                        {
                            Filter = etx,
                            EnableRaisingEvents = true,
                            IncludeSubdirectories = true
                        };
                        // Will update when there is a change
                        watcher.NotifyFilter = NotifyFilters.Attributes
                                         | NotifyFilters.CreationTime
                                         | NotifyFilters.DirectoryName
                                         | NotifyFilters.FileName
                                         | NotifyFilters.LastWrite
                                         | NotifyFilters.Security
                                         | NotifyFilters.Size;

                        watcher.Changed += OnChanged;
                        watcher.Created += OnCreated;
                        watcher.Deleted += OnDeleted;
                        watcher.Renamed += OnRenamed;

                        fileSystemWatchers[i] = watcher;
                        i++;
                    }

                }
                catch (ArgumentException)
                {

                }
              
            }
            //END File system watcher-------
        }
        public static string ReadFile(string path)
        {
            string content;
            try
            {
                if (!path.Contains(".tmp")){// bỏ qua file tmp khi word đang được thay đổi
                    content = File.ReadAllText(path);
                    return content;
                }
  
            }
            catch (FileNotFoundException)
            {

            }
            catch (UnauthorizedAccessException)
            {

            }
            catch (IOException)
            {

            }
            return "";
        }

        // tiến hành chạy để lấy file gửi lên server 
        public void getAllFileInDriver()
        {

            DirectoryInfo info = new DirectoryInfo(@"G:\ahihihi");
            if (IsHandleCreated)
            {
                btnSearch.Invoke(new Action(() => { btnSearch.Enabled = false; })); //đồng bộ để có thể thiết lập disble cho button 
            }
          
            if (info.Exists)
            {
                Task task = new Task(() => RecursiveGetFile(info.GetDirectories()));
                task.Start();
                GetFileInFolder(info);
                task.Wait();
            }



            // dưới này là chạy tất cả file trên hệ thống, nếu muốn test có thể mở comment dưới này và đống đống code bên trên lại để thử, hiện tại thử trên 1 folder nào đó nhỏ cho nhanh
            /*  var ListDriverInfor = DriveInfo.GetDrives();
              btnSearch.Invoke(new Action(() => { btnSearch.Enabled = false; }));
              for (int i = 0; i < ListDriverInfor.Length; i++)
              {
                  DirectoryInfo info = new DirectoryInfo(ListDriverInfor[i].Name);
                  //Debug.WriteLine(i+" "+ info.GetDirectories().Length);
                  //progressBar.UpdateProgress(i, info.GetDirectories().Length);

                  if (info.Exists)
                  {

                      Task task = new Task(() => RecursiveGetFile(info.GetDirectories()));
                      task.Start();// trong thread của tiến trình lấy all file tạo ra 1 thread khác để có thể xử lý bất đồng bộ

                      GetFileInFolder(info);// riêng cho thread này để ko ảnh hưởng đến thread main 
                      task.Wait(); // xử lý bất đồng bộ, buộc phải đợi thread hiện tại trong subThreadForGetAllFile chạy xong mới tạo mới thread khác 

                  }


              }*/
            /* if (progressBar.InvokeRequired)
                 progressBar.BeginInvoke(new Action(() => progressBar.Close()));

             isProcessRunning = false;*/
            if (IsHandleCreated)
            {
                txtInfo.Invoke(new Action(() => txtInfo.Visible = false));
                btnSearch.Invoke(new Action(() => { btnSearch.Enabled = true; }));
            }


            var bulkIndexResponse = dao.AddList(ListJson);
            if (bulkIndexResponse)
            {
                txtInfo.Invoke(new Action(() => txtInfo.Visible = false));
                btnSearch.Invoke(new Action(() => { btnSearch.Enabled = true; }));
                MessageBox.Show("them thanh cong");
            }


        }

        public void RecursiveGetFile(DirectoryInfo[] subDirs)
        {
            DirectoryInfo[] subSubDirs;

            int MaxValue = subDirs.Length;
            //foreach (DirectoryInfo subDir in subDirs) // bắt đầu tìm kiếm trong từng ổ đĩa 
            for (int i = 0; i< subDirs.Length;i++)
            {

                GetFileInFolder(subDirs[i]);
                try
                {
                    subSubDirs = subDirs[i].GetDirectories();

                    if (subSubDirs.Length != 0)
                    {

                        RecursiveGetFile(subSubDirs);// cái này gọi là đệ quy sau khi tìm xong 1 folder sẽ tiếp tục tìm kiếm lại trong folder con của folder đó xem có còn file hay folder nào nữa ko 
                                                   // bắt sự kiện click thì mới gọi đệ quy 
                    }

                }
                catch (UnauthorizedAccessException)
                {

                }
                catch (IOException)
                {

                }
            }

        }

        private List<fileInfo> GetFileInFolder(DirectoryInfo subDir)
        {

            // cứ khoảng 100 data thì gửi lên elasstic và xóa data trong list ( tránh tràn bộ nhớ nếu gửi lên 1 lần) 
            if (ListJson.Count > 100)
            {
                var bulkIndexResponse = dao.AddList(ListJson);
                ListJson.Clear();
            }
            try
            {
                foreach (FileInfo file in subDir.GetFiles())
                {
                  
                    // đọc và lấy ra những path có định dạng file là txt, doc, pdf
                    if (file.Extension == ".txt" || file.Extension == ".docx" || file.Extension == ".pdf")
                    {
                        string content = "";
                        if(file.Extension == ".pdf")
                        {
                            Debug.WriteLine("da vao");
                            content = GetTextFromPDF(file.FullName);
                        }
                        else
                        {
                            content = File.ReadAllText(file.FullName);
                        }
     
                        ListJson.Add(new fileInfo()
                        {
                            name = file.Name,
                            path = file.FullName,
                            content = content // cái chỗ này sẽ đọc nội dung file ra nhưng chưa làm tới 
                        });

                        Debug.WriteLine(file.Name + "path = " + file.FullName);

                        
                    }

                }
            }
            catch (UnauthorizedAccessException)
            {

            }
            catch (IOException)
            {

            }
            return ListJson;

        }

        // hàm của tree view
        private void PopulateTreeView()
        {
            // khởi tạo root gốc trong tree node 
            TreeNode rootNode;

            var ListDriverInfor = DriveInfo.GetDrives();// lây tất cả các ổ đĩa ( các ổ đia trong máy, ko bao gồm các file trong ổ đĩa)
            foreach (DriveInfo drive in ListDriverInfor) // bắt đầu tìm kiếm trong các ổ đĩa để lấy ra các folder và các file 
            {
                string path = drive.Name.ToString();
                DirectoryInfo info = new DirectoryInfo(path);

                if (info.Exists)
                {
                    GetFileInFolder(info);
                    rootNode = new TreeNode(info.Name);// nếu như có tồn tại thư mục con năm trong path ( path là đường dẫn vd khi bắt đầu với ổ c path sẽ là C) 
                    rootNode.ImageIndex = 2;// gắn image cho root node ( đây là image dành cho ổ đĩa c d e ... , các folder được gắn mặc định ) 
                    rootNode.Tag = info;
                    treeViewEx.Nodes.Add(rootNode);// thêm root node vào tree view để tạo ra nhánh của 1 ổ đĩa 
                }
            }
        }

        // hàm lấy tất cả các file và folder con( ko đệ quy )
        private void GetDirectories(DirectoryInfo[] subDirs, TreeNode nodeToAddTo)
        {
         
            TreeNode aNode;
            //DirectoryInfo[] subSubDirs;
         
            foreach (DirectoryInfo subDir in subDirs) // bắt đầu tìm kiếm trong từng ổ đĩa 
            {
                aNode = new TreeNode(subDir.Name, 0, 0);
                aNode.Tag = subDir;
                aNode.ImageKey = "folder";

                try
                {
                    nodeToAddTo.Nodes.Add(aNode);// add folder vào ổ đĩa 
                }
                catch (UnauthorizedAccessException)
                {

                }
                catch (IOException)
                {

                }
            }
        }



        // thiết lập tree view mỗi khi bấm vào thì list view sẽ chuyển theo ứng vs tree view
        // khi bấm vào thì đồng thời gọi hàm GetDirectories để tìm kiếm các hàm con bên trong
        private void treeViewEx_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            TreeNode newSelected = e.Node;
            DirectoryInfo nodeDirInfo = (DirectoryInfo)newSelected.Tag;
            parentDirInfo = nodeDirInfo; // lưu lại parent root để truy suất ngược lại khi cần ( dùng khi muốn load lại listview)
            
            try
            {
                // kiểm tra xem nếu nhánh của cây đã được bấm vào rồi thì khi bấm vào lần 2 trở lên sẽ ko gọi để quy nữa ( tránh tạo ra nhiều nhánh trùng)
                if (!listPath.Contains(nodeDirInfo.FullName))
                {
                    // nếu không có trong list thì add vào list
                    listPath.Add(nodeDirInfo.FullName);
                    // bấm vào sẽ tìm kiếm các file và folder con 
                    GetDirectories(nodeDirInfo.GetDirectories(), newSelected);
                }

                // gắn file vào folder con vào list vỉew
                AddItemToListView(nodeDirInfo);

                //listView1.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
            }
            catch(UnauthorizedAccessException)
            {

            }
            catch (IOException)
            {

            }

        }

        private void treeViewEx_AfterSelect(object sender, TreeViewEventArgs e)
        {

        }

        // hiện tại sẽ viết tạm ở phần dưới này các chức năng như xóa sửa 
       
        //--- file system watcher
        private static void OnChanged(object sender, FileSystemEventArgs e)
        {
            try
            {    
                // không cập nhật trong những ignoreFolder ------ sẽ cập nhật cách kiểm tra "sạch hơn" sau
                var serviceLocation = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
                bool ignoreFolder = e.FullPath.Contains(serviceLocation)
                                    || e.FullPath.Contains("$RECYCLE.BIN")
                                    || e.FullPath.Contains("D:\\Server\\elasticsearch-7.16.0-windows-x86_64")
                                    || e.FullPath.Contains("D:\\Server\\kibana-7.16.0-windows-x86_64")
                                    || e.FullPath.Contains("G:\\elasticsearch-7.15.1");
                Debug.WriteLine(serviceLocation);
                Debug.WriteLine(ignoreFolder);
                if (!ignoreFolder)
                {
                    // sữa lỗi ghi 2 lần một thông tin
                    var path = e.FullPath;
                    string currentLastWriteTime = File.GetLastWriteTime(e.FullPath).ToString();
                    if (!fileWriteTime.ContainsKey(path) ||
                        fileWriteTime[path].ToString() != currentLastWriteTime
                        )
                    {
                        string[] pathIncludeName = e.Name.Split('\\');// name này bao gồm cả folder trước nó nên cần tách ra lấy name 
                        string name = pathIncludeName[pathIncludeName.Length - 1];
                        fileInfo f = new fileInfo();
                        f.name = name;
                        f.path = path;
                        f.content = ReadFile(path);
                        var id = dao.GetId(e.FullPath);
                        if (id != null)
                        {
                           var is_success = dao.Update(f, id);
                            if (is_success)
                            {
                                MessageBox.Show(" thanh cong");
                            }
                        }
                              
                        fileWriteTime[path] = currentLastWriteTime;
                    }
                }
            }
            catch (FileNotFoundException)
            {

            }
       
            
        }

        private static void OnCreated(object sender, FileSystemEventArgs e)
        {
            var serviceLocation = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            bool ignoreFolder = e.FullPath.Contains(serviceLocation)
                                || e.FullPath.Contains("$RECYCLE.BIN")
                                || e.FullPath.Contains("\\elasticsearch\\")
                                || e.FullPath.Contains("\\kibana-elasticsearch\\")
                                || e.FullPath.Contains("\\ASUS\\ASUS")
                                || e.FullPath.Contains("G:\\elasticsearch-7.15.1");
            if (!ignoreFolder)
            {
                var path = e.FullPath;
                string currentLastWriteTime = File.GetLastWriteTime(e.FullPath).ToString();
                if (!fileWriteTime.ContainsKey(path) ||
                    fileWriteTime[path].ToString() != currentLastWriteTime
                    )
                {
                    // ghi lên elastic ở đây
                    string[] pathIncludeName = e.Name.Split('\\');// name này bao gồm cả folder trước nó nên cần tách ra lấy name 
                    string name = pathIncludeName[pathIncludeName.Length - 1];
                    fileInfo fileUpload = new fileInfo();
                    fileUpload.name = name;
                    fileUpload.path = path;
                    fileUpload.content = File.ReadAllText(path);
                    dao.Add(fileUpload);

                    fileWriteTime[path] = currentLastWriteTime;
                }
            }

        }

        private static void OnDeleted(object sender, FileSystemEventArgs e)
        {
            var serviceLocation = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            bool ignoreFolder = e.FullPath.Contains(serviceLocation)
                                || e.FullPath.Contains("$RECYCLE.BIN")
                                || e.FullPath.Contains("C:\\Users\\Tramm\\Downloads\\Programs\\ElasticSearch\\elasticsearch-7.16.1-windows-x86_64")
                                || e.FullPath.Contains("C:\\Users\\Tramm\\Downloads\\Programs\\ElasticSearch\\kibana-7.16.1-windows-x86_64")
                                || e.FullPath.Contains("G:\\elasticsearch-7.15.1");
            if (!ignoreFolder)
            {
                var path = e.FullPath;
                string currentLastWriteTime = File.GetLastWriteTime(e.FullPath).ToString();
                if (!fileWriteTime.ContainsKey(path) ||
                    fileWriteTime[path].ToString() != currentLastWriteTime
                    )
                {
                    // xóa trên elastic ở đây
                    MessageBox.Show(e.FullPath + " Delete");
                    var id = dao.GetId(path);
                    if(id != null)
                    {
                        dao.Deleted(id);
                    }
            
                    //---
                    fileWriteTime[path] = currentLastWriteTime;
                    fileWriteTime[path] = currentLastWriteTime;
                }
            }
        }
        private static void OnRenamed(object sender, RenamedEventArgs e)
        {
            var msg = $"Renamed: Old: {e.OldFullPath} New: {e.FullPath} {System.Environment.NewLine}";


            var serviceLocation = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            bool ignoreFolder = e.FullPath.Contains(serviceLocation)
                                || e.FullPath.Contains("$RECYCLE.BIN")
                                || e.FullPath.Contains("\\Admin\\AppData\\")
                                || e.FullPath.Contains("D:\\Server\\elasticsearch-7.16.0-windows-x86_64")
                                || e.FullPath.Contains("G:\\elasticsearch-7.15.1");
            if (!ignoreFolder)
            {
                var path = e.FullPath;
                string currentLastWriteTime = File.GetLastWriteTime(e.FullPath).ToString();
                if (!fileWriteTime.ContainsKey(path) ||
                    fileWriteTime[path].ToString() != currentLastWriteTime
                    )
                {
                    string[] pathIncludeName = e.Name.Split('\\');// name này bao gồm cả folder trước nó nên cần tách ra lấy name 
                    string name = pathIncludeName[pathIncludeName.Length-1];// sau khi split sẽ ra được mảng chứa name( name luôn nằm ở vị trí cuối cùng )
                    fileInfo fileUpload = new fileInfo();
                    fileUpload.name = name;
                    fileUpload.path = path;
                    var id = dao.GetId(e.OldFullPath);
                    fileUpload.content = ReadFile(path);
                    if (id != null)
                    {
                        dao.Update(fileUpload, id);
                    }

                    MessageBox.Show(e.FullPath + " Rename");


                    //---
                    fileWriteTime[path] = currentLastWriteTime;
                }
            }
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        // Viết chức năng tìm kiếm
        private void btnSearch_Click(object sender, EventArgs e)
        {
            listView1.Items.Clear();
            string test = txtFind.Text;
            var search = dao.Search(test);
            foreach (var data in search)
            {
                ListViewItem item1 = new ListViewItem();
                item1.Text = data.name;
                item1.ImageIndex = 0;
                item1.SubItems.Add(new ListViewItem.ListViewSubItem() { Text = "" });
                item1.SubItems.Add(new ListViewItem.ListViewSubItem() { Text = "" });
                item1.SubItems.Add(new ListViewItem.ListViewSubItem() { Text = data.path });
                listView1.Items.Add(item1);
            }
        }

        private void txtPath_TextChanged(object sender, EventArgs e)
        {

        }

        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void renameToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count > 0)
            {
                listView1.SelectedItems[0].BeginEdit();// cho phép edit trên listview
            }
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult dialogResult = MessageBox.Show("Bạn có muốn xóa?",
                   "Delete",
                   MessageBoxButtons.YesNo,
                   MessageBoxIcon.Exclamation
               );
            if (dialogResult == DialogResult.Yes)
            {
                if (listView1.SelectedItems.Count > 0)
                {
                    Debug.WriteLine(parentDirInfo);
                    int index = listView1.SelectedItems[0].Index;
                    string path = listView1.Items[index].SubItems[3].Text;
                    string type = listView1.Items[index].SubItems[1].Text;
                    if (type == "Directory")
                    {
                        System.IO.Directory.Delete(path, true);
                        listView1.Refresh();
                        AddItemToListView(parentDirInfo);
                    }
                    else
                    {
                        File.Delete(path);
                        AddItemToListView(parentDirInfo);
                    }
                }
            }
         
        }

        private void listView1_AfterLabelEdit(object sender, LabelEditEventArgs e)
        {
            string oldname = listView1.Items[listView1.SelectedIndices[0]].SubItems[0].Text;
            string oldPath = listView1.Items[listView1.SelectedIndices[0]].SubItems[3].Text;
            string pathNotIncludeName = oldPath.Substring(0, oldPath.Length - oldname.Length);
            string newName = e.Label;
            if (string.IsNullOrEmpty(newName))
            {
                e.CancelEdit = true;
                MessageBox.Show("Please enter a valid value.");
                return;
            }

            Debug.WriteLine(newName);
            System.IO.File.Move(@"" + oldPath, @"" + pathNotIncludeName + newName);
            AddItemToListView(parentDirInfo);
        }
        
        public void AddItemToListView(DirectoryInfo nodeDirInfo)
        {
            listView1.Items.Clear();
            ListViewItem.ListViewSubItem[] subItems;
            ListViewItem item = null;

            foreach (DirectoryInfo dir in parentDirInfo.GetDirectories())
            {
                item = new ListViewItem(dir.Name, 0);
                subItems = new ListViewItem.ListViewSubItem[]
                    {new ListViewItem.ListViewSubItem(item, "Directory"),
                        new ListViewItem.ListViewSubItem(item,
                            dir.LastAccessTime.ToShortDateString()),
                        new ListViewItem.ListViewSubItem(item,dir.FullName)}; // thêm dòng này
                item.SubItems.AddRange(subItems);
                listView1.Items.Add(item);
            }
            foreach (FileInfo file in parentDirInfo.GetFiles())
            {
                item = new ListViewItem(file.Name, 1);
                subItems = new ListViewItem.ListViewSubItem[]
                    { new ListViewItem.ListViewSubItem(item, "File"),
                        new ListViewItem.ListViewSubItem(item,
                        file.LastAccessTime.ToShortDateString()),
                        new ListViewItem.ListViewSubItem(item,file.FullName)};

                item.SubItems.AddRange(subItems);
                listView1.Items.Add(item);
            }
        }
        // tạo file
        private void newFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(parentDirInfo != null)
            {
                string path = parentDirInfo.FullName;
                string fileName = Interaction.InputBox("Enter file's name", "Create new file", "New Text Document.txt", 400, 300);
                if (fileName != null)
                {
                    // kiểm tra xem có file nào có tên vừa nhập vào new file hay chưa 
                    foreach (FileInfo file in parentDirInfo.GetFiles())
                    {
              
                        if (file.Name == fileName)
                        {   
                            // nếu có thì dừng 
                            MessageBox.Show("File's name is exist");
                            return;
                        
                        }
                    }
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        var created = File.Create($@"{path}\{fileName}");
                        created.Close(); // create file bằng File bắt buộc phải close nếu koi thì sẽ ko thể sử dụng ở nơi khác do process đang được sử dụng
                        AddItemToListView(parentDirInfo);
                    }
                    else 
                    {
                        return;
                    }

                }

            }

        }
        // tạo folder
        private void newFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (parentDirInfo != null)
            {
                string path = parentDirInfo.FullName;
                string fileName = Interaction.InputBox("Enter file's name", "Create new file", "New folder", 400, 300);
                string newFolderPath = $@"{path}\{fileName}";
                bool exists = System.IO.Directory.Exists(newFolderPath);
                if (!exists)
                {
                   var folder =  Directory.CreateDirectory(newFolderPath);
                    AddItemToListView(parentDirInfo);
                }
                else
                {
                    MessageBox.Show("Folder exists");
                    return;
                }
            }
        }

        private void listView1_DoubleClick(object sender, EventArgs e)
        {
            int index = listView1.SelectedItems[0].Index;
            string path = listView1.Items[index].SubItems[3].Text;
            Process.Start(path);
        }
        private string GetTextFromPDF(string path)
        {
            PdfReader reader = new PdfReader(path);
            string text = string.Empty;
            for (int page = 1; page <= reader.NumberOfPages; page++)
            {
                text += PdfTextExtractor.GetTextFromPage(reader, page);
            }
            reader.Close();

            return text;
        }
    }
}
