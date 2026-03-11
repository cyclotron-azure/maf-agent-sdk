using UglyToad.PdfPig.Filters;
using UglyToad.PdfPig.Filters.Jpx.OpenJpeg;
using UglyToad.PdfPig.Tokens;

namespace Cyclotron.Maf.AgentSdk.Services.Impl;

/// <summary>
/// A PdfPig filter provider that extends the standard filter set with JPX (JPEG 2000) decoding
/// support via the OpenJpegDotNet native library.
/// </summary>
/// <remarks>
/// DefaultFilterProvider can't be used because it uses the JpxDecodeFilter which relies on
/// the Jpeg2000.Net library, which is a pure .NET implementation of JPEG 2000 decoding. This
/// library has been found to have issues with certain JPEG 2000 images, leading to failures
/// in image extraction. By creating a custom filter provider that uses OpenJpegJpxDecodeFilter
/// instead, we can leverage the OpenJpeg library's more robust handling of JPEG 2000 images,
/// improving the reliability of image extraction from PDFs that use this format. We can't just replace the JpxDecodeFilter in the DefaultFilterProvider because it's
/// a ReadOnlyDictionary.
/// This change causes image.TryGetPng() to return true for JPX images which ultimately
/// makes <see cref="PdfPigImageExtractor"/> return an actual png image instead of a zero byte jpg
/// </remarks>
public class JpxFilterProvider : BaseFilterProvider
{
    public JpxFilterProvider() : base(GetFilters())
    {
    }

    private static Dictionary<string, IFilter> GetFilters()
    {
        var ascii85 = new Ascii85Filter();
        var asciiHex = new AsciiHexDecodeFilter();
        var ccitt = new CcittFaxDecodeFilter();
        var dct = new DctDecodeFilter();
        var flate = new FlateFilter();
        var jbig2 = new Jbig2DecodeFilter();
        var jpx = new OpenJpegJpxDecodeFilter();
        var runLength = new RunLengthFilter();
        var lzw = new LzwFilter();

        return new Dictionary<string, IFilter>
        {
            { NameToken.Ascii85Decode.Data, ascii85 },
            { NameToken.Ascii85DecodeAbbreviation.Data, ascii85 },
            { NameToken.AsciiHexDecode.Data, asciiHex },
            { NameToken.AsciiHexDecodeAbbreviation.Data, asciiHex },
            { NameToken.CcittfaxDecode.Data, ccitt },
            { NameToken.CcittfaxDecodeAbbreviation.Data, ccitt },
            { NameToken.DctDecode.Data, dct },
            { NameToken.DctDecodeAbbreviation.Data, dct },
            { NameToken.FlateDecode.Data, flate },
            { NameToken.FlateDecodeAbbreviation.Data, flate },
            { NameToken.Jbig2Decode.Data, jbig2 },
            { NameToken.JpxDecode.Data, jpx },
            { NameToken.RunLengthDecode.Data, runLength },
            { NameToken.RunLengthDecodeAbbreviation.Data, runLength },
            { NameToken.LzwDecode.Data, lzw },
            { NameToken.LzwDecodeAbbreviation.Data, lzw },
        };
    }
}
