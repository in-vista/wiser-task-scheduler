﻿using System;
using System.ComponentModel;
using System.Xml;
using System.Xml.Serialization;
using AutoImportServiceCore.Core.Models;

namespace AutoImportServiceCore.Modules.CleanupItems.Models
{
    [XmlType("CleanupItem")]
    public class CleanupItemModel : ActionModel
    {
        /// <summary>
        /// Gets or sets the name of the entity.
        /// </summary>
        public string EntityName { get; set; }

        /// <summary>
        /// Gets or sets the time before the action needs to be performed on the items of the given entity.
        /// </summary>
        [XmlIgnore]
        public TimeSpan TimeToStore { get; set; }
        
        /// <summary>
        /// Gets or sets <see cref="TimeToStore"/> from a XML file.
        /// </summary>
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [XmlElement("TimeToStore")]
        public string DelayString
        {
            get => XmlConvert.ToString(TimeToStore);
            set => TimeToStore = String.IsNullOrWhiteSpace(value) ? TimeSpan.Zero : value.StartsWith("P") ? XmlConvert.ToTimeSpan(value) : TimeSpan.Parse(value);
        }

        /// <summary>
        /// Gets or sets if the "changed_on" column needs to be used instead of the "added_on" column.
        /// </summary>
        public bool SinceLastChange { get; set; }

        /// <summary>
        /// Gets or sets the connection string to use for the cleanup of this entity to manage multiple customers from a single AIS.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets if the history needs to be saved.
        /// </summary>
        public bool SaveHistory { get; set; } = true;
    }
}