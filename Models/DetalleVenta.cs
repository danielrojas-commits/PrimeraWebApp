using System;

namespace PrimeraWebApp.Models
{
    // Modelo simple para representar un ítem del carrito / detalle de venta
    public class DetalleVenta
    {
        public int Id_producto { get; set; }
        public int Cantidad { get; set; }
        public decimal Precio_unitario { get; set; }
    }
}
