using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleInventorySystem.Database.Contracts
{
    public interface ILamportable
    {
        public long LamportClock { get; set; }
    }
}
