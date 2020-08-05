using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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

namespace Prog
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            datePicker_DateFrom.PreviewMouseDown += Event_DatePickerDefault;
            datePicker_DateTo.PreviewMouseDown += Event_DatePickerDefault;
        }

        private void Event_DatePickerDefault(object sender, RoutedEventArgs e)
        {
            datePicker_DateTo.ClearValue(Border.BorderBrushProperty);
            datePicker_DateFrom.ClearValue(Border.BorderBrushProperty);
        }

        private void button_Browse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "Select a table";
            openFileDialog.Filter = "Excel files (*.xls, *.xlsx)|*.xls;*.xlsx|All files (*.*)|*.*";
            openFileDialog.FileName = String.Empty;
            if (openFileDialog.ShowDialog() == true)
            {
                //Encoding.GetEncoding("UTF-8")
                string filePath = openFileDialog.FileName;
                Debug.WriteLine(filePath, "Debug");
                textBox_FilePath.ClearValue(Border.BorderBrushProperty);
                textBox_FilePath.Text = filePath;
            }
        }

        private void button_GeneratePayroll_Click(object sender, RoutedEventArgs e)
        {
            //connection string creation and data validation
            string connectionString = "";
            string fileExtension = System.IO.Path.GetExtension(textBox_FilePath.Text);
            Debug.WriteLine(fileExtension, "Debug");
            if (fileExtension.ToLower() == ".xls" || fileExtension.ToLower() == ".xlsx")
            {
                // if the File extension is .XLS or .XLSX using below connection string
                connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0; Data Source={textBox_FilePath.Text}; Extended Properties=\"Excel 12.0;HDR=YES;IMEX=1;TypeGuessRows=0;ImportMixedTypes=Text;\"";
            }
            else
            {
                if (textBox_FilePath.Text == String.Empty)
                    Debug.WriteLine("The file path is empty", "Debug");
                else
                    Debug.WriteLine("The file extension is not supported: " + fileExtension, "Debug");
                textBox_FilePath.BorderBrush = Brushes.Red;
                return;
            }

            //dates validation
            if (datePicker_DateTo.SelectedDate == null)
            {
                datePicker_DateTo.BorderBrush = Brushes.Red;
                return;
            }

            if (datePicker_DateFrom.SelectedDate == null)
            {
                datePicker_DateFrom.BorderBrush = Brushes.Red;
                return;
            }

            if (datePicker_DateTo.SelectedDate < datePicker_DateFrom.SelectedDate)
            {
                datePicker_DateTo.BorderBrush = Brushes.Red;
                datePicker_DateFrom.BorderBrush = Brushes.Red;
                return;
            }

            DataSet cargoDataSet = new DataSet();
            DataSet tariffDataSet = new DataSet();

            try
            {
                //filling dataGrid
                using (OleDbConnection conn = new OleDbConnection(connectionString))
                {

                    conn.Open();
                    OleDbDataAdapter objDA_tariff = new OleDbDataAdapter("select * from [Тариф$]", conn);
                    objDA_tariff.Fill(tariffDataSet);
                    OleDbDataAdapter objDA_cargo = new OleDbDataAdapter("select * from [Груз$]", conn);
                    objDA_cargo.Fill(cargoDataSet);
                }
                dataGrid_Payroll.ItemsSource = null;

                dataGrid_Payroll.ItemsSource = PrintRows(cargoDataSet, tariffDataSet)?.Tables["Table"].DefaultView;
            }
            catch (OleDbException exc)
            {
                Debug.WriteLine("\"Груз\" or \"Тариф\" not founded", "Warning");
                MessageBox.Show("\"Груз\" or \"Тариф\" not founded");
                return;
            }
            catch (Exception exc)
            {
                Debug.WriteLine(exc, "Warning");
                return;
            }
        }

        private DataSet PrintRows(DataSet cargoDataSet, DataSet tariffDataSet)
        {
            //Start and end date validation
            DateTime dateFrom = datePicker_DateFrom.SelectedDate.Value.Date;
            DateTime dateTo = new DateTime(datePicker_DateTo.SelectedDate.Value.Year,
                datePicker_DateTo.SelectedDate.Value.Month,
                datePicker_DateTo.SelectedDate.Value.Day,
                23, 59, 59, 999);
            Debug.WriteLine("DateFrom: " + dateFrom, "Debug");
            Debug.WriteLine("DateTo:   " + dateTo, "Debug");

            //Creating a new table and columns
            DataSet resultDataSet = new DataSet("Payroll");
            DataTable resultTable = resultDataSet.Tables.Add("Table");

            resultTable.Columns.Add("Груз", typeof(string));
            resultTable.Columns.Add("Дата прихода на склад", typeof(string));
            resultTable.Columns.Add("Дата ухода со склада", typeof(string));
            resultTable.Columns.Add("Начало расчета", typeof(string));
            resultTable.Columns.Add("Окончание расчета", typeof(string));
            resultTable.Columns.Add("Кол-во дней хранения", typeof(int));
            resultTable.Columns.Add("Ставка", typeof(int));
            resultTable.Columns.Add("Примечание", typeof(string));

            //Adding rows and calculation data to the new table
            foreach (DataRow cargoRow in cargoDataSet.Tables[0].Rows)
            {
                try
                {
                    DateTime dateStart = (DateTime)cargoRow["Дата прихода на склад"];
                    DateTime dateEnd;
                    if (cargoRow["Дата ухода со склада"] is DBNull)
                        dateEnd = dateTo;
                    else
                        dateEnd = (DateTime)cargoRow["Дата ухода со склада"];

                    if (dateEnd < dateFrom || dateStart > dateTo) continue;

                    foreach (DataRow tariffRow in tariffDataSet.Tables[0].Rows)
                    {
                        double tariffStart = Convert.ToDouble(tariffRow["Начало периода"]);
                        DateTime dateStart_Period = tariffStart > 0 ?
                            dateStart.Date.AddDays(--tariffStart) : dateStart;

                        DateTime dateEnd_Period = tariffRow["Окончание периода"] is DBNull ?
                            dateTo : dateStart.Date.AddDays(Convert.ToDouble(tariffRow["Окончание периода"])).AddSeconds(-1);

                        dateEnd_Period = dateEnd_Period < dateTo ? dateEnd_Period : dateTo;

                        if (dateStart_Period < dateEnd && dateStart_Period < dateEnd_Period
                            && !(dateEnd_Period < dateFrom || dateStart_Period > dateTo))
                        {
                            DateTime dateEnd_Result;
                            if (dateEnd <= dateEnd_Period)
                                dateEnd_Result = dateEnd.Date.AddDays(1).AddSeconds(-1);
                            else
                                dateEnd_Result = dateEnd_Period;

                            resultTable.Rows.Add(new Object[] {
                                cargoRow["Груз"],
                                dateStart.ToString("dd.MM.yyyy HH:mm"),
                                dateEnd.ToString("dd.MM.yyyy HH:mm"),
                                dateStart_Period.ToString("dd.MM.yyyy HH:mm"),
                                dateEnd_Result.ToString("dd.MM.yyyy HH:mm"),
                                1 + Convert.ToInt32((dateEnd_Result.Date - dateStart_Period.Date).TotalDays),
                                Convert.ToInt32(tariffRow["Ставка"]),
                                "Период №" + tariffRow["№"] });
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e, "Warning");
                    return null;
                }
            }
            return resultDataSet;
        }

        private void button_ClearFilePath_Click(object sender, RoutedEventArgs e)
        {
            textBox_FilePath.ClearValue(Border.BorderBrushProperty);
            textBox_FilePath.Clear();
        }
    }
}