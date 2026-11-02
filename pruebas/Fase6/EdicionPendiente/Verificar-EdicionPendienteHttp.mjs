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

// Lee el DTO vigente para verificar un reemplazo o rechazo completo.
async function obtenerFactura(numero){const respuesta=await solicitar('GET','/api/facturas/'+numero);if(respuesta.codigo!==200)throw new Error('No se pudo consultar '+numero);return respuesta.cuerpo;}

// Construye el contrato editable conservando los Id de las lineas actuales.
function solicitudEdicion(factura,cambios={}){return {clienteId:factura.clienteId,empleadoId:factura.empleadoId,metodoPago:factura.metodoPago,porcentajeDescuento:factura.porcentajeDescuento,detalles:factura.detalles.map(d=>({id:d.id,productoId:d.productoId,cantidad:d.cantidad})),...cambios};}

// Crea productos con precio conocido y existencias independientes.
async function crearProducto(codigo,precio){return await crearAuxiliar('productos',{proveedorId:(await listar('proveedores'))[0].id,codigo,nombre:codigo,categoria:1,unidadMedida:1,precioUnidadMedida:precio,existenciaActual:10,existenciaMinima:1});}

// Ajusta precios o unidades actuales mediante el contrato publico de productos.
async function actualizarProducto(id,cambios){const actual=(await solicitar('GET','/api/productos/'+id)).cuerpo;const formulario=Object.fromEntries(['proveedorId','codigo','nombre','categoria','unidadMedida','precioUnidadMedida','existenciaActual','existenciaMinima'].map(campo=>[campo,Object.hasOwn(cambios,campo)?cambios[campo]:actual[campo]]));const respuesta=await solicitar('PUT','/api/productos/'+id,formulario);verificar('Ajuste de fixture '+actual.codigo,respuesta.codigo===200);return respuesta.cuerpo;}

// Una edicion aceptada conserva cabecera inmutable y no altera productos.
async function editar(factura,solicitud,caso){const productos=await listar('productos');const respuesta=await solicitar('PUT','/api/facturas/'+factura.numero,solicitud);verificar(caso+' HTTP200',respuesta.codigo===200,{actual:respuesta.codigo});const actual=respuesta.cuerpo;verificar(caso+' Id numero emision estado inmutables',actual.id===factura.id&&actual.numero===factura.numero&&actual.fechaEmision===factura.fechaEmision&&actual.estado===factura.estado);verificar(caso+' stock y productos sin cambios',JSON.stringify(await listar('productos'))===JSON.stringify(productos));verificar(caso+' lectura coincide con respuesta',JSON.stringify(await obtenerFactura(factura.numero))===JSON.stringify(actual));return actual;}

// Un error de entrada o conflicto no puede guardar cabecera ni lineas parcialmente.
async function rechazar(numero,solicitud,codigo,campo,caso){const facturas=await listar('facturas'),productos=await listar('productos');const respuesta=await solicitar('PUT','/api/facturas/'+numero,solicitud);verificarError(caso,respuesta,codigo,campo);verificar(caso+' ninguna factura cambia',JSON.stringify(await listar('facturas'))===JSON.stringify(facturas));verificar(caso+' ningun producto cambia',JSON.stringify(await listar('productos'))===JSON.stringify(productos));}

