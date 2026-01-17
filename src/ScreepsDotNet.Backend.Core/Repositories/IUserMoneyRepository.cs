namespace ScreepsDotNet.Backend.Core.Repositories;

using ScreepsDotNet.Backend.Core.Models;

public interface IUserMoneyRepository
{
    Task<MoneyHistoryPage> GetHistoryAsync(string userId, int page, int pageSize, CancellationToken cancellationToken = default);
}
