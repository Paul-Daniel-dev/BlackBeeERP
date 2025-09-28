using BlackBeeERP.Models;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;
using Syncfusion.Drawing;
using System;
using System.IO;

namespace BlackBeeERP.Services;

public class InvoiceService
{
    public byte[] GenerateInvoicePdf(Order order)
    {
        // Create a new PDF document
        using (PdfDocument document = new PdfDocument())
        {
            // Add a page
            PdfPage page = document.Pages.Add();

            // Create PDF graphics for the page
            PdfGraphics graphics = page.Graphics;

            // Set up fonts
            PdfStandardFont headerFont = new PdfStandardFont(PdfFontFamily.Helvetica, 20, PdfFontStyle.Bold);
            PdfStandardFont normalFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10);
            PdfStandardFont boldFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10, PdfFontStyle.Bold);

            // Page dimensions
            float pageWidth = page.GetClientSize().Width;
            float margin = 50;
            float availableWidth = pageWidth - (2 * margin);
            float yPosition = margin;
            float leftMargin = margin;
            float rightMargin = pageWidth - margin;

            // Header - Company Info (Left Side)
            graphics.DrawString("BlackBee ERP", headerFont, PdfBrushes.Black, new PointF(leftMargin, yPosition));
            yPosition += 25;
            graphics.DrawString("123 Business Street", normalFont, PdfBrushes.Black, new PointF(leftMargin, yPosition));
            yPosition += 15;
            graphics.DrawString("Cape Town, South Africa", normalFont, PdfBrushes.Black, new PointF(leftMargin, yPosition));
            yPosition += 15;
            graphics.DrawString("info@blackbee-erp.com", normalFont, PdfBrushes.Black, new PointF(leftMargin, yPosition));

            // Reset Y position for invoice details
            yPosition = margin;

            // Header - Invoice Details (Right Side)
            SizeF invoiceTextSize = headerFont.MeasureString("INVOICE");
            graphics.DrawString("INVOICE", headerFont, PdfBrushes.Black, new PointF(rightMargin - invoiceTextSize.Width, yPosition));
            yPosition += 25;

            string invoiceNumber = $"Invoice Number: {order.Id}";
            SizeF numberSize = normalFont.MeasureString(invoiceNumber);
            graphics.DrawString(invoiceNumber, normalFont, PdfBrushes.Black, new PointF(rightMargin - numberSize.Width, yPosition));
            yPosition += 15;

            string dateText = $"Date: {order.OrderDate.ToShortDateString()}";
            SizeF dateSize = normalFont.MeasureString(dateText);
            graphics.DrawString(dateText, normalFont, PdfBrushes.Black, new PointF(rightMargin - dateSize.Width, yPosition));
            yPosition += 15;

            string statusText = $"Status: {order.Status}";
            SizeF statusSize = normalFont.MeasureString(statusText);
            graphics.DrawString(statusText, normalFont, PdfBrushes.Black, new PointF(rightMargin - statusSize.Width, yPosition));

            // Start content area below the header
            yPosition = margin + 100;

            // Customer details
            graphics.DrawString("Bill To:", boldFont, PdfBrushes.Black, new PointF(leftMargin, yPosition));
            yPosition += 15;
            graphics.DrawString(order.Customer?.Name ?? "Customer Name", normalFont, PdfBrushes.Black, new PointF(leftMargin, yPosition));
            yPosition += 15;
            graphics.DrawString(order.Customer?.Email ?? "Email", normalFont, PdfBrushes.Black, new PointF(leftMargin, yPosition));
            yPosition += 15;
            graphics.DrawString(order.Customer?.Phone ?? "Phone", normalFont, PdfBrushes.Black, new PointF(leftMargin, yPosition));
            yPosition += 15;
            graphics.DrawString(order.Customer?.Address ?? "Address", normalFont, PdfBrushes.Black, new PointF(leftMargin, yPosition));
            yPosition += 30;

            // Create a PDF grid for order items
            PdfGrid grid = new PdfGrid();
            grid.Style.Font = normalFont;

            // Define columns
            grid.Columns.Add(4);

            // Add headers
            PdfGridRow header = grid.Headers.Add(1)[0];
            header.Cells[0].Value = "Product";
            header.Cells[1].Value = "Unit Price";
            header.Cells[2].Value = "Quantity";
            header.Cells[3].Value = "Subtotal";