try{
 await puertoLibre();
 const carpeta=path.join(raiz,'Carniceria.Api');
 proceso=spawn('dotnet',[path.join(carpeta,'bin','Debug','net10.0','Carniceria.Api.dll'),'--urls',base],{cwd:carpeta,env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development'},windowsHide:true,stdio:['ignore','pipe','pipe']});
 proceso.stdout.on('data',datos=>registro+=datos);proceso.stderr.on('data',datos=>registro+=datos);proceso.on('error',error=>registro+=error.message);await esperarApi();
 const cliente=(await listar('clientes'))[0],empleado=(await listar('empleados'))[0],semilla=(await listar('facturas'))[0];
 const a=await crearProducto('EDITA',1000),b=await crearProducto('EDITB',2000),c=await crearProducto('EDITC',3000);
 let factura=await verificarAlta(await solicitar('POST','/api/facturas',{clienteId:cliente.id,empleadoId:empleado.id,metodoPago:1,porcentajeDescuento:0,detalles:[{productoId:a.id,cantidad:2},{productoId:b.id,cantidad:1}]}),'Factura editable');
 const idA=factura.detalles[0].id,idB=factura.detalles[1].id;
 await actualizarProducto(a.id,{precioUnidadMedida:1500});await actualizarProducto(b.id,{precioUnidadMedida:2500});
 // Omitir el flag conserva snapshots de las lineas retenidas.
 const omitida=solicitudEdicion(factura,{metodoPago:3,porcentajeDescuento:25,detalles:[{id:idA,productoId:a.id,cantidad:3},{id:idB,productoId:b.id,cantidad:2}]});
 factura=await editar(factura,omitida,'Flag omitido historico');
 verificar('Flag omitido conserva ambos precios y unidades',factura.detalles[0].precioUnitario===1000&&factura.detalles[1].precioUnitario===2000&&factura.detalles.every(d=>d.unidadMedida===1));
 verificar('Historico recalcula cantidades descuento IVA',factura.subtotal===7000&&factura.montoDescuento===1750&&factura.baseGravable===5250&&factura.montoIva===682.5&&factura.total===5932.5);
 const explicitamenteFalsa=await editar(factura,solicitudEdicion(factura,{actualizarPreciosYUnidades:false}),'Flag false explicito');
 verificar('False explicito es estable',JSON.stringify(explicitamenteFalsa)===JSON.stringify(factura));factura=explicitamenteFalsa;
 // El opt-in actualiza los dos valores desde el servidor, no desde campos inventados.
 factura=await editar(factura,solicitudEdicion(factura,{actualizarPreciosYUnidades:true}),'Flag true actual');
 verificar('Flag true captura precios actuales',factura.detalles[0].precioUnitario===1500&&factura.detalles[1].precioUnitario===2500&&factura.detalles[0].id===idA&&factura.detalles[1].id===idB);
 verificar('Opt-in recalcula todos los importes',factura.subtotal===9500&&factura.montoDescuento===2375&&factura.baseGravable===7125&&factura.montoIva===926.25&&factura.total===8051.25);
 await actualizarProducto(a.id,{precioUnidadMedida:1800});await actualizarProducto(b.id,{precioUnidadMedida:2800});await actualizarProducto(c.id,{precioUnidadMedida:3500});
 // Una sustitucion y lineas nuevas usan datos actuales aunque no se pida actualizar retenidas.
 factura=await editar(factura,solicitudEdicion(factura,{porcentajeDescuento:0,detalles:[{id:idA,productoId:c.id,cantidad:2},{id:idB,productoId:b.id,cantidad:1},{productoId:a.id,cantidad:1},{id:null,productoId:c.id,cantidad:0.5}]}),'Sustitucion y lineas nuevas');
 verificar('Sustitucion toma precio actual y conserva Id propio',factura.detalles[0].id===idA&&factura.detalles[0].productoId===c.id&&factura.detalles[0].precioUnitario===3500);
 verificar('Retenida conserva ultimo snapshot',factura.detalles[1].id===idB&&factura.detalles[1].precioUnitario===2500);
 verificar('Id omitido y null crean lineas independientes actuales',factura.detalles[2].precioUnitario===1800&&factura.detalles[3].precioUnitario===3500&&factura.detalles[2].id!==idA&&factura.detalles[2].id!==idB&&factura.detalles[3].id!==idA&&factura.detalles[3].id!==idB&&new Set(factura.detalles.map(d=>d.id)).size===4);
 verificar('Mezcla de snapshots calcula14746.5',factura.subtotal===13050&&factura.total===14746.5);
 const idNuevoA=factura.detalles[2].id;
 factura=await editar(factura,solicitudEdicion(factura,{detalles:[{id:idA,productoId:c.id,cantidad:2},{id:idNuevoA,productoId:a.id,cantidad:1}]}),'Quitar lineas');
 verificar('Quitar lineas conserva solo las indicadas',factura.detalles.length===2&&factura.detalles[0].id===idA&&factura.detalles[1].id===idNuevoA&&factura.total===9944);
 // La cabecera de servidor y valores comerciales del cliente nunca se aceptan como autoridad.
 const suplantada=solicitudEdicion(factura,{id:'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',numero:'SUPLANTADO',fechaEmision:'2000-01-01T00:00:00Z',estado:2,total:0.01,detalles:factura.detalles.map(d=>({id:d.id,productoId:d.productoId,cantidad:d.cantidad,precioUnitario:0.01,unidadMedida:999,subtotalLinea:0.01}))});
 const anteriorSuplantacion=factura;factura=await editar(factura,suplantada,'Campos servidor suplantados');
 verificar('Campos servidor ignorados sin alterar datos',JSON.stringify(factura)===JSON.stringify(anteriorSuplantacion));
 // Errores de identidad, validacion e inventario se asocian a sus campos exactos.
 const baseEdicion=solicitudEdicion(factura),linea=baseEdicion.detalles[0],segunda=baseEdicion.detalles[1];
 const casos=[
 {nombre:'Id vacio',cuerpo:{...baseEdicion,detalles:[{...linea,id:'00000000-0000-0000-0000-000000000000'}]},codigo:400,campo:'Detalles[0].Id'},
 {nombre:'Id ajeno a factura',cuerpo:{...baseEdicion,detalles:[{...linea,id:semilla.detalles[0].id}]},codigo:400,campo:'Detalles[0].Id'},
 {nombre:'Id repetido',cuerpo:{...baseEdicion,detalles:[linea,linea]},codigo:400,campo:'Detalles[1].Id'},
 {nombre:'Id mal formado',cuerpo:{...baseEdicion,detalles:[{...linea,id:'no-guid'}]},codigo:400,campo:'Detalles[0].Id'},
 {nombre:'Solicitud nula',cuerpo:null,codigo:400},
 {nombre:'Lista nula',cuerpo:{...baseEdicion,detalles:null},codigo:400,campo:'Detalles'},
 {nombre:'Lista vacia',cuerpo:{...baseEdicion,detalles:[]},codigo:400,campo:'Detalles'},
 {nombre:'Linea nula',cuerpo:{...baseEdicion,detalles:[null]},codigo:400,campo:'Detalles[0]'},
 {nombre:'Primera valida segunda cantidad invalida',cuerpo:{...baseEdicion,detalles:[linea,{...segunda,cantidad:0}]},codigo:400,campo:'Detalles[1].Cantidad'},
 {nombre:'Producto inexistente',cuerpo:{...baseEdicion,detalles:[linea,{...segunda,productoId:'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'}]},codigo:400,campo:'Detalles[1].ProductoId'},
 {nombre:'Stock agregado excedido',cuerpo:{...baseEdicion,detalles:[{...linea,cantidad:6},{...segunda,productoId:c.id,cantidad:5}]},codigo:409,campo:'Detalles[0].Cantidad'},
 {nombre:'Descuento fuera del maximo',cuerpo:{...baseEdicion,porcentajeDescuento:25.01},codigo:400,campo:'PorcentajeDescuento'},
 {nombre:'Metodo indefinido',cuerpo:{...baseEdicion,metodoPago:999},codigo:400,campo:'MetodoPago'},
 {nombre:'Cliente vacio',cuerpo:{...baseEdicion,clienteId:'00000000-0000-0000-0000-000000000000'},codigo:400,campo:'ClienteId'},
 {nombre:'Empleado inexistente',cuerpo:{...baseEdicion,empleadoId:'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'},codigo:400,campo:'EmpleadoId'}
 ];
 for(const caso of casos)await rechazar(factura.numero,caso.cuerpo,caso.codigo,caso.campo,caso.nombre);
 await rechazar('FAC999999',baseEdicion,404,null,'Factura inexistente');
 // La compatibilidad de unidad protege el default historico, sin decidir la politica global.
 await actualizarProducto(c.id,{unidadMedida:2,precioUnidadMedida:4000});
 await rechazar(factura.numero,solicitudEdicion(factura),409,'Detalles[0].UnidadMedida','Unidad historica incompatible');
 factura=await editar(factura,solicitudEdicion(factura,{actualizarPreciosYUnidades:true}),'Opt-in de unidad actual');
 verificar('Opt-in captura unidad y precio actuales juntos',factura.detalles[0].unidadMedida===2&&factura.detalles[0].precioUnitario===4000&&factura.detalles[1].precioUnitario===1800&&factura.total===11074);
 // Las ediciones y rechazos no consumen la siguiente numeracion de alta.
 const pagada=await verificarAlta(await solicitar('POST','/api/facturas',{clienteId:cliente.id,empleadoId:empleado.id,metodoPago:1,porcentajeDescuento:0,detalles:[{productoId:semilla.detalles[0].productoId,cantidad:0.25}]}),'Fixture pagada');
 verificar('Ediciones no consumen numero definitivo',pagada.numero==='FAC000003');
 const pago=await solicitar('POST','/api/facturas/'+pagada.numero+'/pagar');verificar('Fixture cambia aPagada',pago.codigo===200&&pago.cuerpo.estado===2);
 await rechazar(pagada.numero,solicitudEdicion(pago.cuerpo),409,'Estado','Edicion pagada fuera de este paso');
 verificar('Ninguna respuesta produce500',solicitudes.every(s=>s.codigo<500));
}catch(error){fallo=error;console.error(error.stack);}
finally{
 try{if(proceso&&proceso.exitCode===null&&proceso.signalCode===null){const cierre=new Promise(resolver=>proceso.once('close',resolver));proceso.kill();await Promise.race([cierre,esperar(5000)]);}if(proceso&&proceso.exitCode===null&&proceso.signalCode===null)throw new Error('No se confirmo cierre API propia.');}catch(error){errorCierre=error.message;}
 let libre=false;try{await puertoLibre();libre=true;}catch{}
 await fs.writeFile(path.join(salida,'solicitudes.json'),JSON.stringify(solicitudes,null,2));await fs.writeFile(path.join(salida,'api.log'),registro);
 const resumen={grupo:'EdicionPendienteHttp',total:resultados.length,aprobadas:resultados.filter(r=>r.aprobado).length,fallo:fallo?.message??null,errorCierre,puerto:Number(opciones.puerto),puertoLibre:libre,resultados};
 await fs.writeFile(path.join(salida,'resumen.json'),JSON.stringify(resumen,null,2));console.log(JSON.stringify({total:resumen.total,aprobadas:resumen.aprobadas,fallo:resumen.fallo,errorCierre,puertoLibre:libre}));
 if(fallo||errorCierre||!libre)process.exitCode=1;
}
