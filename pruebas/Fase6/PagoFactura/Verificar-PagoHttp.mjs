import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import net from 'node:net';
import {spawn} from 'node:child_process';
import {parseArgs} from 'node:util';
import {setTimeout as esperar} from 'node:timers/promises';

// Aisla la API y conserva resultados de cada solicitud.
const {values: opciones}=parseArgs({options:{raiz:{type:'string',default:process.cwd()},salida:{type:'string'},puerto:{type:'string',default:'5234'}}});
const raiz=path.resolve(opciones.raiz);
const salida=opciones.salida?path.resolve(opciones.salida):await fs.mkdtemp(path.join(os.tmpdir(),'carniceria-facturas-http-'));
await fs.mkdir(salida,{recursive:true});
const base='http://127.0.0.1:'+opciones.puerto;
const resultados=[],solicitudes=[],creadas=[];
let proceso,fallo=null,errorCierre=null,registro='';

// Registra una condicion observable y detiene el grupo si falla.
function verificar(caso,condicion,detalle={}){resultados.push({caso,aprobado:!!condicion,...detalle});if(!condicion)throw new Error(caso);}

// Comprueba el puerto antes de iniciar la API y despues de cerrarla.
async function puertoLibre(){const servidor=net.createServer();await new Promise((resolver,rechazar)=>{servidor.once('error',rechazar);servidor.listen(Number(opciones.puerto),'127.0.0.1',resolver);});await new Promise(resolver=>servidor.close(resolver));}

// Envia HTTP real y conserva codigo, cabeceras y contrato JSON de cada respuesta.
async function solicitar(metodo,ruta,cuerpo,crudo=false){const respuesta=await fetch(new URL(ruta,base),{method:metodo,headers:cuerpo===undefined?{}:{'Content-Type':'application/json'},body:cuerpo===undefined?undefined:crudo?cuerpo:JSON.stringify(cuerpo),redirect:'manual',signal:AbortSignal.timeout(10000)});const texto=await respuesta.text();let json;try{json=JSON.parse(texto);}catch{}const observacion={metodo,ruta,codigo:respuesta.status,cabeceras:Object.fromEntries(respuesta.headers),cuerpo:json??texto};solicitudes.push(observacion);return observacion;}

// Obtiene una coleccion de API y comprueba su respuesta basica.
async function listar(recurso){const respuesta=await solicitar('GET','/api/'+recurso);if(respuesta.codigo!==200||!Array.isArray(respuesta.cuerpo))throw new Error('No se pudo listar '+recurso);return respuesta.cuerpo;}

// Espera la ruta de salud sin iniciar compilaciones.
async function esperarApi(){const fin=Date.now()+15000;while(Date.now()<fin){try{if((await fetch(base+'/api/estado',{signal:AbortSignal.timeout(1000)})).ok)return;}catch{}await esperar(100);}throw new Error('La API propia no inicio.');}

// Comprueba el formato uniforme y el campo exacto, incluidos prefijos del enlazador JSON.
function verificarError(caso,respuesta,codigo,campo){verificar(caso+' codigo HTTP',respuesta.codigo===codigo,{esperado:codigo,actual:respuesta.codigo});verificar(caso+' ErrorApi completo',typeof respuesta.cuerpo?.codigo==='string'&&typeof respuesta.cuerpo?.mensaje==='string'&&respuesta.cuerpo.mensaje.trim().length>0&&respuesta.cuerpo.erroresPorCampo&&typeof respuesta.cuerpo.erroresPorCampo==='object');if(campo){const claves=Object.keys(respuesta.cuerpo.erroresPorCampo).map(c=>c.toLowerCase());verificar(caso+' campo exacto '+campo,claves.includes(campo.toLowerCase())||claves.includes('$.'+campo.toLowerCase()),{claves});}}

// Comprueba Location y que el documento publico sea el mismo registro creado.
async function verificarAlta(respuesta,caso){verificar(caso+' devuelve201',respuesta.codigo===201,{actual:respuesta.codigo});const factura=respuesta.cuerpo;verificar(caso+' Id y numero asignados',/^[0-9a-f-]{36}$/i.test(factura?.id??'')&&factura.id!=='00000000-0000-0000-0000-000000000000'&&/^FAC\d{6,}$/.test(factura.numero));verificar(caso+' Pendiente',factura.estado===1);const ubicacion=respuesta.cabeceras.location;verificar(caso+' Location apunta al numero',!!ubicacion&&new URL(ubicacion,base).pathname==='/api/facturas/'+factura.numero);const consulta=await solicitar('GET',ubicacion);verificar(caso+' Location obtiene mismo DTO',consulta.codigo===200&&JSON.stringify(consulta.cuerpo)===JSON.stringify(factura));creadas.push(factura);return factura;}

