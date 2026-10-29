param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Validaciones', 'Busquedas', 'Proveedores', 'Integridad', 'Concurrencia')]
    [string]$Grupo,

    [int]$Puerto = 5233
)

$ErrorActionPreference = 'Stop'
$cantidadVerificaciones = 0
$procesoApi = $null
$clienteHttpConcurrente = $null
$rutaSalidaApi = Join-Path $env:TEMP 'carniceria-api-fase3-salida.log'
$rutaErrorApi = Join-Path $env:TEMP 'carniceria-api-fase3-error.log'
$uriBase = "http://127.0.0.1:$Puerto"
$rutaRaiz = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$rutaProyectoApi = Join-Path $rutaRaiz 'Carniceria.Api\Carniceria.Api.csproj'

function Confirmar {
    param(
        [bool]$Condicion,
        [string]$Descripcion
    )

    if (-not $Condicion) {
        throw "Falló la verificación: $Descripcion"
    }

    $script:cantidadVerificaciones += 1
    Write-Host "APROBADO: $Descripcion"
}

function InvocarApi {
    param(
        [string]$Metodo,
        [string]$Ruta,
        [object]$Cuerpo,
        [switch]$CuerpoCrudo
    )

    $parametrosSolicitud = @{
        Uri = "$script:uriBase$Ruta"
        Method = $Metodo
        UseBasicParsing = $true
        TimeoutSec = 10
    }

    if ($PSBoundParameters.ContainsKey('Cuerpo')) {
        $parametrosSolicitud.ContentType = 'application/json; charset=utf-8'
        $parametrosSolicitud.Body = if ($CuerpoCrudo) {
            [string]$Cuerpo
        }
        else {
            ConvertTo-Json -InputObject $Cuerpo -Depth 12 -Compress
        }
    }

    try {
        $respuesta = Invoke-WebRequest @parametrosSolicitud
        $contenido = [string]$respuesta.Content
        $datos = if (-not [string]::IsNullOrWhiteSpace($contenido)) {
            ConvertFrom-Json -InputObject $contenido
        }
        else {
            $null
        }

        return [pscustomobject]@{
            Codigo = [int]$respuesta.StatusCode
            Contenido = $contenido
            Datos = $datos
            Ubicacion = [string]$respuesta.Headers['Location']
        }
    }
    catch [System.Net.WebException] {
        $respuestaError = $_.Exception.Response
        if ($null -eq $respuestaError) {
            throw
        }

        $lector = New-Object System.IO.StreamReader(
            $respuestaError.GetResponseStream(),
            [System.Text.Encoding]::UTF8)
        try {
            $contenido = $lector.ReadToEnd()
        }
        finally {
            $lector.Dispose()
        }

        $datos = if (-not [string]::IsNullOrWhiteSpace($contenido)) {
            ConvertFrom-Json -InputObject $contenido
        }
        else {
            $null
        }

        return [pscustomobject]@{
            Codigo = [int]$respuestaError.StatusCode
            Contenido = $contenido
            Datos = $datos
            Ubicacion = [string]$respuestaError.Headers['Location']
        }
    }
}

function CopiarObjeto {
    param([object]$Objeto)

    return ,(ConvertFrom-Json -InputObject (
        ConvertTo-Json -InputObject $Objeto -Depth 12 -Compress))
}

function ConfirmarErrorApi {
    param(
        [object]$Respuesta,
        [int]$Codigo,
        [string]$Descripcion,
        [string]$Campo
    )

    Confirmar ($Respuesta.Codigo -eq $Codigo) "$Descripcion devuelve HTTP $Codigo"
    Confirmar ($null -ne $Respuesta.Datos -and
        -not [string]::IsNullOrWhiteSpace([string]$Respuesta.Datos.codigo) -and
        -not [string]::IsNullOrWhiteSpace([string]$Respuesta.Datos.mensaje) -and
        $null -ne $Respuesta.Datos.erroresPorCampo) "$Descripcion conserva el formato ErrorApi"
    if ($Campo) {
        $camposInformados = @($Respuesta.Datos.erroresPorCampo.PSObject.Properties.Name)
        $campoRutaJson = "`$.$Campo"
        $campoCoincide = @($camposInformados | Where-Object {
            [string]::Equals($_, $Campo, [System.StringComparison]::OrdinalIgnoreCase) -or
            [string]::Equals($_, $campoRutaJson, [System.StringComparison]::OrdinalIgnoreCase)
        }).Count -gt 0
        Confirmar $campoCoincide "$Descripcion informa el campo $Campo"
    }
}

function EnviarLoteConcurrente {
    param(
        [object]$ClienteHttp,
        [object[]]$Solicitudes
    )

    $peticiones = @()
    $tareas = @()
    $respuestas = @()
    try {
        foreach ($solicitud in $Solicitudes) {
            $metodoHttp = [System.Net.Http.HttpMethod]::new([string]$solicitud.Metodo)
            $peticion = [System.Net.Http.HttpRequestMessage]::new(
                $metodoHttp,
                "$script:uriBase$($solicitud.Ruta)")
            if ($null -ne $solicitud.Cuerpo) {
                $json = ConvertTo-Json -InputObject $solicitud.Cuerpo -Depth 12 -Compress
                $peticion.Content = [System.Net.Http.StringContent]::new(
                    $json,
                    [System.Text.Encoding]::UTF8,
                    'application/json')
            }

            $peticiones += $peticion
            $tareas += $ClienteHttp.SendAsync($peticion)
        }

        $termino = [System.Threading.Tasks.Task]::WaitAll(
            [System.Threading.Tasks.Task[]]$tareas,
            [TimeSpan]::FromSeconds(30))
        if (-not $termino) {
            throw 'Las solicitudes HTTP concurrentes excedieron el límite de 30 segundos.'
        }

        for ($indice = 0; $indice -lt $tareas.Count; $indice++) {
            $respuestaHttp = $tareas[$indice].GetAwaiter().GetResult()
            $contenido = if ($null -ne $respuestaHttp.Content) {
                $respuestaHttp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            }
            else {
                ''
            }
            $datos = if (-not [string]::IsNullOrWhiteSpace($contenido)) {
                ConvertFrom-Json -InputObject $contenido
            }
            else {
                $null
            }
            $respuestas += [pscustomobject]@{
                Codigo = [int]$respuestaHttp.StatusCode
                Contenido = $contenido
                Datos = $datos
                Ruta = [string]$Solicitudes[$indice].Ruta
            }
            $respuestaHttp.Dispose()
        }

        return ,$respuestas
    }
    finally {
        foreach ($peticionHttp in $peticiones) {
            $peticionHttp.Dispose()
        }
    }
}

function ConfirmarProductoRelacionado {
    param(
        [object]$Producto,
        [string]$IdProducto,
        [object]$ProveedorA,
        [object]$ProveedorB,
        [string]$Descripcion
    )

    $idA = [string]$ProveedorA.id
    $idB = [string]$ProveedorB.id
    $coincideA = [string]$Producto.proveedorId -eq $idA -and
        [string]$Producto.proveedorRazonSocial -eq [string]$ProveedorA.razonSocial -and
        [string]$Producto.proveedorIdentificacion -eq [string]$ProveedorA.identificacion
    $coincideB = [string]$Producto.proveedorId -eq $idB -and
        [string]$Producto.proveedorRazonSocial -eq [string]$ProveedorB.razonSocial -and
        [string]$Producto.proveedorIdentificacion -eq [string]$ProveedorB.identificacion
    Confirmar ([string]$Producto.id -eq $IdProducto -and ($coincideA -or $coincideB)) $Descripcion
}

function CrearRegistro {
    param(
        [string]$Ruta,
        [object]$Formulario,
        [string]$Descripcion
    )

    $respuesta = InvocarApi -Metodo 'Post' -Ruta $Ruta -Cuerpo $Formulario
    Confirmar ($respuesta.Codigo -eq 201) "$Descripcion acepta alta válida"
    Confirmar ($null -ne $respuesta.Datos.id -and
        [guid]$respuesta.Datos.id -ne [guid]$Formulario.id) "$Descripcion genera Id de servidor e ignora Id del cuerpo"

    $rutaUbicacion = ([uri]$respuesta.Ubicacion).PathAndQuery
    Confirmar ($rutaUbicacion -match "/$([regex]::Escape([string]$respuesta.Datos.id))$") "$Descripcion responde Location para el Id generado"

    $consulta = InvocarApi -Metodo 'Get' -Ruta $rutaUbicacion
    Confirmar ($consulta.Codigo -eq 200 -and
        [string]$consulta.Datos.id -eq [string]$respuesta.Datos.id) "$Descripcion Location resuelve el registro creado"

    return $respuesta.Datos
}

