using SimpleInventorySystem.Database.Models;
using System;
using System.Collections.Generic;
using System.Text;
using static SimpleInventorySystem.Database.InventoryRepository;

namespace SimpleInventorySystem.Database.Contracts
{
    public interface IInventoryRepository
    {
        Task<bool> AddNewEntryAsync(InventoryItem newItem);
        Task<bool> UpdateEntryAsync(InventoryItem updatedItem);
        Task<InventoryItem?> GetEntryByIdAsync(Guid id);
        Task<IEnumerable<InventoryItem>> GetPageAsync(int page, int pageSize, string orderBy, OrderByDirection orderDirection = OrderByDirection.Ascending);
    }
}
