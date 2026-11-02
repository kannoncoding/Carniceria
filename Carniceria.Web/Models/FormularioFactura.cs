using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Carniceria.Modelos.Contratos.Respuestas;
using Carniceria.Modelos.Enumeraciones;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Carniceria.Web.Models;

// Conserva los valores editables y la presentacion temporal del formulario.
public sealed class FormularioFactura
{
    [Required(ErrorMessage = "Seleccione un cliente.")]
    [Display(Name = "Cliente")]
    public Guid? ClienteId { get; set; }

    [Required(ErrorMessage = "Seleccione un empleado.")]
    [Display(Name = "Empleado")]
    public Guid? EmpleadoId { get; set; }

    [Required(ErrorMessage = "Seleccione un método de pago.")]
    [EnumDataType(typeof(MetodoPago), ErrorMessage = "Seleccione un método de pago válido.")]
    [Display(Name = "Método de pago")]
    public MetodoPago? MetodoPago { get; set; }

    [Required(ErrorMessage = "Ingrese el porcentaje de descuento.")]
    [Display(Name = "Porcentaje de descuento")]
    public decimal? PorcentajeDescuento { get; set; }

    [Display(Name = "Actualizar precios y unidades actuales")]
    public bool ActualizarPreciosYUnidades { get; set; }

    // Conserva las filas enviadas con claves de enlace independientes.
    public List<DetalleFacturaFormulario> Detalles { get; set; } = new();

    // Datos informativos de la factura que no se aceptan desde el formulario.
    [BindNever]
    public bool EsEdicion { get; set; }

    [BindNever]
    public string NumeroFactura { get; set; } = string.Empty;

    [BindNever]
    public string NumeroRuta { get; set; } = string.Empty;

    [BindNever]
    public DateTimeOffset? FechaEmision { get; set; }

    [BindNever]
    public EstadoFactura? EstadoActual { get; set; }

    [BindNever]
    public bool PuedeEditar { get; set; }

    [BindNever]
    public bool CatalogosDisponibles { get; set; }

    // Catalogos y calculos recibidos de la API para presentacion.
    [BindNever]
    public IReadOnlyList<RespuestaCliente> Clientes { get; set; } = Array.Empty<RespuestaCliente>();

    [BindNever]
    public IReadOnlyList<RespuestaEmpleado> Empleados { get; set; } = Array.Empty<RespuestaEmpleado>();

    [BindNever]
    public IReadOnlyList<RespuestaProducto> Productos { get; set; } = Array.Empty<RespuestaProducto>();

    [BindNever]
    public decimal PorcentajeDescuentoMaximo { get; set; }

    [BindNever]
    public RespuestaCalculoFactura? Calculo { get; set; }
}

// Mantiene una clave estable para enlazar la fila durante el postback.
public sealed class DetalleFacturaFormulario
{
    [Required(ErrorMessage = "La clave de la fila es obligatoria.")]
    [Display(Name = "Clave de fila")]
    public string ClaveFormulario { get; set; } = string.Empty;

    // Id solo identifica una linea existente en la factura editada.
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Seleccione un producto.")]
    [Display(Name = "Producto")]
    public Guid? ProductoId { get; set; }

    [Required(ErrorMessage = "Ingrese una cantidad.")]
    [Display(Name = "Cantidad")]
    public decimal? Cantidad { get; set; }

    // Valores informativos proyectados desde la API.
    [BindNever]
    public UnidadMedida? UnidadMedida { get; set; }

    [BindNever]
    public decimal? PrecioUnitario { get; set; }

    [BindNever]
    public decimal? SubtotalLinea { get; set; }
}
