using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleInventorySystem.Database.Models
{
    public class InventoryItemProperty
    {
        public Guid Id { get; set; }
        public Guid InventoryItemId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public string PropertyValue { get; set; } = string.Empty;
        public bool Deleted { get; set; }
    }
}
