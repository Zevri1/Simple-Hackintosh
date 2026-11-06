using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace SimpleHackintosh.App;

internal static class SafePlistXml
{
    private const long MaxDocumentCharacters = 5 * 1024 * 1024;

    public static XDocument Load(string xmlText)
    {
        if (string.IsNullOrWhiteSpace(xmlText))
            throw new InvalidDataException("The plist is empty.");
        if (xmlText.Length > MaxDocumentCharacters)
            throw new InvalidDataException("The XML exceeds the 5 MB safety limit.");

        var settings = new XmlReaderSettings
        {
            // Standard Apple property lists declare the public plist DTD.
            // Ignore the declaration rather than resolving or loading it.
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
            MaxCharactersInDocument = MaxDocumentCharacters,
            MaxCharactersFromEntities = 1024,
            IgnoreComments = false,
            IgnoreWhitespace = false,
            CloseInput = true
        };

        using var stringReader = new StringReader(xmlText);
        using var reader = XmlReader.Create(stringReader, settings);
        return XDocument.Load(reader, LoadOptions.None);
    }
}
