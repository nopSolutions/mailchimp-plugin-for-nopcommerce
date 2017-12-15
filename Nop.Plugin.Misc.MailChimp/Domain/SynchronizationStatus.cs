
namespace Nop.Plugin.Misc.MailChimp.Domain
{
    /// <summary>
    /// Represents a synchronization status enumeration
    /// </summary>
    public enum SynchronizationStatus
    {
        /// <summary>
        /// Pending
        /// </summary>
        Pending,

        /// <summary>
        /// Preprocessing
        /// </summary>
        Preprocessing,

        /// <summary>
        /// Started
        /// </summary>
        Started,

        /// <summary>
        /// Finalizing
        /// </summary>
        Finalizing,

        /// <summary>
        /// Finished
        /// </summary>
        Finished
    }
}