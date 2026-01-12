namespace ScreepsDotNet.Backend.Core.Services;

using System.Threading;
using System.Threading.Tasks;
using ScreepsDotNet.Backend.Core.Models.UserMessages;

public interface IUserMessageService
{
    Task<UserMessageListResult> GetMessagesAsync(string userId, string respondentId, CancellationToken cancellationToken = default);

    Task<UserMessageIndexResult> GetMessageIndexAsync(string userId, CancellationToken cancellationToken = default);

    Task SendMessageAsync(string senderId, string respondentId, string text, CancellationToken cancellationToken = default);

    Task<bool> MarkReadAsync(string userId, string messageId, CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default);
}
