using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace BlobEngine;

public partial class MainWindow : System.Windows.Window
{
    private readonly ObservableCollection<MemoryEntry> _entries = new();
    private Process? _process;
    private IntPtr _handle;
    private const uint PROCESS_VM_OPERATION = 0x0008, PROCESS_VM_READ = 0x0010, PROCESS_VM_WRITE = 0x0020, PROCESS_QUERY_INFORMATION = 0x0400;

    public MainWindow()
    {
        InitializeComponent();
        ResultsGrid.ItemsSource = _entries;
        RefreshProcesses();
    }

    private void RefreshProcesses()
    {
        var selected = ProcessBox.SelectedValue as ProcessItem;
        var items = Process.GetProcesses().OrderBy(p => p.ProcessName).Select(p => new ProcessItem(p.Id, p.ProcessName)).ToList();
        ProcessBox.ItemsSource = items;
        if (selected != null) ProcessBox.SelectedItem = items.FirstOrDefault(x => x.Id == selected.Id);
        FooterText.Text = $"Найдено процессов: {items.Count}";
    }

    private void RefreshProcesses_Click(object sender, System.Windows.RoutedEventArgs e) => RefreshProcesses();

    private void ProcessBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded || ProcessBox.SelectedItem is not ProcessItem item) return;
        Detach();
        try
        {
            _process = Process.GetProcessById(item.Id);
            _handle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION, false, item.Id);
            if (_handle == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            StatusText.Text = $"Подключено: {item.Name} ({item.Id})";
            FooterText.Text = "Процесс открыт. Для поиска по названиям загрузите профиль игры.";
        }
        catch (Exception ex) { Detach(); StatusText.Text = "Не удалось подключиться"; FooterText.Text = ex.Message; }
    }

    private void Search_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var term = SearchBox.Text.Trim();
        _entries.Clear();
        if (_handle == IntPtr.Zero) { FooterText.Text = "Сначала выбери процесс."; return; }
        FooterText.Text = $"«{term}»: для семантического поиска нужен профиль игры с названиями параметров и адресами. Сканирование памяти будет добавлено следующим этапом.";
    }

    private void Change_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ResultsGrid.SelectedItem is not MemoryEntry entry || _handle == IntPtr.Zero) { FooterText.Text = "Выбери найденную запись."; return; }
        try
        {
            var bytes = ParseValue(NewValueBox.Text, ValueTypeBox.SelectedIndex);
            if (!WriteProcessMemory(_handle, (IntPtr)entry.RawAddress, bytes, (nuint)bytes.Length, out var written) || written != (nuint)bytes.Length)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            entry.Value = NewValueBox.Text;
            ResultsGrid.Items.Refresh();
            FooterText.Text = "Значение записано.";
        }
        catch (Exception ex) { FooterText.Text = "Ошибка записи: " + ex.Message; }
    }

    private static byte[] ParseValue(string value, int type) => type switch
    {
        0 => BitConverter.GetBytes(int.Parse(value, CultureInfo.InvariantCulture)),
        1 => BitConverter.GetBytes(long.Parse(value, CultureInfo.InvariantCulture)),
        2 => BitConverter.GetBytes(float.Parse(value, CultureInfo.InvariantCulture)),
        3 => BitConverter.GetBytes(double.Parse(value, CultureInfo.InvariantCulture)),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private void Detach()
    {
        if (_handle != IntPtr.Zero) CloseHandle(_handle);
        _handle = IntPtr.Zero; _process = null;
    }

    private void Settings_Click(object sender, System.Windows.RoutedEventArgs e) =>
        System.Windows.MessageBox.Show("Настройки языка, темы и профилей будут добавлены в следующем этапе.", "BlobEngine");
    private void Help_Click(object sender, System.Windows.RoutedEventArgs e) =>
        System.Windows.MessageBox.Show("BlobEngine — редактор памяти процессов. Используй его только в программах, где у тебя есть разрешение на изменение данных.", "О BlobEngine");

    protected override void OnClosed(EventArgs e) { Detach(); base.OnClosed(e); }

    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, int processId);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ReadProcessMemory(IntPtr process, IntPtr address, [Out] byte[] buffer, nuint size, out nuint read);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool WriteProcessMemory(IntPtr process, IntPtr address, byte[] buffer, nuint size, out nuint written);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr handle);
}

public sealed record ProcessItem(int Id, string Name) { public string Display => $"{Name}  (PID {Id})"; }
public sealed class MemoryEntry
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "4 байта";
    public string Value { get; set; } = "";
    public string Address { get; set; } = "";
    public long RawAddress { get; set; }
}