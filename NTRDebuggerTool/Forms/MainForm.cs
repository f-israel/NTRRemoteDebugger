﻿using NTRDebuggerTool.Forms.FormEnums;
using NTRDebuggerTool.Objects;
using NTRDebuggerTool.Remote;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace NTRDebuggerTool.Forms
{
    public partial class MainForm : Form
    {
        internal bool ControlEnabledButtonConnectDisconnect = true;
        private bool ControlEnabledIP = true;
        private bool ControlEnabledPort = true;

        private bool ControlEnabledProcesses = false;
        internal bool ControlEnabledButtonOpenProcess = false;

        internal bool ControlEnabledMemoryRange = false;
        private bool ControlEnabledButtonRefreshMemoryRange = false;
        private bool ControlEnabledButtonAddResults = false;
        private bool ControlEnabledResetButton = false;
        private bool ControlEnabledResultsGrid = false;
        internal bool ControlEnabledSearchButton = false;
        internal bool ControlEnabledDataType = false;
        private bool ControlEnabledSearchType = false;
        private bool ControlEnabledSearchValue = false;
        private bool ControlEnabledSearchValue2 = false;
        private bool ControlEnabledValuesGrid = false;

        private bool ControlEnabledStart = false;
        private bool ControlEnabledSize = false;

        internal bool FormEnabled = true;

        private Thread EventDispatcherThread;
        private Thread ButtonStateThread;

        private int PointerSearchRow = 0;

        internal string SetConnectText = null;

        internal NTRRemoteConnection NTRConnection;

        private MainFormThreadEventDispatcher ThreadEventDispatcher;
        private MainFormThreadButtonState ThreadButtonState;
        internal bool SearchComplete = false;

        internal SearchCriteria LastSearchCriteria;

        private Regex HexRegex = new Regex("^[0-9A-F]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private Regex ParserRegex = new Regex("\\(\\*(?<Address>.+)\\)(?<Offset>(?:\\[[0-9A-F]+\\])?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private Stopwatch LockValuesStopwatch = new Stopwatch();

        private Dictionary<uint, uint> Pointers = new Dictionary<uint, uint>();

        public MainForm(NTRRemoteConnection NTRConnection)
        {
            InitializeComponent();
            this.NTRConnection = NTRConnection;
            this.ThreadEventDispatcher = new MainFormThreadEventDispatcher(this);
            this.EventDispatcherThread = new Thread(new ThreadStart(this.ThreadEventDispatcher.ThreadEventDispatcher));
            this.EventDispatcherThread.Name = "EventDispatcherThread";
            this.EventDispatcherThread.Start();

            this.ThreadButtonState = new MainFormThreadButtonState(this);
            this.ButtonStateThread = new Thread(new ThreadStart(this.ThreadButtonState.ThreadButtonState));
            this.ButtonStateThread.Name = "ButtonStateThread";
            this.ButtonStateThread.Start();

            this.IP.Text = Config.DefaultIP;
            LockValuesStopwatch.Start();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            ComboSearchType.Items.AddRange(SearchTypeInitialTool.GetValues());
            ComboDataType.Items.AddRange(DataTypeExactTool.GetValues());
            ValuesGridTypeColumn.Items.AddRange(DataTypeExactTool.GetValues());
            this.ComboSearchType.SelectedIndex = 0;
            this.ComboDataType.SelectedIndex = 2;
        }

        private void UpdateLockedControls()
        {
            this.ButtonAddResults.Enabled = !NTRConnection.LockControls && this.ControlEnabledButtonAddResults;
            this.ButtonConnectDisconnect.Enabled = !NTRConnection.LockControls && this.ControlEnabledButtonConnectDisconnect;
            this.ButtonOpenProcess.Enabled = !NTRConnection.LockControls && this.ControlEnabledButtonOpenProcess;
            this.ButtonRefreshMemoryRange.Enabled = !NTRConnection.LockControls && this.ControlEnabledButtonRefreshMemoryRange;
            this.IP.Enabled = !NTRConnection.LockControls && this.ControlEnabledIP;
            this.MemoryRange.Enabled = !NTRConnection.LockControls && this.ControlEnabledMemoryRange;
            this.Port.Enabled = !NTRConnection.LockControls && this.ControlEnabledPort;
            this.Processes.Enabled = !NTRConnection.LockControls && this.ControlEnabledProcesses;
            this.ResetButton.Enabled = !NTRConnection.LockControls && this.ControlEnabledResetButton;
            this.ResultsGrid.Enabled = !NTRConnection.LockControls && this.ControlEnabledResultsGrid;
            this.SearchButton.Enabled = !NTRConnection.LockControls && this.ControlEnabledSearchButton;
            this.ComboSearchType.Enabled = !NTRConnection.LockControls && this.ControlEnabledSearchType;
            this.ComboDataType.Enabled = !NTRConnection.LockControls && this.ControlEnabledDataType;
            this.SearchValue.Enabled = !NTRConnection.LockControls && this.ControlEnabledSearchValue;
            this.SearchValue2.Enabled = !NTRConnection.LockControls && this.ControlEnabledSearchValue2;
            this.ValuesGrid.Enabled = !NTRConnection.LockControls && this.ControlEnabledValuesGrid;
            this.MemoryStart.Enabled = !NTRConnection.LockControls && this.ControlEnabledStart;
            this.MemorySize.Enabled = !NTRConnection.LockControls && this.ControlEnabledSize;
        }

        internal void SetConnectedControls(bool Enabled)
        {
            ControlEnabledProcesses = Enabled;
            ControlEnabledButtonOpenProcess = Enabled;
        }

        internal void SetProcessSelectedControls(bool Enabled)
        {
            ControlEnabledMemoryRange = Enabled;
            ControlEnabledButtonRefreshMemoryRange = Enabled;
            ControlEnabledButtonAddResults = Enabled;
            ControlEnabledResetButton = Enabled;
            ControlEnabledResultsGrid = Enabled;
            ControlEnabledSearchButton = Enabled;
            ControlEnabledSearchType = Enabled;
            ControlEnabledDataType = Enabled;
            ControlEnabledSearchValue = Enabled;
            ControlEnabledSearchValue2 = Enabled;
            ControlEnabledValuesGrid = Enabled;
        }

        internal void SetConnectionControls(bool Enabled)
        {
            ControlEnabledButtonConnectDisconnect = Enabled;
            ControlEnabledIP = Enabled;
            ControlEnabledPort = Enabled;
        }

        private void GUIUpdateTimer_Tick(object sender, EventArgs e)
        {
            if (Enabled != FormEnabled)
            {
                Enabled = FormEnabled;
                if (FormEnabled)
                {
                    Activate();
                }
            }

            NTRConnection.SendHeartbeatPacket();

            int LocalReadMax = (int)NTRConnection.ProgressReadMax, LocalRead = (int)NTRConnection.ProgressRead, LocalScanMax = (int)NTRConnection.ProgressScanMax, LocalScan = (int)NTRConnection.ProgressScan;

            if (LocalReadMax > 0 && LocalRead >= 0 && LocalReadMax >= LocalRead)
            {
                ProgressBarMemoryRead.Maximum = LocalReadMax;
                ProgressBarMemoryRead.Value = LocalRead;
            }
            else
            {
                ProgressBarMemoryRead.Maximum = ProgressBarMemoryRead.Value = 0;
            }

            if (LocalScanMax > 0 && LocalScan >= 0 && LocalScanMax >= LocalScan)
            {
                ProgressBarMemoryScan.Maximum = LocalScanMax;
                ProgressBarMemoryScan.Value = LocalScan;
            }
            else
            {
                ProgressBarMemoryScan.Maximum = ProgressBarMemoryScan.Value = 0;
            }

            if (NTRConnection.IsProcessListUpdated)
            {
                NTRConnection.IsProcessListUpdated = false;
                string CurrentProcess = Processes.SelectedValue == null ? null : Processes.SelectedValue.ToString();
                Processes.Items.Clear();
                Processes.Items.AddRange(NTRConnection.Processes.ToArray());
                if (CurrentProcess != null && Processes.Items.Contains(CurrentProcess))
                {
                    Processes.SelectedValue = CurrentProcess;
                    Processes.SelectedIndex = Processes.Items.IndexOf(CurrentProcess);
                }
                else if (!Processes.Items[0].ToString().Contains(','))
                {
                    Processes.SelectedIndex = 0;
                }
                SetConnectedControls(true);
                ControlEnabledButtonConnectDisconnect = true;
                NTRConnection.SetCurrentOperationText = "";
            }

            if (NTRConnection.IsMemoryListUpdated)
            {
                NTRConnection.IsMemoryListUpdated = false;
                string CurrentRange = MemoryRange.SelectedValue == null ? "All" : MemoryRange.SelectedValue.ToString();
                MemoryRange.Items.Clear();
                MemoryRange.Items.Add("All");
                foreach (uint Start in NTRConnection.AddressSpaces.Keys)
                {
                    MemoryRange.Items.Add(Utilities.GetStringFromByteArray(BitConverter.GetBytes(Start).Reverse().ToArray()) + "|" + Utilities.GetStringFromByteArray(BitConverter.GetBytes(NTRConnection.AddressSpaces[Start]).Reverse().ToArray()));
                }
                MemoryRange.Items.Add("Custom");
                if (CurrentRange != null && MemoryRange.Items.Contains(CurrentRange))
                {
                    MemoryRange.SelectedValue = CurrentRange;
                    MemoryRange.SelectedIndex = MemoryRange.Items.IndexOf(CurrentRange);
                }
                SetProcessSelectedControls(true);
                SetConnectedControls(true);
                NTRConnection.SetCurrentOperationText = "";
            }

            if (LastSearchCriteria != null && LastSearchCriteria.SearchValue != null)
            {
                LabelLastSearch.Text = "Last Search\n" + LastSearchCriteria.AddressesFound.Count + " results found for " + GetDisplayForByteArray(LastSearchCriteria.SearchValue);

                if (SearchComplete)
                {
                    ControlEnabledSearchButton = true;
                    if (LastSearchCriteria.AddressesFound.Count < Config.MaxValuesToDisplay)
                    {
                        ResultsGrid.Rows.Clear();
                        foreach (uint Address in LastSearchCriteria.AddressesFound.Keys)
                        {
                            int Row = ResultsGrid.Rows.Add();
                            ResultsGrid[0, Row].Value = Utilities.GetStringFromByteArray(BitConverter.GetBytes(Address).Reverse().ToArray());
                            ResultsGrid[1, Row].Value = GetDisplayForByteArray(LastSearchCriteria.AddressesFound[Address]);
                        }
                    }
                    NTRConnection.SetCurrentOperationText = "";
                    ComboDataType_SelectedValueChanged(null, null);
                    SearchComplete = false;
                }
            }

            if (SetConnectText != null)
            {
                ButtonConnectDisconnect.Text = SetConnectText;
                SetConnectText = null;
            }

            LabelCurrentOperation.Text = NTRConnection.SetCurrentOperationText;
            LabelButtonState.Text = NTRConnection.SetCurrentOperationText2;

            UpdateLockedControls();

            if (NTRConnection.IsConnected)
            {
                if (LockValuesStopwatch.ElapsedMilliseconds > Config.LockValuesDelay)
                {
                    Pointers.Clear();
                    for (int i = 0; i < ValuesGrid.Rows.Count; ++i)
                    {
                        if (ValuesGrid[0, i].Value is string)
                        {
                            SetMemory(i);
                        }
                    }
                    Pointers.Clear();
                    LockValuesStopwatch.Restart();
                }
            }

            if (ThreadEventDispatcher.FoundPointerAddress != null)
            {
                ValuesGrid[1, PointerSearchRow].Value = ThreadEventDispatcher.FoundPointerAddress;
                ThreadEventDispatcher.FoundPointerAddress = null;
            }
        }

        public string GetDisplayForByteArray(byte[] p)
        {
            return GetDisplayForByteArray(p, ThreadEventDispatcher.CurrentSelectedDataType);
        }

        public string GetDisplayForByteArray(byte[] p, DataTypeExact DataType)
        {
            switch (DataType)
            {
                case DataTypeExact.Bytes1: //1 Byte
                    return ((uint)p[0]).ToString();
                case DataTypeExact.Bytes2: //2 Bytes
                    return BitConverter.ToUInt16(p, 0).ToString();
                case DataTypeExact.Bytes4: //4 Bytes
                    return BitConverter.ToUInt32(p, 0).ToString();
                case DataTypeExact.Bytes8: //8 Bytes
                    return BitConverter.ToUInt64(p, 0).ToString();
                case DataTypeExact.Float: //Float
                    return BitConverter.ToSingle(p, 0).ToString();
                case DataTypeExact.Double: //Double
                    return BitConverter.ToDouble(p, 0).ToString();
                case DataTypeExact.Raw: //Raw Bytes
                    return Utilities.GetStringFromByteArray(p);
                default: //Text
                    return System.Text.Encoding.Default.GetString(p);
            }
        }

        internal uint GetSearchMemorySize()
        {
            return GetSearchMemorySize(ThreadEventDispatcher.CurrentSelectedDataType);
        }

        private uint GetSearchMemorySize(DataTypeExact DataType)
        {
            switch (DataType)
            {
                case DataTypeExact.Bytes1: //1 Byte
                    return 1;
                case DataTypeExact.Bytes2: //2 Bytes
                    return 2;
                case DataTypeExact.Bytes4: //4 Bytes
                    return 4;
                case DataTypeExact.Bytes8: //8 Bytes
                    return 8;
                case DataTypeExact.Float: //Float
                    return (uint)BitConverter.GetBytes(float.MinValue).Length;
                case DataTypeExact.Double: //Double
                    return (uint)BitConverter.GetBytes(double.MinValue).Length;
                case DataTypeExact.Raw: //Raw Bytes
                    return (uint)Utilities.GetByteArrayFromByteString(SearchValue.Text).Length;
                default: //Text
                    return (uint)System.Text.Encoding.Default.GetBytes(SearchValue.Text).Length;
            }
        }

        private void ResetButton_Click(object sender, EventArgs e)
        {
            ComboSearchType.Items.Clear();
            ComboSearchType.Items.AddRange(SearchTypeInitialTool.GetValues());
            ComboSearchType.SelectedIndex = 0;
            LastSearchCriteria = null;
            ResultsGrid.Rows.Clear();
            ControlEnabledSearchType = ControlEnabledMemoryRange = ControlEnabledDataType = true;
            if (MemoryRange.SelectedIndex == MemoryRange.Items.Count - 1)
            {
                ControlEnabledStart = ControlEnabledSize = true;
            }

            LabelLastSearch.Text = "Last Search\n";
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            NTRConnection.Disconnect();
            this.EventDispatcherThread.Abort();
            this.ButtonStateThread.Abort();
            Application.Exit();
        }

        private void ButtonAddResults_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow Row in ResultsGrid.SelectedRows)
            {
                if (!IsInValues((string)Row.Cells[0].Value))
                {
                    int RowIndex = ValuesGrid.Rows.Add();
                    ValuesGrid[0, RowIndex].Value = null;
                    ValuesGrid[3, RowIndex].Value = ComboDataType.Text;
                    ValuesGrid[1, RowIndex].Value = Row.Cells[0].Value;
                    ValuesGrid[2, RowIndex].Value = SearchValue.Text;
                }
            }
        }

        private bool IsInValues(String Address)
        {
            for (int i = 0; i < ValuesGrid.RowCount; ++i)
            {
                if (((string)ValuesGrid[1, i].Value) == Address)
                {
                    return true;
                }
            }
            return false;
        }

        private void ValuesGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 2)
            {
                Pointers.Clear();
                SetMemory(e.RowIndex);
                Pointers.Clear();
            }
        }

        private void SetMemory(int RowIndex)
        {
            string TextAddress = (string)ValuesGrid[1, RowIndex].Value;

            uint Address;

            if (HexRegex.IsMatch(TextAddress))
            {
                Address = BitConverter.ToUInt32(Utilities.GetByteArrayFromByteString((string)ValuesGrid[1, RowIndex].Value).Reverse().ToArray(), 0); ;
            }
            else
            {
                Match TopMatch = ParserRegex.Match(TextAddress);

                if (!TopMatch.Success)
                {
                    return;
                }

                Address = ResolvePointer(TopMatch);
            }

            if (IsValidMemoryAddress(Address))
            {
                ValuesGrid[1, RowIndex].ToolTipText = Utilities.GetStringFromByteArray(BitConverter.GetBytes(Address).Reverse().ToArray());
                uint ProcessID = BitConverter.ToUInt32(Utilities.GetByteArrayFromByteString(Processes.Text.Split('|')[0]), 0);
                switch (DataTypeExactTool.GetValue((string)ValuesGrid[3, RowIndex].Value))
                {
                    case DataTypeExact.Bytes1: //1 Byte
                        NTRConnection.SendWriteMemoryPacket(
                            ProcessID,
                            Address,
                            (byte)uint.Parse((string)ValuesGrid[2, RowIndex].Value));
                        break;
                    case DataTypeExact.Bytes2: //2 Bytes
                        NTRConnection.SendWriteMemoryPacket(
                            ProcessID,
                            Address,
                            ushort.Parse((string)ValuesGrid[2, RowIndex].Value));
                        break;
                    case DataTypeExact.Bytes4: //4 Bytes
                        NTRConnection.SendWriteMemoryPacket(
                            ProcessID,
                            Address,
                            uint.Parse((string)ValuesGrid[2, RowIndex].Value));
                        break;
                    case DataTypeExact.Bytes8: //8 Bytes
                        NTRConnection.SendWriteMemoryPacket(
                            ProcessID,
                            Address,
                            ulong.Parse((string)ValuesGrid[2, RowIndex].Value));
                        break;
                    case DataTypeExact.Float: //Float
                        NTRConnection.SendWriteMemoryPacket(
                            ProcessID,
                            Address,
                            float.Parse((string)ValuesGrid[2, RowIndex].Value));
                        break;
                    case DataTypeExact.Double: //Double
                        NTRConnection.SendWriteMemoryPacket(
                            ProcessID,
                            Address,
                            double.Parse((string)ValuesGrid[2, RowIndex].Value));
                        break;
                    case DataTypeExact.Raw: //Raw Bytes
                        NTRConnection.SendWriteMemoryPacket(
                            ProcessID,
                            Address,
                            Utilities.GetByteArrayFromByteString((string)ValuesGrid[2, RowIndex].Value));
                        break;
                    case DataTypeExact.Text: //Raw Bytes
                        NTRConnection.SendWriteMemoryPacket(
                            ProcessID,
                            Address,
                            System.Text.Encoding.Default.GetBytes((string)ValuesGrid[2, RowIndex].Value));
                        break;
                }
            }
        }

        private uint ResolvePointer(Match Match)
        {
            string AddressString = Match.Groups["Address"].Value;
            string OffsetString = Match.Groups["Offset"].Value;


            uint Address;

            Match RecurseMatch = ParserRegex.Match(AddressString);

            if (RecurseMatch.Success)
            {
                Address = ResolvePointer(RecurseMatch);
            }
            else
            {
                uint Pointer = BitConverter.ToUInt32(Utilities.GetByteArrayFromByteString(AddressString).Reverse().ToArray(), 0);
                if (!Pointers.ContainsKey(Pointer))
                {
                    byte[] Data = GetMemoryAtAddress(Processes.Text.Split('|')[0], AddressString, DataTypeExact.Bytes4);

                    Address = BitConverter.ToUInt32(Data, 0);
                    Pointers[Pointer] = Address;
                }
                else if (!IsValidMemoryAddress(Pointer))
                {
                    return 0;
                }
                else
                {
                    Address = Pointers[Pointer];
                }
            }

            if (Address != 0 && !string.IsNullOrWhiteSpace(OffsetString))
            {
                OffsetString = OffsetString.Replace("[", "").Replace("]", "");
                Address += BitConverter.ToUInt32(Utilities.GetByteArrayFromByteString(OffsetString.PadLeft(8, '0')).Reverse().ToArray(), 0);
            }

            return Address;
        }

        private byte[] GetMemoryAtAddress(string ProcessID, string Address, DataTypeExact DataType)
        {
            return GetMemoryAtAddress(BitConverter.ToUInt32(Utilities.GetByteArrayFromByteString(ProcessID), 0), BitConverter.ToUInt32(Utilities.GetByteArrayFromByteString(Address).Reverse().ToArray(), 0), DataType);
        }

        private byte[] GetMemoryAtAddress(uint ProcessID, string Address, DataTypeExact DataType)
        {
            return GetMemoryAtAddress(ProcessID, BitConverter.ToUInt32(Utilities.GetByteArrayFromByteString(Address).Reverse().ToArray(), 0), DataType);
        }

        private byte[] GetMemoryAtAddress(string ProcessID, uint Address, DataTypeExact DataType)
        {
            return GetMemoryAtAddress(BitConverter.ToUInt32(Utilities.GetByteArrayFromByteString(ProcessID), 0), Address, DataType);
        }

        private byte[] GetMemoryAtAddress(uint ProcessID, uint Address, DataTypeExact DataType)
        {
            SearchCriteria Criteria = new SearchCriteria();
            Criteria.ProcessID = ProcessID;
            Criteria.DataType = DataType;
            Criteria.StartAddress = Address;
            Criteria.Length = Criteria.Size = GetSearchMemorySize(DataType);
            Criteria.SearchType = SearchTypeBase.Unknown;
            Criteria.SearchValue = new byte[] { 0 };
            NTRConnection.SearchCriteria.Add(Criteria);
            NTRConnection.SendReadMemoryPacket(Criteria);
            return Criteria.AddressesFound.Values.First();
        }

        private void ValuesGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == 0)
            {
                ValuesGrid.BeginEdit(true);
            }
            else if (e.ColumnIndex == 3)
            {
                ValuesGrid.BeginEdit(true);
                ((ComboBox)ValuesGrid.EditingControl).DroppedDown = true;
            }
        }

        private void ValuesGrid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            ValuesGrid.BeginEdit(true);
        }

        private void ValuesGrid_CellLeave(object sender, DataGridViewCellEventArgs e)
        {
            ValuesGrid.EndEdit();
        }

        private void MemoryRange_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (MemoryRange.SelectedIndex == 0)
            {
                this.MemoryStart.Text = this.MemorySize.Text = "";
                ControlEnabledStart = ControlEnabledSize = false;
            }
            else if (MemoryRange.SelectedIndex == MemoryRange.Items.Count - 1)
            {
                ControlEnabledStart = ControlEnabledSize = true;
            }
            else
            {
                string[] Parts = MemoryRange.Text.Split('|');
                this.MemoryStart.Text = Parts[0];
                this.MemorySize.Text = Parts[1];
                ControlEnabledStart = ControlEnabledSize = false;
            }
        }

        private void ButtonConnectDisconnect_Click(object sender, EventArgs e)
        {
            ButtonConnectDisconnect.Enabled = ControlEnabledButtonConnectDisconnect = false;
            ThreadEventDispatcher.DispatchConnect = true;
        }

        private void ButtonOpenProcess_Click(object sender, EventArgs e)
        {
            ButtonOpenProcess.Enabled = ControlEnabledButtonOpenProcess = false;
            ThreadEventDispatcher.CurrentSelectedProcess = Processes.Text;
            ThreadEventDispatcher.DispatchOpenProcess = true;
        }

        private void ButtonRefreshMemoryRange_Click(object sender, EventArgs e)
        {
            ButtonRefreshMemoryRange.Enabled = ControlEnabledButtonRefreshMemoryRange = false;
            ThreadEventDispatcher.CurrentSelectedProcess = Processes.Text;
            ThreadEventDispatcher.DispatchOpenProcess = true;
        }

        private void SearchButton_Click(object sender, EventArgs e)
        {
            MemoryStart.Text = MemoryStart.Text.PadLeft(8, '0');
            MemorySize.Text = MemorySize.Text.PadLeft(8, '0');
            uint StartAddress = BitConverter.ToUInt32(Utilities.GetByteArrayFromByteString(MemoryStart.Text).Reverse().ToArray(), 0);
            uint EndAddress = BitConverter.ToUInt32(Utilities.GetByteArrayFromByteString(TextEndAddress.Text).Reverse().ToArray(), 0);
            if (!MemoryRange.Text.Equals("All") && (!IsValidMemoryAddress(StartAddress) || !IsValidMemoryAddress(EndAddress)))
            {
                NTRConnection.SetCurrentOperationText = "Invalid start address or size!";
                return;
            }
            SearchButton.Enabled = ControlEnabledSearchButton = ControlEnabledDataType = ControlEnabledMemoryRange = false;
            ThreadEventDispatcher.CurrentSelectedDataType = DataTypeExactTool.GetValue(ComboDataType.SelectedItem.ToString());
            ThreadEventDispatcher.CurrentSelectedSearchType = SearchTypeBaseTool.GetValue(ComboSearchType.SelectedItem.ToString());
            ThreadEventDispatcher.CurrentMemoryRange = this.MemoryRange.Text;
            ThreadEventDispatcher.DispatchSearch = true;
        }

        private void ComboSearchType_SelectedValueChanged(object sender, EventArgs e)
        {
            string CurrentDataType = ComboDataType.SelectedItem == null ? null : ComboDataType.SelectedItem.ToString();
            switch (SearchTypeBaseTool.GetValue(ComboSearchType.SelectedItem.ToString()))
            {
                case SearchTypeBase.Exact:
                    ComboDataType.Items.Clear();
                    ComboDataType.Items.AddRange(DataTypeExactTool.GetValues());
                    SearchValue.Width = 286;
                    SearchValue2.Visible = LabelDash.Visible = false;
                    break;
                case SearchTypeBase.Range:
                    ComboDataType.Items.Clear();
                    ComboDataType.Items.AddRange(DataTypeNumericTool.GetValues());
                    SearchValue.Width = 136;
                    SearchValue2.Visible = LabelDash.Visible = true;
                    break;
                case SearchTypeBase.IncreasedBy:
                case SearchTypeBase.DecreasedBy:
                case SearchTypeBase.Increased:
                case SearchTypeBase.Decreased:
                case SearchTypeBase.Unknown:
                    ComboDataType.Items.Clear();
                    ComboDataType.Items.AddRange(DataTypeNumericTool.GetValues());
                    SearchValue.Width = 286;
                    SearchValue2.Visible = LabelDash.Visible = false;
                    break;
            }
            if (CurrentDataType != null && ComboDataType.Items.Contains(CurrentDataType))
            {
                ComboDataType.SelectedIndex = ComboDataType.Items.IndexOf(CurrentDataType);
                ComboDataType.SelectedItem = ComboDataType.SelectedValue = CurrentDataType;
            }
            else
            {
                ComboDataType.SelectedIndex = 0;
                ComboDataType.SelectedItem = ComboDataType.SelectedValue = ComboDataType.Items[0];
            }
        }

        private void ComboDataType_SelectedValueChanged(object sender, EventArgs e)
        {
            string CurrentSearchType = ComboSearchType.SelectedItem == null ? null : ComboSearchType.SelectedItem.ToString();
            if (LastSearchCriteria == null)
            {
                ComboSearchType.Items.Clear();
                ComboSearchType.Items.AddRange(SearchTypeInitialTool.GetValues());
            }
            else
            {
                switch (DataTypeExactTool.GetValue(ComboDataType.SelectedItem.ToString()))
                {
                    case DataTypeExact.Bytes1:
                    case DataTypeExact.Bytes2:
                    case DataTypeExact.Bytes4:
                    case DataTypeExact.Bytes8:
                    case DataTypeExact.Float:
                    case DataTypeExact.Double:
                        ComboSearchType.Items.Clear();
                        ComboSearchType.Items.AddRange(SearchTypeNumericTool.GetValues());
                        break;
                    case DataTypeExact.Raw:
                    case DataTypeExact.Text:
                        ComboSearchType.Items.Clear();
                        ComboSearchType.Items.AddRange(SearchTypeTextTool.GetValues());
                        break;
                }
            }
            if (CurrentSearchType != null && ComboSearchType.Items.Contains(CurrentSearchType))
            {
                ComboSearchType.SelectedIndex = ComboSearchType.Items.IndexOf(CurrentSearchType);
                ComboSearchType.SelectedItem = ComboSearchType.SelectedValue = CurrentSearchType;
            }
            else
            {
                ComboSearchType.SelectedIndex = 0;
                ComboSearchType.SelectedItem = ComboSearchType.SelectedValue = ComboSearchType.Items[0];
            }
        }

        private void PopulateComboBox(Type enumType, ComboBox box)
        {
            box.Items.Clear();
            box.Items.AddRange(Enum.GetNames(enumType));
        }

        private void ValuesGridContextMenuStrip_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            switch (e.ClickedItem.Name)
            {
                case "ValuesGridCopyResolvedAddress":
                    foreach (DataGridViewCell Cell in ValuesGrid.SelectedCells)
                    {
                        Clipboard.SetText(Cell.OwningRow.Cells[1].ToolTipText);
                        break;
                    }
                    break;
                case "ValuesGridPointerSearch":
                    foreach (DataGridViewCell Cell in ValuesGrid.SelectedCells)
                    {
                        PointerSearchRow = Cell.OwningRow.Index;
                        ThreadEventDispatcher.CurrentSelectedProcess = Processes.Text.Split('|')[0];
                        ThreadEventDispatcher.DispatchPointerSearch = (string)Cell.OwningRow.Cells[1].Value;
                        break;
                    }
                    break;
                case "ValuesGridDeleteItem":
                    foreach (DataGridViewCell Cell in ValuesGrid.SelectedCells)
                    {
                        ValuesGrid.Rows.Remove(Cell.OwningRow);
                    }
                    break;
                case "ValuesGridAddItem":
                    ValuesGrid.Rows.Add();
                    break;
                case "ValuesGridConvertCode":
                    foreach (DataGridViewCell Cell in ValuesGrid.SelectedCells)
                    {
                        Cell.OwningRow.Cells[1].Value = ConvertCode((string)Cell.OwningRow.Cells[1].Value);
                    }
                    break;
                case "ValuesGridRefreshItem":
                    foreach (DataGridViewCell Cell in ValuesGrid.SelectedCells)
                    {
                        string TextAddress = (string)Cell.OwningRow.Cells[1].Value;
                        uint Address;

                        if (HexRegex.IsMatch(TextAddress))
                        {
                            Address = BitConverter.ToUInt32(Utilities.GetByteArrayFromByteString(TextAddress).Reverse().ToArray(), 0); ;
                        }
                        else
                        {
                            Match TopMatch = ParserRegex.Match(TextAddress);

                            if (!TopMatch.Success)
                            {
                                return;
                            }

                            Pointers.Clear();
                            Address = ResolvePointer(TopMatch);
                            Pointers.Clear();
                        }

                        if (!IsValidMemoryAddress(Address) || Cell.OwningRow.Cells[3].Value == null)
                        {
                            continue;
                        }

                        DataTypeExact DataType = DataTypeExactTool.GetValue((string)Cell.OwningRow.Cells[3].Value);

                        byte[] Value = GetMemoryAtAddress(Processes.Text.Split('|')[0], Address, DataType);

                        Cell.OwningRow.Cells[2].Value = GetDisplayForByteArray(Value, DataType);
                    }
                    break;
            }
        }

        private string ConvertCode(string Address)
        {
            uint OriginalCodeAddress = BitConverter.ToUInt32(Utilities.GetByteArrayFromByteString(Address).Reverse().ToArray(), 0);
            uint ConvertedMemoryAddress = OriginalCodeAddress + 0x14000000u; //High memory region
            if (IsValidMemoryAddress(ConvertedMemoryAddress))
            {
                return Utilities.GetStringFromByteArray(BitConverter.GetBytes(ConvertedMemoryAddress).Reverse().ToArray());
            }
            uint ConversionModifier = GetConversionModifier(ConvertedMemoryAddress);
            if (ConversionModifier == 0)
            {
                return Address;
            }
            uint Offset = ConvertedMemoryAddress - ConversionModifier;
            ConvertedMemoryAddress = 0x08000000 + Offset;
            if (IsValidMemoryAddress(ConvertedMemoryAddress))
            {
                return Utilities.GetStringFromByteArray(BitConverter.GetBytes(ConvertedMemoryAddress).Reverse().ToArray());
            }
            ConvertedMemoryAddress = 0x00100000 + Offset;
            if (IsValidMemoryAddress(ConvertedMemoryAddress))
            {
                return Utilities.GetStringFromByteArray(BitConverter.GetBytes(ConvertedMemoryAddress).Reverse().ToArray());
            }
            return Address;
        }

        private bool IsValidMemoryAddress(uint Address)
        {
            return GetAddressSpaceForAddress(Address) != null;
        }

        internal Nullable<KeyValuePair<uint, uint>> GetAddressSpaceForAddress(uint Address)
        {
            foreach (uint RangeStart in NTRConnection.AddressSpaces.Keys)
            {
                if (Address >= RangeStart && Address <= (RangeStart + NTRConnection.AddressSpaces[RangeStart]))
                {
                    return new KeyValuePair<uint, uint>(RangeStart, NTRConnection.AddressSpaces[RangeStart]);
                }
            }
            return null;
        }

        private uint GetConversionModifier(uint Address)
        {
            uint[] Keys = NTRConnection.AddressSpaces.Keys.ToArray();
            Array.Sort(Keys);
            foreach (uint RangeStart in Keys.Reverse())
            {
                if (RangeStart <= Address)
                {
                    //This is ugly.
                    return BitConverter.ToUInt32(Utilities.GetByteArrayFromByteString(Utilities.GetStringFromByteArray(BitConverter.GetBytes(RangeStart + NTRConnection.AddressSpaces[RangeStart]).Reverse().ToArray()).Substring(0, 3).PadRight(8, '0')).Reverse().ToArray(), 0) + 0x00100000u;
                }
            }
            return 0;
        }

        private void ButtonConfig_Click(object sender, EventArgs e)
        {
            ThreadEventDispatcher.DispatchConfig = true;
        }

        private void Memory_TextChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(MemoryStart.Text) && !string.IsNullOrWhiteSpace(MemorySize.Text))
            {
                string Start = MemoryStart.Text.PadLeft(8, '0');
                string Size = MemorySize.Text.PadLeft(8, '0');
                uint StartInt = BitConverter.ToUInt32(Utilities.GetByteArrayFromByteString(Start).Reverse().ToArray(), 0);
                uint SizeInt = BitConverter.ToUInt32(Utilities.GetByteArrayFromByteString(Size).Reverse().ToArray(), 0);
                uint EndInt = StartInt + SizeInt;
                string End = Utilities.GetStringFromByteArray(BitConverter.GetBytes(EndInt).Reverse().ToArray());
                TextEndAddress.Text = End;
            }
            else
            {
                TextEndAddress.Text = "";
            }
        }

    }
}
