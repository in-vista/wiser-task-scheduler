using System.Threading.Tasks;
using AutoImportServiceCore.Core.Models;

namespace AutoImportServiceCore.Core.Interfaces
{
    /// <summary>
    /// A service for an action.
    /// </summary>
    public interface IActionsService
    {
        /// <summary>
        /// Execute the action based on the type.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <returns></returns>
        Task Execute(ActionModel action);
    }
}
