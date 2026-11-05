using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace BlobEngine;

public partial class MainWindow : System.Windows.Window
{
    private readonly ObservableCollection<MemoryEntry> _entries = new();
    private IntPtr _handle;
    private bool _isScanning;
    private const uint PROCESS_VM_OPERATION = 0x0008, PROCESS_VM_READ = 0x0010, PROCESS_VM_WRITE = 0x0020, PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint MEM_COMMIT = 0x1000, PAGE_GUARD = 0x100, PAGE_NOACCESS = 0x01;
    private const ulong MaxScanBytes = 512UL * 1024 * 1024;
    private const int ChunkSize = 1024 * 1024;
    private const int MaxResults = 500;

    public MainWindow()
    {
        InitializeComponent();
        ResultsGrid.ItemsSource = _entries;
        RefreshProcesses();
    }

    private void RefreshProcesses()
    {
        try
        {
            var selectedId = (ProcessBox.SelectedItem as ProcessItem)?.Id;
            var items = Process.GetProcesses().Select(p =>
            {
                try { return new ProcessItem(p.Id, p.ProcessName); }
                catch { return null; }
                finally { p.Dispose(); }
            }).Where(p => p != null).Cast<ProcessItem>().OrderBy(p => p.Name).ToList();
            ProcessBox.ItemsSource = items;
            if (selectedId.HasValue) ProcessBox.SelectedItem = items.FirstOrDefault(x => x.Id == selectedId.Value);
            FooterText.Text = $"Найдено процессов: {items.Count}";
        }
        catch (Exception ex) { FooterText.Text = "Не удалось получить список процессов: " + ex.Message; }
    }

    private void RefreshProcesses_Click(object sender, System.Windows.RoutedEventArgs e) => RefreshProcesses();

