namespace SpamDetection.Models;

/// <summary>
/// Represents extracted invoice data from a PDF document.
/// </summary>
public class InvoiceData
{
    /// <summary>
    /// The unique invoice number or identifier.
    /// </summary>
    public string? InvoiceNumber { get; set; }

    /// <summary>
    /// The name of the vendor/seller.
    /// </summary>
    public string? VendorName { get; set; }

    /// <summary>
    /// The date the invoice was issued (YYYY-MM-DD format).
    /// </summary>
    public string? InvoiceDate { get; set; }

    /// <summary>
    /// The total amount due on the invoice.
    /// </summary>
    public decimal? TotalAmount { get; set; }

    /// <summary>
    /// The currency code or symbol (e.g., USD, EUR).
    /// </summary>
    public string? Currency { get; set; }

    /// <summary>
    /// The date by which payment is due (YYYY-MM-DD format).
    /// </summary>
    public string? DueDate { get; set; }

    /// <summary>
    /// Payment terms description (e.g., Net 30, Due upon receipt).
    /// </summary>
    public string? PaymentTerms { get; set; }

    /// <summary>
    /// Collection of line items on the invoice.
    /// </summary>
    public List<InvoiceLineItem> LineItems { get; set; } = [];

    /// <summary>
    /// Subtotal amount before taxes.
    /// </summary>
    public decimal? Subtotal { get; set; }

    /// <summary>
    /// Tax amount.
    /// </summary>
    public decimal? Tax { get; set; }

    /// <summary>
    /// Current status of the invoice (e.g., draft, issued, paid, overdue).
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Any notes or observations about the extraction or document.
    /// </summary>
    public string? ExtractionNotes { get; set; }
}

/// <summary>
/// Represents a single line item on an invoice.
/// </summary>
public class InvoiceLineItem
{
    /// <summary>
    /// Description of the product or service.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Quantity ordered or delivered.
    /// </summary>
    public decimal? Quantity { get; set; }

    /// <summary>
    /// Price per unit.
    /// </summary>
    public decimal? UnitPrice { get; set; }

    /// <summary>
    /// Total price for this line item (quantity × unit price).
    /// </summary>
    public decimal? LineTotal { get; set; }
}