// Crea un recurso auxiliar valido para comprobar bajas libres.
async function crearAuxiliar(recurso,formulario){const respuesta=await solicitar('POST','/api/'+recurso,formulario);verificar('Alta auxiliar '+recurso,respuesta.codigo===201);return respuesta.cuerpo;}

// Obtiene una factura y exige un DTO disponible para comparar cada operacion.
async function obtenerFactura(numero){const respuesta=await solicitar('GET','/api/facturas/'+numero);if(respuesta.codigo!==200)throw new Error('No se pudo consultar '+numero);return respuesta.cuerpo;}

// Crea productos separados para evitar dependencia entre escenarios.
async function crearProducto(codigo){return await crearAuxiliar('productos',{proveedorId:(await listar('proveedores'))[0].id,codigo,nombre:codigo,categoria:1,unidadMedida:1,precioUnidadMedida:1000,existenciaActual:10,existenciaMinima:1});}

// Ajusta solo los campos editables del producto mediante el contrato publico.
async function actualizarProducto(id,cambios){const actual=(await solicitar('GET','/api/productos/'+id)).cuerpo;const formulario=Object.fromEntries(['proveedorId','codigo','nombre','categoria','unidadMedida','precioUnidadMedida','existenciaActual','existenciaMinima'].map(campo=>[campo,Object.hasOwn(cambios,campo)?cambios[campo]:actual[campo]]));const respuesta=await solicitar('PUT','/api/productos/'+id,formulario);verificar('Ajuste de fixture producto '+actual.codigo,respuesta.codigo===200);return respuesta.cuerpo;}

// Registra una factura pendiente para la demanda indicada.
async function crearFactura(clienteId,empleadoId,detalles){return await verificarAlta(await solicitar('POST','/api/facturas',{clienteId,empleadoId,metodoPago:1,porcentajeDescuento:0,detalles}),'Alta fixture de pago');}

// Contrasta un pago correcto con la misma factura y todas las existencias anteriores.
async function comprobarPago(numero,demanda,caso){
 const factura=await obtenerFactura(numero),productos=await listar('productos');
 const respuesta=await solicitar('POST','/api/facturas/'+numero+'/pagar');
 verificar(caso+' HTTP200',respuesta.codigo===200);
 verificar(caso+' solo cambia estado aPagada',JSON.stringify(respuesta.cuerpo)===JSON.stringify({...factura,estado:2}));
 const posteriores=await listar('productos');
 verificar(caso+' descuenta cantidades agregadas exactas',posteriores.every(producto=>producto.existenciaActual===(productos.find(antes=>antes.id===producto.id).existenciaActual-(demanda[producto.id]??0))));
 verificar(caso+' conserva otros campos de productos',posteriores.every(producto=>JSON.stringify({...producto,existenciaActual:0,tieneExistenciaBaja:false})===JSON.stringify({...productos.find(antes=>antes.id===producto.id),existenciaActual:0,tieneExistenciaBaja:false})));
 verificar(caso+' actualiza el indicador derivado de stock',posteriores.every(producto=>producto.tieneExistenciaBaja===(producto.existenciaActual<=producto.existenciaMinima)));
 verificar(caso+' stock nunca negativo',posteriores.every(producto=>producto.existenciaActual>=0));
 verificar(caso+' DTO coincide con consulta posterior',JSON.stringify(await obtenerFactura(numero))===JSON.stringify(respuesta.cuerpo));
 return respuesta.cuerpo;
}

// Un rechazo debe conservar todas las facturas y todo el inventario.
async function comprobarRechazo(numero,codigo,campo,caso){
 const facturas=await listar('facturas'),productos=await listar('productos');
 const respuesta=await solicitar('POST','/api/facturas/'+numero+'/pagar');
 verificarError(caso,respuesta,codigo,campo);
 verificar(caso+' ninguna factura cambia',JSON.stringify(await listar('facturas'))===JSON.stringify(facturas));
 verificar(caso+' ningun producto cambia',JSON.stringify(await listar('productos'))===JSON.stringify(productos));
}

