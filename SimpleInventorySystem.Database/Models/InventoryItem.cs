using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleInventorySystem.Database.Models
{
    public class InventoryItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Dictionary<string, object> UserAttributes { get; set; } = new();
        public bool Deleted { get; set; }
    }
}
