using backend.Models.DTOs;
using OfficeOpenXml;
using System.Text.RegularExpressions;

namespace backend.Services;

public interface IExcelParserService
{
    Task<(List<ExcelDataRow> ValidRows, List<string> Errors)> ParseExcelAsync(Stream fileStream, string phoneColumnName);
}

public class ExcelParserService : IExcelParserService
{
    public ExcelParserService()
    {
        // EPPlus 8+ requires the new static License API
        ExcelPackage.License.SetNonCommercialPersonal("WhatsApp Bulk Sender");
    }

    public async Task<(List<ExcelDataRow> ValidRows, List<string> Errors)> ParseExcelAsync(Stream fileStream, string phoneColumnName)
    {
        var validRows = new List<ExcelDataRow>();
        var errors = new List<string>();

        using (var package = new ExcelPackage(fileStream))
        {
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
            {
                errors.Add("Excel file is empty or missing worksheets.");
                return (validRows, errors);
            }

            var rowCount = worksheet.Dimension?.Rows ?? 0;
            var colCount = worksheet.Dimension?.Columns ?? 0;

            if (rowCount == 0 || colCount == 0)
            {
                errors.Add("Worksheet contains no data.");
                return (validRows, errors);
            }

            // 1. Find column headers
            var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var col = 1; col <= colCount; col++)
            {
                var headerText = worksheet.Cells[1, col].Text?.Trim();
                if (!string.IsNullOrEmpty(headerText))
                {
                    headers[headerText] = col;
                }
            }

            // 2. Validate Phone Column exists
            if (!headers.TryGetValue(phoneColumnName, out int phoneColumnIndex))
            {
                errors.Add($"Required column '{phoneColumnName}' not found in the header row.");
                return (validRows, errors);
            }

            // 3. Parse Data Rows
            for (var row = 2; row <= rowCount; row++)
            {
                var rowError = false;
                
                // Get Phone Number
                var rawPhone = worksheet.Cells[row, phoneColumnIndex].Text?.Trim();
                
                if (string.IsNullOrEmpty(rawPhone))
                {
                    errors.Add($"Row {row}: Phone number is empty.");
                    continue; // Skip empty rows
                }

                // Clean phone number (remove +, spaces, dashes)
                var cleanPhone = Regex.Replace(rawPhone, @"[^\d]", "");

                if (cleanPhone.Length < 10 || cleanPhone.Length > 15)
                {
                    errors.Add($"Row {row}: Phone number '{rawPhone}' is invalid. Must be 10-15 digits with country code.");
                    rowError = true;
                }

                if (!rowError)
                {
                    var dataRow = new ExcelDataRow { PhoneNumber = cleanPhone };

                    // Read other columns as variables
                    foreach (var header in headers)
                    {
                        if (header.Value != phoneColumnIndex)
                        {
                            var cellValue = worksheet.Cells[row, header.Value].Text?.Trim() ?? string.Empty;
                            dataRow.Variables[header.Key] = cellValue;
                        }
                    }

                    validRows.Add(dataRow);
                }
            }
        }

        return (validRows, errors);
    }
}