try{
 await puertoLibre();
 const carpeta=path.join(raiz,'Carniceria.Api');
 proceso=spawn('dotnet',[path.join(carpeta,'bin','Debug','net10.0','Carniceria.Api.dll'),'--urls',base],{cwd:carpeta,env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development'},windowsHide:true,stdio:['ignore','pipe','pipe']});
 proceso.stdout.on('data',datos=>registro+=datos);proceso.stderr.on('data',datos=>registro+=datos);proceso.on('error',error=>registro+=error.message);
 await esperarApi();
 const clientes=await listar('clientes'),empleados=await listar('empleados'),semillas=await listar('facturas');
 const semilla=semillas[0],clienteId=clientes[0].id,empleadoId=empleados[0].id;
 const res=(await listar('productos')).find(producto=>producto.codigo==='RES001'),pol=(await listar('productos')).find(producto=>producto.codigo==='POL001');
 verificar('Semilla pendiente para pago',semilla.estado===1&&semilla.total===11187);
 // El precio actual cambia despues del registro; pagar conserva el precio capturado.
 await actualizarProducto(res.id,{precioUnidadMedida:5000});
 const semillaPagada=await comprobarPago(semilla.numero,{[res.id]:2,[pol.id]:1},'Pago normal historico');
 verificar('Pagar no revaloriza lineas ni importes',semillaPagada.detalles.find(d=>d.productoId===res.id).precioUnitario===4000&&semillaPagada.total===11187);
 await comprobarRechazo(semilla.numero,409,'Estado','Pago repetido');
 await comprobarRechazo('FAC999999',404,null,'Factura inexistente');
 // La segunda linea falla tras reducir stock; la primera no puede descontarse parcialmente.
 const productoA=await crearProducto('PAGOA'),productoB=await crearProducto('PAGOB');
 const dosLineas=await crearFactura(clienteId,empleadoId,[{productoId:productoA.id,cantidad:2},{productoId:productoB.id,cantidad:3}]);
 await actualizarProducto(productoB.id,{existenciaActual:2});
 await comprobarRechazo(dosLineas.numero,409,'Detalles[1].Cantidad','Stock cambio en segunda linea');
 await actualizarProducto(productoB.id,{existenciaActual:3});
 await comprobarPago(dosLineas.numero,{[productoA.id]:2,[productoB.id]:3},'Pago tras reponer segunda linea');
 // Dos detalles del mismo producto deben contrastarse y descontarse como demanda unica.
 const duplicado=await crearProducto('PAGODUP');
 const facturaDuplicada=await crearFactura(clienteId,empleadoId,[{productoId:duplicado.id,cantidad:6},{productoId:duplicado.id,cantidad:4}]);
 await actualizarProducto(duplicado.id,{existenciaActual:9});
 await comprobarRechazo(facturaDuplicada.numero,409,'Detalles[0].Cantidad','Demanda duplicada stock insuficiente');
 await actualizarProducto(duplicado.id,{existenciaActual:10});
 await comprobarPago(facturaDuplicada.numero,{[duplicado.id]:10},'Pago demanda duplicada exacta');
 await comprobarRechazo(facturaDuplicada.numero,409,'Estado','Duplicado pago repetido');
 // Una unidad incompatible se rechaza sin convertir cantidades ni alterar estado.
 const unidad=await crearProducto('PAGOUNIDAD');
 const facturaUnidad=await crearFactura(clienteId,empleadoId,[{productoId:unidad.id,cantidad:1}]);
 await actualizarProducto(unidad.id,{unidadMedida:2});
 await comprobarRechazo(facturaUnidad.numero,409,'Detalles[0].UnidadMedida','Unidad actual incompatible');
 await actualizarProducto(unidad.id,{unidadMedida:1});
 await comprobarPago(facturaUnidad.numero,{[unidad.id]:1},'Pago tras restaurar unidad compatible');
 verificar('Ninguna respuesta produce500',solicitudes.every(s=>s.codigo<500));
}catch(error){fallo=error;console.error(error.stack);}
finally{
 try{if(proceso&&proceso.exitCode===null&&proceso.signalCode===null){const cierre=new Promise(resolver=>proceso.once('close',resolver));proceso.kill();await Promise.race([cierre,esperar(5000)]);}if(proceso&&proceso.exitCode===null&&proceso.signalCode===null)throw new Error('No se confirmo cierre API propia.');}catch(error){errorCierre=error.message;}
 let libre=false;try{await puertoLibre();libre=true;}catch{}
 await fs.writeFile(path.join(salida,'solicitudes.json'),JSON.stringify(solicitudes,null,2));await fs.writeFile(path.join(salida,'api.log'),registro);
 const resumen={grupo:'PagoHttp',total:resultados.length,aprobadas:resultados.filter(r=>r.aprobado).length,fallo:fallo?.message??null,errorCierre,puerto:Number(opciones.puerto),puertoLibre:libre,resultados};
 await fs.writeFile(path.join(salida,'resumen.json'),JSON.stringify(resumen,null,2));console.log(JSON.stringify({total:resumen.total,aprobadas:resumen.aprobadas,fallo:resumen.fallo,errorCierre,puertoLibre:libre}));
 if(fallo||errorCierre||!libre)process.exitCode=1;
}