function ActualizarRegistro {
    param(
        [string]$Ruta,
        [object]$Formulario,
        [object]$Registro,
        [string]$Descripcion
    )

    $formularioPrueba = CopiarObjeto $Formulario
    $formularioPrueba.id = [guid]::NewGuid().ToString()
    $respuesta = InvocarApi -Metodo 'Put' -Ruta "$Ruta/$($Registro.id)" -Cuerpo $formularioPrueba
    Confirmar ($respuesta.Codigo -eq 200) "$Descripcion permite autoedición sin duplicado"
    Confirmar ([string]$respuesta.Datos.id -eq [string]$Registro.id) "$Descripcion conserva el Id de ruta e ignora el Id del cuerpo"
}

try {
    $puertoEnUso = Get-NetTCPConnection -State Listen -LocalPort $Puerto -ErrorAction SilentlyContinue
    if ($puertoEnUso) {
        throw "El puerto $Puerto ya está ocupado; cierre su proceso o indique otro puerto con -Puerto."
    }

    $rutaApiCompilada = Join-Path $rutaRaiz 'Carniceria.Api\bin\Debug\net10.0\Carniceria.Api.dll'
    if (-not (Test-Path -LiteralPath $rutaApiCompilada)) {
        $rutaApiCompilada = Join-Path $rutaRaiz 'Carniceria.Api\bin\Release\net10.0\Carniceria.Api.dll'
    }
    if (-not (Test-Path -LiteralPath $rutaApiCompilada)) {
        throw 'No se encontró la API compilada. Compile la solución antes de ejecutar el grupo.'
    }

    $argumentosApi = '"{0}" --urls "{1}"' -f $rutaApiCompilada, $uriBase
    $procesoApi = Start-Process -FilePath 'dotnet' `
        -ArgumentList $argumentosApi `
        -WorkingDirectory $rutaRaiz `
        -WindowStyle Hidden `
        -PassThru `
        -RedirectStandardOutput $rutaSalidaApi `
        -RedirectStandardError $rutaErrorApi

    $apiDisponible = $false
    for ($intento = 0; $intento -lt 40; $intento++) {
        if ($procesoApi.HasExited) {
            throw "La API terminó al iniciar. Revise $rutaErrorApi"
        }

        try {
            $estado = Invoke-RestMethod -Uri "$uriBase/api/estado" -TimeoutSec 2
            if ($estado.estado -eq 'Disponible') {
                $apiDisponible = $true
                break
            }
        }
        catch {
            Start-Sleep -Milliseconds 250
        }
    }
    Confirmar $apiDisponible 'La API aislada inicia con sus datos semilla'

    if ($Grupo -eq 'Busquedas') {
        $proveedorSemilla = ((InvocarApi -Metodo 'Get' -Ruta '/api/proveedores').Datos | Select-Object -First 1)
        $clienteConsulta = @{
            tipoIdentificacion = 3; identificacion = 'PasF3Abc'; nombre = 'Ángela'
            primerApellido = 'Mora'; segundoApellido = 'Naranjo'; fechaNacimiento = '1995-04-02'
        }
        $respuestaCliente = InvocarApi -Metodo 'Post' -Ruta '/api/clientes' -Cuerpo $clienteConsulta
        Confirmar ($respuestaCliente.Codigo -eq 201) 'Datos de búsqueda crean el cliente de prueba'

        $proveedoresConsulta = @(
            @{ tipoIdentificacion = 4; identificacion = '3-301-234567'; razonSocial = 'ÁmbarCarnes'; nombreContacto = 'Mario Rojas'; telefono = '88887777'; correoElectronico = 'ambar1@example.com'; tipoSuministro = 8; frecuenciaEntrega = 3 },
            @{ tipoIdentificacion = 4; identificacion = '3-301-234568'; razonSocial = 'ÁmbarPollo'; nombreContacto = 'Ana Mora'; telefono = '88887776'; correoElectronico = 'ambar2@example.com'; tipoSuministro = 3; frecuenciaEntrega = 3 }
        )
        $proveedoresPrueba = @()
        foreach ($proveedorConsulta in $proveedoresConsulta) {
            $altaProveedor = InvocarApi -Metodo 'Post' -Ruta '/api/proveedores' -Cuerpo $proveedorConsulta
            Confirmar ($altaProveedor.Codigo -eq 201) "Alta de proveedor para búsqueda $($proveedorConsulta.razonSocial)"
            $proveedoresPrueba += $altaProveedor.Datos
        }

        $productosConsulta = @(
            @{ proveedorId = [string]$proveedoresPrueba[0].id; codigo = 'F3Arbol1'; nombre = 'ResÁmbar'; categoria = 1; unidadMedida = 1; precioUnidadMedida = 1400; existenciaActual = 5; existenciaMinima = 1 },
            @{ proveedorId = [string]$proveedoresPrueba[1].id; codigo = 'F3Arbol2'; nombre = 'PolloÁmbar'; categoria = 3; unidadMedida = 1; precioUnidadMedida = 1600; existenciaActual = 6; existenciaMinima = 1 }
        )
        $productosPrueba = @()
        foreach ($productoConsulta in $productosConsulta) {
            $altaProducto = InvocarApi -Metodo 'Post' -Ruta '/api/productos' -Cuerpo $productoConsulta
            Confirmar ($altaProducto.Codigo -eq 201) "Alta de producto para búsqueda $($productoConsulta.codigo)"
            $productosPrueba += $altaProducto.Datos
        }

        $consultaCodificada = [uri]::EscapeDataString(' PasF3aBc ')
        $resultado = InvocarApi -Metodo 'Get' -Ruta "/api/clientes/buscar?identificacion=$consultaCodificada"
        Confirmar ($resultado.Codigo -eq 200 -and @($resultado.Datos).Count -eq 1 -and
            [string]$resultado.Datos[0].id -eq [string]$respuestaCliente.Datos.id) 'Cliente busca identificación exacta con espacios y sin distinguir mayúsculas'
        $resultado = InvocarApi -Metodo 'Get' -Ruta '/api/clientes/buscar?identificacion=%20%20'
        Confirmar ($resultado.Codigo -eq 200 -and @($resultado.Datos).Count -eq 0) 'Cliente retorna lista vacía ante identificación en blanco'
        $resultado = InvocarApi -Metodo 'Get' -Ruta '/api/clientes/buscar?identificacion=PasF3'
        Confirmar ($resultado.Codigo -eq 200 -and @($resultado.Datos).Count -eq 0) 'Cliente exige identificación exacta'
        $resultado = InvocarApi -Metodo 'Get' -Ruta "/api/clientes/$($respuestaCliente.Datos.id)"
        Confirmar ($resultado.Codigo -eq 200 -and
            [string]$resultado.Datos.id -eq [string]$respuestaCliente.Datos.id) 'Ruta de cliente por Guid no colisiona con buscar'

        $empleadoSemilla = ((InvocarApi -Metodo 'Get' -Ruta '/api/empleados').Datos | Select-Object -First 1)
        $identificacionEmpleado = [uri]::EscapeDataString(" $($empleadoSemilla.identificacion) ")
        $resultado = InvocarApi -Metodo 'Get' -Ruta "/api/empleados/buscar?identificacion=$identificacionEmpleado"
        Confirmar ($resultado.Codigo -eq 200 -and @($resultado.Datos).Count -eq 1 -and
            [string]$resultado.Datos[0].id -eq [string]$empleadoSemilla.id) 'Empleado busca identificación exacta tras normalizar espacios'
        $resultado = InvocarApi -Metodo 'Get' -Ruta '/api/empleados/buscar?identificacion=%20'
        Confirmar ($resultado.Codigo -eq 200 -and @($resultado.Datos).Count -eq 0) 'Empleado retorna lista vacía ante identificación en blanco'
        $resultado = InvocarApi -Metodo 'Get' -Ruta '/api/empleados/buscar?identificacion=999999'
        Confirmar ($resultado.Codigo -eq 200 -and @($resultado.Datos).Count -eq 0) 'Empleado retorna lista vacía sin coincidencias'

        $resultado = InvocarApi -Metodo 'Get' -Ruta '/api/productos/buscar?criterio=%20cOdIgO%20&consulta=f3arbol1'
        Confirmar ($resultado.Codigo -eq 200 -and @($resultado.Datos).Count -eq 1 -and
            [string]$resultado.Datos[0].id -eq [string]$productosPrueba[0].id) 'Producto busca código exacto sin distinguir mayúsculas y normaliza criterio'
        $resultado = InvocarApi -Metodo 'Get' -Ruta ("/api/productos/buscar?criterio=Nombre&consulta=" + [uri]::EscapeDataString('áMBAR'))
        Confirmar ($resultado.Codigo -eq 200 -and @($resultado.Datos).Count -eq 2) 'Producto busca nombre parcial con tilde y devuelve múltiples coincidencias'
        $resultado = InvocarApi -Metodo 'Get' -Ruta '/api/productos/buscar?criterio=Codigo&consulta=F3Arbol'
        Confirmar ($resultado.Codigo -eq 200 -and @($resultado.Datos).Count -eq 0) 'Producto exige código exacto'
        $resultado = InvocarApi -Metodo 'Get' -Ruta '/api/productos/buscar?criterio=Nombre&consulta=%20%20'
        Confirmar ($resultado.Codigo -eq 200 -and @($resultado.Datos).Count -eq 0) 'Producto retorna lista vacía ante consulta en blanco'
        $resultado = InvocarApi -Metodo 'Get' -Ruta '/api/productos/buscar?criterio=Nombre&consulta=NoExiste'
        Confirmar ($resultado.Codigo -eq 200 -and @($resultado.Datos).Count -eq 0) 'Producto retorna lista vacía sin coincidencias'
        ConfirmarErrorApi (InvocarApi -Metodo 'Get' -Ruta '/api/productos/buscar?criterio=Otro&consulta=x') 400 'Criterio de producto desconocido' 'Criterio'
        ConfirmarErrorApi (InvocarApi -Metodo 'Get' -Ruta '/api/productos/buscar?consulta=x') 400 'Criterio de producto ausente' 'Criterio'
        $resultado = InvocarApi -Metodo 'Get' -Ruta "/api/productos/$($productosPrueba[0].id)"
        Confirmar ($resultado.Codigo -eq 200 -and
            [string]$resultado.Datos.id -eq [string]$productosPrueba[0].id) 'Ruta de producto por Guid no colisiona con buscar'

        $identificacionProveedor = [uri]::EscapeDataString($proveedoresPrueba[0].identificacion.ToLowerInvariant())
        $resultado = InvocarApi -Metodo 'Get' -Ruta "/api/proveedores/buscar?criterio=identificacion&consulta=$identificacionProveedor"
        Confirmar ($resultado.Codigo -eq 200 -and @($resultado.Datos).Count -eq 1 -and
            [string]$resultado.Datos[0].id -eq [string]$proveedoresPrueba[0].id) 'Proveedor busca identificación exacta sin distinguir mayúsculas'
        $resultado = InvocarApi -Metodo 'Get' -Ruta ("/api/proveedores/buscar?criterio=%20razonsocial%20&consulta=" + [uri]::EscapeDataString(' áMBAR '))
        Confirmar ($resultado.Codigo -eq 200 -and @($resultado.Datos).Count -eq 2) 'Proveedor busca razón social parcial con tilde y normaliza criterio y consulta'
        $resultado = InvocarApi -Metodo 'Get' -Ruta '/api/proveedores/buscar?criterio=Identificacion&consulta=3-301'
        Confirmar ($resultado.Codigo -eq 200 -and @($resultado.Datos).Count -eq 0) 'Proveedor exige identificación exacta'
        $resultado = InvocarApi -Metodo 'Get' -Ruta '/api/proveedores/buscar?criterio=RazonSocial&consulta=%20'
        Confirmar ($resultado.Codigo -eq 200 -and @($resultado.Datos).Count -eq 0) 'Proveedor retorna lista vacía ante consulta en blanco'
        $resultado = InvocarApi -Metodo 'Get' -Ruta '/api/proveedores/buscar?criterio=RazonSocial&consulta=NoExiste'
        Confirmar ($resultado.Codigo -eq 200 -and @($resultado.Datos).Count -eq 0) 'Proveedor retorna lista vacía sin coincidencias'
        ConfirmarErrorApi (InvocarApi -Metodo 'Get' -Ruta '/api/proveedores/buscar?criterio=Nombre&consulta=x') 400 'Criterio de proveedor desconocido' 'Criterio'
        ConfirmarErrorApi (InvocarApi -Metodo 'Get' -Ruta '/api/proveedores/buscar?consulta=x') 400 'Criterio de proveedor ausente' 'Criterio'
        $resultado = InvocarApi -Metodo 'Get' -Ruta "/api/proveedores/$($proveedoresPrueba[0].id)"
        Confirmar ($resultado.Codigo -eq 200 -and
            [string]$resultado.Datos.id -eq [string]$proveedoresPrueba[0].id) 'Ruta de proveedor por Guid no colisiona con buscar'

        Write-Host "Grupo $Grupo completado: $cantidadVerificaciones verificaciones."
    }
    elseif ($Grupo -eq 'Proveedores') {
        $proveedoresIniciales = (InvocarApi -Metodo 'Get' -Ruta '/api/proveedores').Datos
        $proveedorOrigen = $proveedoresIniciales | Select-Object -First 1
        $productoInicial = @{
            proveedorId = [string]$proveedorOrigen.id; codigo = 'F3RELACION'; nombre = 'ProductoRelacion'
            categoria = 1; unidadMedida = 1; precioUnidadMedida = 1700
            existenciaActual = 4; existenciaMinima = 1
        }
        $altaProducto = InvocarApi -Metodo 'Post' -Ruta '/api/productos' -Cuerpo $productoInicial
        Confirmar ($altaProducto.Codigo -eq 201) 'Producto de prueba queda relacionado con el proveedor inicial'
        $productoId = [string]$altaProducto.Datos.id
        Confirmar ([string]$altaProducto.Datos.proveedorId -eq [string]$proveedorOrigen.id) 'Alta expone el Id del proveedor inicial'
        $conteoProductos = @((InvocarApi -Metodo 'Get' -Ruta '/api/productos').Datos).Count
        $productoAntes = (InvocarApi -Metodo 'Get' -Ruta "/api/productos/$productoId").Datos

        $casosProveedorInvalido = @(
            @{ Nombre = 'nulo'; Tipo = 'nulo'; Valor = $null },
            @{ Nombre = 'ausente'; Tipo = 'ausente'; Valor = $null },
            @{ Nombre = 'Guid vacío'; Tipo = 'valor'; Valor = [guid]::Empty.ToString() },
            @{ Nombre = 'inexistente'; Tipo = 'valor'; Valor = [guid]::NewGuid().ToString() },
            @{ Nombre = 'formato Guid inválido'; Tipo = 'valor'; Valor = 'no-es-un-guid' }
        )

        foreach ($casoProveedor in $casosProveedorInvalido) {
            foreach ($operacion in @('Post', 'Put')) {
                $formularioInvalido = CopiarObjeto $productoInicial
                if ($casoProveedor.Tipo -eq 'ausente') {
                    $formularioInvalido.PSObject.Properties.Remove('proveedorId')
                }
                else {
                    $formularioInvalido.proveedorId = $casoProveedor.Valor
                }

                $rutaProducto = if ($operacion -eq 'Post') {
                    '/api/productos'
                }
                else {
                    "/api/productos/$productoId"
                }
                $cuerpoCrudo = $null
                $esCuerpoCrudo = $false
                if ($casoProveedor.Nombre -eq 'formato Guid inválido') {
                    $cuerpoCrudo = ConvertTo-Json -InputObject $formularioInvalido -Depth 12 -Compress
                    $esCuerpoCrudo = $true
                }

                if ($esCuerpoCrudo) {
                    $respuestaInvalida = InvocarApi -Metodo $operacion -Ruta $rutaProducto -Cuerpo $cuerpoCrudo -CuerpoCrudo
                }
                else {
                    $respuestaInvalida = InvocarApi -Metodo $operacion -Ruta $rutaProducto -Cuerpo $formularioInvalido
                }
                ConfirmarErrorApi $respuestaInvalida 400 "$operacion de producto con proveedor $($casoProveedor.Nombre)" 'proveedorId'

                $productoDespues = (InvocarApi -Metodo 'Get' -Ruta "/api/productos/$productoId").Datos
                $productosDespues = (InvocarApi -Metodo 'Get' -Ruta '/api/productos').Datos
                Confirmar ([string]$productoDespues.id -eq $productoId -and
                    [string]$productoDespues.proveedorId -eq [string]$productoAntes.proveedorId -and
                    [string]$productoDespues.codigo -eq [string]$productoAntes.codigo) "$operacion rechazada conserva los datos y la relación del producto"
                Confirmar (@($productosDespues).Count -eq $conteoProductos) "$operacion rechazada no cambia la cantidad de productos"
            }
        }

        $proveedorNuevo = @{
            tipoIdentificacion = 4; identificacion = '3-303-456789'; razonSocial = 'ProveedorF3Nuevo'
            nombreContacto = 'Elena Mora'; telefono = '88887775'; correoElectronico = 'nuevo@example.com'
            tipoSuministro = 8; frecuenciaEntrega = 3
        }
        $altaProveedor = InvocarApi -Metodo 'Post' -Ruta '/api/proveedores' -Cuerpo $proveedorNuevo
        Confirmar ($altaProveedor.Codigo -eq 201) 'Se registra el segundo proveedor para reasignar el producto'
        $proveedorIdNuevo = [string]$altaProveedor.Datos.id

        $formularioProductoActualizado = CopiarObjeto $productoInicial
        $formularioProductoActualizado.proveedorId = $proveedorIdNuevo
        $actualizacionProducto = InvocarApi -Metodo 'Put' -Ruta "/api/productos/$productoId" -Cuerpo $formularioProductoActualizado
        Confirmar ($actualizacionProducto.Codigo -eq 200 -and
            [string]$actualizacionProducto.Datos.id -eq $productoId -and
            [string]$actualizacionProducto.Datos.proveedorId -eq $proveedorIdNuevo) 'Cambio válido reasigna proveedor y conserva el Id del producto'
        Confirmar ([string]$actualizacionProducto.Datos.proveedorRazonSocial -eq $proveedorNuevo.razonSocial -and
            [string]$actualizacionProducto.Datos.proveedorIdentificacion -eq $proveedorNuevo.identificacion) 'Respuesta de actualización incluye datos descriptivos del nuevo proveedor'

        $proveedoresConsulta = @(
            (InvocarApi -Metodo 'Get' -Ruta "/api/productos/$productoId").Datos,
            (((InvocarApi -Metodo 'Get' -Ruta '/api/productos').Datos | Where-Object { [string]$_.id -eq $productoId } | Select-Object -First 1)),
            (((InvocarApi -Metodo 'Get' -Ruta '/api/productos/buscar?criterio=Codigo&consulta=F3RELACION').Datos | Select-Object -First 1))
        )
        $indiceRespuesta = 0
        foreach ($respuestaProducto in $proveedoresConsulta) {
            Confirmar ($null -ne $respuestaProducto -and
                [string]$respuestaProducto.id -eq $productoId -and
                [string]$respuestaProducto.proveedorId -eq $proveedorIdNuevo -and
                [string]$respuestaProducto.proveedorRazonSocial -eq $proveedorNuevo.razonSocial -and
                [string]$respuestaProducto.proveedorIdentificacion -eq $proveedorNuevo.identificacion) "Detalle, listado y búsqueda reflejan el nuevo proveedor (consulta $indiceRespuesta)"
            $indiceRespuesta++
        }

        $proveedorEditado = CopiarObjeto $proveedorNuevo
        $proveedorEditado.razonSocial = 'ProveedorF3Editado'
        $proveedorEditado.identificacion = '3-303-456788'
        $actualizacionProveedor = InvocarApi -Metodo 'Put' -Ruta "/api/proveedores/$proveedorIdNuevo" -Cuerpo $proveedorEditado
        Confirmar ($actualizacionProveedor.Codigo -eq 200 -and
            [string]$actualizacionProveedor.Datos.id -eq $proveedorIdNuevo) 'Edición del proveedor conserva su Id'

        $proveedoresConsulta = @(
            (InvocarApi -Metodo 'Get' -Ruta "/api/productos/$productoId").Datos,
            (((InvocarApi -Metodo 'Get' -Ruta '/api/productos').Datos | Where-Object { [string]$_.id -eq $productoId } | Select-Object -First 1)),
            (((InvocarApi -Metodo 'Get' -Ruta '/api/productos/buscar?criterio=Codigo&consulta=F3RELACION').Datos | Select-Object -First 1))
        )
        $indiceRespuesta = 0
        foreach ($respuestaProducto in $proveedoresConsulta) {
            Confirmar ($null -ne $respuestaProducto -and
                [string]$respuestaProducto.id -eq $productoId -and
                [string]$respuestaProducto.proveedorId -eq $proveedorIdNuevo -and
                [string]$respuestaProducto.proveedorRazonSocial -eq $proveedorEditado.razonSocial -and
                [string]$respuestaProducto.proveedorIdentificacion -eq $proveedorEditado.identificacion) "Cambio de razón social e identificación se refleja en todas las consultas (consulta $indiceRespuesta)"
            $indiceRespuesta++
        }
        $proveedorObtenido = (InvocarApi -Metodo 'Get' -Ruta "/api/proveedores/$proveedorIdNuevo").Datos
        Confirmar ([string]$proveedorObtenido.razonSocial -eq $proveedorEditado.razonSocial -and
            [string]$proveedorObtenido.identificacion -eq $proveedorEditado.identificacion) 'GET de proveedor refleja la edición de razón social e identificación'

        Write-Host "Grupo $Grupo completado: $cantidadVerificaciones verificaciones."
    }
    elseif ($Grupo -eq 'Integridad') {
        $proveedoresIntegridad = @(
            @{ tipoIdentificacion = 4; identificacion = '3-404-000001'; razonSocial = 'ProveedorF3A'; nombreContacto = 'Ana Mora'; telefono = '88887771'; correoElectronico = 'f3a@example.com'; tipoSuministro = 8; frecuenciaEntrega = 3 },
            @{ tipoIdentificacion = 4; identificacion = '3-404-000002'; razonSocial = 'ProveedorF3B'; nombreContacto = 'Luis Mora'; telefono = '88887772'; correoElectronico = 'f3b@example.com'; tipoSuministro = 8; frecuenciaEntrega = 3 },
            @{ tipoIdentificacion = 4; identificacion = '3-404-000003'; razonSocial = 'ProveedorF3C'; nombreContacto = 'Eva Mora'; telefono = '88887773'; correoElectronico = 'f3c@example.com'; tipoSuministro = 8; frecuenciaEntrega = 3 }
        )
        $proveedoresCreados = @()
        foreach ($formularioProveedor in $proveedoresIntegridad) {
            $altaProveedor = InvocarApi -Metodo 'Post' -Ruta '/api/proveedores' -Cuerpo $formularioProveedor
            Confirmar ($altaProveedor.Codigo -eq 201) "Alta de $($formularioProveedor.razonSocial)"
            $proveedoresCreados += $altaProveedor.Datos
        }
        $proveedorA = $proveedoresCreados[0]
        $proveedorB = $proveedoresCreados[1]
        $proveedorLibre = $proveedoresCreados[2]

        $formulariosProducto = @(
            @{ proveedorId = [string]$proveedorA.id; codigo = 'F3INTEGRIDAD1'; nombre = 'ProductoIntegridadUno'; categoria = 1; unidadMedida = 1; precioUnidadMedida = 1800; existenciaActual = 5; existenciaMinima = 1 },
            @{ proveedorId = [string]$proveedorA.id; codigo = 'F3INTEGRIDAD2'; nombre = 'ProductoIntegridadDos'; categoria = 1; unidadMedida = 1; precioUnidadMedida = 1900; existenciaActual = 6; existenciaMinima = 1 }
        )
        $productosIntegridad = @()
        foreach ($formularioProducto in $formulariosProducto) {
            $altaProducto = InvocarApi -Metodo 'Post' -Ruta '/api/productos' -Cuerpo $formularioProducto
            Confirmar ($altaProducto.Codigo -eq 201 -and
                [string]$altaProducto.Datos.proveedorId -eq [string]$proveedorA.id) 'Producto de prueba se asocia con el proveedor compartido'
            $productosIntegridad += $altaProducto.Datos
        }
        $productoUno = $productosIntegridad[0]
        $productoDos = $productosIntegridad[1]
        $cantidadProveedores = @((InvocarApi -Metodo 'Get' -Ruta '/api/proveedores').Datos).Count
        $cantidadProductos = @((InvocarApi -Metodo 'Get' -Ruta '/api/productos').Datos).Count

        $eliminacionBloqueada = InvocarApi -Metodo 'Delete' -Ruta "/api/proveedores/$($proveedorA.id)"
        ConfirmarErrorApi $eliminacionBloqueada 409 'Borrado de proveedor con productos relacionados' 'proveedorId'
        Confirmar ($eliminacionBloqueada.Datos.codigo -eq 'ConflictoIntegridad') 'El conflicto de integridad usa su código ErrorApi'
        $proveedorConsultado = (InvocarApi -Metodo 'Get' -Ruta "/api/proveedores/$($proveedorA.id)").Datos
        Confirmar ([string]$proveedorConsultado.id -eq [string]$proveedorA.id -and
            [string]$proveedorConsultado.identificacion -eq [string]$proveedorA.identificacion) 'Borrado bloqueado conserva el proveedor'
        foreach ($productoEsperado in @($productoUno, $productoDos)) {
            $productoConsultado = (InvocarApi -Metodo 'Get' -Ruta "/api/productos/$($productoEsperado.id)").Datos
            Confirmar ([string]$productoConsultado.id -eq [string]$productoEsperado.id -and
                [string]$productoConsultado.proveedorId -eq [string]$proveedorA.id) 'Borrado bloqueado conserva cada producto relacionado'
        }
        Confirmar (@((InvocarApi -Metodo 'Get' -Ruta '/api/proveedores').Datos).Count -eq $cantidadProveedores -and
            @((InvocarApi -Metodo 'Get' -Ruta '/api/productos').Datos).Count -eq $cantidadProductos) 'Borrado bloqueado no cambia las cantidades'

        $formularioUnoReasignado = CopiarObjeto $formulariosProducto[0]
        $formularioUnoReasignado.proveedorId = [string]$proveedorB.id
        $cambioUno = InvocarApi -Metodo 'Put' -Ruta "/api/productos/$($productoUno.id)" -Cuerpo $formularioUnoReasignado
        Confirmar ($cambioUno.Codigo -eq 200 -and
            [string]$cambioUno.Datos.proveedorId -eq [string]$proveedorB.id) 'Se reasigna el primer producto al segundo proveedor'

        $eliminacionAunBloqueada = InvocarApi -Metodo 'Delete' -Ruta "/api/proveedores/$($proveedorA.id)"
        ConfirmarErrorApi $eliminacionAunBloqueada 409 'Borrado tras reasignar solo un producto' 'proveedorId'
        Confirmar ($eliminacionAunBloqueada.Datos.codigo -eq 'ConflictoIntegridad') 'Un producto restante mantiene el conflicto de integridad'
        $productoUnoActual = (InvocarApi -Metodo 'Get' -Ruta "/api/productos/$($productoUno.id)").Datos
        $productoDosActual = (InvocarApi -Metodo 'Get' -Ruta "/api/productos/$($productoDos.id)").Datos
        Confirmar ([string]$productoUnoActual.proveedorId -eq [string]$proveedorB.id -and
            [string]$productoDosActual.proveedorId -eq [string]$proveedorA.id) 'El rechazo conserva ambas relaciones después de la reasignación parcial'

        $formularioDosReasignado = CopiarObjeto $formulariosProducto[1]
        $formularioDosReasignado.proveedorId = [string]$proveedorB.id
        $cambioDos = InvocarApi -Metodo 'Put' -Ruta "/api/productos/$($productoDos.id)" -Cuerpo $formularioDosReasignado
        Confirmar ($cambioDos.Codigo -eq 200 -and
            [string]$cambioDos.Datos.proveedorId -eq [string]$proveedorB.id) 'Se reasigna el segundo producto al segundo proveedor'

        $eliminacionPermitida = InvocarApi -Metodo 'Delete' -Ruta "/api/proveedores/$($proveedorA.id)"
        Confirmar ($eliminacionPermitida.Codigo -eq 204) 'Borrado procede cuando ya no hay productos relacionados'
        $proveedorEliminado = InvocarApi -Metodo 'Get' -Ruta "/api/proveedores/$($proveedorA.id)"
        ConfirmarErrorApi $proveedorEliminado 404 'Consulta del proveedor eliminado'
        foreach ($productoEsperado in @($productoUno, $productoDos)) {
            $productoConsultado = (InvocarApi -Metodo 'Get' -Ruta "/api/productos/$($productoEsperado.id)").Datos
            Confirmar ([string]$productoConsultado.id -eq [string]$productoEsperado.id -and
                [string]$productoConsultado.proveedorId -eq [string]$proveedorB.id) 'Los productos se conservan tras borrar el proveedor anterior'
        }

        $borradoLibre = InvocarApi -Metodo 'Delete' -Ruta "/api/proveedores/$($proveedorLibre.id)"
        Confirmar ($borradoLibre.Codigo -eq 204) 'Se puede borrar el proveedor libre'
        ConfirmarErrorApi (InvocarApi -Metodo 'Get' -Ruta "/api/proveedores/$($proveedorLibre.id)") 404 'Consulta del proveedor libre eliminado'
        ConfirmarErrorApi (InvocarApi -Metodo 'Delete' -Ruta "/api/proveedores/$($proveedorA.id)") 404 'Segundo borrado del proveedor ya eliminado'
        ConfirmarErrorApi (InvocarApi -Metodo 'Delete' -Ruta "/api/proveedores/$([guid]::NewGuid())") 404 'Borrado de proveedor inexistente'

        Write-Host "Grupo $Grupo completado: $cantidadVerificaciones verificaciones."
    }
    elseif ($Grupo -eq 'Concurrencia') {
        Add-Type -AssemblyName System.Net.Http
        $clienteHttpConcurrente = [System.Net.Http.HttpClient]::new()
        $clienteHttpConcurrente.Timeout = [TimeSpan]::FromSeconds(15)

        for ($indice = 1; $indice -le 20; $indice++) {
            $identificador = '{0:D6}' -f $indice
            $formularioProveedor = @{
                tipoIdentificacion = 4; identificacion = "3-505-$identificador"; razonSocial = "ProveedorAlta$identificador"
                nombreContacto = 'Ana Mora'; telefono = '88887761'; correoElectronico = "alta$identificador@example.com"
                tipoSuministro = 8; frecuenciaEntrega = 3
            }
            $altaProveedor = InvocarApi -Metodo 'Post' -Ruta '/api/proveedores' -Cuerpo $formularioProveedor
            Confirmar ($altaProveedor.Codigo -eq 201) "Carrera alta/borrado prepara proveedor $identificador"
            $formularioProducto = @{
                proveedorId = [string]$altaProveedor.Datos.id; codigo = "F3ALTA$identificador"; nombre = "ProductoAlta$identificador"
                categoria = 1; unidadMedida = 1; precioUnidadMedida = 1500
                existenciaActual = 5; existenciaMinima = 1
            }
            $cantidadAntes = @((InvocarApi -Metodo 'Get' -Ruta '/api/productos').Datos).Count
            $solicitudes = @(
                @{ Metodo = 'Post'; Ruta = '/api/productos'; Cuerpo = $formularioProducto },
                @{ Metodo = 'Delete'; Ruta = "/api/proveedores/$($altaProveedor.Datos.id)"; Cuerpo = $null }
            )
            $respuestas = EnviarLoteConcurrente $clienteHttpConcurrente $solicitudes
            $respuestaAlta = $respuestas[0]
            $respuestaBorrado = $respuestas[1]
            if ($respuestaAlta.Codigo -eq 201) {
                Confirmar ($respuestaBorrado.Codigo -eq 409 -and
                    $respuestaBorrado.Datos.codigo -eq 'ConflictoIntegridad') "Carrera $identificador conserva proveedor si alta gana"
                Confirmar ([string]$respuestaAlta.Datos.proveedorId -eq [string]$altaProveedor.Datos.id -and
                    [string]$respuestaAlta.Datos.proveedorRazonSocial -eq [string]$altaProveedor.Datos.razonSocial -and
                    [string]$respuestaAlta.Datos.proveedorIdentificacion -eq [string]$altaProveedor.Datos.identificacion) "Carrera $identificador devuelve DTO completo para alta aceptada"
                Confirmar (@((InvocarApi -Metodo 'Get' -Ruta '/api/productos').Datos).Count -eq $cantidadAntes + 1) "Carrera $identificador persiste solo el producto aceptado"
            }
            elseif ($respuestaAlta.Codigo -eq 400) {
                ConfirmarErrorApi $respuestaAlta 400 "Carrera $identificador rechaza alta si proveedor desaparece" 'proveedorId'
                Confirmar ($respuestaBorrado.Codigo -eq 204) "Carrera $identificador permite borrar proveedor si alta pierde"
                Confirmar (@((InvocarApi -Metodo 'Get' -Ruta '/api/productos').Datos).Count -eq $cantidadAntes) "Carrera $identificador no agrega producto tras rechazo"
                ConfirmarErrorApi (InvocarApi -Metodo 'Get' -Ruta "/api/proveedores/$($altaProveedor.Datos.id)") 404 "Carrera $identificador deja proveedor borrado"
            }
            else {
                throw "Carrera de alta/borrado $identificador devolvió HTTP $($respuestaAlta.Codigo) inesperado."
            }
        }

        for ($indice = 1; $indice -le 20; $indice++) {
            $identificador = '{0:D6}' -f (100 + $indice)
            $formularioProveedorA = @{
                tipoIdentificacion = 4; identificacion = "3-506-$identificador"; razonSocial = "Origen$identificador"
                nombreContacto = 'Marta Mora'; telefono = '88887762'; correoElectronico = "origen$identificador@example.com"
                tipoSuministro = 8; frecuenciaEntrega = 3
            }
            $formularioProveedorB = @{
                tipoIdentificacion = 4; identificacion = "3-507-$identificador"; razonSocial = "Destino$identificador"
                nombreContacto = 'Luis Mora'; telefono = '88887763'; correoElectronico = "destino$identificador@example.com"
                tipoSuministro = 3; frecuenciaEntrega = 3
            }
            $altaProveedorA = InvocarApi -Metodo 'Post' -Ruta '/api/proveedores' -Cuerpo $formularioProveedorA
            $altaProveedorB = InvocarApi -Metodo 'Post' -Ruta '/api/proveedores' -Cuerpo $formularioProveedorB
            Confirmar ($altaProveedorA.Codigo -eq 201 -and $altaProveedorB.Codigo -eq 201) "Carrera reasignación $identificador prepara proveedores"
            $proveedorA = $altaProveedorA.Datos
            $proveedorB = $altaProveedorB.Datos
            $formularioProducto = @{
                proveedorId = [string]$proveedorA.id; codigo = "F3MOV$identificador"; nombre = "ProductoMov$identificador"
                categoria = 1; unidadMedida = 1; precioUnidadMedida = 1600
                existenciaActual = 5; existenciaMinima = 1
            }
            $altaProducto = InvocarApi -Metodo 'Post' -Ruta '/api/productos' -Cuerpo $formularioProducto
            Confirmar ($altaProducto.Codigo -eq 201) "Carrera reasignación $identificador prepara producto"
            $idProducto = [string]$altaProducto.Datos.id
            $productoActualizado = CopiarObjeto $formularioProducto
            $productoActualizado.proveedorId = [string]$proveedorB.id
            $consultaCodigo = [uri]::EscapeDataString($formularioProducto.codigo)
            $consultaProveedor = [uri]::EscapeDataString($proveedorB.identificacion)
            $solicitudes = @(
                @{ Metodo = 'Put'; Ruta = "/api/productos/$idProducto"; Cuerpo = $productoActualizado },
                @{ Metodo = 'Delete'; Ruta = "/api/proveedores/$($proveedorA.id)"; Cuerpo = $null },
                @{ Metodo = 'Get'; Ruta = "/api/productos/$idProducto"; Cuerpo = $null },
                @{ Metodo = 'Get'; Ruta = '/api/productos'; Cuerpo = $null },
                @{ Metodo = 'Get'; Ruta = "/api/productos/buscar?criterio=Codigo&consulta=$consultaCodigo"; Cuerpo = $null },
                @{ Metodo = 'Get'; Ruta = "/api/proveedores/$($proveedorA.id)"; Cuerpo = $null },
                @{ Metodo = 'Get'; Ruta = "/api/proveedores/buscar?criterio=Identificacion&consulta=$consultaProveedor"; Cuerpo = $null }
            )
            $respuestas = EnviarLoteConcurrente $clienteHttpConcurrente $solicitudes
            Confirmar ($respuestas[0].Codigo -eq 200 -and
                [string]$respuestas[0].Datos.id -eq $idProducto -and
                [string]$respuestas[0].Datos.proveedorId -eq [string]$proveedorB.id) "Carrera reasignación $identificador guarda el proveedor de destino"
            Confirmar ($respuestas[1].Codigo -in @(204, 409)) "Carrera reasignación $identificador devuelve estado permitido al borrar origen"
            ConfirmarProductoRelacionado $respuestas[0].Datos $idProducto $proveedorA $proveedorB "Carrera reasignación $identificador devuelve DTO completo"
            Confirmar ($respuestas[2].Codigo -eq 200) "Carrera consulta $identificador devuelve detalle del producto"
            ConfirmarProductoRelacionado $respuestas[2].Datos $idProducto $proveedorA $proveedorB "Carrera consulta $identificador conserva snapshot de detalle coherente"
            Confirmar ($respuestas[3].Codigo -eq 200 -and @($respuestas[3].Datos).Count -gt 0) "Carrera consulta $identificador devuelve listado de productos"
            $productoListado = $respuestas[3].Datos | Where-Object { [string]$_.id -eq $idProducto } | Select-Object -First 1
            ConfirmarProductoRelacionado $productoListado $idProducto $proveedorA $proveedorB "Carrera consulta $identificador conserva snapshot de listado coherente"
            Confirmar ($respuestas[4].Codigo -eq 200 -and @($respuestas[4].Datos).Count -eq 1) "Carrera consulta $identificador devuelve búsqueda del producto"
            ConfirmarProductoRelacionado $respuestas[4].Datos[0] $idProducto $proveedorA $proveedorB "Carrera consulta $identificador conserva snapshot de búsqueda coherente"
            Confirmar ($respuestas[5].Codigo -in @(200, 404)) "Carrera consulta $identificador devuelve estado válido del proveedor origen"
            if ($respuestas[5].Codigo -eq 200) {
                Confirmar ([string]$respuestas[5].Datos.id -eq [string]$proveedorA.id -and
                    [string]$respuestas[5].Datos.razonSocial -eq [string]$proveedorA.razonSocial -and
                    [string]$respuestas[5].Datos.identificacion -eq [string]$proveedorA.identificacion) "Carrera consulta $identificador conserva snapshot del proveedor origen"
            }
            Confirmar ($respuestas[6].Codigo -eq 200 -and @($respuestas[6].Datos).Count -eq 1 -and
                [string]$respuestas[6].Datos[0].id -eq [string]$proveedorB.id) "Carrera consulta $identificador mantiene visible el proveedor destino"

            $productoFinal = (InvocarApi -Metodo 'Get' -Ruta "/api/productos/$idProducto").Datos
            Confirmar ([string]$productoFinal.id -eq $idProducto -and
                [string]$productoFinal.proveedorId -eq [string]$proveedorB.id -and
                [string]$productoFinal.proveedorRazonSocial -eq [string]$proveedorB.razonSocial -and
                [string]$productoFinal.proveedorIdentificacion -eq [string]$proveedorB.identificacion) "Carrera reasignación $identificador deja relación final consistente"
            if ($respuestas[1].Codigo -eq 204) {
                ConfirmarErrorApi (InvocarApi -Metodo 'Get' -Ruta "/api/proveedores/$($proveedorA.id)") 404 "Carrera reasignación $identificador eliminó origen sin referencias"
            }
            else {
                Confirmar ((InvocarApi -Metodo 'Get' -Ruta "/api/proveedores/$($proveedorA.id)").Codigo -eq 200) "Carrera reasignación $identificador conserva origen si fue bloqueado"
            }
        }

        $proveedorBase = InvocarApi -Metodo 'Post' -Ruta '/api/proveedores' -Cuerpo @{
            tipoIdentificacion = 4; identificacion = '3-508-765432'; razonSocial = 'ProveedorCRUDBase'
            nombreContacto = 'Base Prueba'; telefono = '88887764'; correoElectronico = 'crudbase@example.com'
            tipoSuministro = 8; frecuenciaEntrega = 3
        }
        Confirmar ($proveedorBase.Codigo -eq 201) 'Se crea proveedor base para carreras CRUD'
        for ($indice = 1; $indice -le 5; $indice++) {
            $identificador = '{0:D4}' -f $indice
            $casosCrud = @(
                @{ Recurso = 'clientes'; Formulario = @{ tipoIdentificacion = 1; identificacion = "1-6000-$identificador"; nombre = 'Ana'; primerApellido = 'Mora'; segundoApellido = 'Prueba'; fechaNacimiento = '1992-02-02' } },
                @{ Recurso = 'empleados'; Formulario = @{ tipoIdentificacion = 1; identificacion = "2-6000-$identificador"; nombre = 'Luis'; primerApellido = 'Mora'; segundoApellido = 'Prueba'; fechaNacimiento = '1988-03-03'; salarioMensual = 600000; fechaIngreso = '2020-01-01'; areaTrabajo = 2; puesto = 'Cajero'; turno = 1 } },
                @{ Recurso = 'proveedores'; Formulario = @{ tipoIdentificacion = 4; identificacion = "3-509-00$identificador"; razonSocial = "ProveedorCRUD$identificador"; nombreContacto = 'Marta Mora'; telefono = '88887765'; correoElectronico = "crud$identificador@example.com"; tipoSuministro = 8; frecuenciaEntrega = 3 } },
                @{ Recurso = 'productos'; Formulario = @{ proveedorId = [string]$proveedorBase.Datos.id; codigo = "F3CRUD$identificador"; nombre = "ProductoCRUD$identificador"; categoria = 1; unidadMedida = 1; precioUnidadMedida = 1300; existenciaActual = 5; existenciaMinima = 1 } }
            )
            foreach ($casoCrud in $casosCrud) {
                $alta = InvocarApi -Metodo 'Post' -Ruta "/api/$($casoCrud.Recurso)" -Cuerpo $casoCrud.Formulario
                Confirmar ($alta.Codigo -eq 201) "Carrera PUT/DELETE prepara $($casoCrud.Recurso) $identificador"
                $idRegistro = [string]$alta.Datos.id
                $formularioActualizado = CopiarObjeto $casoCrud.Formulario
                switch ($casoCrud.Recurso) {
                    'clientes' { $formularioActualizado.nombre = 'Elena' }
                    'empleados' { $formularioActualizado.salarioMensual = 650000 }
                    'proveedores' { $formularioActualizado.razonSocial = "ProveedorEditado$identificador" }
                    'productos' { $formularioActualizado.nombre = "ProductoEditado$identificador" }
                }
                $solicitudes = @(
                    @{ Metodo = 'Put'; Ruta = "/api/$($casoCrud.Recurso)/$idRegistro"; Cuerpo = $formularioActualizado },
                    @{ Metodo = 'Delete'; Ruta = "/api/$($casoCrud.Recurso)/$idRegistro"; Cuerpo = $null }
                )
                $respuestas = EnviarLoteConcurrente $clienteHttpConcurrente $solicitudes
                Confirmar ($respuestas[0].Codigo -in @(200, 404) -and
                    $respuestas[1].Codigo -eq 204) "Carrera PUT/DELETE $($casoCrud.Recurso) produce respuestas serializadas"
                if ($respuestas[0].Codigo -eq 200) {
                    Confirmar ([string]$respuestas[0].Datos.id -eq $idRegistro) "PUT concurrente conserva Id de $($casoCrud.Recurso)"
                }
                Confirmar ((InvocarApi -Metodo 'Get' -Ruta "/api/$($casoCrud.Recurso)/$idRegistro").Codigo -eq 404) "DELETE concurrente deja $($casoCrud.Recurso) inexistente"
            }
        }

        $clienteHttpConcurrente.Dispose()
        $clienteHttpConcurrente = $null
        Write-Host "Grupo $Grupo completado: $cantidadVerificaciones verificaciones."
    }
    else {

    $cliente = @{
        tipoIdentificacion = 1; identificacion = '9-9999-9998'; nombre = 'Elena'
        primerApellido = 'Prueba'; segundoApellido = 'Validacion'; fechaNacimiento = '1991-04-12'
        id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
    }
    $empleado = @{
        tipoIdentificacion = 1; identificacion = '8-8888-8888'; nombre = 'Luis'
        primerApellido = 'Prueba'; segundoApellido = 'Validacion'; fechaNacimiento = '1989-02-11'
        salarioMensual = 750000; fechaIngreso = '2021-03-01'; areaTrabajo = 2
        puesto = 'Cajero'; turno = 1; id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
    }
    $proveedor = @{
        tipoIdentificacion = 4; identificacion = '3-202-123456'; razonSocial = 'ProveedorPrueba'
        nombreContacto = 'Mario Prueba'; telefono = '88887777'; correoElectronico = 'prueba@example.com'
        tipoSuministro = 8; frecuenciaEntrega = 3; id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
    }
    $proveedoresSemilla = (InvocarApi -Metodo 'Get' -Ruta '/api/proveedores').Datos
    $proveedorSemilla = $proveedoresSemilla | Select-Object -First 1
    $producto = @{
        proveedorId = [string]$proveedorSemilla.id; codigo = 'F3PRODUCTO1'; nombre = 'ProductoPrueba'
        categoria = 3; unidadMedida = 1; precioUnidadMedida = 1250
        existenciaActual = 5; existenciaMinima = 1; id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
    }

    $clienteCreado = CrearRegistro '/api/clientes' $cliente 'Cliente'
    $empleadoCreado = CrearRegistro '/api/empleados' $empleado 'Empleado'
    $proveedorCreado = CrearRegistro '/api/proveedores' $proveedor 'Proveedor'
    $producto.proveedorId = [string]$proveedorCreado.id
    $productoCreado = CrearRegistro '/api/productos' $producto 'Producto'

    $clienteLimite = CopiarObjeto $cliente
    $clienteLimite.identificacion = '9-9999-9997'
    $clienteLimite.nombre = 'N' * 50
    $clienteLimite.primerApellido = 'P' * 75
    $clienteLimite.segundoApellido = 'S' * 75
    CrearRegistro '/api/clientes' $clienteLimite 'Cliente con longitudes máximas'

    $empleadoLimite = CopiarObjeto $empleado
    $empleadoLimite.identificacion = '8-8888-8887'
    $empleadoLimite.nombre = 'N' * 50
    $empleadoLimite.primerApellido = 'P' * 75
    $empleadoLimite.segundoApellido = 'S' * 75
    $empleadoLimite.salarioMensual = 0
    CrearRegistro '/api/empleados' $empleadoLimite 'Empleado con longitudes máximas y salario mínimo'

    $empleadoMaximo = CopiarObjeto $empleado
    $empleadoMaximo.identificacion = '8-8888-8886'
    $empleadoMaximo.salarioMensual = 5000000
    CrearRegistro '/api/empleados' $empleadoMaximo 'Empleado con salario máximo'

    $proveedorLimite = CopiarObjeto $proveedor
    $proveedorLimite.identificacion = '3-202-123457'
    $proveedorLimite.razonSocial = 'R' * 100
    $proveedorLimite.nombreContacto = 'C' * 100
    CrearRegistro '/api/proveedores' $proveedorLimite 'Proveedor con longitudes máximas'

    $productoLimite = CopiarObjeto $producto
    $productoLimite.codigo = 'C' * 20
    $productoLimite.nombre = 'N' * 80
    $productoLimite.existenciaActual = 0
    $productoLimite.existenciaMinima = 0
    CrearRegistro '/api/productos' $productoLimite 'Producto con longitudes máximas y existencia mínima'

    ActualizarRegistro '/api/clientes' $cliente $clienteCreado 'Cliente'
    ActualizarRegistro '/api/empleados' $empleado $empleadoCreado 'Empleado'
    ActualizarRegistro '/api/proveedores' $proveedor $proveedorCreado 'Proveedor'
    ActualizarRegistro '/api/productos' $producto $productoCreado 'Producto'

    $clientes = (InvocarApi -Metodo 'Get' -Ruta '/api/clientes').Datos
    $empleados = (InvocarApi -Metodo 'Get' -Ruta '/api/empleados').Datos
    $proveedores = (InvocarApi -Metodo 'Get' -Ruta '/api/proveedores').Datos
    $productos = (InvocarApi -Metodo 'Get' -Ruta '/api/productos').Datos

    $clienteDuplicado = CopiarObjeto $cliente
    $clienteDuplicado.identificacion = [string]$clientes[0].identificacion
    ConfirmarErrorApi (InvocarApi 'Post' '/api/clientes' $clienteDuplicado) 409 'Alta duplicada de cliente'

    $empleadoDuplicado = CopiarObjeto $empleado
    $empleadoDuplicado.identificacion = [string]$empleados[0].identificacion
    ConfirmarErrorApi (InvocarApi 'Post' '/api/empleados' $empleadoDuplicado) 409 'Alta duplicada de empleado'

    $proveedorDuplicado = CopiarObjeto $proveedor
    $proveedorDuplicado.identificacion = [string]$proveedores[0].identificacion
    ConfirmarErrorApi (InvocarApi 'Post' '/api/proveedores' $proveedorDuplicado) 409 'Alta duplicada de proveedor'

    $productoDuplicado = CopiarObjeto $producto
    $productoDuplicado.codigo = [string]$productos[0].codigo
    ConfirmarErrorApi (InvocarApi 'Post' '/api/productos' $productoDuplicado) 409 'Alta duplicada de producto'

    $clienteConflicto = CopiarObjeto $cliente
    $clienteConflicto.identificacion = [string]$clientes[0].identificacion
    ConfirmarErrorApi (InvocarApi 'Put' "/api/clientes/$($clienteCreado.id)" $clienteConflicto) 409 'Edición duplicada de cliente'

    $empleadoConflicto = CopiarObjeto $empleado
    $empleadoConflicto.identificacion = [string]$empleados[0].identificacion
    ConfirmarErrorApi (InvocarApi 'Put' "/api/empleados/$($empleadoCreado.id)" $empleadoConflicto) 409 'Edición duplicada de empleado'

    $proveedorConflicto = CopiarObjeto $proveedor
    $proveedorConflicto.identificacion = [string]$proveedores[0].identificacion
    ConfirmarErrorApi (InvocarApi 'Put' "/api/proveedores/$($proveedorCreado.id)" $proveedorConflicto) 409 'Edición duplicada de proveedor'

    $productoConflicto = CopiarObjeto $producto
    $productoConflicto.codigo = [string]$productos[0].codigo
    ConfirmarErrorApi (InvocarApi 'Put' "/api/productos/$($productoCreado.id)" $productoConflicto) 409 'Edición duplicada de producto'

    Confirmar ([string](InvocarApi 'Get' "/api/clientes/$($clienteCreado.id)").Datos.identificacion -eq $cliente.identificacion) 'Edición duplicada conserva el cliente'
    Confirmar ([string](InvocarApi 'Get' "/api/empleados/$($empleadoCreado.id)").Datos.identificacion -eq $empleado.identificacion) 'Edición duplicada conserva el empleado'
    Confirmar ([string](InvocarApi 'Get' "/api/proveedores/$($proveedorCreado.id)").Datos.identificacion -eq $proveedor.identificacion) 'Edición duplicada conserva el proveedor'
    Confirmar ([string](InvocarApi 'Get' "/api/productos/$($productoCreado.id)").Datos.codigo -eq $producto.codigo) 'Edición duplicada conserva el producto'

    $casosInvalidos = @(
        @{ Ruta = '/api/clientes'; Base = $cliente; Campo = 'nombre'; Valor = 'AB'; Descripcion = 'nombre corto' },
        @{ Ruta = '/api/clientes'; Base = $cliente; Campo = 'nombre'; Valor = ''; Descripcion = 'nombre vacío' },
        @{ Ruta = '/api/clientes'; Base = $cliente; Campo = 'tipoIdentificacion'; Valor = 99; Descripcion = 'tipo de identificación fuera de catálogo' },
        @{ Ruta = '/api/clientes'; Base = $cliente; Campo = 'fechaNacimiento'; Valor = 'fecha-invalida'; Descripcion = 'fecha con formato inválido' },
        @{ Ruta = '/api/empleados'; Base = $empleado; Campo = 'salarioMensual'; Valor = 5000001; Descripcion = 'salario fuera de rango' },
        @{ Ruta = '/api/empleados'; Base = $empleado; Campo = 'areaTrabajo'; Valor = 99; Descripcion = 'área fuera de catálogo' },
        @{ Ruta = '/api/empleados'; Base = $empleado; Campo = 'puesto'; Valor = 'Cortador'; Descripcion = 'puesto ajeno al área' },
        @{ Ruta = '/api/empleados'; Base = $empleado; Campo = 'turno'; Valor = 99; Descripcion = 'turno fuera de catálogo' },
        @{ Ruta = '/api/empleados'; Base = $empleado; Campo = 'fechaIngreso'; Valor = 'fecha-invalida'; Descripcion = 'fecha de ingreso inválida' },
        @{ Ruta = '/api/proveedores'; Base = $proveedor; Campo = 'razonSocial'; Valor = 'A' * 101; Descripcion = 'razón social demasiado larga' },
        @{ Ruta = '/api/proveedores'; Base = $proveedor; Campo = 'tipoSuministro'; Valor = 99; Descripcion = 'suministro fuera de catálogo' },
        @{ Ruta = '/api/proveedores'; Base = $proveedor; Campo = 'correoElectronico'; Valor = 'correo-invalido'; Descripcion = 'correo inválido' },
        @{ Ruta = '/api/proveedores'; Base = $proveedor; Campo = 'telefono'; Valor = '123'; Descripcion = 'teléfono corto' },
        @{ Ruta = '/api/productos'; Base = $producto; Campo = 'codigo'; Valor = 'C' * 21; Descripcion = 'código demasiado largo' },
        @{ Ruta = '/api/productos'; Base = $producto; Campo = 'codigo'; Valor = ''; Descripcion = 'código vacío' },
        @{ Ruta = '/api/productos'; Base = $producto; Campo = 'nombre'; Valor = 'N' * 81; Descripcion = 'nombre demasiado largo' },
        @{ Ruta = '/api/productos'; Base = $producto; Campo = 'categoria'; Valor = 99; Descripcion = 'categoría fuera de catálogo' },
        @{ Ruta = '/api/productos'; Base = $producto; Campo = 'precioUnidadMedida'; Valor = 0; Descripcion = 'precio no positivo' },
        @{ Ruta = '/api/productos'; Base = $producto; Campo = 'existenciaActual'; Valor = -1; Descripcion = 'existencia negativa' },
        @{ Ruta = '/api/productos'; Base = $producto; Campo = 'proveedorId'; Valor = [guid]::Empty.ToString(); Descripcion = 'proveedor vacío' }
    )
    foreach ($caso in $casosInvalidos) {
        $formularioInvalido = CopiarObjeto $caso.Base
        $formularioInvalido.($caso.Campo) = $caso.Valor
        ConfirmarErrorApi (InvocarApi 'Post' $caso.Ruta $formularioInvalido) 400 `
            "Validación de $($caso.Descripcion)" $caso.Campo
    }

    $cuerpoConDesbordamiento = '{"proveedorId":"' + $producto.proveedorId + '","codigo":"F3OVERFLOW","nombre":"Overflow","categoria":3,"unidadMedida":1,"precioUnidadMedida":1e999,"existenciaActual":1,"existenciaMinima":0}'
    ConfirmarErrorApi (InvocarApi 'Post' '/api/productos' $cuerpoConDesbordamiento -CuerpoCrudo) 400 'Número fuera del rango decimal' 'precioUnidadMedida'

    $clienteAntes = (InvocarApi 'Get' "/api/clientes/$($clienteCreado.id)").Datos
    $clienteActualizacionInvalida = CopiarObjeto $cliente
    $clienteActualizacionInvalida.nombre = ''
    ConfirmarErrorApi (InvocarApi 'Put' "/api/clientes/$($clienteCreado.id)" $clienteActualizacionInvalida) 400 'Edición de cliente inválida'
    $clienteDespues = (InvocarApi 'Get' "/api/clientes/$($clienteCreado.id)").Datos
    Confirmar ($clienteDespues.nombre -eq $clienteAntes.nombre) 'Rechazo de edición conserva el cliente'

    $empleadoAntes = (InvocarApi 'Get' "/api/empleados/$($empleadoCreado.id)").Datos
    $empleadoActualizacionInvalida = CopiarObjeto $empleado
    $empleadoActualizacionInvalida.turno = 99
    ConfirmarErrorApi (InvocarApi 'Put' "/api/empleados/$($empleadoCreado.id)" $empleadoActualizacionInvalida) 400 'Edición de empleado inválida'
    $empleadoDespues = (InvocarApi 'Get' "/api/empleados/$($empleadoCreado.id)").Datos
    Confirmar ($empleadoDespues.turno -eq $empleadoAntes.turno) 'Rechazo de edición conserva el empleado'

    $proveedorAntes = (InvocarApi 'Get' "/api/proveedores/$($proveedorCreado.id)").Datos
    $proveedorActualizacionInvalida = CopiarObjeto $proveedor
    $proveedorActualizacionInvalida.telefono = '123'
    ConfirmarErrorApi (InvocarApi 'Put' "/api/proveedores/$($proveedorCreado.id)" $proveedorActualizacionInvalida) 400 'Edición de proveedor inválida'
    $proveedorDespues = (InvocarApi 'Get' "/api/proveedores/$($proveedorCreado.id)").Datos
    Confirmar ($proveedorDespues.telefono -eq $proveedorAntes.telefono) 'Rechazo de edición conserva el proveedor'

    $productoAntes = (InvocarApi 'Get' "/api/productos/$($productoCreado.id)").Datos
    $productoActualizacionInvalida = CopiarObjeto $producto
    $productoActualizacionInvalida.existenciaActual = -1
    ConfirmarErrorApi (InvocarApi 'Put' "/api/productos/$($productoCreado.id)" $productoActualizacionInvalida) 400 'Edición de producto inválida'
    $productoDespues = (InvocarApi 'Get' "/api/productos/$($productoCreado.id)").Datos
    Confirmar ($productoDespues.existenciaActual -eq $productoAntes.existenciaActual) 'Rechazo de edición conserva el producto'

    $productoConProveedorDesconocido = CopiarObjeto $producto
    $productoConProveedorDesconocido.proveedorId = [guid]::NewGuid().ToString()
    ConfirmarErrorApi (InvocarApi 'Post' '/api/productos' $productoConProveedorDesconocido) 400 'Referencia a proveedor inexistente'

    $conteosFinales = @{
        clientes = @((InvocarApi 'Get' '/api/clientes').Datos).Count
        empleados = @((InvocarApi 'Get' '/api/empleados').Datos).Count
        proveedores = @((InvocarApi 'Get' '/api/proveedores').Datos).Count
        productos = @((InvocarApi 'Get' '/api/productos').Datos).Count
    }
    Confirmar ($conteosFinales.clientes -eq $clientes.Count -and
        $conteosFinales.empleados -eq $empleados.Count -and
        $conteosFinales.proveedores -eq $proveedores.Count -and
        $conteosFinales.productos -eq $productos.Count) 'Altas duplicadas y formularios inválidos no agregan registros'

    Write-Output "Grupo $Grupo completado: $cantidadVerificaciones verificaciones aprobadas."
    }
}
finally {
    if ($null -ne $clienteHttpConcurrente) {
        $clienteHttpConcurrente.Dispose()
    }
    if ($null -ne $procesoApi -and -not $procesoApi.HasExited) {
        Stop-Process -Id $procesoApi.Id -Force -ErrorAction SilentlyContinue
        $procesoApi.WaitForExit(5000)
    }
}
