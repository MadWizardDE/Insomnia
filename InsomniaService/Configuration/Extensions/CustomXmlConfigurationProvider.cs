using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Extensions.Configuration.Xml
{
    /// <summary>
    /// Represents an XML file as an <see cref="IConfigurationSource"/>.
    /// </summary>
    class CustomXmlConfigurationProvider : XmlConfigurationProvider
    {
        internal const string EMPTY_ATTRIBUTE_NAME = "empty";
        internal const string TEXT_ATTRIBUTE_NAME = "text";

        /// <summary>
        /// Initializes a new instance with the specified source.
        /// </summary>
        /// <param name="source">The source settings.</param>
        public CustomXmlConfigurationProvider(XmlConfigurationSource source) : base(source) { }

        /// <summary>
        /// Loads the XML data from a stream.
        /// </summary>
        /// <param name="stream">The stream to read.</param>
        public override void Load(Stream stream)
        {
            XDocument xml = XDocument.Load(stream);

            PopulateEmptyNodes(xml.Root);

            using (MemoryStream memory = new MemoryStream())
            {
                xml.Save(memory);

                memory.Position = 0;

                base.Load(memory);
            }
        }

        private static void PopulateEmptyNodes(XElement element)
        {
            var empty = true;
            bool hasAttributes = false;
            if (element.Attributes().Count() > 0)
                empty = !(hasAttributes = true);
            else if (element.Elements().Count() > 0)
                empty = false;

            var text = string.Concat(element.Nodes().OfType<XText>().Select(t => t.Value));

            if (hasAttributes && !string.IsNullOrWhiteSpace(text))
            {
                element.Add(new XAttribute(TEXT_ATTRIBUTE_NAME, text));
            }

            if (empty)
            {
                element.Add(new XAttribute(EMPTY_ATTRIBUTE_NAME, "true"));
            }
            else
            {
                foreach (XElement childElement in element.Elements())
                    PopulateEmptyNodes(childElement);
            }
        }
    }
}