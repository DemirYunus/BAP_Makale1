using System;
using System.Collections.Generic;

// Code scaffolded by EF Core assumes nullable reference types (NRTs) are not used or disabled.
// If you have enabled NRTs for your project, then un-comment the following line:
// #nullable disable

namespace BAP.DAL
{
    public partial class ProcessingTime
    {
        public int? Siparis { get; set; }
        public int? AltBilesen { get; set; }
        public int? Makine { get; set; }
        public double? Sure { get; set; }
    }
}