    private void ProcessBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded || ProcessBox.SelectedItem is not ProcessItem item) return;
        Detach();
        try
        {
            using var process = Process.GetProcessById(item.Id);
            _handle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION, false, item.Id);
            if (_handle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            StatusText.Text = $"Подключено: {item.Name} ({item.Id})";
            FooterText.Text = "Введи точное числовое значение и нажми «Найти».";
        }
        catch (Exception ex) { Detach(); StatusText.Text = "Не удалось подключиться"; FooterText.Text = ex.Message; }
    }

    private async void Search_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_isScanning) return;
        if (_handle == IntPtr.Zero) { FooterText.Text = "Сначала выбери процесс."; return; }
        byte[] needle;
        try { needle = ParseValue(SearchBox.Text.Trim(), ValueTypeBox.SelectedIndex); }
        catch { FooterText.Text = "Введи число для поиска (например, 100 или 250.5). Тип выбери справа внизу."; return; }

        _isScanning = true;
        _entries.Clear();
        FooterText.Text = "Сканирование памяти…";
        try
        {
            var handle = _handle;
            var scanType = ValueTypeBox.SelectedIndex;
            var found = await Task.Run(() => ScanMemory(handle, needle, scanType));
            foreach (var item in found) _entries.Add(item);
            FooterText.Text = found.Count == 0
                ? "Совпадений не найдено. Проверь значение и тип данных."
                : $"Найдено совпадений: {found.Count}. Выбери строку, введи новое значение и нажми «Изменить».";
        }
        catch (Exception ex) { FooterText.Text = "Ошибка сканирования: " + ex.Message; }
        finally { _isScanning = false; }
    }

    private static List<MemoryEntry> ScanMemory(IntPtr process, byte[] needle, int valueType)
    {
        var results = new List<MemoryEntry>();
        var address = 0UL;
        ulong scanned = 0;
        var chunk = new byte[ChunkSize];
        while (scanned < MaxScanBytes && results.Count < MaxResults)
        {
            var queried = VirtualQueryEx(process, new IntPtr(unchecked((long)address)), out var info, (nuint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>());
            if (queried == 0) break;
            var baseAddress = unchecked((ulong)info.BaseAddress.ToInt64());
            var regionSize = info.RegionSize.ToUInt64();
            var next = baseAddress + regionSize;
            if (next <= address) break;
            address = next;
            if (info.State != MEM_COMMIT || (info.Protect & (PAGE_GUARD | PAGE_NOACCESS)) != 0 || !IsReadable(info.Protect) || regionSize == 0) continue;

            var cursor = baseAddress;
            var remaining = regionSize;
            var carry = 0;
            while (remaining > 0 && scanned < MaxScanBytes && results.Count < MaxResults)
            {
                var request = (int)Math.Min((ulong)ChunkSize, remaining);
                if (!ReadProcessMemory(process, new IntPtr(unchecked((long)cursor)), chunk, (nuint)request, out var readNative))
                {
                    cursor += (ulong)request; remaining -= (ulong)request; scanned += (ulong)request; carry = 0; continue;
                }
                var bytesRead = (int)Math.Min((ulong)request, readNative.ToUInt64());
                if (bytesRead <= 0) break;
                var combined = new byte[carry + bytesRead];
                if (carry > 0) Buffer.BlockCopy(buffer, 0, combined, 0, carry);
                Buffer.BlockCopy(chunk, 0, combined, carry, bytesRead);
                var total = combined.Length;
                for (var i = 0; i <= total - needle.Length && results.Count < MaxResults; i++)
                {
                    var match = true;
                    for (var j = 0; j < needle.Length; j++)
                        if (combined[i + j] != needle[j]) { match = false; break; }
                    if (match)
                    {
                        var foundAddress = cursor - (ulong)carry + (ulong)i;
                        results.Add(new MemoryEntry { Name = "Найденное значение", Type = TypeName(needle.Length), Value = FormatValue(combined, i, valueType), Address = $"0x{foundAddress:X}", RawAddress = unchecked((long)foundAddress) });
                    }
                }
                carry = Math.Min(needle.Length - 1, total);
                if (carry > 0) Buffer.BlockCopy(combined, total - carry, buffer, 0, carry);
                cursor += (ulong)bytesRead;
                remaining -= (ulong)bytesRead;
                scanned += (ulong)bytesRead;
                if (bytesRead < request) { carry = 0; }
            }
        }
        return results;
    }

    private static bool IsReadable(uint protect)
    {
        var basic = protect & 0xFF;
        return basic is 0x02 or 0x04 or 0x08 or 0x20 or 0x40 or 0x80;
    }

    private static string TypeName(int length) => length switch { 4 => "4 байта", 8 => "8 байт", _ => "Данные" };
    private static string FormatValue(byte[] data, int offset, int type) => type switch
    {
        0 => BitConverter.ToInt32(data, offset).ToString(CultureInfo.InvariantCulture),
        1 => BitConverter.ToInt64(data, offset).ToString(CultureInfo.InvariantCulture),
        2 => BitConverter.ToSingle(data, offset).ToString("G", CultureInfo.InvariantCulture),
        3 => BitConverter.ToDouble(data, offset).ToString("G", CultureInfo.InvariantCulture),
        _ => "?"
    };

    private void Change_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ResultsGrid.SelectedItem is not MemoryEntry entry || _handle == IntPtr.Zero) { FooterText.Text = "Выбери найденную запись."; return; }
        try
        {
            var bytes = ParseValue(NewValueBox.Text, ValueTypeBox.SelectedIndex);
            if (!WriteProcessMemory(_handle, new IntPtr(entry.RawAddress), bytes, (nuint)bytes.Length, out var written) || written != (nuint)bytes.Length)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            entry.Value = NewValueBox.Text;
            ResultsGrid.Items.Refresh();
            FooterText.Text = "Значение записано.";
        }
        catch (Exception ex) { FooterText.Text = "Ошибка записи: " + ex.Message; }
    }

    private static byte[] ParseValue(string value, int type) => type switch
    {
        0 => BitConverter.GetBytes(int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)),
        1 => BitConverter.GetBytes(long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)),
        2 => BitConverter.GetBytes(float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture)),
        3 => BitConverter.GetBytes(double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture)),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private void Detach()
    {
        if (_handle != IntPtr.Zero) CloseHandle(_handle);
        _handle = IntPtr.Zero;
    }

    private void Settings_Click(object sender, System.Windows.RoutedEventArgs e) =>
        System.Windows.MessageBox.Show("Поиск выполняется по точному числовому значению. Названия вроде «деньги» требуют таблицы адресов или профиля конкретной игры.", "BlobEngine — справка");
    private void Help_Click(object sender, System.Windows.RoutedEventArgs e) =>
        System.Windows.MessageBox.Show("1. Выбери процесс игры.\n2. Выбери тип данных.\n3. Введи известное числовое значение в поле поиска.\n4. После нахождения совпадений выбери адрес и измени значение.\n\nПоиск ограничен первыми 512 МБ читаемой памяти и 500 результатами.", "О BlobEngine");

    protected override void OnClosed(EventArgs e) { Detach(); base.OnClosed(e); }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public ushort PartitionId;
        public UIntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, int processId);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern nuint VirtualQueryEx(IntPtr process, IntPtr address, out MEMORY_BASIC_INFORMATION info, nuint length);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ReadProcessMemory(IntPtr process, IntPtr address, [Out] byte[] buffer, nuint size, out nuint read);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool WriteProcessMemory(IntPtr process, IntPtr address, byte[] buffer, nuint size, out nuint written);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr handle);
}

public sealed record ProcessItem(int Id, string Name) { public string Display => $"{Name}  (PID {Id})"; }
public sealed class MemoryEntry
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Value { get; set; } = "";
    public string Address { get; set; } = "";
    public long RawAddress { get; set; }
}