            // Format header - fix PdfColor usage
            PdfGridCellStyle headerStyle = new PdfGridCellStyle();
            // Use RGB constructor directly
            PdfColor headerColor = new PdfColor(204, 204, 204);
            headerStyle.Borders.All = new PdfPen(headerColor);
            headerStyle.BackgroundBrush = new PdfSolidBrush(headerColor);
            headerStyle.Font = boldFont;

            // Apply formatting to header cells
            for (int i = 0; i < header.Cells.Count; i++)
            {
                header.Cells[i].Style = headerStyle;

                // Align right for numeric columns
                if (i > 0)
                {
                    header.Cells[i].StringFormat = new PdfStringFormat()
                    {
                        Alignment = PdfTextAlignment.Right,
                        LineAlignment = PdfVerticalAlignment.Middle
                    };
                }
                else
                {
                    header.Cells[i].StringFormat = new PdfStringFormat()
                    {
                        Alignment = PdfTextAlignment.Left,
                        LineAlignment = PdfVerticalAlignment.Middle
                    };
                }
            }

            // Add order items
            foreach (var item in order.OrderItems)
            {
                PdfGridRow row = grid.Rows.Add();
                row.Cells[0].Value = item.Product?.Name ?? $"Product #{item.ProductId}";
                row.Cells[1].Value = $"R {item.UnitPrice:N2}";
                row.Cells[2].Value = item.Quantity.ToString();
                row.Cells[3].Value = $"R {(item.UnitPrice * item.Quantity):N2}";

                // Align right for numeric columns
                for (int i = 1; i < row.Cells.Count; i++)
                {
                    row.Cells[i].StringFormat = new PdfStringFormat()
                    {
                        Alignment = PdfTextAlignment.Right,
                        LineAlignment = PdfVerticalAlignment.Middle
                    };
                }
            }

            // Fix cell style setting - apply to individual cells instead
            PdfPen lightBorderPen = new PdfPen(new PdfColor(220, 220, 220));
            PdfPen bottomBorderPen = new PdfPen(new PdfColor(180, 180, 180));

            // Apply style to each cell instead of using CellStyle property
            foreach (PdfGridRow row in grid.Rows)
            {
                for (int i = 0; i < row.Cells.Count; i++)
                {
                    PdfGridCellStyle style = new PdfGridCellStyle();
                    style.Borders.All = lightBorderPen;
                    style.Borders.Bottom = bottomBorderPen;
                    style.CellPadding = new PdfPaddings(5, 5, 5, 5);
                    row.Cells[i].Style = style;
                }
            }

            // Apply column widths
            grid.Columns[0].Width = availableWidth * 0.45f;  // Product (wider)
            grid.Columns[1].Width = availableWidth * 0.2f;   // Unit Price
            grid.Columns[2].Width = availableWidth * 0.15f;  // Quantity
            grid.Columns[3].Width = availableWidth * 0.2f;   // Subtotal

            // Draw the grid and measure its size
            PdfLayoutResult result = grid.Draw(page, new PointF(leftMargin, yPosition));

            // Fix grid height issue - use the result's bounds instead
            float gridBottom = result.Bounds.Bottom;
            yPosition = gridBottom + 20;

            // Add Total
            string totalText = "Total:";
            string totalAmount = $"R {order.TotalAmount:N2}";
            SizeF totalTextSize = boldFont.MeasureString(totalText);
            SizeF totalAmountSize = boldFont.MeasureString(totalAmount);

            graphics.DrawString(totalText, boldFont, PdfBrushes.Black,
                new PointF(rightMargin - totalAmountSize.Width - totalTextSize.Width - 20, yPosition));

            graphics.DrawString(totalAmount, boldFont, PdfBrushes.Black,
                new PointF(rightMargin - totalAmountSize.Width, yPosition));

            // Add footer
            string footerText = $"Thank you for your business! Generated on {DateTime.Now:yyyy-MM-dd HH:mm}";
            SizeF footerSize = normalFont.MeasureString(footerText);
            float footerY = page.GetClientSize().Height - 50; // 50 from bottom
            float footerX = (pageWidth - footerSize.Width) / 2;

            graphics.DrawString(footerText, normalFont, PdfBrushes.Black, new PointF(footerX, footerY));

            // Save the PDF to memory stream
            using (MemoryStream stream = new MemoryStream())
            {
                document.Save(stream);
                return stream.ToArray();
            }
        }
    }
}