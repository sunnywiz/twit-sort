using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace azuremvcapp1.Models
{
    public class UserConfig
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long TwitterUserId { get; set; }
        public string Configuration { get; set; }
    }

    public class ReadUntil
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        // I could NOT get both UserConfig + FilterName to be compound keys.  It kept putting
        // only Filtername as the key.  so MOVING ON.   I'll control it in code for now,
        // because I WANT TO GET THIS WORKING.

        public long TwitterUserId { get; set; }
        public string FilterName { get; set; }
        public DateTime When { get; set; }
    }
}