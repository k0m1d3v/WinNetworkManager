using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using ModernUI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows.Forms;

public partial class MainForm : MetroSuite.MetroForm
{
    private const int AverageWindow = 5;
    private System.Windows.Forms.Timer refreshTimer;
    private System.Windows.Forms.ToolTip toolTip;

    private ConcurrentDictionary<int, long> totalTcpSent = new ConcurrentDictionary<int, long>();
    private ConcurrentDictionary<int, long> totalTcpRecv = new ConcurrentDictionary<int, long>();
    private ConcurrentDictionary<int, long> totalUdpSent = new ConcurrentDictionary<int, long>();
    private ConcurrentDictionary<int, long> totalUdpRecv = new ConcurrentDictionary<int, long>();

    private Dictionary<int, long> lastTcpSent = new Dictionary<int, long>();
    private Dictionary<int, long> lastTcpRecv = new Dictionary<int, long>();
    private Dictionary<int, long> lastUdpSent = new Dictionary<int, long>();
    private Dictionary<int, long> lastUdpRecv = new Dictionary<int, long>();

    private Dictionary<int, Queue<long>> historyTcpIn = new Dictionary<int, Queue<long>>();
    private Dictionary<int, Queue<long>> historyTcpOut = new Dictionary<int, Queue<long>>();
    private Dictionary<int, Queue<long>> historyUdpIn = new Dictionary<int, Queue<long>>();
    private Dictionary<int, Queue<long>> historyUdpOut = new Dictionary<int, Queue<long>>();

    public MainForm()
    {
        InitializeComponent();

        midnightListView1.View = View.Details;
        midnightListView1.ShowGroups = true;
        midnightListView1.FullRowSelect = true;

        refreshTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        refreshTimer.Tick += (s, e) => UpdateProcessList();
        refreshTimer.Start();

        guna2TextBox1.TextChanged += (s, e) => UpdateProcessList();

        // Set initial tooltips for better UX
        toolTip = new System.Windows.Forms.ToolTip();
        toolTip.SetToolTip(guna2TextBox1, "Type to filter processes by name");
        toolTip.SetToolTip(midnightListView1, "Click on a process to view its network connections");
        toolTip.SetToolTip(guna2TextBox2, "TCP connections for the selected process");
        toolTip.SetToolTip(guna2TextBox3, "UDP connections for the selected process");

        new Thread(TrackNetworkEvents) { IsBackground = true }.Start();
        UpdateProcessList();
    }

