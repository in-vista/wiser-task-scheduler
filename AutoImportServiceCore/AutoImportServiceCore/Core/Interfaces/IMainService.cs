using System.Threading.Tasks;

namespace AutoImportServiceCore.Core.Interfaces
{
    /// <summary>
    /// A service to manage all AIS configurations that are provided by Wiser.
    /// </summary>
    public interface IMainService
    {
        /// <summary>
        /// Manage the AIS configurations.
        /// </summary>
        /// <returns></returns>
        Task ManageConfigurations();
    }
}
