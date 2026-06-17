using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Xml.Linq;

namespace PromptBar
{
    public static class ScriptFileIO
    {
        public static string ImportText(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".docx")
            {
                return ImportDocxText(path);
            }

            if (ext == ".rtf")
            {
                return ImportRtfText(path);
            }

            byte[] bytes = File.ReadAllBytes(path);
            Encoding[] encodings = new Encoding[]
            {
                new UTF8Encoding(false, true),
                Encoding.Unicode,
                Encoding.BigEndianUnicode,
                Encoding.UTF32,
                Encoding.Default
            };

            for (int i = 0; i < encodings.Length; i++)
            {
                try
                {
                    string decoded = encodings[i].GetString(bytes);
                    if (decoded.Trim().Length > 0)
                    {
                    return ScriptTextSanitizer.Clean(decoded);
                    }
                }
                catch
                {
                }
            }

            throw new InvalidOperationException("Couldn't read text from this file.");
        }

        public static void ExportText(string path, string text)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".docx")
            {
                ExportDocxText(path, text);
                return;
            }

            if (ext == ".rtf")
            {
                ExportRtfText(path, text);
                return;
            }

            File.WriteAllText(path, text ?? "", new UTF8Encoding(false));
        }

        private static string ImportRtfText(string path)
        {
            FlowDocument document = new FlowDocument();
            TextRange range = new TextRange(document.ContentStart, document.ContentEnd);
            using (FileStream stream = File.OpenRead(path))
            {
                range.Load(stream, DataFormats.Rtf);
            }

            string text = ScriptTextSanitizer.Clean(range.Text);
            if (text.Trim().Length == 0)
            {
                throw new InvalidOperationException("Couldn't read text from this RTF file.");
            }

            return text;
        }

        private static void ExportRtfText(string path, string text)
        {
            FlowDocument document = new FlowDocument(new Paragraph(new Run(text ?? "")));
            TextRange range = new TextRange(document.ContentStart, document.ContentEnd);
            using (FileStream stream = File.Create(path))
            {
                range.Save(stream, DataFormats.Rtf);
            }
        }

        private static string ImportDocxText(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                ZipArchiveEntry documentEntry = archive.GetEntry("word/document.xml");
                if (documentEntry == null)
                {
                    throw new InvalidOperationException("This DOCX file does not contain word/document.xml.");
                }

                using (Stream xmlStream = documentEntry.Open())
                {
                    XDocument document = XDocument.Load(xmlStream);
                    XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                    StringBuilder output = new StringBuilder();

                    foreach (XElement paragraph in document.Descendants(w + "p"))
                    {
                        string paragraphText = ExtractParagraphText(paragraph, w);
                        output.AppendLine(paragraphText);
                    }

                    string text = ScriptTextSanitizer.Clean(output.ToString());
                    if (text.Trim().Length == 0)
                    {
                        throw new InvalidOperationException("Couldn't extract text from this DOCX file.");
                    }

                    return text;
                }
            }
        }

        private static string ExtractParagraphText(XElement paragraph, XNamespace w)
        {
            StringBuilder text = new StringBuilder();
            foreach (XElement element in paragraph.Descendants())
            {
                if (element.Name == w + "t")
                {
                    text.Append(element.Value);
                }
                else if (element.Name == w + "tab")
                {
                    text.Append('\t');
                }
                else if (element.Name == w + "br" || element.Name == w + "cr")
                {
                    text.AppendLine();
                }
            }

            return text.ToString();
        }

        private static void ExportDocxText(string path, string text)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            using (FileStream stream = File.Create(path))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "[Content_Types].xml", ContentTypesXml());
                WriteEntry(archive, "_rels/.rels", PackageRelationshipsXml());
                WriteEntry(archive, "word/_rels/document.xml.rels", DocumentRelationshipsXml());
                WriteEntry(archive, "word/document.xml", BuildDocumentXml(text ?? ""));
            }
        }

        private static void WriteEntry(ZipArchive archive, string name, string contents)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name);
            using (Stream stream = entry.Open())
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(contents);
            }
        }

        private static string BuildDocumentXml(string text)
        {
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            XElement body = new XElement(w + "body");

            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                XElement paragraph = new XElement(w + "p");
                if (lines[i].Length > 0)
                {
                    paragraph.Add(
                        new XElement(
                            w + "r",
                            new XElement(
                                w + "t",
                                new XAttribute(XNamespace.Xml + "space", "preserve"),
                                lines[i])));
                }

                body.Add(paragraph);
            }

            body.Add(
                new XElement(
                    w + "sectPr",
                    new XElement(w + "pgSz", new XAttribute(w + "w", "12240"), new XAttribute(w + "h", "15840")),
                    new XElement(w + "pgMar",
                        new XAttribute(w + "top", "1440"),
                        new XAttribute(w + "right", "1440"),
                        new XAttribute(w + "bottom", "1440"),
                        new XAttribute(w + "left", "1440"),
                        new XAttribute(w + "header", "720"),
                        new XAttribute(w + "footer", "720"),
                        new XAttribute(w + "gutter", "0"))));

            XDocument document = new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(w + "document", body));

            return document.ToString(SaveOptions.DisableFormatting);
        }

        private static string ContentTypesXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                   "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                   "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                   "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
                   "</Types>";
        }

        private static string PackageRelationshipsXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                   "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>" +
                   "</Relationships>";
        }

        private static string DocumentRelationshipsXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"/>";
        }
    }
}
