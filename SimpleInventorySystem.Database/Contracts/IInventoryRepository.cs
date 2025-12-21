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
        Task AddNewInventoryItemAsync(InventoryItem newItem, IEnumerable<InventoryItemProperty> properties);
        Task UpdateInventoryItemAsync(InventoryItem updatedItem, string[] currentProperties, IEnumerable<InventoryItemProperty> newProperties);
        Task<InventoryItem?> GetInventoryItemByIdAsync(Guid id);
        Task<IEnumerable<InventoryItem>> GetInventoryItemPageAsync(int page, int pageSize, string orderBy, OrderByDirection orderDirection = OrderByDirection.Ascending);
        Task<IEnumerable<InventoryItemProperty>> GetInventoryItemPropertiesAsync(Guid inventoryItemId);
        Task<bool> RemoveUnitItemAsync(Guid itemUnitId);
        Task<bool> AddUnitItemAsync(Guid inventoryItemId);
        Task<int> GetTotalItemCountAsync(Guid ItemId);

    }
}
