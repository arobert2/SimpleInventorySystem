using Dapper;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using SimpleInventorySystem.Database.Contracts;
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
        private string tableName = "inventory_items";
        private string lampTableName = "inventory_item_lamps";

        public InventoryRepository(IDbConnection db)
        {
            this.db = db;
        }
        /// <summary>
        /// Create a new inventory item entry along with its Lamport clock initialization.
        /// </summary>
        /// <param name="newItem">A new inventory item</param>
        /// <returns>Success or failure</returns>
        public async Task<bool> AddNewEntryAsync(InventoryItem newItem)
        {
            var sql = $@"
                -- Insert new inventory item and initialize Lamport clock
                WITH new_item AS (
                    INSERT INTO {tableName} (
                        {nameof(InventoryItem.Name).ToLower()}, 
                        {nameof(InventoryItem.Description).ToLower()}, 
                        {nameof(InventoryItem.SerialNumber).ToLower()}, 
                        {nameof(InventoryItem.CreatedAt).ToLower()}, 
                        {nameof(InventoryItem.UpdatedAt).ToLower()}, 
                        {nameof(InventoryItem.Deleted).ToLower()}, 
                        {nameof(InventoryItem.UserAttributes).ToLower()}
                    )
                    VALUES 
                    (@Name, @Description, @SerialNumber, @CreatedAt, @UpdatedAt, @Deleted, CAST(@UserAttributes AS jsonb))
                    RETURNING id
                )
                INSERT INTO {lampTableName} (inv_id)
                SELECT id FROM new_item;
            ";
            db.Open();
            using var transaction = db.BeginTransaction();
            try
            {
                var result = await db.ExecuteAsync(sql, new
                {
                    Name = newItem.Name,
                    Description = newItem.Description,
                    SerialNumber = newItem.SerialNumber,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Deleted = newItem.Deleted,
                    UserAttributes = newItem.UserAttributes != null ? System.Text.Json.JsonSerializer.Serialize(newItem.UserAttributes) : null
                }, transaction);

                if(result <= 0)
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

        /// <summary>
        /// Add an updated inventory item entry if the provided Lamport clock is greater than the existing one.
        /// </summary>
        /// <param name="updatedItem">Data to update</param>
        /// <param name="lamportClock">Provided lamport clock</param>
        /// <returns>Success or failure</returns>
        public async Task<bool> UpdateEntryAsync(InventoryItem updatedItem, int lamportClock)
        {
            var sql = $@"
                UPDATE {tableName}
                SET 
                    {nameof(InventoryItem.Name).ToLower()} = @Name,
                    {nameof(InventoryItem.Description).ToLower()} = @Description,
                    {nameof(InventoryItem.SerialNumber).ToLower()} = @SerialNumber,
                    {nameof(InventoryItem.UpdatedAt).ToLower()} = @UpdatedAt,
                    {nameof(InventoryItem.UserAttributes).ToLower()} = @UserAttributes
                WHERE {nameof(InventoryItem.Id).ToLower()} = @Id
                    AND EXISTS (
                        SELECT lamport_clock
                        FROM {lampTableName}
                        WHERE inv_id = @Id
                        AND lamport_clock < @UserLamport;
                    );
                UPDATE {lampTableName}
                SET lamport_clock = @UserLamport
                WHERE inv_id = @Id 
                AND lamport_clock < @UserLamport;
            ";

            int result = -1;
            db.Open();
            var transaction = db.BeginTransaction();
            try
            {
                result = await db.ExecuteAsync(sql, new
                {
                    Id = updatedItem.Id,
                    Name = updatedItem.Name,
                    Description = updatedItem.Description,
                    SerialNumber = updatedItem.SerialNumber,
                    UpdatedAt = DateTime.UtcNow,
                    Deleted = updatedItem.Deleted,
                    UserAttributes = updatedItem.UserAttributes != null ? System.Text.Json.JsonSerializer.Serialize(updatedItem.UserAttributes) : null,
                    UserLamport = lamportClock
                });
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
            return result > 0;
        }

        /// <summary>
        /// Get an inventory item by its unique identifier.
        /// </summary>
        /// <param name="id">Id</param>
        /// <returns>Entry</returns>
        public async Task<InventoryItem?> GetEntryByIdAsync(Guid id)
        {
            var sql = $@"
                SELECT 
                    {nameof(InventoryItem.Id).ToLower()},
                    {nameof(InventoryItem.Name).ToLower()},
                    {nameof(InventoryItem.Description).ToLower()},
                    {nameof(InventoryItem.SerialNumber).ToLower()},
                    {nameof(InventoryItem.CreatedAt).ToLower()},
                    {nameof(InventoryItem.UpdatedAt).ToLower()},
                    {nameof(InventoryItem.Deleted).ToLower()},
                    {nameof(InventoryItem.UserAttributes).ToLower()}
                FROM {tableName}
                WHERE {nameof(InventoryItem.Id).ToLower()} = @Id;
            ";
            var item = await db.QuerySingleOrDefaultAsync<InventoryItem>(sql, new { Id = id });
            return item;
        }

        public async Task<IEnumerable<InventoryItem>> GetPageAsync(int page, int pageSize, string orderBy, OrderByDirection orderDirection = OrderByDirection.Ascending)
        {
            var currentPage = page < 0 ? 0 : page - 1;
            var direction = orderDirection == OrderByDirection.Ascending ? "ASC" : "DESC";
            var ob = string.IsNullOrWhiteSpace(orderBy) ? nameof(InventoryItem.Id).ToLower() : orderBy.ToLower();
            var sql = $@"
                SELECT 
                    {nameof(InventoryItem.Id).ToLower()},
                    {nameof(InventoryItem.Name).ToLower()},
                    {nameof(InventoryItem.Description).ToLower()},
                    {nameof(InventoryItem.SerialNumber).ToLower()},
                    {nameof(InventoryItem.UserAttributes).ToLower()}
                FROM {tableName}
                ORDER BY {ob} {direction}
                LIMIT @PageSize OFFSET @Offset;";
            var items = await db.QueryAsync<InventoryItem>(sql, new { PageSize = pageSize, Offset = currentPage * pageSize });
            return items;
        }

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
                CREATE TABLE IF NOT EXISTS {tableName} (
                    {nameof(InventoryItem.Id).ToLower()} UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                    {nameof(InventoryItem.Name).ToLower()} VARCHAR(255),
                    {nameof(InventoryItem.Description).ToLower()} TEXT,
                    {nameof(InventoryItem.SerialNumber).ToLower()} VARCHAR(100),
                    {nameof(InventoryItem.CreatedAt).ToLower()} TIMESTAMP WITHOUT TIME ZONE,
                    {nameof(InventoryItem.UpdatedAt).ToLower()} TIMESTAMP WITHOUT TIME ZONE,
                    {nameof(InventoryItem.Deleted).ToLower()} BOOLEAN DEFAULT FALSE,
                    {nameof(InventoryItem.UserAttributes).ToLower()} JSONB
                );
                CREATE TABLE IF NOT EXISTS {lampTableName} (
                    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                    inv_id UUID REFERENCES {tableName}(id) ON DELETE CASCADE,
                    lamport_clock INTEGER NOT NULL DEFAULT 0
                );
                CREATE INDEX IF NOT EXISTS idx_lamport_inv_id ON {lampTableName}(inv_id);";
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
    }
}
