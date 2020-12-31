using System;
using System.Collections.Generic;
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
    public partial class MainWindow : Window
    {
        string pdfFilePath = "";
        string outPinsData = "";
        string outPinsDesc = "";


        public MainWindow()
        {
            InitializeComponent();
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

                if (data[data.Length - 1] == ',')
                {
                    data = data.Remove(data.Length - 1);
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

            Regex rx = new Regex(@"^[A-Z]?\d+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            string outputData = "";
            outPinsData = "";
            outPinsDesc = "";

            foreach (Row inrow in rows)
            {
                if (rx.IsMatch(inrow.cells[footprintColumn]))
                {
                    inrow.GetMergeCells(mergeColumnsInt.ToArray(), excludeWords);


                    outputData += inrow.cells[footprintColumn].ToString() + "\t" + inrow.GetDescription() + "\n";
                    
                    outPinsData += inrow.cells[footprintColumn].ToString() + "\n";
                    outPinsDesc += inrow.GetDescription() + "\r\n";
                }
            }

            OutData.Text = outputData;
        }

        private void CopyPins_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(outPinsData);
        }

        private void CopyDesc_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(outPinsDesc);
        }
    }
}
