using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SimpleInventorySystem.Database.Models
{
    internal class ItemUnit
    {
        public Guid Id { get; set; }
        [Required]
        public Guid InventoryItemId { get; set; }
        [Required]
        public DateTime RecordedInInventory { get; set; }
        public DateTime? RemovedFromInventory { get; set; }
        public bool Removed { get; set; }
    }
}
