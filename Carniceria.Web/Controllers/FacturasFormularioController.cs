using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Carniceria.Modelos.Contratos.Respuestas;
using Carniceria.Modelos.Contratos.Solicitudes;
using Carniceria.Modelos.Enumeraciones;
using Carniceria.Web.Helpers;
using Carniceria.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Carniceria.Controllers;

public sealed partial class FacturasController
{
    // Presenta el formulario de una factura nueva con opciones de la API.
    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        var formulario = NuevoFormulario();
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            formulario.CatalogosDisponibles = await CargarCatalogosAsync(
                formulario, clienteHttp, HttpContext.RequestAborted);
            if (!formulario.CatalogosDisponibles)
            {
                return ServicioNoDisponible("No se pudieron cargar las opciones para crear la factura.");
            }

            return View(formulario);
        }
        catch (HttpRequestException)
        {
            return ErrorDeComunicacion(HttpContext.RequestAborted, false);
        }
        catch (OperationCanceledException)
        {
            return ErrorDeComunicacion(HttpContext.RequestAborted, false);
        }
        catch (JsonException)
        {
            return ServicioNoDisponible("La API respondió con opciones de factura inesperadas.");
        }
    }

    // Conserva los valores del formulario al agregar, quitar, calcular o guardar.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Crear(
        FormularioFactura formulario,
        [FromForm] string? accion,
        [FromForm] string? comandoFila)
    {
        return ProcesarFormularioAsync(formulario, null, accion, comandoFila);
    }

    // Presenta una factura editable con datos vigentes de la API.
    [HttpGet]
    public async Task<IActionResult> Editar([FromRoute] string numero)
    {
        if (string.IsNullOrWhiteSpace(numero))
        {
            return this.RegistroNoEncontrado("El enlace de la factura no contiene un número válido.");
        }

        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            var (codigo, factura) = await ObtenerFacturaAsync(clienteHttp, numero, cancelacion);
            if (codigo == HttpStatusCode.NotFound)
            {
                return this.RegistroNoEncontrado("La factura solicitada no existe o ya fue eliminada.");
            }

            if (codigo != HttpStatusCode.OK || factura is null)
            {
                return ServicioNoDisponible("No se pudieron consultar los datos para editar la factura.");
            }

            var formulario = CrearFormularioEdicion(factura);
            formulario.NumeroRuta = numero;
            if (factura.Estado == EstadoFactura.Anulada)
            {
                ModelState.AddModelError(string.Empty, "La factura no está Pendiente ni Pagada.");
            }

            if (!await CargarCatalogosAsync(formulario, clienteHttp, cancelacion))
            {
                return ServicioNoDisponible("No se pudieron cargar las opciones para editar la factura.");
            }

            return View(formulario);
        }
        catch (HttpRequestException)
        {
            return ErrorDeComunicacion(cancelacion, false);
        }
        catch (OperationCanceledException)
        {
            return ErrorDeComunicacion(cancelacion, false);
        }
        catch (JsonException)
        {
            return ServicioNoDisponible("La API respondió con datos de factura inesperados.");
        }
    }

    // Envia cambios sin aceptar identidad ni importes de la factura.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Editar(
        [FromRoute] string numero,
        FormularioFactura formulario,
        [FromForm] string? accion,
        [FromForm] string? comandoFila)
    {
        if (string.IsNullOrWhiteSpace(numero))
        {
            return Task.FromResult<IActionResult>(
                this.RegistroNoEncontrado("El enlace de la factura no contiene un número válido."));
        }

        return ProcesarFormularioAsync(formulario, numero, accion, comandoFila);
    }

    // Ejecuta una accion de fila o solicita calculo y guardado a la API.
    private async Task<IActionResult> ProcesarFormularioAsync(
        FormularioFactura formulario,
        string? numero,
        string? accion,
        string? comandoFila)
    {
        formulario.EsEdicion = numero is not null;
        formulario.NumeroRuta = numero ?? string.Empty;
        formulario.Calculo = null;
        formulario.Detalles ??= new List<DetalleFacturaFormulario>();
        if (!ClavesDeFilaValidas(formulario))
        {
            return BadRequest("Las claves de las filas no son válidas.");
        }

        if (!string.IsNullOrEmpty(comandoFila))
        {
            ProcesarComandoFila(formulario, comandoFila);
            return await VolverAlFormularioAsync(formulario, numero);
        }

        if (formulario.Detalles.Count == 0)
        {
            ModelState.AddModelError(nameof(formulario.Detalles), "Agregue al menos una línea a la factura.");
        }

        if (!ModelState.IsValid)
        {
            return await VolverAlFormularioAsync(formulario, numero);
        }

        var cancelacion = HttpContext.RequestAborted;
        try
        {
            using var clienteHttp = CrearClienteHttpApi();
            if (accion == "calcular")
            {
                return await CalcularFormularioAsync(
                    formulario, numero, clienteHttp, cancelacion);
            }

            if (accion != "guardar")
            {
                ModelState.AddModelError(string.Empty, "Seleccione calcular o guardar la factura.");
                return await VolverAlFormularioAsync(formulario, numero, clienteHttp);
            }

            return await GuardarFormularioAsync(
                formulario, numero, clienteHttp, cancelacion);
        }
        catch (HttpRequestException)
        {
            if (accion == "guardar")
            {
                AgregarErrorMutacionNoConfirmada();
            }
            else
            {
                ModelState.AddModelError(string.Empty, "No se pudo calcular la factura con la API.");
            }
            return await VolverAlFormularioAsync(formulario, numero);
        }
        catch (OperationCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            if (accion == "guardar")
            {
                AgregarErrorMutacionNoConfirmada();
            }
            else
            {
                ModelState.AddModelError(string.Empty, "El cálculo de la factura tardó demasiado.");
            }
            return await VolverAlFormularioAsync(formulario, numero);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException)
        {
            if (accion == "guardar")
            {
                AgregarErrorMutacionNoConfirmada();
            }
            else
            {
                ModelState.AddModelError(string.Empty, "La API respondió con un cálculo inesperado.");
            }

            return await VolverAlFormularioAsync(formulario, numero);
        }
    }

    // Solicita la vista previa sin reemplazar las filas ni sus Id.
    private async Task<IActionResult> CalcularFormularioAsync(
        FormularioFactura formulario,
        string? numero,
        HttpClient clienteHttp,
        CancellationToken cancelacion)
    {
        var solicitud = CrearSolicitud(formulario, numero is not null);
        var ruta = numero is null
            ? "api/facturas/calcular"
            : $"api/facturas/{Uri.EscapeDataString(numero)}/calcular";
        using var respuesta = await clienteHttp.PostAsJsonAsync(ruta, solicitud, cancelacion);
        if (respuesta.StatusCode == HttpStatusCode.NotFound && numero is not null)
        {
            return this.RegistroNoEncontrado("La factura solicitada no existe o ya fue eliminada.");
        }

        if (respuesta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            AgregarErroresFormulario(
                await LeerErrorApiAsync(respuesta, cancelacion), formulario);
            return await VolverAlFormularioAsync(formulario, numero, clienteHttp);
        }

        if (respuesta.StatusCode != HttpStatusCode.OK)
        {
            ModelState.AddModelError(string.Empty, "No se pudo calcular la factura con la API.");
            return await VolverAlFormularioAsync(formulario, numero, clienteHttp);
        }

        var calculo = await LeerRespuestaAsync<RespuestaCalculoFactura>(respuesta, cancelacion);
        if (!CalculoValido(calculo, formulario))
        {
            ModelState.AddModelError(string.Empty, "La API respondió con un cálculo inesperado.");
            return await VolverAlFormularioAsync(formulario, numero, clienteHttp);
        }

        var calculoValido = calculo!;
        formulario.Calculo = calculoValido;
        for (int indice = 0; indice < formulario.Detalles.Count; indice++)
        {
            var detalleCalculado = calculoValido.Detalles[indice];
            formulario.Detalles[indice].UnidadMedida = detalleCalculado.UnidadMedida;
            formulario.Detalles[indice].PrecioUnitario = detalleCalculado.PrecioUnitario;
            formulario.Detalles[indice].SubtotalLinea = detalleCalculado.SubtotalLinea;
        }

        if (!await CargarContextoFormularioAsync(formulario, numero, clienteHttp, cancelacion))
        {
            ModelState.AddModelError(string.Empty, "No se pudieron recargar todas las opciones de la API.");
        }

        return View(numero is null ? "Crear" : "Editar", formulario);
    }

    // Guarda la solicitud correcta para creacion o edicion.
    private async Task<IActionResult> GuardarFormularioAsync(
        FormularioFactura formulario,
        string? numero,
        HttpClient clienteHttp,
        CancellationToken cancelacion)
    {
        if (numero is not null)
        {
            var (codigoActual, facturaActual) = await ObtenerFacturaAsync(
                clienteHttp, numero, cancelacion);
            if (codigoActual == HttpStatusCode.NotFound)
            {
                return this.RegistroNoEncontrado("La factura solicitada no existe o ya fue eliminada.");
            }

            if (codigoActual != HttpStatusCode.OK || facturaActual is null)
            {
                ModelState.AddModelError(string.Empty, "No se pudo consultar la factura antes de guardar.");
                return await VolverAlFormularioAsync(formulario, numero, clienteHttp);
            }

            formulario.NumeroFactura = facturaActual.Numero;
            formulario.NumeroRuta = numero;
            formulario.FechaEmision = facturaActual.FechaEmision;
            formulario.EstadoActual = facturaActual.Estado;
            formulario.PuedeEditar = facturaActual.Estado is EstadoFactura.Pendiente or EstadoFactura.Pagada;
            if (!formulario.PuedeEditar)
            {
                ModelState.AddModelError(string.Empty, "La factura no está Pendiente ni Pagada.");
                return await VolverAlFormularioAsync(formulario, numero, clienteHttp);
            }
        }

        HttpResponseMessage respuesta = numero is null
            ? await clienteHttp.PostAsJsonAsync(
                "api/facturas", CrearSolicitud(formulario, false), cancelacion)
            : await clienteHttp.PutAsJsonAsync(
                $"api/facturas/{Uri.EscapeDataString(numero)}",
                CrearSolicitud(formulario, true),
                cancelacion);
        using (respuesta)
        {
            if (respuesta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            {
                AgregarErroresFormulario(
                    await LeerErrorApiAsync(respuesta, cancelacion), formulario);
                return await VolverAlFormularioAsync(formulario, numero, clienteHttp);
            }

            if (respuesta.StatusCode == HttpStatusCode.NotFound && numero is not null)
            {
                return this.RegistroNoEncontrado("La factura solicitada no existe o ya fue eliminada.");
            }

            HttpStatusCode codigoEsperado = numero is null
                ? HttpStatusCode.Created
                : HttpStatusCode.OK;
            if (respuesta.StatusCode != codigoEsperado)
            {
                AgregarErrorMutacionNoConfirmada();
                return await VolverAlFormularioAsync(formulario, numero, clienteHttp);
            }

            var factura = await LeerRespuestaAsync<RespuestaFactura>(respuesta, cancelacion);
            if (!RespuestaCoincideConFormulario(
                    factura, formulario, numero))
            {
                AgregarErrorMutacionNoConfirmada();
                return await VolverAlFormularioAsync(formulario, numero, clienteHttp);
            }

            TempData["MensajeExito"] = numero is null
                ? $"La factura {factura!.Numero} se registró correctamente."
                : $"La factura {factura!.Numero} se actualizó correctamente.";
            return RedirectToAction(nameof(Detalles), new { numero = factura!.Numero });
        }
    }

    // Carga catalogos y cabecera sin reemplazar los valores enviados.
    private async Task<IActionResult> VolverAlFormularioAsync(
        FormularioFactura formulario,
        string? numero,
        HttpClient? clienteExistente = null)
    {
        if (clienteExistente is not null)
        {
            return await VolverAlFormularioConClienteAsync(
                formulario, numero, clienteExistente);
        }

        using var clienteHttp = CrearClienteHttpApi();
        return await VolverAlFormularioConClienteAsync(formulario, numero, clienteHttp);
    }

    // Recarga opciones sin disponer el cliente que pertenece a la accion.
    private async Task<IActionResult> VolverAlFormularioConClienteAsync(
        FormularioFactura formulario,
        string? numero,
        HttpClient clienteHttp)
    {
        var cancelacion = HttpContext.RequestAborted;
        try
        {
            bool contextoValido = await CargarContextoFormularioAsync(
                formulario, numero, clienteHttp, cancelacion);
            if (!contextoValido)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "No se pudieron recargar todas las opciones. Revise los datos antes de guardar.");
            }

            return View(numero is null ? "Crear" : "Editar", formulario);
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, "No se pudieron recargar los datos de la API.");
            return View(numero is null ? "Crear" : "Editar", formulario);
        }
        catch (OperationCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            ModelState.AddModelError(string.Empty, "La recarga de opciones tardó demasiado.");
            return View(numero is null ? "Crear" : "Editar", formulario);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException)
        {
            ModelState.AddModelError(string.Empty, "La API respondió con opciones inesperadas.");
            return View(numero is null ? "Crear" : "Editar", formulario);
        }
    }

    // Lee el estado vigente y los catalogos requeridos por el formulario.
    private async Task<bool> CargarContextoFormularioAsync(
        FormularioFactura formulario,
        string? numero,
        HttpClient clienteHttp,
        CancellationToken cancelacion)
    {
        bool correcto = true;
        if (numero is not null)
        {
            var (codigo, factura) = await ObtenerFacturaAsync(clienteHttp, numero, cancelacion);
            if (codigo == HttpStatusCode.NotFound)
            {
                return false;
            }

            if (codigo == HttpStatusCode.OK && factura is not null)
            {
                formulario.NumeroFactura = factura.Numero;
                formulario.FechaEmision = factura.FechaEmision;
                formulario.EstadoActual = factura.Estado;
                formulario.PuedeEditar = factura.Estado is EstadoFactura.Pendiente or EstadoFactura.Pagada;
            }
            else
            {
                correcto = false;
            }
        }

        bool catalogosCargados = await CargarCatalogosAsync(formulario, clienteHttp, cancelacion);
        formulario.CatalogosDisponibles = catalogosCargados;
        return catalogosCargados && correcto;
    }

    // Carga listas de seleccion y reglas numericas desde la API.
    private async Task<bool> CargarCatalogosAsync(
        FormularioFactura formulario,
        HttpClient clienteHttp,
        CancellationToken cancelacion)
    {
        var clientes = await LeerColeccionAsync<RespuestaCliente>(clienteHttp, "api/clientes", cancelacion);
        var empleados = await LeerColeccionAsync<RespuestaEmpleado>(clienteHttp, "api/empleados", cancelacion);
        var productos = await LeerColeccionAsync<RespuestaProducto>(clienteHttp, "api/productos", cancelacion);
        using var respuestaConfiguracion = await clienteHttp.GetAsync(
            "api/facturas/configuracion", HttpCompletionOption.ResponseContentRead, cancelacion);
        var configuracion = respuestaConfiguracion.IsSuccessStatusCode
            ? await LeerRespuestaAsync<RespuestaConfiguracionFacturas>(respuestaConfiguracion, cancelacion)
            : null;

        bool correcto = clientes is not null && clientes.All(ClienteValido) &&
            empleados is not null && empleados.All(EmpleadoValido) &&
            productos is not null && productos.All(ProductoValido) &&
            configuracion is not null && configuracion.PorcentajeDescuentoMaximo >= 0m &&
            configuracion.PorcentajeIva >= 0m;

        if (clientes is not null && clientes.All(ClienteValido)) formulario.Clientes = clientes;
        if (empleados is not null && empleados.All(EmpleadoValido)) formulario.Empleados = empleados;
        if (productos is not null && productos.All(ProductoValido)) formulario.Productos = productos;
        if (configuracion is not null && configuracion.PorcentajeDescuentoMaximo >= 0m)
            formulario.PorcentajeDescuentoMaximo = configuracion.PorcentajeDescuentoMaximo;

        return correcto;
    }

    // Lee una coleccion de catalogo y descarta cuerpos vacios o invalidos.
    private static async Task<T[]?> LeerColeccionAsync<T>(
        HttpClient clienteHttp,
        string ruta,
        CancellationToken cancelacion)
    {
        using var respuesta = await clienteHttp.GetAsync(
            ruta, HttpCompletionOption.ResponseContentRead, cancelacion);
        if (!respuesta.IsSuccessStatusCode)
        {
            return null;
        }

        return await LeerRespuestaAsync<T[]>(respuesta, cancelacion);
    }

    // Obtiene la factura vigente y comprueba su contrato antes de presentarla.
    private async Task<(HttpStatusCode Codigo, RespuestaFactura? Factura)> ObtenerFacturaAsync(
        HttpClient clienteHttp,
        string numero,
        CancellationToken cancelacion)
    {
        using var respuesta = await clienteHttp.GetAsync(
            $"api/facturas/{Uri.EscapeDataString(numero)}",
            HttpCompletionOption.ResponseContentRead,
            cancelacion);
        if (respuesta.StatusCode != HttpStatusCode.OK)
        {
            return (respuesta.StatusCode, null);
        }

        var factura = await LeerRespuestaAsync<RespuestaFactura>(respuesta, cancelacion);
        return RespuestaValida(factura) && NumeroCoincide(factura!, numero)
            ? (HttpStatusCode.OK, factura)
            : (HttpStatusCode.ServiceUnavailable, null);
    }

    // Crea un formulario sin identidades de servidor.
    private static FormularioFactura NuevoFormulario(bool edicion = false)
    {
        return new FormularioFactura
        {
            EsEdicion = edicion,
            Detalles = new List<DetalleFacturaFormulario> { NuevaFila() }
        };
    }

    // Proyecta la factura consultada sin recalcular sus importes.
    private static FormularioFactura CrearFormularioEdicion(RespuestaFactura factura)
    {
        return new FormularioFactura
        {
            EsEdicion = true,
            ClienteId = factura.ClienteId,
            EmpleadoId = factura.EmpleadoId,
            MetodoPago = factura.MetodoPago,
            PorcentajeDescuento = factura.PorcentajeDescuento,
            NumeroFactura = factura.Numero,
            FechaEmision = factura.FechaEmision,
            EstadoActual = factura.Estado,
            PuedeEditar = factura.Estado is EstadoFactura.Pendiente or EstadoFactura.Pagada,
            Calculo = new RespuestaCalculoFactura
            {
                PorcentajeDescuento = factura.PorcentajeDescuento,
                Subtotal = factura.Subtotal,
                MontoDescuento = factura.MontoDescuento,
                BaseGravable = factura.BaseGravable,
                MontoIva = factura.MontoIva,
                Total = factura.Total,
                Detalles = Array.AsReadOnly(factura.Detalles.Select(detalle =>
                    new RespuestaDetalleCalculoFactura
                    {
                        ProductoId = detalle.ProductoId,
                        ProductoCodigo = detalle.ProductoCodigo,
                        ProductoNombre = detalle.ProductoNombre,
                        UnidadMedida = detalle.UnidadMedida,
                        Cantidad = detalle.Cantidad,
                        PrecioUnitario = detalle.PrecioUnitario,
                        SubtotalLinea = detalle.SubtotalLinea
                    }).ToArray())
            },
            Detalles = factura.Detalles.Select(detalle => new DetalleFacturaFormulario
            {
                ClaveFormulario = NuevaClaveFila(),
                Id = detalle.Id,
                ProductoId = detalle.ProductoId,
                Cantidad = detalle.Cantidad,
                UnidadMedida = detalle.UnidadMedida,
                PrecioUnitario = detalle.PrecioUnitario,
                SubtotalLinea = detalle.SubtotalLinea
            }).ToList()
        };
    }

    // Convierte campos validados a uno de los dos contratos de escritura.
    private static object CrearSolicitud(FormularioFactura formulario, bool edicion)
    {
        return edicion
            ? new SolicitudEdicionFactura
            {
                ClienteId = formulario.ClienteId!.Value,
                EmpleadoId = formulario.EmpleadoId!.Value,
                MetodoPago = formulario.MetodoPago!.Value,
                PorcentajeDescuento = formulario.PorcentajeDescuento!.Value,
                ActualizarPreciosYUnidades = formulario.ActualizarPreciosYUnidades,
                Detalles = formulario.Detalles.Select(detalle =>
                    (SolicitudDetalleEdicionFactura?)new SolicitudDetalleEdicionFactura
                    {
                        Id = detalle.Id,
                        ProductoId = detalle.ProductoId!.Value,
                        Cantidad = detalle.Cantidad!.Value
                    }).ToList()
            }
            : new SolicitudFactura
            {
                ClienteId = formulario.ClienteId!.Value,
                EmpleadoId = formulario.EmpleadoId!.Value,
                MetodoPago = formulario.MetodoPago!.Value,
                PorcentajeDescuento = formulario.PorcentajeDescuento!.Value,
                Detalles = formulario.Detalles.Select(detalle => new SolicitudDetalleFactura
                {
                    ProductoId = detalle.ProductoId!.Value,
                    Cantidad = detalle.Cantidad!.Value
                }).ToList()
            };
    }

    // Conserva o retira una fila sin validar los demas valores del formulario.
    private void ProcesarComandoFila(FormularioFactura formulario, string comandoFila)
    {
        if (string.Equals(comandoFila, "agregar", StringComparison.Ordinal))
        {
            formulario.Detalles.Add(NuevaFila());
            return;
        }

        const string prefijoQuitar = "quitar:";
        if (!comandoFila.StartsWith(prefijoQuitar, StringComparison.Ordinal))
        {
            ModelState.AddModelError(string.Empty, "La accion de fila no es válida.");
            return;
        }

        var clave = comandoFila[prefijoQuitar.Length..];
        var indice = formulario.Detalles.FindIndex(detalle =>
            string.Equals(detalle.ClaveFormulario, clave, StringComparison.Ordinal));
        if (indice < 0)
        {
            ModelState.AddModelError(string.Empty, "La fila solicitada ya no existe.");
            return;
        }

        formulario.Detalles.RemoveAt(indice);
        string prefijoCampo = $"Detalles[{clave}]";
        foreach (string claveModelState in ModelState.Keys
            .Where(claveModelState => claveModelState.StartsWith(
                prefijoCampo + ".", StringComparison.OrdinalIgnoreCase))
            .ToArray())
        {
            ModelState.Remove(claveModelState);
        }
    }

    // Comprueba que cada token del binder coincida con una fila unica y acotada.
    private bool ClavesDeFilaValidas(FormularioFactura formulario)
    {
        if (!Request.HasFormContentType)
        {
            return false;
        }

        var indices = Request.Form["Detalles.Index"];
        if (indices.Count != formulario.Detalles.Count)
        {
            return false;
        }

        var vistos = new HashSet<string>(StringComparer.Ordinal);
        for (int indice = 0; indice < indices.Count; indice++)
        {
            string? clave = indices[indice];
            var detalle = formulario.Detalles[indice];
            if (clave is null ||
                detalle is null ||
                !Guid.TryParseExact(clave, "N", out Guid identificador) ||
                identificador == Guid.Empty ||
                !vistos.Add(clave) ||
                !string.Equals(
                    detalle.ClaveFormulario,
                    clave,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    // Asocia errores por ordinal a la clave estable de cada fila.
    private void AgregarErroresFormulario(ErrorApi error, FormularioFactura formulario)
    {
        int erroresIniciales = ModelState.ErrorCount;
        foreach (var (claveOriginal, mensajesOriginales) in error.ErroresPorCampo ?? new())
        {
            string clave = NormalizarClaveApi(claveOriginal);
            string? claveMvc = ResolverClaveMvc(clave, formulario);
            var mensajes = (mensajesOriginales ?? Array.Empty<string>())
                .Where(mensaje => !string.IsNullOrWhiteSpace(mensaje))
                .ToArray();

            if (claveMvc is null)
            {
                if (mensajes.Length == 0)
                {
                    ModelState.AddModelError(string.Empty, MensajeErrorApi(error));
                }
                else
                {
                    foreach (string mensaje in mensajes)
                    {
                        ModelState.AddModelError(string.Empty, mensaje);
                    }
                }

                continue;
            }

            if (mensajes.Length == 0)
            {
                ModelState.AddModelError(claveMvc, MensajeErrorApi(error));
                continue;
            }

            foreach (string mensaje in mensajes)
            {
                ModelState.AddModelError(claveMvc, mensaje);
            }
        }

        if (ModelState.ErrorCount == erroresIniciales)
        {
            ModelState.AddModelError(string.Empty, MensajeErrorApi(error));
        }
    }

    // Normaliza prefijos de las rutas JSON sin aceptar nombres arbitrarios.
    private static string NormalizarClaveApi(string clave)
    {
        string normalizada = clave.Trim();
        if (normalizada.StartsWith("$.", StringComparison.Ordinal))
        {
            normalizada = normalizada[2..];
        }

        if (normalizada.StartsWith("solicitud.", StringComparison.OrdinalIgnoreCase))
        {
            normalizada = normalizada["solicitud.".Length..];
        }

        return normalizada;
    }

    // Limita las claves de error a campos visibles del formulario.
    private static string? ResolverClaveMvc(string clave, FormularioFactura formulario)
    {
        string[] camposCabecera =
        {
            nameof(formulario.ClienteId),
            nameof(formulario.EmpleadoId),
            nameof(formulario.MetodoPago),
            nameof(formulario.PorcentajeDescuento),
            nameof(formulario.ActualizarPreciosYUnidades)
        };
        string? campoCabecera = camposCabecera.FirstOrDefault(campo =>
            string.Equals(campo, clave, StringComparison.OrdinalIgnoreCase));
        if (campoCabecera is not null)
        {
            return campoCabecera;
        }

        const string prefijoDetalle = "Detalles[";
        if (!clave.StartsWith(prefijoDetalle, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        int cierreIndice = clave.IndexOf(']', prefijoDetalle.Length);
        if (cierreIndice < 0 || cierreIndice + 2 >= clave.Length || clave[cierreIndice + 1] != '.')
        {
            return null;
        }

        string textoIndice = clave[prefijoDetalle.Length..cierreIndice];
        if (!int.TryParse(textoIndice, NumberStyles.None, CultureInfo.InvariantCulture, out int ordinal) ||
            ordinal < 0 || ordinal >= formulario.Detalles.Count)
        {
            return null;
        }

        string campoDetalle = clave[(cierreIndice + 2)..];
        string? nombreReal = new[]
        {
            nameof(DetalleFacturaFormulario.Id),
            nameof(DetalleFacturaFormulario.ProductoId),
            nameof(DetalleFacturaFormulario.Cantidad)
        }.FirstOrDefault(campo => string.Equals(
            campo, campoDetalle, StringComparison.OrdinalIgnoreCase));
        return nombreReal is null
            ? null
            : $"Detalles[{formulario.Detalles[ordinal].ClaveFormulario}].{nombreReal}";
    }

    // Devuelve un mensaje no vacio para errores de API con detalles incompletos.
    private static string MensajeErrorApi(ErrorApi error) =>
        string.IsNullOrWhiteSpace(error.Mensaje)
            ? "La API rechazó los datos de la factura."
            : error.Mensaje;

    // Verifica que el calculo corresponda a las filas enviadas.
    private static bool CalculoValido(
        RespuestaCalculoFactura? calculo,
        FormularioFactura formulario)
    {
        return calculo is not null &&
            calculo.PorcentajeDescuento == formulario.PorcentajeDescuento &&
            calculo.Subtotal >= 0m && calculo.MontoDescuento >= 0m &&
            calculo.BaseGravable >= 0m && calculo.MontoIva >= 0m && calculo.Total >= 0m &&
            calculo.Detalles is { Count: > 0 } &&
            calculo.Detalles.Count == formulario.Detalles.Count &&
            calculo.Detalles.Select((detalle, indice) =>
                detalle is not null &&
                detalle.ProductoId == formulario.Detalles[indice].ProductoId &&
                detalle.Cantidad == formulario.Detalles[indice].Cantidad &&
                Enum.IsDefined(typeof(UnidadMedida), detalle.UnidadMedida) &&
                detalle.Cantidad > 0m && detalle.PrecioUnitario >= 0m &&
                detalle.SubtotalLinea >= 0m).All(valido => valido);
    }

    // Comprueba que la respuesta corresponda a los datos enviados al servicio.
    private static bool RespuestaCoincideConFormulario(
        RespuestaFactura? factura,
        FormularioFactura formulario,
        string? numero)
    {
        if (!RespuestaValida(factura))
        {
            return false;
        }

        var facturaValidada = factura!;
        if (facturaValidada.ClienteId != formulario.ClienteId ||
            facturaValidada.EmpleadoId != formulario.EmpleadoId ||
            facturaValidada.MetodoPago != formulario.MetodoPago ||
            facturaValidada.PorcentajeDescuento != formulario.PorcentajeDescuento ||
            facturaValidada.Detalles.Count != formulario.Detalles.Count ||
            (numero is null && facturaValidada.Estado != EstadoFactura.Pendiente) ||
            (numero is not null && facturaValidada.Estado is not (EstadoFactura.Pendiente or EstadoFactura.Pagada)) ||
            (numero is not null && !NumeroCoincide(facturaValidada, numero)))
        {
            return false;
        }

        for (int indice = 0; indice < formulario.Detalles.Count; indice++)
        {
            var fila = formulario.Detalles[indice];
            var detalle = facturaValidada.Detalles[indice];
            if (detalle.ProductoId != fila.ProductoId ||
                detalle.Cantidad != fila.Cantidad ||
                (fila.Id.HasValue && detalle.Id != fila.Id.Value))
            {
                return false;
            }
        }

        return true;
    }

    // Crea una clave de formulario que no representa el Id de la linea.
    private static DetalleFacturaFormulario NuevaFila() => new()
    {
        ClaveFormulario = NuevaClaveFila()
    };

    // Genera el token estable que enlaza una fila del postback.
    private static string NuevaClaveFila() => Guid.NewGuid().ToString("N");

    // Comprueba los datos minimos para ofrecer un cliente en el formulario.
    private static bool ClienteValido(RespuestaCliente cliente) =>
        cliente is not null && cliente.Id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(cliente.Identificacion) &&
        !string.IsNullOrWhiteSpace(cliente.Nombre);

    // Comprueba los datos minimos para ofrecer un empleado en el formulario.
    private static bool EmpleadoValido(RespuestaEmpleado empleado) =>
        empleado is not null && empleado.Id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(empleado.Identificacion) &&
        !string.IsNullOrWhiteSpace(empleado.Nombre) &&
        Enum.IsDefined(typeof(AreaTrabajo), empleado.AreaTrabajo) &&
        Enum.IsDefined(typeof(Turno), empleado.Turno);

    // Comprueba los datos minimos para ofrecer un producto en el formulario.
    private static bool ProductoValido(RespuestaProducto producto) =>
        producto is not null && producto.Id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(producto.Codigo) &&
        !string.IsNullOrWhiteSpace(producto.Nombre) &&
        Enum.IsDefined(typeof(UnidadMedida), producto.UnidadMedida) &&
        producto.PrecioUnidadMedida >= 0m && producto.ExistenciaActual >= 0m;
}
