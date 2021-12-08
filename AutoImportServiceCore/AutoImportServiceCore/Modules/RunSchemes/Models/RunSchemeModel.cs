using System;
using System.ComponentModel.DataAnnotations;
using AutoImportServiceCore.Modules.RunSchemes.Enums;

namespace AutoImportServiceCore.Modules.RunSchemes.Models
{
    /// <summary>
    /// A model for the run scheme.
    /// </summary>
    public class RunSchemeModel
    {
        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        [Required]
        public RunSchemeTypes Type { get; set; }

        /// <summary>
        /// Gets or sets the time ID.
        /// </summary>
        [Required]
        public int TimeId { get; set; }

        /// <summary>
        /// Gets or sets the delay for <see cref="RunSchemeTypes"/>.Continuous.
        /// </summary>
        public TimeSpan Delay { get; set; }

        /// <summary>
        /// Gets or sets the time the run needs to start at <see cref="RunSchemeTypes"/>.Continuous.
        /// </summary>
        public TimeSpan StartTime { get; set; }

        /// <summary>
        /// Gets or sets the time the run steeds to stop at <see cref="RunSchemeTypes"/>.Continuous.
        /// </summary>
        public TimeSpan StopTime { get; set; }

        /// <summary>
        /// Gets or sets if the weekend needs to be skipped.
        /// </summary>
        public bool SkipWeekend { get; set; }

        /// <summary>
        /// Gets or sets what days need to be skipped.
        /// </summary>
        public string SkipDays { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the day of the week for <see cref="RunSchemeTypes"/>.Weekly.
        /// </summary>
        public int DayOfWeek { get; set; }

        /// <summary>
        /// Gets or sets the day of the month for <see cref="RunSchemeTypes"/>.Monthly.
        /// </summary>
        public int DayOfMonth { get; set; } = 1;

        /// <summary>
        /// Gets or sets the hour for <see cref="RunSchemeTypes"/>.Weekly and <see cref="RunSchemeTypes"/>.Monthly.
        /// </summary>
        public TimeSpan Hour { get; set; }
    }
}