    private string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] Suffix = { "B", "KB", "MB", "GB" };
        int i;
        double dblSByte = bytes;
        for (i = 0; i < Suffix.Length && bytes >= 1024; i++, bytes /= 1024)
            dblSByte = bytes / 1024.0;
        return $"{dblSByte:0.##} {Suffix[i]}";
    }

    public void TrackNetworkEvents()
    {
        try
        {
            string sessionName = "NetworkMonitorSession_" + Guid.NewGuid().ToString();
            using (var session = new TraceEventSession(sessionName))
            {
                session.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);

                session.Source.Kernel.TcpIpSend += data =>
                    totalTcpSent.AddOrUpdate(data.ProcessID, data.size, (_, old) => old + data.size);
                session.Source.Kernel.TcpIpRecv += data =>
                    totalTcpRecv.AddOrUpdate(data.ProcessID, data.size, (_, old) => old + data.size);
                session.Source.Kernel.UdpIpSend += data =>
                    totalUdpSent.AddOrUpdate(data.ProcessID, data.size, (_, old) => old + data.size);
                session.Source.Kernel.UdpIpRecv += data =>
                    totalUdpRecv.AddOrUpdate(data.ProcessID, data.size, (_, old) => old + data.size);

                session.Source.Process();
            }
        }
        catch (Exception ex) { Debug.WriteLine("Errore ETW: " + ex.Message); }
    }

    private void UpdateProcessList()
    {
        string filter = (guna2TextBox1.Text ?? string.Empty).Trim();
        string topKey = (midnightListView1.TopItem != null) ? ItemKey(midnightListView1.TopItem) : null;
        string selectedKey = (midnightListView1.SelectedItems.Count > 0) ? ItemKey(midnightListView1.SelectedItems[0]) : null;

        midnightListView1.BeginUpdate();
        try
        {
            Process[] allProcs = Process.GetProcesses();
            var procs = string.IsNullOrEmpty(filter)
                ? allProcs
                : allProcs.Where(p => {
                    try { return p.ProcessName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0; }
                    catch { return false; }
                }).ToArray();

            var currentKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var activePids = new HashSet<int>();
            var existingItems = new Dictionary<string, ListViewItem>(StringComparer.OrdinalIgnoreCase);
            foreach (ListViewItem it in midnightListView1.Items) existingItems[ItemKey(it)] = it;

            foreach (var proc in procs)
            {
                string procName;
                try { procName = proc.ProcessName; } catch { procName = "Unknown"; }
                string key = $"{proc.Id}_{procName}";
                currentKeys.Add(key);
                activePids.Add(proc.Id);

                long avgTcpIn = GetMovingAverage(proc.Id, totalTcpRecv, lastTcpRecv, historyTcpIn);
                long avgTcpOut = GetMovingAverage(proc.Id, totalTcpSent, lastTcpSent, historyTcpOut);
                long avgUdpIn = GetMovingAverage(proc.Id, totalUdpRecv, lastUdpRecv, historyUdpIn);
                long avgUdpOut = GetMovingAverage(proc.Id, totalUdpSent, lastUdpSent, historyUdpOut);

                if (existingItems.TryGetValue(key, out var item))
                {
                    UpdateSubItem(item, 2, FormatBytes(avgTcpIn) + "/s");
                    UpdateSubItem(item, 3, FormatBytes(avgTcpOut) + "/s");
                    UpdateSubItem(item, 4, FormatBytes(avgUdpIn) + "/s");
                    UpdateSubItem(item, 5, FormatBytes(avgUdpOut) + "/s");
                }
                else
                {
                    var newItem = new ListViewItem(proc.Id.ToString());
                    newItem.SubItems.Add(procName);
                    newItem.SubItems.Add(FormatBytes(avgTcpIn) + "/s");
                    newItem.SubItems.Add(FormatBytes(avgTcpOut) + "/s");
                    newItem.SubItems.Add(FormatBytes(avgUdpIn) + "/s");
                    newItem.SubItems.Add(FormatBytes(avgUdpOut) + "/s");

                    var grp = midnightListView1.Groups.Cast<ListViewGroup>().FirstOrDefault(g => g.Name == procName)
                              ?? new ListViewGroup(procName, procName);
                    if (!midnightListView1.Groups.Contains(grp)) midnightListView1.Groups.Add(grp);

                    newItem.Group = grp;
                    midnightListView1.Items.Add(newItem);
                }
            }

            for (int i = midnightListView1.Items.Count - 1; i >= 0; i--)
            {
                if (!currentKeys.Contains(ItemKey(midnightListView1.Items[i])))
                    midnightListView1.Items.RemoveAt(i);
            }
            CleanupHistory(activePids);

            if (!string.IsNullOrEmpty(selectedKey))
            {
                var sel = midnightListView1.Items.Cast<ListViewItem>().FirstOrDefault(i => ItemKey(i) == selectedKey);
                if (sel != null) { sel.Selected = true; sel.Focused = true; }
            }
            if (!string.IsNullOrEmpty(topKey))
            {
                var top = midnightListView1.Items.Cast<ListViewItem>().FirstOrDefault(i => ItemKey(i) == topKey);
                if (top != null) try { midnightListView1.TopItem = top; } catch { }
            }

            // Update status label with process count
            string statusText = $"📊 Monitoring {procs.Length} process(es) • Updated: {DateTime.Now:HH:mm:ss}";
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => {
                    labelStatus.Text = statusText;
                }));
            }
            else
            {
                labelStatus.Text = statusText;
            }
        }
        finally { midnightListView1.EndUpdate(); }
    }

    private long GetMovingAverage(int pid, ConcurrentDictionary<int, long> totalMap, Dictionary<int, long> lastMap, Dictionary<int, Queue<long>> historyMap)
    {
        totalMap.TryGetValue(pid, out long currentTotal);
        lastMap.TryGetValue(pid, out long previousTotal);
        long delta = Math.Max(0, currentTotal - previousTotal);
        lastMap[pid] = currentTotal;

        if (!historyMap.TryGetValue(pid, out var queue))
        {
            queue = new Queue<long>();
            historyMap[pid] = queue;
        }

        queue.Enqueue(delta);
        if (queue.Count > AverageWindow) queue.Dequeue();

        return (long)queue.Average();
    }

    private void CleanupHistory(HashSet<int> activePids)
    {
        var pidsInHistory = historyTcpIn.Keys.ToList();
        foreach (var pid in pidsInHistory)
        {
            if (!activePids.Contains(pid))
            {
                historyTcpIn.Remove(pid); historyTcpOut.Remove(pid);
                historyUdpIn.Remove(pid); historyUdpOut.Remove(pid);
                lastTcpSent.Remove(pid); lastTcpRecv.Remove(pid);
                lastUdpSent.Remove(pid); lastUdpRecv.Remove(pid);
            }
        }
    }

    private void UpdateSubItem(ListViewItem item, int index, string text)
    {
        if (item.SubItems.Count > index && item.SubItems[index].Text != text)
            item.SubItems[index].Text = text;
    }

    private string ItemKey(ListViewItem item)
    {
        if (item == null) return string.Empty;
        return item.Text + "_" + ((item.SubItems.Count > 1) ? item.SubItems[1].Text : "");
    }

    private void midnightListView1_SelectedIndexChanged(object sender, EventArgs e)
    {
        guna2TextBox2.Text = "";
        guna2TextBox3.Text = "";

        try
        {
            int processId = int.Parse(midnightListView1.SelectedItems[0].Text);
            IEnumerable<(int Pid, IPAddress RemoteIp, int RemotePort, string Proto)> tcpValues = NetworkConnections.GetTcpConnections();
            string tcpText = "";

            foreach (var tcpValue in tcpValues)
            {
                try
                {
                    if (tcpValue.Pid.Equals(processId))
                    {
                        string line = tcpValue.RemoteIp.ToString() + ":" + tcpValue.RemotePort.ToString();

                        if (tcpText == "")
                        {
                            tcpText = line;
                        }
                        else
                        {
                            tcpText = $"{tcpText}\r\n{line}";
                        }
                    }
                }
                catch
                {

                }
            }

            guna2TextBox2.Text = tcpText;
        }
        catch
        {

        }

        try
        {
            int processId = int.Parse(midnightListView1.SelectedItems[0].Text);
            IEnumerable<(int Pid, IPAddress RemoteIp, int RemotePort, string Proto)> udpValues = NetworkConnections.GetUdpConnections();
            string udpText = "";

            foreach (var udpValue in udpValues)
            {
                try
                {
                    if (udpValue.Pid.Equals(processId))
                    {
                        string line = udpValue.RemoteIp.ToString() + ":" + udpValue.RemotePort.ToString();

                        if (udpText == "")
                        {
                            udpText = line;
                        }
                        else
                        {
                            udpText = $"{udpText}\r\n{line}";
                        }
                    }
                }
                catch
                {

                }
            }

            guna2TextBox3.Text = udpText;
        }
        catch
        {

        }
    }
}

public static class ListViewExtensions
{
    public static void DoubleBuffered(this MidnightListView listView, bool enabled)
    {
        var prop = typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        prop.SetValue(listView, enabled, null);
    }
}