using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrimeraWebApp.Models
{
    [Table("ganancias")]
    public class Ganancia
    {
        [Key]
        [Column("id_ganancia")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id_ganancia { get; set; }

        [Required]
        [Column("id_venta")]
        public int Id_venta { get; set; }

        [Column("fecha")]
        public DateTime Fecha { get; set; } = DateTime.Now;

        [Column("total_venta")]
        public int Total_venta { get; set; }

        [Column("costo_total")]
        public int Costo_total { get; set; }

        [Column("ganancia_neta")]
        public int Ganancia_neta { get; set; }

        [ForeignKey("Id_venta")]
        public virtual Venta? Venta { get; set; }
    }
}
