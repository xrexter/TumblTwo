﻿namespace TumblOne
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    //using System.ComponentModel;
    using System.Diagnostics;
    //using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Net;
    //using System.Runtime.InteropServices;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.Formatters.Binary;
    using System.Threading;
    using System.Threading.Tasks;
    //using System.Threading.Tasks.Schedulers;
    using System.Windows.Forms;
    using System.Xml.Linq;

    public partial class Form1 : Form
    {
        private List<TumblrBlog> TumblrActiveList = new List<TumblrBlog>();
        string crawlingBlogs = "";
        //public TumblrBlog blog;
        private Task worker;
        private BlockingCollection<TumblrBlog> bin = new BlockingCollection<TumblrBlog>();
        //private QueuedTaskScheduler qts = new QueuedTaskScheduler(TaskScheduler.Default, 2); 
        private CancellationTokenSource cts = new CancellationTokenSource();

        public Form1()
        {
            this.InitializeComponent();
            this.LoadLibrary();
        }

        private void AddBlog(object sender, EventArgs e)
        {
            ListViewItem lvItem;
            string str = this.ExtractBlogname(this.tBlogUrl.Text);
            if (str != null)
            {
                foreach (ListViewItem item in this.lvBlog.Items)
                {
                    if (item.Text.Equals(str))
                    {
                        MessageBox.Show("Entered Url is always in Library!", Application.ProductName);
                        this.tBlogUrl.Text = string.Empty;
                        return;
                    }
                }
                lvItem = new ListViewItem();
                if ((this.worker != null) && !this.worker.IsCompleted)
                {
                    TumblrBlog ThreadBlog = new TumblrBlog();
                    this.Invoke((Action)delegate
                    {
                        ThreadBlog._URL = this.ExtractUrl(this.tBlogUrl.Text);
                        ThreadBlog.TOTAL_COUNT = 0;
                        ThreadBlog._Name = this.ExtractBlogname(this.tBlogUrl.Text);
                        ThreadBlog._DateAdded = DateTime.Now;
                        lvItem.Text = ThreadBlog._Name;
                        lvItem.SubItems.Add("");
                        lvItem.SubItems.Add(this.tBlogUrl.Text);
                        lvItem.SubItems.Add(ThreadBlog._DateAdded.ToString("G"));
                        lvItem.SubItems.Add(ThreadBlog._LastCrawled.ToString("G"));
                        lvItem.SubItems.Add(ThreadBlog._finishedCrawl.ToString());
                        this.lvBlog.Items.Add(lvItem);
                    });
                    this.SaveBlog(ThreadBlog);
                    ThreadBlog = null;
                }
                else
                {
                    TumblrBlog newBlog = new TumblrBlog
                    {
                        _Name = str,
                        _URL = this.ExtractUrl(this.tBlogUrl.Text),
                        _DateAdded = DateTime.Now,
                        //_LastCrawled = "",
                        _finishedCrawl = false
                    };
                    lvItem.Text = newBlog._Name;
                    lvItem.SubItems.Add("");
                    lvItem.SubItems.Add(newBlog._URL);
                    lvItem.SubItems.Add(newBlog._DateAdded.ToString("G"));
                    lvItem.SubItems.Add(newBlog._LastCrawled.ToString("G"));
                    lvItem.SubItems.Add(newBlog._finishedCrawl.ToString());
                    this.lvBlog.Items.Add(lvItem);
                    this.SaveBlog(newBlog);
                    newBlog = null;
                }
                this.tBlogUrl.Text = "http://";
                if (Directory.Exists(Properties.Settings.Default.configDownloadLocation.ToString() + "Index/"))
                {
                    if (Directory.GetFiles(Properties.Settings.Default.configDownloadLocation.ToString() + "Index/", "*.tumblr").Count<string>() > 0)
                    {
                        this.toolShowExplorer.Enabled = true;
                        this.toolRemoveBlog.Enabled = true;
                        this.toolCrawl.Enabled = true;
                    }
                    else
                    {
                        this.toolShowExplorer.Enabled = false;
                        this.toolRemoveBlog.Enabled = false;
                        this.toolCrawl.Enabled = false;
                    }
                }
            }
        }

        private bool CreateDataFolder(string blogname = null)
        {
            if (blogname == null)
            {
                return false;
            }
            string path = "./Blogs";
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                if (!Directory.Exists(path + "/Index"))
                {
                    Directory.CreateDirectory(path + "/Index");
                }
                if (!Directory.Exists(path + "/" + blogname))
                {
                    Directory.CreateDirectory(path + "/" + blogname);
                }
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        private bool Download(string filename, string url)
        {
            if (!System.IO.File.Exists(filename))
            {
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        client.DownloadFile(url, filename);
                    }
                }
                catch (Exception)
                {
                    return false;
                }
            }
            return true;
        }

        private string ExtractBlogname(string url)
        {
            if ((url == null) || (url.Length < 0x11))
            {
                MessageBox.Show("Incomplete Url detected!", Application.ProductName);
                return null;
            }
            if (!url.Contains(".tumblr.com"))
            {
                MessageBox.Show("No valid Tumblr Url detected!", Application.ProductName);
                return null;
            }
            string[] source = url.Split(new char[] { '.' });
            if ((source.Count<string>() >= 3) && source[0].StartsWith("http://", true, null))
            {
                return source[0].Replace("http://", string.Empty);
            }
            MessageBox.Show("Invalid Url detected!", Application.ProductName);
            return null;
        }

        private string ExtractUrl(string url)
        {
            return ("http://" + this.ExtractBlogname(url) + ".tumblr.com/");
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if ((this.worker != null) && !this.worker.IsCompleted)
            {
                try
                {
                    if (TumblrActiveList.Count != 0)
                    {
                        foreach (TumblrBlog tumblr in TumblrActiveList)
                        {
                            this.SaveBlog(tumblr);
                        }
                    }
                    TumblrActiveList.Clear();
                    if (this.wait_handle != null)
                    {
                        this.wait_handle.Close();
                    }
                    if (cts != null)
                        cts.Cancel();
                }
                catch (ThreadAbortException exception)
                {
                    MessageBox.Show("Process stopped by User. " + exception.Message, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
                this.worker = null;
                this.wait_handle = null;
            }
            // Save Settings
            Properties.Settings.Default.Save();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            using (SplashScreen screen = new SplashScreen())
            {
                screen.ShowDialog();
            }
        }



        private TumblrBlog LoadBlog(string blogname)
        {
            TumblrBlog blog = new TumblrBlog();
            try
            {
                using (Stream stream = new FileStream(Properties.Settings.Default.configDownloadLocation.ToString() + "Index/" + blogname + ".tumblr", FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    IFormatter formatter = new BinaryFormatter();
                    blog = (TumblrBlog)formatter.Deserialize(stream);
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Hand);
            }
            return blog;
        }

        private void LoadLibrary()
        {
            this.lvBlog.Items.Clear();
            this.lblProcess.Text = "";
            this.lblUrl.Text = "";
            if (Directory.Exists(Properties.Settings.Default.configDownloadLocation.ToString() + "Index/"))
            {
                string[] files = Directory.GetFiles(Properties.Settings.Default.configDownloadLocation.ToString() + "Index/", "*.tumblr");
                if (files.Count<string>() > 0)
                {
                    this.toolShowExplorer.Enabled = true;
                    this.toolRemoveBlog.Enabled = true;
                    this.toolCrawl.Enabled = true;
                }
                else
                {
                    this.toolShowExplorer.Enabled = false;
                    this.toolRemoveBlog.Enabled = false;
                    this.toolCrawl.Enabled = false;
                }
                foreach (string str in files)
                {
                    TumblrBlog blog = this.LoadBlog(Path.GetFileNameWithoutExtension(str));
                    if ((blog != null) && (blog._URL != null))
                    {
                        blog.TOTAL_COUNT = Directory.GetFiles(Properties.Settings.Default.configDownloadLocation.ToString() + blog._Name + "/").Length;
                        ListViewItem item = new ListViewItem
                        {
                            Text = blog._Name
                        };
                        if (blog.TOTAL_COUNT > 0)
                        {
                            item.SubItems.Add(blog.TOTAL_COUNT.ToString());
                        }
                        else
                        {
                            item.SubItems.Add("Not crawled yet!");
                        }
                        item.SubItems.Add(blog._URL);
                        item.SubItems.Add(blog._DateAdded.ToString("G"));
                        item.SubItems.Add(blog._LastCrawled.ToString("G"));
                        item.SubItems.Add(blog._finishedCrawl.ToString());
                        this.lvBlog.Items.Add(item);
                        blog = null;
                    }
                }
            }
        }

        private void mnuExit_Click(object sender, EventArgs e)
        {
            base.Close();
        }

        private void mnuRescanBlog_Click(object sender, EventArgs e)
        {
            //this.worker = new Thread(new ParameterizedThreadStart(this.RunParser));
            cts = new CancellationTokenSource();
            crawlingBlogs = "";
            for (int i = 0; i < Properties.Settings.Default.configSimultaneousDownloads; i++ )
                this.worker = Task.Run(() => runProducer(bin, cts.Token));
            //this.worker.Name = "TumblOne Thread";
            //this.worker.IsBackground = true;
            this.wait_handle = new ManualResetEvent(true);
            //this.worker.Start(text);
            this.panelInfo.Visible = false;
            this.toolPause.Enabled = true;
            this.toolResume.Enabled = false;
            this.toolStop.Enabled = true;
            this.toolCrawl.Enabled = false;
            this.toolRemoveBlog.Enabled = false;
            this.contextBlog.Items[3].Enabled = false;
        }

        private void mnuShowFilesInExplorer_Click(object sender, EventArgs e)
        {
            if (this.lvBlog.SelectedItems.Count > 0)
            {
                try
                {
                    Process.Start("explorer.exe", Application.StartupPath + @"\Blogs\" + this.lvBlog.SelectedItems[0].Text);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(exception.Message, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Hand);
                }
            }
        }

        private void mnuVisit_Click(object sender, EventArgs e)
        {
            if (this.lvBlog.SelectedItems.Count >= 0)
            {
                try
                {
                    Process.Start(this.lvBlog.SelectedItems[0].SubItems[2].Text);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(exception.Message, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Hand);
                }
            }
        }

        private void RemoveBlog(object sender, EventArgs e)
        {
            if ((this.worker != null) && !this.worker.IsCompleted)
            {
                MessageBox.Show("During a active Crawl Process it is not possible to remove a Blog!", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            else if ((this.lvBlog.SelectedItems != null) && (this.lvBlog.SelectedItems.Count != 0))
            {
                string path = Properties.Settings.Default.configDownloadLocation.ToString() + "Index/" + this.lvBlog.SelectedItems[0].Text + ".tumblr";
                string str2 = Properties.Settings.Default.configDownloadLocation.ToString() + this.lvBlog.SelectedItems[0].Text;
                try
                {
                    if (MessageBox.Show("Should the selected Blog really deleted from Library?", Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        if (System.IO.File.Exists(path))
                        {
                            System.IO.File.Delete(path);
                        }
                        Directory.Delete(str2, true);
                        this.LoadLibrary();
                    }
                }
                catch (Exception exception)
                {
                    MessageBox.Show(exception.Message, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Hand);
                }
            }
        }

        private void RunParser(TumblrBlog _blog)
        {
            MethodInvoker method = null;
            MethodInvoker invoker3 = null;
            int num = 0;
            int num2 = 0;
            int num3 = 0;
            //string blogname = ExtractBlogname(ApiUrl.ToString());
            String ApiUrl = _blog._URL ;
            if (ApiUrl.Last<char>() != '/')
            {
                ApiUrl = ApiUrl + "/api/read?start=";
            }
            else
            {
                ApiUrl = ApiUrl + "api/read?start=";
            }
            this.CreateDataFolder(_blog._Name);
            this.BeginInvoke((Action)delegate
            {
                this.pgBar.Minimum = 0;
                this.pgBar.Maximum = _blog.TOTAL_COUNT;
            });
            while (true)
            {
                this.wait_handle.WaitOne();
                XDocument document = null;
                try
                {
                    document = XDocument.Load(ApiUrl.ToString() + num.ToString() + "&num=50");
                }
                catch (WebException)
                {
                    //this.toolStop_Click(this, null);
                    break;
                }
                if (num == 0)
                {
                    try
                    {
                        foreach (var type in from data in document.Descendants("posts") select new { Total = data.Attribute("total").Value })
                        {
                            _blog.TOTAL_COUNT = Convert.ToInt32(type.Total.ToString());
                        }
                        if (method == null)
                        {
                            method = delegate
                            {
                                this.pgBar.Minimum = 0;
                                this.pgBar.Maximum = _blog.TOTAL_COUNT;
                            };
                        }
                        this.BeginInvoke(method);
                    }
                    catch
                    {
                        _blog.TOTAL_COUNT = 0;
                        //this.toolStop_Click(this, null);
                        break;
                    }
                }
                using (IEnumerator<XElement> enumerator2 = (from s in document.Descendants("photo-url")
                                                            where (s.HasAttributes && s.Attribute("max-width").Value.Equals(Properties.Settings.Default.configImageSize.ToString())) && !s.Value.Contains("www.tumblr.com")
                                                            select s).GetEnumerator())
                {
                    while (enumerator2.MoveNext())
                    {
                        XElement p = enumerator2.Current;
                        MethodInvoker invoker = null;
                        string FileLocation;
                        this.wait_handle.WaitOne();
                        string fileName = Path.GetFileName(new Uri(p.Value).LocalPath);
                        if (!this.chkGIF.Checked || (Path.GetExtension(fileName).ToLower() != ".gif"))
                        {
                            FileLocation = Properties.Settings.Default.configDownloadLocation.ToString() + _blog._Name + "/" + fileName;
                            _blog.Links.Add(new Post(p.Value, fileName));
                            if (invoker3 == null)
                            {
                                invoker3 = delegate
                                {
                                    foreach (ListViewItem item in this.lvBlog.Items)
                                    {
                                        if (item.Text == _blog._Name)
                                        {
                                            item.SubItems[1].Text = Directory.GetFiles(Properties.Settings.Default.configDownloadLocation.ToString() + _blog._Name + "/").Length.ToString();
                                            break;
                                        }
                                    }
                                };
                            }
                            this.BeginInvoke(invoker3);
                            try
                            {
                                if (this.Download(FileLocation, p.Value))
                                {
                                    num2++;
                                    if (invoker == null)
                                    {
                                        invoker = delegate
                                        {
                                            this.lblUrl.Text = p.Value;
                                            this.smallImage.ImageLocation = FileLocation;
                                            if ((this.pgBar.Value + 1) < (this.pgBar.Maximum + 1))
                                            {
                                                this.pgBar.Value++;
                                            }
                                        };
                                    }
                                    this.BeginInvoke(invoker);
                                }
                            }
                            catch (Exception)
                            {
                                continue;
                            }
                            num3++;
                        }
                    }
                }
                if (num3 == 0)
                {
                    //this.toolStop_Click(this, null);
                    // Finished Blog
                    _blog._LastCrawled = DateTime.Now;
                    _blog._finishedCrawl = true;
                    // Update UI
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        foreach (ListViewItem item in this.lvBlog.Items)
                        {
                            if (item.Text == _blog._Name)
                            {
                                // Update Listview about completed blog
                                item.SubItems[4].Text = DateTime.Now.ToString();
                                item.SubItems[5].Text = "True";
                                // Update current crawling progress label
                                int indexBlogInProgress = crawlingBlogs.IndexOf(_blog._Name);
                                int lengthBlogInProgress = _blog._Name.Length;
                                this.crawlingBlogs = crawlingBlogs.Remove(indexBlogInProgress, (lengthBlogInProgress+1));
                                this.lblProcess.Text = "Crawling Blogs - " + this.crawlingBlogs;
                            }
                        }
                    });
                    return;
                }
                num += num3;
                num3 = 0;
            }
        }

        private bool SaveBlog(TumblrBlog newBlog)
        {
            if (newBlog == null)
            {
                return false;
            }
            this.CreateDataFolder(newBlog._Name);
            try
            {
                using (Stream stream = new FileStream(Properties.Settings.Default.configDownloadLocation.ToString() + "Index/" + newBlog._Name + ".tumblr", FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    IFormatter formatter = new BinaryFormatter();
                    formatter.Serialize(stream, newBlog);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Current Blog cannot saved to Disk!\nBe sure, that u have enough Memory and User Permission...", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return false;
            }
            return true;
        }

        private void toolAbout_Click(object sender, EventArgs e)
        {
            using (SplashScreen screen = new SplashScreen())
            {
                screen.ShowDialog();
            }
        }

        private void toolPause_Click(object sender, EventArgs e)
        {
            this.wait_handle.Reset();
            this.Invoke((Action)delegate
            {
                this.lblProcess.Text = "PAUSE! Click on Resume Button to continue...";
                this.toolPause.Enabled = false;
                this.toolResume.Enabled = true;
                this.toolStop.Enabled = true;
            });
        }

        private void toolResume_Click(object sender, EventArgs e)
        {
            this.wait_handle.Set();
            this.Invoke((Action)delegate
            {
                //Fixme
                this.lblProcess.Text = "Crawling Blogs - " + this.crawlingBlogs;
                this.toolPause.Enabled = true;
                this.toolResume.Enabled = false;
                this.toolStop.Enabled = true;
            });
        }

        private void toolStop_Click(object sender, EventArgs e)
        {
            MethodInvoker method = null;
            try
            {
                this.wait_handle.Reset();
                if (method == null)
                {
                    method = delegate
                    {
                        this.panelInfo.Visible = true;
                        this.lblProcess.Text = "Crawling of " + this.crawlingBlogs + "has stopped!";
                        //FIXME
                        this.lblUrl.Text = "";
                        this.smallImage.ImageLocation = "";
                        this.crawlingBlogs = "";
                        this.pgBar.Value = 0;
                        this.toolPause.Enabled = false;
                        this.toolResume.Enabled = false;
                        this.toolStop.Enabled = false;
                        this.toolCrawl.Enabled = true;
                        this.toolRemoveBlog.Enabled = true;
                        this.contextBlog.Items[3].Enabled = false;
                    };
                }
                this.Invoke(method);
            }
            catch (Exception)
            {
            }
            if (TumblrActiveList.Count != 0)
            {
                foreach (TumblrBlog tumblr in TumblrActiveList)
                {
                    this.SaveBlog(tumblr);
                }
                this.TumblrActiveList.Clear();
                while (bin.Count > 0)
                    {
                        var obj = bin.Take();
                    }
                this.lvQueue.Items.Clear();
            }
            try
            {
                if (cts != null)
                    cts.Cancel();
            }
            catch (ThreadAbortException)
            {
            }
        }

        private void optionsSaved()
        {
            loadPreferences();
        }

        // Load program preferences

        private void loadPreferences()
        {
            if ((this.worker != null) && !this.worker.IsCompleted) 
            {


            }

        }

        private void chkGIF_CheckedChanged(object sender, EventArgs e)
        {
            if (this.chkGIF.Checked)
            {
                Properties.Settings.Default.configChkGIFState = true;
            }
            else
            {
                Properties.Settings.Default.configChkGIFState = false;
            }
        }

        private void toolSettings_Click(object sender, EventArgs e)
        {
            Settings settingsWindow = new Settings(this);
            settingsWindow.Show();
            optionsSaved();
        }

        private void toolAddQueue_Click(object sender, EventArgs e)
        {
            AddBlogtoQueue(bin, cts.Token);
        }

        private void AddBlogtoQueue(BlockingCollection<TumblrBlog> bin, CancellationToken ct) 
        {
            bool success = false;

            // Cancellation causes OCE. 
            try
            {
                if (this.lvBlog.SelectedItems.Count > 0)
                {
                    TumblrBlog blog = this.LoadBlog(this.ExtractBlogname(this.lvBlog.SelectedItems[0].SubItems[2].Text));
                    string text = this.lvBlog.SelectedItems[0].SubItems[2].Text;
                    //blog.Links.Clear();

                    //this.worker = new Thread(new ParameterizedThreadStart(this.RunParser));
                    //success = bin.TryAdd(blog, 2, ct);
                    success = bin.TryAdd(blog);
                    if (success)
                    {
                        this.addToQueueUI(blog);
                    }
                }
            }
            catch (OperationCanceledException exception)
            {
                MessageBox.Show(exception.Message);
            }
        }

        private void runProducer(BlockingCollection<TumblrBlog> bin, CancellationToken ct)
        {
            // IsCompleted == (IsAddingCompleted && Count == 0) 
            while (!bin.IsCompleted)
            {
                TumblrBlog nextBlog;
                try
                {
                    if (!bin.TryTake(out nextBlog, 4000, ct))
                    {

                    }
                    else
                    {
                        TumblrActiveList.Add(nextBlog);

//                        if (TumblrActiveList.Count > Properties.Settings.Default.configSimultaneousDownloads)
//                        {
//                            TumblrBlog finishedBlog = (TumblrList.Find(x => x.Equals(TumblrActiveList.Take(1))));
//                            finishedBlog._LastCrawled = DateTime.Now;
//                            finishedBlog._finishedCrawl = true;
//                        }
//                        else
//                        {
//                        }

                        this.BeginInvoke((MethodInvoker)delegate
                        {
                            // Update UI:
                            // Processlabel
                            this.crawlingBlogs += this.lvQueue.Items[0].Text + " ";
                            this.lblProcess.Text = "Crawling Blogs - " + this.crawlingBlogs;
                            // Queue
                            lvQueue.Items.RemoveAt(0);
                        });

                        this.RunParser(nextBlog);

                        //IEnumerable<TumblrBlog> differenceQuery = this.TumblrList.Except(this.bin);
                        //foreach (TumblrBlog active in differenceQuery)
                        //    foreach (string active._Name  in lvQueue.Items. (active._Name.Equals())
                    }
                }

                catch (OperationCanceledException)
                {
                    Console.WriteLine("Taking canceled.");
                    break;
                }


                // Slow down consumer just a little to cause 
                // collection to fill up faster, and lead to "AddBlocked"
                // Thread.SpinWait(500000);
            }
        }

        private void RemoveBlogFromQueue(BlockingCollection<TumblrBlog> bin, CancellationToken ct)
        {
            // IsCompleted == (IsAddingCompleted && Count == 0) 
            if (bin.Count != 0)
            {
                TumblrBlog nextBlog = null;
                bin.TryTake(out nextBlog, 0, ct);
                this.lvQueue.Items.RemoveAt(0);
                //this.TumblrList.RemoveAt(TumblrList.Count - 1);
                //this.lvQueue.Items.RemoveAt(lvQueue.Items.Count - 1);
            }
        }

        private void toolRemoveQueue_Click(object sender, EventArgs e)
        {
            RemoveBlogFromQueue(bin, cts.Token);
        }

        private void addToQueueUI(TumblrBlog _blog)
        {
            //Update UI
            ListViewItem lvQueueItem = new ListViewItem();
            lvQueueItem.Text = _blog._Name;
            //lvQueueItem.SubItems.Add("");
            lvQueueItem.SubItems.Add("queued");
            this.lvQueue.Items.Add(lvQueueItem);
        }
    }
}

