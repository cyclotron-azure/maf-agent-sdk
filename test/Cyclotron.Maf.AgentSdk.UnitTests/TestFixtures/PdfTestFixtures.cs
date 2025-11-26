using System.Text;

namespace Cyclotron.Maf.AgentSdk.UnitTests.TestFixtures;

/// <summary>
/// Provides minimal valid PDF test fixtures for unit testing.
/// These PDFs are valid but minimal, suitable for testing PDF parsing logic.
/// </summary>
public static class PdfTestFixtures
{
    /// <summary>
    /// Gets a minimal valid PDF with a single page containing "Hello World" text.
    /// This is the smallest valid PDF that PdfPig can parse.
    /// </summary>
    public static byte[] CreateMinimalPdf()
    {
        // Minimal valid PDF structure
        // Reference: PDF 1.4 specification
        var pdf = new StringBuilder();

        pdf.Append("%PDF-1.4\n");
        pdf.Append("%âãÏÓ\n");

        // Catalog object
        pdf.Append("1 0 obj\n");
        pdf.Append("<< /Type /Catalog /Pages 2 0 R >>\n");
        pdf.Append("endobj\n");

        // Pages object
        pdf.Append("2 0 obj\n");
        pdf.Append("<< /Type /Pages /Kids [3 0 R] /Count 1 >>\n");
        pdf.Append("endobj\n");

        // Page object
        pdf.Append("3 0 obj\n");
        pdf.Append("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\n");
        pdf.Append("endobj\n");

        // Content stream
        var contentStream = "BT /F1 12 Tf 100 700 Td (Hello World) Tj ET";
        pdf.Append("4 0 obj\n");
        pdf.Append($"<< /Length {contentStream.Length} >>\n");
        pdf.Append("stream\n");
        pdf.Append(contentStream + "\n");
        pdf.Append("endstream\n");
        pdf.Append("endobj\n");

        // Font object
        pdf.Append("5 0 obj\n");
        pdf.Append("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\n");
        pdf.Append("endobj\n");

        // Cross-reference table
        pdf.Append("xref\n");
        pdf.Append("0 6\n");
        pdf.Append("0000000000 65535 f \n");
        pdf.Append("0000000015 00000 n \n");
        pdf.Append("0000000066 00000 n \n");
        pdf.Append("0000000125 00000 n \n");
        pdf.Append("0000000266 00000 n \n");
        pdf.Append("0000000371 00000 n \n");

        // Trailer
        pdf.Append("trailer\n");
        pdf.Append("<< /Size 6 /Root 1 0 R >>\n");
        pdf.Append("startxref\n");
        pdf.Append("453\n");
        pdf.Append("%%EOF\n");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    /// <summary>
    /// Gets a valid PDF with multiple text blocks for testing layout analysis.
    /// </summary>
    public static byte[] CreateMultiBlockPdf()
    {
        var pdf = new StringBuilder();

        pdf.Append("%PDF-1.4\n");
        pdf.Append("%âãÏÓ\n");

        // Catalog object
        pdf.Append("1 0 obj\n");
        pdf.Append("<< /Type /Catalog /Pages 2 0 R >>\n");
        pdf.Append("endobj\n");

        // Pages object
        pdf.Append("2 0 obj\n");
        pdf.Append("<< /Type /Pages /Kids [3 0 R] /Count 1 >>\n");
        pdf.Append("endobj\n");

        // Page object
        pdf.Append("3 0 obj\n");
        pdf.Append("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\n");
        pdf.Append("endobj\n");

        // Content stream with multiple text blocks
        var contentStream = new StringBuilder();
        contentStream.Append("BT /F1 16 Tf 100 700 Td (Document Title) Tj ET ");
        contentStream.Append("BT /F1 12 Tf 100 650 Td (First paragraph with some content.) Tj ET ");
        contentStream.Append("BT /F1 12 Tf 100 600 Td (Second paragraph with different content.) Tj ET ");
        contentStream.Append("BT /F1 12 Tf 100 550 Td (Third paragraph for testing.) Tj ET");

        var contentStr = contentStream.ToString();
        pdf.Append("4 0 obj\n");
        pdf.Append($"<< /Length {contentStr.Length} >>\n");
        pdf.Append("stream\n");
        pdf.Append(contentStr + "\n");
        pdf.Append("endstream\n");
        pdf.Append("endobj\n");

        // Font object
        pdf.Append("5 0 obj\n");
        pdf.Append("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\n");
        pdf.Append("endobj\n");

        // Cross-reference table
        pdf.Append("xref\n");
        pdf.Append("0 6\n");
        pdf.Append("0000000000 65535 f \n");
        pdf.Append("0000000015 00000 n \n");
        pdf.Append("0000000066 00000 n \n");
        pdf.Append("0000000125 00000 n \n");
        pdf.Append("0000000266 00000 n \n");
        pdf.Append("0000000600 00000 n \n");

        // Trailer
        pdf.Append("trailer\n");
        pdf.Append("<< /Size 6 /Root 1 0 R >>\n");
        pdf.Append("startxref\n");
        pdf.Append("680\n");
        pdf.Append("%%EOF\n");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    /// <summary>
    /// Gets a valid PDF with multiple pages for testing page handling.
    /// </summary>
    public static byte[] CreateMultiPagePdf()
    {
        var pdf = new StringBuilder();

        pdf.Append("%PDF-1.4\n");
        pdf.Append("%âãÏÓ\n");

        // Catalog object
        pdf.Append("1 0 obj\n");
        pdf.Append("<< /Type /Catalog /Pages 2 0 R >>\n");
        pdf.Append("endobj\n");

        // Pages object with 2 pages
        pdf.Append("2 0 obj\n");
        pdf.Append("<< /Type /Pages /Kids [3 0 R 6 0 R] /Count 2 >>\n");
        pdf.Append("endobj\n");

        // First page object
        pdf.Append("3 0 obj\n");
        pdf.Append("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\n");
        pdf.Append("endobj\n");

        // First page content
        var content1 = "BT /F1 12 Tf 100 700 Td (Page 1 Content) Tj ET";
        pdf.Append("4 0 obj\n");
        pdf.Append($"<< /Length {content1.Length} >>\n");
        pdf.Append("stream\n");
        pdf.Append(content1 + "\n");
        pdf.Append("endstream\n");
        pdf.Append("endobj\n");

        // Font object (shared)
        pdf.Append("5 0 obj\n");
        pdf.Append("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\n");
        pdf.Append("endobj\n");

        // Second page object
        pdf.Append("6 0 obj\n");
        pdf.Append("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 7 0 R /Resources << /Font << /F1 5 0 R >> >> >>\n");
        pdf.Append("endobj\n");

        // Second page content
        var content2 = "BT /F1 12 Tf 100 700 Td (Page 2 Content) Tj ET";
        pdf.Append("7 0 obj\n");
        pdf.Append($"<< /Length {content2.Length} >>\n");
        pdf.Append("stream\n");
        pdf.Append(content2 + "\n");
        pdf.Append("endstream\n");
        pdf.Append("endobj\n");

        // Cross-reference table
        pdf.Append("xref\n");
        pdf.Append("0 8\n");
        pdf.Append("0000000000 65535 f \n");
        pdf.Append("0000000015 00000 n \n");
        pdf.Append("0000000066 00000 n \n");
        pdf.Append("0000000132 00000 n \n");
        pdf.Append("0000000273 00000 n \n");
        pdf.Append("0000000377 00000 n \n");
        pdf.Append("0000000456 00000 n \n");
        pdf.Append("0000000597 00000 n \n");

        // Trailer
        pdf.Append("trailer\n");
        pdf.Append("<< /Size 8 /Root 1 0 R >>\n");
        pdf.Append("startxref\n");
        pdf.Append("700\n");
        pdf.Append("%%EOF\n");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    /// <summary>
    /// Gets a valid PDF with an empty page (no content).
    /// </summary>
    public static byte[] CreateEmptyPagePdf()
    {
        var pdf = new StringBuilder();

        pdf.Append("%PDF-1.4\n");
        pdf.Append("%âãÏÓ\n");

        // Catalog object
        pdf.Append("1 0 obj\n");
        pdf.Append("<< /Type /Catalog /Pages 2 0 R >>\n");
        pdf.Append("endobj\n");

        // Pages object
        pdf.Append("2 0 obj\n");
        pdf.Append("<< /Type /Pages /Kids [3 0 R] /Count 1 >>\n");
        pdf.Append("endobj\n");

        // Page object with no content stream
        pdf.Append("3 0 obj\n");
        pdf.Append("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>\n");
        pdf.Append("endobj\n");

        // Cross-reference table
        pdf.Append("xref\n");
        pdf.Append("0 4\n");
        pdf.Append("0000000000 65535 f \n");
        pdf.Append("0000000015 00000 n \n");
        pdf.Append("0000000066 00000 n \n");
        pdf.Append("0000000125 00000 n \n");

        // Trailer
        pdf.Append("trailer\n");
        pdf.Append("<< /Size 4 /Root 1 0 R >>\n");
        pdf.Append("startxref\n");
        pdf.Append("205\n");
        pdf.Append("%%EOF\n");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    /// <summary>
    /// Creates a temporary PDF file and returns its path.
    /// The caller is responsible for cleaning up the file.
    /// </summary>
    /// <param name="pdfBytes">The PDF bytes to write.</param>
    /// <returns>The path to the temporary PDF file.</returns>
    public static string CreateTempPdfFile(byte[] pdfBytes)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.pdf");
        File.WriteAllBytes(tempPath, pdfBytes);
        return tempPath;
    }

    /// <summary>
    /// Creates a temporary directory for output files and returns its path.
    /// The caller is responsible for cleaning up the directory.
    /// </summary>
    /// <returns>The path to the temporary output directory.</returns>
    public static string CreateTempOutputDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"pdf_output_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }
}
