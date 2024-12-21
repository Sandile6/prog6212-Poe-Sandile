using Microsoft.Win32;
using System;
using Microsoft.Data.Sqlite;
using System.Windows;
using System.IO;


namespace CMCS_POE_PROG6212
{
    public partial class MainWindow : Window
    {

        private const string ConnectionString = "Data Source=claims.db";
        private string _uploadedDocumentPath;

        public MainWindow()
        {
            InitializeComponent();
            CreateDatabaseAndTable();
            LoadClaims();
        }
        // Upload Document Logic
        private void UploadDocument_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "All Files|*.*|PDF Files (*.pdf)|*.pdf|Word Documents (*.docx)|*.docx|Excel Files (*.xlsx)|*.xlsx";

            if (openFileDialog.ShowDialog() == true)
            {
                _uploadedDocumentPath = openFileDialog.FileName; // Store the document path
                MessageBox.Show($"Document '{_uploadedDocumentPath}' selected for upload.", "Document Selected", MessageBoxButton.OK);
            }
        }

        // Submit Claim and Save to Database
        private void SubmitClaim_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                double hours = Convert.ToDouble(txtHours.Text);
                double rate = Convert.ToDouble(txtRate.Text);

                if (hours < 0 || rate < 0)
                {
                    MessageBox.Show("Hours worked and hourly rate must be non-negative.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                double totalAmount = hours * rate;

                using (var connection = new SqliteConnection(ConnectionString))
                {
                    connection.Open();
                    string query = "INSERT INTO Claims (HoursWorked, HourlyRate, TotalAmount, Status, SubmittedAt, DocumentPath) " +
                                   "VALUES (@Hours, @Rate, @Total, 'Pending', @SubmittedAt, @DocumentPath)";
                    var command = new SqliteCommand(query, connection);
                    command.Parameters.AddWithValue("@Hours", hours);
                    command.Parameters.AddWithValue("@Rate", rate);
                    command.Parameters.AddWithValue("@Total", totalAmount);
                    command.Parameters.AddWithValue("@SubmittedAt", DateTime.Now);
                    command.Parameters.AddWithValue("@DocumentPath", _uploadedDocumentPath ?? (object)DBNull.Value);

                    command.ExecuteNonQuery();
                }

                MessageBox.Show($"Claim Submitted! Total Amount: R{totalAmount:F2}", "Success", MessageBoxButton.OK);
                txtHours.Clear();
                txtRate.Clear();
                LoadClaims();
                _uploadedDocumentPath = null; // Reset the document path after submission
            }
            catch (FormatException)
            {
                MessageBox.Show("Invalid input. Please enter valid numbers for hours and rate.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateDatabaseAndTable()
        {
            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();

                string tableCreationQuery = @"
            CREATE TABLE IF NOT EXISTS Claims (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                HoursWorked REAL NOT NULL,
                HourlyRate REAL NOT NULL,
                TotalAmount REAL NOT NULL,
                Status TEXT NOT NULL CHECK (Status IN ('Pending', 'Approved', 'Rejected')),
                SubmittedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                DocumentPath TEXT
            );";

                var command = new SqliteCommand(tableCreationQuery, connection);
                command.ExecuteNonQuery();
            }
        }

        private void LoadClaims()
        {
            lstClaims.Items.Clear();
            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();
                string query = "SELECT Id, HoursWorked, HourlyRate, TotalAmount, Status, DocumentPath FROM Claims WHERE Status = 'Pending'";
                var command = new SqliteCommand(query, connection);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string claim = $"ID: {reader.GetInt32(0)}, Total: R{reader.GetDouble(3):F2}, Status: {reader.GetString(4)}, Document: {(reader.IsDBNull(5) ? "No Document" : "Available")}";
                        lstClaims.Items.Add(claim);
                    }
                }
            }
        }

        // Approve Selected Claim
        private void ApproveClaim_Click(object sender, RoutedEventArgs e)
        {
            UpdateClaimStatus("Approved");
        }

        // Reject Selected Claim
        private void RejectClaim_Click(object sender, RoutedEventArgs e)
        {
            UpdateClaimStatus("Rejected");
        }

        // Update Claim Status in Database
        private void UpdateClaimStatus(string newStatus)
        {
            if (lstClaims.SelectedItem != null)
            {
                string selectedClaim = lstClaims.SelectedItem.ToString();
                int claimId = int.Parse(selectedClaim.Split(',')[0].Split(':')[1].Trim());

                using (var connection = new SqliteConnection(ConnectionString))
                {
                    connection.Open();
                    string query = "UPDATE Claims SET Status = @Status WHERE Id = @Id";
                    var command = new SqliteCommand(query, connection);
                    command.Parameters.AddWithValue("@Status", newStatus);
                    command.Parameters.AddWithValue("@Id", claimId);
                    command.ExecuteNonQuery();
                }

                MessageBox.Show($"Claim {newStatus}.", "Success", MessageBoxButton.OK);
                LoadClaims();
            }
            else
            {
                MessageBox.Show("Please select a claim to proceed.", "Selection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }


        private void txtHours_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            CalculateTotal();
        }

        private void txtRate_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            CalculateTotal();
        }

        private void CalculateTotal()
        {
            if (double.TryParse(txtHours.Text, out double hours) && double.TryParse(txtRate.Text, out double rate))
            {
                double totalAmount = hours * rate;
                MessageBox.Show($"Total Amount: R{totalAmount:F2}", "Calculation", MessageBoxButton.OK);
            }
        }

        private void GenerateReport_Click(object sender, RoutedEventArgs e)
        {
            GenerateReport();
        }

        private void GenerateReport()
        {
            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();
                string query = "SELECT ID, HoursWorked, HourlyRate, TotalAmount, SubmittedAt FROM Claims WHERE Status = 'Approved'";
                var command = new SqliteCommand(query, connection);

                using (var reader = command.ExecuteReader())
                {
                    string report = "===================================================================\n";
                    report += "        Approved Claims       \n";
                    report += "=========================================================================\n";
                    report += "ID\tHours Worked\tHourly Rate\tTotal Amount\tSubmitted At\n";
                    report += "---------------------------------------------------------------\n";


                    while (reader.Read())
                    {
                        int id = reader.GetInt32(0);
                        double hoursWorked = reader.GetDouble(1);
                        double hourlyRate = reader.GetDouble(2);
                        double totalAmount = reader.GetDouble(3);
                        DateTime submittedAt = reader.GetDateTime(4);

                        report += $"{id}\t{hoursWorked}\tR{hourlyRate:F2}\tR{totalAmount:F2}\t{submittedAt}\n";
                    }

                    report += "==========================================================================\n";
                    report += "End of Report\n";
                    report += "==========================================================================\n";

                    var result = MessageBox.Show("Would you like to view the report on the screen instead of downloading it?",
                                          "View or Download Report",
                                          MessageBoxButton.YesNo,
                                          MessageBoxImage.Question);

                    if (result == MessageBoxResult.No)
                    {

                        SaveReportToFile(report);

                    }
                    else
                    {

                        MessageBox.Show($"Report saved successfully to {report}", "Report Saved", MessageBoxButton.OK);
                    }
                }

            }
        }

        private void SaveReportToFile(string reportContent)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                Title = "Save Report"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(saveFileDialog.FileName, reportContent);
                    MessageBox.Show("Report saved successfully!", "Report Saved", MessageBoxButton.OK);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred while saving the report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }


        // View Document Logic
        private void ViewDocument_Click(object sender, RoutedEventArgs e)
        {
            if (lstClaims.SelectedItem != null)
            {
                string selectedClaim = lstClaims.SelectedItem.ToString();
                int claimId = int.Parse(selectedClaim.Split(',')[0].Split(':')[1].Trim());

                using (var connection = new SqliteConnection(ConnectionString))
                {
                    connection.Open();
                    string query = "SELECT DocumentPath FROM Claims WHERE Id = @Id";
                    var command = new SqliteCommand(query, connection);
                    command.Parameters.AddWithValue("@Id", claimId);

                    var documentPath = command.ExecuteScalar() as string;

                    if (!string.IsNullOrEmpty(documentPath) && System.IO.File.Exists(documentPath))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = documentPath,
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        MessageBox.Show("Document not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            else
            {
                MessageBox.Show("Please select a claim to view the document.", "Selection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
