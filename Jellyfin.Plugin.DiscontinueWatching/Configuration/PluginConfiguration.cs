using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.DiscontinueWatching.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    private readonly ConcurrentDictionary<Guid, Collection<string>> _userDenylists;
    private readonly object _lock = new object();

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        _userDenylists = new ConcurrentDictionary<Guid, Collection<string>>();
        DaysThreshold = 180;
    }

    /// <summary>
    /// Gets the thread-safe user-specific denylist dictionary.
    /// </summary>
    [XmlIgnore]
    public ConcurrentDictionary<Guid, Collection<string>> UserDenylists => _userDenylists;

    /// <summary>
    /// Gets or sets the user-specific denylist entries for XML serialization.
    /// This property is used for backward compatibility and XML serialization only.
    /// </summary>
#pragma warning disable CA2227
    public SerializableDictionary<Guid, Collection<string>> UserDenylistEntries
    {
        get
        {
            lock (_lock)
            {
                var dict = new SerializableDictionary<Guid, Collection<string>>();
                foreach (var kvp in _userDenylists)
                {
                    dict[kvp.Key] = kvp.Value;
                }
                return dict;
            }
        }
        set
        {
            lock (_lock)
            {
                _userDenylists.Clear();
                if (value != null)
                {
                    foreach (var kvp in value)
                    {
                        _userDenylists[kvp.Key] = kvp.Value;
                    }
                }
            }
        }
    }
#pragma warning restore CA2227

    /// <summary>
    /// Gets or sets the number of days after which items should be removed from Continue Watching.
    /// </summary>
    public int DaysThreshold { get; set; }
}

/// <summary>
/// A dictionary that can be serialized to XML.
/// </summary>
/// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
[XmlRoot("dictionary")]
public class SerializableDictionary<TKey, TValue>
       : Dictionary<TKey, TValue>, IXmlSerializable
    where TKey : notnull
{
    /// <summary>
    /// Gets the XML schema for the dictionary.
    /// </summary>
    /// <returns>Always returns null as no schema is required.</returns>
    public XmlSchema? GetSchema()
    {
        return null;
    }

    /// <summary>
    /// Reads the dictionary from XML.
    /// </summary>
    /// <param name="reader">The XML reader to read from.</param>
    public void ReadXml(XmlReader reader)
    {
        if (reader == null)
        {
            return;
        }

        XmlSerializer keySerializer = new XmlSerializer(typeof(TKey));
        XmlSerializer valueSerializer = new XmlSerializer(typeof(TValue));

        bool wasEmpty = reader.IsEmptyElement;
        reader.Read();

        if (wasEmpty)
        {
            return;
        }

        while (reader.NodeType != XmlNodeType.EndElement)
        {
            reader.ReadStartElement("item");

            reader.ReadStartElement("key");
            var keyObj = keySerializer.Deserialize(reader);
            reader.ReadEndElement();

            reader.ReadStartElement("value");
            var valueObj = valueSerializer.Deserialize(reader);
            reader.ReadEndElement();

            if (keyObj != null && valueObj != null)
            {
                this.Add((TKey)keyObj, (TValue)valueObj);
            }

            reader.ReadEndElement();
            reader.MoveToContent();
        }

        reader.ReadEndElement();
    }

    /// <summary>
    /// Writes the dictionary to XML.
    /// </summary>
    /// <param name="writer">The XML writer to write to.</param>
    public void WriteXml(XmlWriter writer)
    {
        if (writer == null)
        {
            return;
        }

        XmlSerializer keySerializer = new XmlSerializer(typeof(TKey));
        XmlSerializer valueSerializer = new XmlSerializer(typeof(TValue));

        foreach (TKey key in this.Keys)
        {
            writer.WriteStartElement("item");

            writer.WriteStartElement("key");
            keySerializer.Serialize(writer, key);
            writer.WriteEndElement();

            writer.WriteStartElement("value");
            TValue value = this[key];
            valueSerializer.Serialize(writer, value);
            writer.WriteEndElement();

            writer.WriteEndElement();
        }
    }
}
