using Dapper;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using SimpleInventorySystem.Database.Contracts;
using SimpleInventorySystem.Database.Exceptions;
using SimpleInventorySystem.Database.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace SimpleInventorySystem.Database
{
    public class InventoryRepository : IInventoryRepository
    {
        public enum OrderByDirection
        {
            Ascending,
            Descending
        }

        private readonly IDbConnection db;
        private const string INVENTORY_ITEM_TABLE_NAME = "inventory_items";
        private const string UNIT_ITEM_TABLE_NAME = "item_units";
        private const string ITEM_PROPERTY_TABLE_NAME = "item_properties";
        private const string LAMPORT_CLOCK_COLUMN = "lamport_clock";

        public InventoryRepository(IDbConnection db)
        {
            this.db = db;
        }

        public async Task<int> GetTotalItemCountAsync(Guid ItemId)
        {
            var sql = $@"
                SELECT (
                    SELECT COUNT(*) FROM {UNIT_ITEM_TABLE_NAME}
                    WHERE {nameof(ItemUnit.InventoryItemId).ToLower()} = @ItemId AND {nameof(ItemUnit.Removed).ToLower()} = FALSE
                ) * 
                {nameof(InventoryItem.QuantityPerUnit).ToLower()} FROM {INVENTORY_ITEM_TABLE_NAME}
                WHERE {nameof(InventoryItem.Id).ToLower()} = @ItemId;
            ";

            var count = await db.ExecuteScalarAsync<int>(sql);
            return count;
        }
        
        private async Task<bool> ValidateLamportClock(Guid itemId, string tableName, int userLamport)
        {
            var sql = $@"
                SELECT {LAMPORT_CLOCK_COLUMN}
                FROM @tName
                WHERE {nameof(InventoryItem.Id).ToLower()} = @Id;
            ";
            var existingLamport = await db.ExecuteScalarAsync<int>(sql, new { tName = tableName, Id = itemId });
            return userLamport > existingLamport;
        }

        #region ItemUnit CRUD Operations
        public async Task<bool> RemoveUnitItemAsync(Guid itemUnitId)
        {
            var sql = $@"
                UPDATE {UNIT_ITEM_TABLE_NAME}
                SET 
                    {nameof(ItemUnit.RemovedFromInventory).ToLower()} = @RemovedFromInventory,
                    {nameof(ItemUnit.Removed).ToLower()} = @Removed
                WHERE {nameof(ItemUnit.Id).ToLower()} = @Id;
            ";
            db.Open();
            using var transaction = db.BeginTransaction();
            try
            {
                var result = await db.ExecuteAsync(sql, new
                {
                    Id = itemUnitId,
                    RemovedFromInventory = DateTime.UtcNow,
                    Removed = true
                }, transaction);
                if (result <= 0)
                {
                    transaction.Rollback();
                    return false;
                }
                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
            finally
            {
                db.Close();
            }
        }

        public async Task<bool> AddUnitItemAsync(Guid inventoryItemId)
        {
            var sql = $@"
                INSERT INTO {UNIT_ITEM_TABLE_NAME} (
                    {nameof(ItemUnit.InventoryItemId).ToLower()},
                    {nameof(ItemUnit.RecordedInInventory).ToLower()},
                )
                VALUES 
                (@InventoryItemId, @RecordedInInventory)
            ";
            db.Open();
            using var transaction = db.BeginTransaction();
            try
            {
                var result = await db.ExecuteAsync(sql, new
                {
                    InventoryItemId = inventoryItemId,
                    RecordedInInventory = DateTime.UtcNow,
                }, transaction);
                if (result <= 0)
                {
                    transaction.Rollback();
                    return false;
                }
                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
            finally
            {
                db.Close();
            }
        }
        #endregion
        #region InventoryItem CRUD Operations
        /// <summary>
        /// Create a new inventory item entry along with its Lamport clock initialization.
        /// </summary>
        /// <param name="newItem">A new inventory item</param>
        /// <returns>Success or failure</returns>
        public async Task AddNewInventoryItemAsync(InventoryItem newItem, IEnumerable<InventoryItemProperty> properties)
        {
            var sql = $@"
            -- Insert new inventory item and initialize Lamport clock
                INSERT INTO {INVENTORY_ITEM_TABLE_NAME} (
                    {nameof(InventoryItem.Name).ToLower()}, 
                    {nameof(InventoryItem.Description).ToLower()}, 
                    {nameof(InventoryItem.PartNumber).ToLower()}, 
                    {nameof(InventoryItem.QuantityPerUnit).ToLower()},
                    {nameof(InventoryItem.UnitName).ToLower()},
                    {nameof(InventoryItem.CreatedAt).ToLower()}, 
                    {nameof(InventoryItem.UpdatedAt).ToLower()}, 
                    {nameof(InventoryItem.Deleted).ToLower()}, 
                )
                VALUES 
                (@Name, @Description, @PartNumber, @QuantityPerUnit, @UnitName, @CreatedAt, @UpdatedAt, @Deleted)
                RETURNING id;             
            ";

            var addProperties = $@"
                INSERT INTO {ITEM_PROPERTY_TABLE_NAME} (
                    {nameof(InventoryItemProperty.InventoryItemId).ToLower()},
                    {nameof(InventoryItemProperty.PropertyName).ToLower()},
                    {nameof(InventoryItemProperty.PropertyValue).ToLower()}
                )
                VALUES 
                (@InventoryItemId, @PropertyName, @PropertyValue);
            ";

            db.Open();
            using var transaction = db.BeginTransaction();
            try
            {
                // Add new item
                var result = await db.ExecuteScalarAsync<Guid?>(sql, new
                {
                    Name = newItem.Name,
                    Description = newItem.Description,
                    PartNumber = newItem.PartNumber,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Deleted = newItem.Deleted,
                }, transaction);

                if(result == null)
                {
                    transaction.Rollback();
                    throw new Exception("Failed to insert new inventory item.");
                }

                // add new item properties
                await db.ExecuteAsync(addProperties, properties.Select(p => new
                {
                    InventoryItemId = result,
                    PropertyName = p.PropertyName,
                    PropertyValue = p.PropertyValue
                }), transaction);

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
            finally
            {
                db.Close();
            }
        }

        /// <summary>
        /// Add an updated inventory item entry if the provided Lamport clock is greater than the existing one.
        /// </summary>
        /// <param name="updatedItem">Data to update</param>
        /// <param name="lamportClock">Provided lamport clock</param>
        /// <returns>Success or failure</returns>
        public async Task UpdateInventoryItemAsync(InventoryItem updatedItem, string[] currentProperties, IEnumerable<InventoryItemProperty> newProperties)
        {
            var updateItem = $@"
                UPDATE {INVENTORY_ITEM_TABLE_NAME}
                SET 
                    {nameof(InventoryItem.Name).ToLower()} = @Name,
                    {nameof(InventoryItem.Description).ToLower()} = @Description,
                    {nameof(InventoryItem.PartNumber).ToLower()} = @PartNumber,
                    {nameof(InventoryItem.QuantityPerUnit).ToLower()} = @QuantityPerUnit,
                    {nameof(InventoryItem.UnitName).ToLower()} = @UnitName,
                    {nameof(InventoryItem.UpdatedAt).ToLower()} = @UpdatedAt,
                    {nameof(InventoryItem.LamportClock).ToLower()} = @UserLamport
                WHERE {nameof(InventoryItem.Id).ToLower()} = @Id;
            ";

            var removePropertiesQuery = $@"
                UPDATE {ITEM_PROPERTY_TABLE_NAME}
                SET {nameof(InventoryItemProperty.Deleted).ToLower()} = TRUE
                WHERE {nameof(InventoryItemProperty.InventoryItemId).ToLower()} = @InventoryItemId
                AND {nameof(InventoryItemProperty.PropertyName).ToLower()} = ANY(@PropertyNames);
            ";

            var addNewProperties = $@"
                INSERT INTO {ITEM_PROPERTY_TABLE_NAME} (
                    {nameof(InventoryItemProperty.InventoryItemId).ToLower()},
                    {nameof(InventoryItemProperty.PropertyName).ToLower()},
                    {nameof(InventoryItemProperty.PropertyValue).ToLower()}
                )
                VALUES 
                (@InventoryItemId, @PropertyName, @PropertyValue);
            ";

            int result = -1;
            db.Open();
            var transaction = db.BeginTransaction();
            try
            {
                // Lamport Check
                if(!await ValidateLamportClock(updatedItem.Id, INVENTORY_ITEM_TABLE_NAME, updatedItem.LamportClock))              
                    throw new InvalidLamportException("Provided Lamport clock is not greater than existing value.");
                
                // Update Item
                result = await db.ExecuteAsync(updateItem, new
                {
                    Id = updatedItem.Id,
                    Name = updatedItem.Name,
                    Description = updatedItem.Description,
                    PartNumber = updatedItem.PartNumber,
                    QuantityPerUnit = updatedItem.QuantityPerUnit,
                    UnitName = updatedItem.UnitName,
                    UpdatedAt = DateTime.UtcNow,
                    UserLamport = updatedItem.LamportClock
                });

                // Remove old properties
                result = await db.ExecuteAsync(removePropertiesQuery, new
                {
                    InventoryItemId = updatedItem.Id,
                    PropertyNames = currentProperties
                });

                // Add new properties
                result = await db.ExecuteAsync(addNewProperties, newProperties.Select(p => new
                {
                    InventoryItemId = updatedItem.Id,
                    PropertyName = p.PropertyName,
                    PropertyValue = p.PropertyValue
                }), transaction);

                transaction.Commit();
            } catch
            {
                transaction.Rollback();
                throw;
            }
            finally
            {
                db.Close();
            }
        }

        /// <summary>
        /// Get an inventory item by its unique identifier.
        /// </summary>
        /// <param name="id">Id</param>
        /// <returns>Entry</returns>
        public async Task<InventoryItem?> GetInventoryItemByIdAsync(Guid id)
        {
            var sql = $@"
                SELECT *
                FROM {INVENTORY_ITEM_TABLE_NAME}
                WHERE {nameof(InventoryItem.Id).ToLower()} = @Id;
            ";
            var item = await db.QuerySingleOrDefaultAsync<InventoryItem>(sql, new { Id = id });
            return item;
        }

        public async Task<IEnumerable<InventoryItemProperty>> GetInventoryItemPropertiesAsync(Guid inventoryItemId)
        {
            var sql = $@"
                SELECT *
                FROM {ITEM_PROPERTY_TABLE_NAME}
                WHERE {nameof(InventoryItemProperty.InventoryItemId).ToLower()} = @InventoryItemId
                AND {nameof(InventoryItemProperty.Deleted).ToLower()} = FALSE;
            ";
            var property = await db.QueryAsync<InventoryItemProperty>(sql, new { InventoryItemId = inventoryItemId });
            return property;
        }

        public async Task<IEnumerable<InventoryItem>> GetInventoryItemPageAsync(int page, int pageSize, string orderBy, OrderByDirection orderDirection = OrderByDirection.Ascending)
        {
            var currentPage = page < 0 ? 0 : page - 1;
            var direction = orderDirection == OrderByDirection.Ascending ? "ASC" : "DESC";
            var ob = string.IsNullOrWhiteSpace(orderBy) ? nameof(InventoryItem.Id).ToLower() : orderBy.ToLower();
            var sql = $@"
                SELECT *
                FROM {INVENTORY_ITEM_TABLE_NAME}
                ORDER BY @orderColumn @orderDirection
                LIMIT @PageSize OFFSET @Offset;";
            var items = await db.QueryAsync<InventoryItem>(sql, new { PageSize = pageSize, Offset = currentPage * pageSize, orderColumn = ob, orderDirection = direction });
            return items;
        }
        #endregion
        #region Database and Table Creation
        public void CreateDatabase(string dbName, IDbConnection dbConnection)
        {
            var sql = $@"SELECT EXISTS (
                SELECT 1 
                FROM pg_catalog.pg_database 
                WHERE datname='{dbName}'
            );";

            if (dbConnection.ExecuteScalar<bool>(sql))
                return;
           
            try
            {
                dbConnection.Execute($"CREATE DATABASE {dbName}");
            }
            catch 
            {
                throw;
            }
        }

        /// <summary>
        /// Create a new table for inventory items if it does not already exist.
        /// </summary>
        /// <returns>Success or failure</returns>
        public bool CreateInventoryTable()
        {

            var sql = @$"
                CREATE TABLE IF NOT EXISTS {INVENTORY_ITEM_TABLE_NAME} (
                    {nameof(InventoryItem.Id).ToLower()} UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                    {nameof(InventoryItem.Name).ToLower()} VARCHAR(255),
                    {nameof(InventoryItem.Description).ToLower()} TEXT,
                    {nameof(InventoryItem.PartNumber).ToLower()} VARCHAR(100),
                    {nameof(InventoryItem.QuantityPerUnit).ToLower()} INTEGER,
                    {nameof(InventoryItem.UnitName).ToLower()} VARCHAR(50),
                    {nameof(InventoryItem.CreatedAt).ToLower()} TIMESTAMP WITHOUT TIME ZONE NOT NULL,
                    {nameof(InventoryItem.UpdatedAt).ToLower()} TIMESTAMP WITHOUT TIME ZONE NOT NULL,
                    {LAMPORT_CLOCK_COLUMN} INTEGER DEFAULT 0,
                    {nameof(InventoryItem.Deleted).ToLower()} BOOLEAN DEFAULT FALSE              
                );
                CREATE TABLE IF NOT EXISTS {UNIT_ITEM_TABLE_NAME} (
                    {nameof(ItemUnit.Id).ToLower()} UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                    {nameof(ItemUnit.InventoryItemId).ToLower()} UUID REFERENCES {INVENTORY_ITEM_TABLE_NAME}({nameof(InventoryItem.Id).ToLower()}) ON DELETE CASCADE,
                    {nameof(ItemUnit.RecordedInInventory).ToLower()} TIMESTAMP WITHOUT TIME ZONE NOT NULL,
                    {nameof(ItemUnit.RemovedFromInventory).ToLower()} TIMESTAMP WITHOUT TIME ZONE,
                    {nameof(ItemUnit.Removed).ToLower()} BOOLEAN DEFAULT FALSE
                );
                CREATE TABLE IF NOT EXISTS {ITEM_PROPERTY_TABLE_NAME} (
                    {nameof(InventoryItemProperty.Id).ToLower()} UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                    {nameof(InventoryItemProperty.InventoryItemId).ToLower()} UUID REFERENCES {INVENTORY_ITEM_TABLE_NAME}({nameof(InventoryItem.Id).ToLower()}) ON DELETE CASCADE,
                    {nameof(InventoryItemProperty.PropertyName).ToLower()} VARCHAR(100) NOT NULL,
                    {nameof(InventoryItemProperty.PropertyValue).ToLower()} TEXT NOT NULL,
                    {nameof(InventoryItemProperty.Deleted).ToLower()} BOOLEAN DEFAULT FALSE
                );
                ";
            db.Open();
            using var transaction = db.BeginTransaction();
            try
            {
                db.Execute(sql);               
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
            finally
            {
                db.Close();
            }
            return true;
        }
        #endregion
    }
}
