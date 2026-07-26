using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrimeraWebApp.Models
{
    [Table("trabajadores")]
    public class Trabajador
    {
        [Key]
        [Column("id_trabajador")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id_trabajador { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [Column("nombre")]
        public string Nombre { get; set; }

        [Required(ErrorMessage = "El correo es obligatorio")]
        [Column("correo")]
        public string Correo { get; set; }

        // La clave se procesa/valida en el controlador (password/confirmPassword),
        // por eso no marcamos [Required] aquí para no bloquear el ModelState antes de hashear.
        [Column("clave")]
        public string? Clave { get; set; }

        [Required(ErrorMessage = "El apellido es obligatorio")]
        [Column("apellido")]
        public string Apellido { get; set; }

        [Column("admin")]
        public bool Admin { get; set; }
    }
}
