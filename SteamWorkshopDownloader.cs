using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Steamworks;

namespace StellarisPlaysetSync
{
    // singleton for downloading steam mods
    public class SteamWorkshopDownloader
    {
        private bool _isConnected = false;
        public bool isConnected
        {
            get { return _isConnected; }
        }
        public List<ulong> InstalledMods { get; set; } 
        private PopupWindow popupWindow { get; set; }
        private MainWindow mainWindow { get; set; }
        // internal sleep for waiting cb's
        private TaskCompletionSource<bool> __iamdone;
        // thread for the runcallback loop
        private Thread __rcbLoop { get; set; }
        private ObservableCollection<string> StatusList { get; set; }
        // TextToAccumilateFromOtherThread. yes, i have to do this because the callback func is on another thread (cannot call StatusList.Add()) on it
        private string __TTAFOT { get; set; }

        public SteamWorkshopDownloader()
        {
            InstalledMods = new List<ulong>();
            StatusList = new ObservableCollection<string>();
            _isConnected = SteamAPI.Init();
            if (_isConnected)
            {
                fetchInsalledMods();
                __rcbLoop = new Thread(DangerousLoop);
                __rcbLoop.Start();
            }
        }

        private void DangerousLoop()
        {
            while(true)
            {
                if (!_isConnected)
                {
                    return; // break the loop forever
                }
                SteamAPI.RunCallbacks();
                Thread.Sleep(500); // 5ms delay
            }
        }

        public void Shutdown()
        {
            if (_isConnected)
            {
                _isConnected = false;
                SteamAPI.Shutdown();
            }
        }

        public void fetchInsalledMods()
        {
            uint maxsubbed = SteamUGC.GetNumSubscribedItems();
            PublishedFileId_t[] published_table = new PublishedFileId_t[maxsubbed];
            SteamUGC.GetSubscribedItems(published_table, maxsubbed);
            InstalledMods.Clear();
            foreach (ulong id in published_table)
            {
                if (!SteamUGC.GetItemInstallInfo((PublishedFileId_t)id, out ulong _, out string _, 42069, out uint _))
                {
                    continue;
                }
                InstalledMods.Add(id);
            }
        }

        public async Task InstallMissingModsAsync(List<ulong> mods, MainWindow mainwindow, PopupWindow popupwindow)
        {
            if (mods.Count <= 0)
            {
                return;
            }
            mainWindow = mainwindow;
            popupWindow = popupwindow;
            StatusList.Clear();
            mainWindow.IsEnabled = false;
            popupWindow.Show();
            popupWindow.underControl = true;
            popupWindow.download_bar.Value = 0;
            popupWindow.download_bar.Maximum = mods.Count;
            popupWindow.download_errorlist.ItemsSource = StatusList;
            foreach (ulong item in mods)
            {
                popupWindow.download_text.Text = "Downloading: " + item.ToString() + " (" +  Math.Round((popupWindow.download_bar.Value / popupWindow.download_bar.Maximum) * 100, 2).ToString() + "%)";
                SteamUGC.SubscribeItem((PublishedFileId_t)item); // the downloaditem doesnt subscribe!
                bool valid = SteamUGC.DownloadItem((PublishedFileId_t)item, true);
                if (valid)
                {
                    // https://partner.steamgames.com/doc/features/workshop/implementation
                    // todo: properly implement this
                    // currently, this wait system works because the callback invokes when we finished downloading
                    // if we tried to do this (1) and it's already done, we return with a info download of 0bytes
                    // future implementation would be a while true checking if k_EItemStateNeedsUpdate is true on the item
                    // (1) -> SteamUGC.GetItemDownloadInfo((PublishedFileId_t)item, out ulong bytesDownloaded, out ulong bytesTotal);
                    __iamdone = new TaskCompletionSource<bool>();
                    Callback<DownloadItemResult_t>.Create(SubscribeCallback);
                    // wait for task to return, then go to next loop (managing multiple dl is a clusterfuck)
                    await __iamdone.Task;
                    __iamdone.Task.Dispose();
                } 
                else
                {
                    __TTAFOT = "Invalid Mod ID! " + item.ToString();
                }
                popupWindow.download_bar.Value++;
                StatusList.Add(__TTAFOT);
                __iamdone = null;
            }
            popupWindow.download_text.Text = "Download finished.";
            popupWindow.underControl = false;
            mainWindow.IsEnabled = true;
            fetchInsalledMods(); // reload
            mainWindow.UpdateModList();
        }

        private void SubscribeCallback(DownloadItemResult_t callback)
        {
            // make sure to listen only on stellaris downloads
            if (__iamdone == null || __iamdone.Task.IsCompleted || Convert.ToInt32(callback.m_unAppID.ToString()) != 281990)
            {
                return; // iirc yes, if you delay it too much it invokes 2 callbacks instead of one. for some fucking reason
            }
            __TTAFOT = (callback.m_eResult != EResult.k_EResultOK && callback.m_eResult != EResult.k_EResultAdministratorOK) ? "Errored: " : "Downloaded: " + callback.m_nPublishedFileId.ToString() + " - " + callback.m_eResult.ToString();
            __iamdone.TrySetResult(true);
        }
    }
}
