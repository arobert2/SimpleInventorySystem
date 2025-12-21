using SimpleInventorySystem.Database.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using static SimpleInventorySystem.Database.InventoryRepository;

namespace SimpleInventorySystem.Database.Contracts
{
    public interface IInventoryRepository
    {
        Task<bool> AddNewEntryAsync(InventoryItem newItem, IEnumerable<InventoryItemProperty> properties);
        Task UpdateItemEntryAsync(InventoryItem updatedItem, string[] currentProperties, IEnumerable<InventoryItemProperty> newProperties);
        Task<InventoryItem?> GetEntryByIdAsync(Guid id);
        Task<IEnumerable<InventoryItem>> GetPageAsync(int page, int pageSize, string orderBy, OrderByDirection orderDirection = OrderByDirection.Ascending);
        Task<bool> RemoveUnitFromInventoryAsync(Guid itemUnitId);
        Task<bool> AddUnitToInventoryAsync(Guid inventoryItemId);
        Task<int> GetTotalItemCountAsync(Guid ItemId);

    }
}
