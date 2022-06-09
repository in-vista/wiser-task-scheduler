namespace AutoImportServiceCore.Core.Models.Cleanup
{
    public enum CleanupActions
    {
        /// <summary>
        /// Move the item to the archive table.
        /// </summary>
        Archive,
        
        /// <summary>
        /// Completely delete the item.
        /// </summary>
        Delete
    }
}