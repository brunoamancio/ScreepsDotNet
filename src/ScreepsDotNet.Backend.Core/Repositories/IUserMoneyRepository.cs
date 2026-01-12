using ScreepsDotNet.Backend.Core.Models;

namespace ScreepsDotNet.Backend.Core.Repositories;

public interface IUserMoneyRepository
{
    Task<MoneyHistoryPage> GetHistoryAsync(string userId, int page, int pageSize, CancellationToken cancellationToken = default);
}
