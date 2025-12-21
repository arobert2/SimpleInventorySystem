using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SimpleInventorySystem.Database.Models
{
    public class InventoryItem
    {
        public Guid Id { get; set; }
        [Required]
        public string Name { get; set; }
        public string? Description { get; set; }
        public string? PartNumber { get; set; }
        [Required]
        public int QuantityPerUnit { get; set; }
        [Required]
        public string UnitName { get; set; }
        [Required]
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int LamportClock { get; set; }
        public bool Deleted { get; set; }
    }
}
