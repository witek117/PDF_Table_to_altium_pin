using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PDF_Table_to_pin
{
    /// <summary>
    /// Logika interakcji dla klasy MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        string pdfFilePath = "";
        string outPinsData = "";
        string outPinsDesc = "";

        public event PropertyChangedEventHandler PropertyChanged;
        private void RaisePropertyChanged(string NazwaWlasciwosci)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(NazwaWlasciwosci));
            }
        }

        public class Pin : INotifyPropertyChanged
        {
            private bool _selected;
            public bool selected 
            { 
                get { return _selected; } 
                set { _selected = value; OnPropertyChanged("selected"); }
            }

            public string designator { get; set; }
            public string name { get; set; }
            public string description { get; set; }

            public Pin(string designator, string name, string description)
            {
                this.designator = designator;
                this.name = name;
                this.description = description;
                this._selected = false;
            }

            public Pin(Pin pin)
            {
                this.designator = pin.designator;
                this.description = pin.description;
                this.name = pin.name;
                this._selected = false;
            }

            public event PropertyChangedEventHandler PropertyChanged;
            public void OnPropertyChanged(string propertyName)
            {
                PropertyChangedEventHandler handler = PropertyChanged;
                if (handler != null)
                    handler(this, new PropertyChangedEventArgs(propertyName));
            }
        };

        public ObservableCollection<Pin> Pins { get; set; }
        private Pin _selectedPin;
        public Pin selectedPin
        {
            get { return _selectedPin; }
            set 
            { 
                if (value != null)
                {
                    _selectedPin = value;
                    RaisePropertyChanged("selectedPin");
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            Pins = new ObservableCollection<Pin>();
            selectedPin = new Pin("0", "0", "null");
        }

        class Row
        {
            public List<string> cells;

            public string description = "";

            public Row()
            {
                cells = new List<string>();
            }

            public Row(Row row)
            {
                cells = new List<string>(row.cells);
            }

            public void Clear()
            {
                cells.Clear();
            }

            public void AddCell(string cell)
            {
                int index = cell.IndexOf("<cell>");
                if (index != -1)
                {
                    cell = cell.Remove(0, index + "<cell>".Length);

                    index = cell.IndexOf("</cell>");
                    if (index != -1)
                    {
                        cell = cell.Remove(index);
                    }
                }

                if (cell.IndexOf("<cell />") != -1)
                {
                    cells.Add("");
                }
                else
                {
                    cells.Add(cell);
                }
            }

            public string GetMergeCells(int[] columnsToMerge, string[] excludeWords)
            {
                string data = "";

                for (int i = 0; i < columnsToMerge.Length; i++)
                {
                    if (cells[columnsToMerge[i]] != "" && cells[columnsToMerge[i]] != "-")
                    {
                        data += cells[columnsToMerge[i]] + ",";
                    }
                }

                if (data.Length > 1)
                {
                    if (data[data.Length - 1] == ',')
                    {
                        data = data.Remove(data.Length - 1);
                    }
                }

                data = data.ToCharArray()
                             .Where(c => !Char.IsWhiteSpace(c))
                             .Select(c => c.ToString())
                             .Aggregate((a, b) => a + b);

                for (int i = 0; i < excludeWords.Length; i++)
                {
                    if (data.Length > 0 && excludeWords[i].Length > 0)
                    {
                        data = data.Replace(excludeWords[i], "");
                    }

                }

                while (data.IndexOf(",,") != -1)
                {
                    data = data.Replace(",,", ",");
                }

                while (data.IndexOf(",") != -1)
                {
                    data = data.Replace(",", "/");
                }

                if (data[data.Length - 1] == '/')
                {
                    data = data.Remove(data.Length - 1);
                }

                while (data.IndexOf("//") != -1)
                {
                    data = data.Replace("//", "/");
                }

                description = data;
                return data;
            }

            public string GetDescription()
            {
                return description;
            }
        };

        List<Row> ReadPage(string path, int pageNumber, int cellsNumber)
        {
            List<Row> rows = new List<Row>();

            // https://sautinsoft.com/products/pdf-focus/examples/convert-pdf-to-excel-csharp-vb-net.php

            string pathToPdf = path;
            string pathToExcel = System.IO.Path.ChangeExtension(pathToPdf, ".xls");
            string pathToXml = System.IO.Path.ChangeExtension(pathToPdf, ".xml");

            // Convert only tables from PDF to XLS spreadsheet and skip all textual data.
            SautinSoft.PdfFocus f = new SautinSoft.PdfFocus();

            // This property is necessary only for registered version
            //f.Serial = "XXXXXXXXXXX";

            // 'true' = Convert all data to spreadsheet (tabular and even textual).
            // 'false' = Skip textual data and convert only tabular (tables) data.
            f.ExcelOptions.ConvertNonTabularDataToSpreadsheet = false;

            // 'true'  = Preserve original page layout.
            // 'false' = Place tables before text.
            f.ExcelOptions.PreservePageLayout = true;

            // The information includes the names for the culture, the writing system,
            // the calendar used, the sort order of strings, and formatting for dates and numbers.
            System.Globalization.CultureInfo ci = new System.Globalization.CultureInfo("en-US");
            ci.NumberFormat.NumberDecimalSeparator = ",";
            ci.NumberFormat.NumberGroupSeparator = ".";
            f.ExcelOptions.CultureInfo = ci;

            f.OpenPdf(pathToPdf);

            if (f.PageCount > 0)
            {
                int result = f.ToXml(pathToXml, pageNumber, pageNumber);
            }

            string[] dataFile = File.ReadAllLines(pathToXml);

            for (int i = 0; i < dataFile.Length; i++)
            {
                if (dataFile[i].IndexOf("<table ") != -1 && dataFile[i].IndexOf("cols=\"" + cellsNumber.ToString() + "\"") != -1)
                {
                    i++;
                    for (; i < dataFile.Length; i++)
                    {
                        if (dataFile[i].IndexOf("</table>") != -1)
                        {
                            break;
                        }
                        else
                        {
                            if (dataFile[i].IndexOf("<row>") != -1)
                            {
                                Row row = new Row();

                                i++;

                                for (; i < dataFile.Length; i++)
                                {
                                    if (dataFile[i].IndexOf("</row>") != -1)
                                    {
                                        if (row.cells.Count == cellsNumber)
                                        {
                                            rows.Add(new Row(row));
                                        }
                                        row.Clear();
                                        break;
                                    }
                                    else
                                    {
                                        row.AddCell(dataFile[i]);
                                    }

                                }
                            }
                        }
                    }
                }
            }
            return rows;
        }

        private void LoadPDF_Click(object sender, RoutedEventArgs e)
        {
            // Configure open file dialog box
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            // dlg.FileName = ""; // Default file name
            dlg.DefaultExt = ".pdf"; // Default file extension
            dlg.Filter = "PDF documents (.pdf)|*.pdf"; // Filter files by extension

            // Show open file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            if (result == true)
            {
                pdfFilePath = dlg.FileName;
                FileName.Content = pdfFilePath;
            }
        }

        private void ParsePDF_Click(object sender, RoutedEventArgs e)
        {
            int columnCount = 0;
            int startPage = 0;
            int stopPage = 0;
            int footprintColumn = 0;
            string[] excludeWords = ExclusiveWordsPDF.Text.Split(';');

            if (!Int32.TryParse(ColumnCount.Text, out columnCount))
            {
                MessageBox.Show("Can not convert Column Count to int");
                return;
            }

            if (!Int32.TryParse(StartPage.Text, out startPage))
            {
                MessageBox.Show("Can not convert Start Page to int");
                return;
            }

            if (!Int32.TryParse(StopPage.Text, out stopPage))
            {
                MessageBox.Show("Can not convert Stop Page to int");
                return;
            }

            if (!Int32.TryParse(FootprintColumn.Text, out footprintColumn))
            {
                MessageBox.Show("Can not convert Footprint Column to int");
                return;
            }

            string[] mergeColumns = MergeColumn.Text.Split(',');

            List<int> mergeColumnsInt = new List<int>();

            foreach (string column in mergeColumns)
            {
                int number = 0;
                if (Int32.TryParse(column, out number))
                {
                    mergeColumnsInt.Add(number);
                }
            }

            string path = pdfFilePath;
            Console.WriteLine(path);

            List<Row> rows = new List<Row>();

            int maxPagesCount = stopPage - startPage;
            int pagesCount = 0;

            for (int i = startPage; i < (stopPage + 1); i++)
            {
                rows.AddRange(ReadPage(path, i, columnCount));
                
                pagesCount++;

                double value = 100 * pagesCount / maxPagesCount ;

                ParseProgressBar.Dispatcher.Invoke(() => ParseProgressBar.Value = value, DispatcherPriority.Background);
            }

            Regex rx = new Regex(@"^[A-Z]?\d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            string outputData = "";
            outPinsData = "";
            outPinsDesc = "";

            Pins.Clear();

            foreach (Row inrow in rows)
            {
                if (rx.IsMatch(inrow.cells[footprintColumn]))
                {
                    inrow.GetMergeCells(mergeColumnsInt.ToArray(), excludeWords);

                    outputData += inrow.cells[footprintColumn].ToString() + "\t" + inrow.GetDescription() + "\n";
                    
                    outPinsData += inrow.cells[footprintColumn].ToString() + "\n";
                    outPinsDesc += inrow.GetDescription() + "\r\n";

                    Pins.Add(new Pin(inrow.cells[footprintColumn].ToString(), inrow.cells[mergeColumnsInt.ToArray()[0]].ToString(), inrow.GetDescription()));
                }
            }
        }

        private void CopyPins_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(outPinsData);
        }

        private void CopyDesc_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(outPinsDesc);
        }

        private void Control_Click(object sender, RoutedEventArgs e)
        {
            foreach (Pin pin in Pins)
            {
                // Console.WriteLine(pin.selected.ToString() + "\t" + pin.designator);
                pin.selected = true;
            }
        }

        private void SelectAll_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox box = e.Source as CheckBox;

            bool check = box.IsChecked.Value;

            foreach(Pin pin in Pins)
            {
                pin.selected = check;
            }
        }

        private void ExportDescriptors_Click(object sender, RoutedEventArgs e)
        {
            string data = "";

            foreach(Pin pin in Pins)
            {
                if (pin.selected)
                {
                    data += pin.designator + "\n";
                }
            }

            Clipboard.SetText(data);
        }

        private void ExportNames_Click(object sender, RoutedEventArgs e)
        {
            string data = "";

            foreach (Pin pin in Pins)
            {
                if (pin.selected)
                {
                    data += pin.name + "\n";
                }
            }

            Clipboard.SetText(data);
        }

        private void ExportDescriptions_Click(object sender, RoutedEventArgs e)
        {
            string data = "";

            foreach (Pin pin in Pins)
            {
                if (pin.selected)
                {
                    data += pin.description + "\n";
                }
            }

            Clipboard.SetText(data);
        }

        private void PinSelected_Click(object sender, RoutedEventArgs e)
        {

            if (!(e.Source as CheckBox).IsChecked.Value)
            {
                SelectAll.IsChecked = false;
            }
            else
            {
                foreach(Pin pin in Pins)
                {
                    if (pin.selected == false)
                    {
                        SelectAll.IsChecked = false;
                        return;
                    }
                }

                SelectAll.IsChecked = true;
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            PinBox.Items.Refresh();
        }

        private void Pin_TextChanged(object sender, TextChangedEventArgs e)
        {
            PinBox.Items.Refresh();
        }

        private void PinInfo_TextChanged(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                if ((e.Source as TextBox).Name == "PinDesignator")
                {
                    selectedPin.designator = (e.Source as TextBox).Text;
                } 
                else if ((e.Source as TextBox).Name == "PinName")
                {
                    selectedPin.name = (e.Source as TextBox).Text;
                }
                else if ((e.Source as TextBox).Name == "PinDescription")
                {
                    selectedPin.description = (e.Source as TextBox).Text;
                }

                PinBox.Items.Refresh();
            }
        }

        private void PinBox_MenuItem_Delete(object sender, RoutedEventArgs e)
        {
            if (PinBox.SelectedIndex == -1)
            {
                return;
            }

            Pins.RemoveAt(PinBox.SelectedIndex);

            PinBox.Items.Refresh();
        }
        
        private void PinBox_MenuItem_New(object sender, RoutedEventArgs e)
        {
            if (PinBox.SelectedIndex == -1)
            {
                return;
            }

            Pins.Insert(PinBox.SelectedIndex + 1, new Pin("0", "0", "0"));
            PinBox.Items.Refresh();
        }

        private void PinBox_MenuItem_Duplicate(object sender, RoutedEventArgs e)
        {
            if (PinBox.SelectedIndex == -1)
            {
                return;
            }

            Pins.Insert(PinBox.SelectedIndex + 1, new Pin(Pins[PinBox.SelectedIndex]));
            PinBox.Items.Refresh();
        }
    }
}
