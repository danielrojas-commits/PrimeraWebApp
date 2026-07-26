using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrimeraWebApp.Models
{
    [Table("ventas")]
    public class Venta
    {
        [Key]
        [Column("id_venta")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id_venta { get; set; }

        [Column("fecha")]
        public DateTime Fecha { get; set; }

        [Column("total")]
        public decimal Total { get; set; }

        [Column("estado")]
        public bool Estado { get; set; }

        [Column("id_trabajador")]
        public int? Id_trabajador { get; set; }

        [ForeignKey("Id_trabajador")]
        public virtual Trabajador? Trabajador { get; set; }
    }
}
