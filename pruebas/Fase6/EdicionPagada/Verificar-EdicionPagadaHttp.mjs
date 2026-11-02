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
async function enviarActualizacionProducto(id,cambios){const actual=(await solicitar('GET','/api/productos/'+id)).cuerpo;const formulario=Object.fromEntries(['proveedorId','codigo','nombre','categoria','unidadMedida','precioUnidadMedida','existenciaActual','existenciaMinima'].map(campo=>[campo,Object.hasOwn(cambios,campo)?cambios[campo]:actual[campo]]));return await solicitar('PUT','/api/productos/'+id,formulario);}
async function actualizarProducto(id,cambios){const respuesta=await enviarActualizacionProducto(id,cambios);verificar('Ajuste de fixture producto',respuesta.codigo===200);return respuesta.cuerpo;}

// Una edicion aceptada conserva cabecera inmutable y no altera productos.
async function editar(factura,solicitud,caso){const productos=await listar('productos');const respuesta=await solicitar('PUT','/api/facturas/'+factura.numero,solicitud);verificar(caso+' HTTP200',respuesta.codigo===200,{actual:respuesta.codigo});const actual=respuesta.cuerpo;verificar(caso+' Id numero emision estado inmutables',actual.id===factura.id&&actual.numero===factura.numero&&actual.fechaEmision===factura.fechaEmision&&actual.estado===factura.estado);verificar(caso+' stock y productos sin cambios',JSON.stringify(await listar('productos'))===JSON.stringify(productos));verificar(caso+' lectura coincide con respuesta',JSON.stringify(await obtenerFactura(factura.numero))===JSON.stringify(actual));return actual;}

// Un error de entrada o conflicto no puede guardar cabecera ni lineas parcialmente.
async function rechazar(numero,solicitud,codigo,campo,caso){const facturas=await listar('facturas'),productos=await listar('productos');const respuesta=await solicitar('PUT','/api/facturas/'+numero,solicitud);verificarError(caso,respuesta,codigo,campo);verificar(caso+' ninguna factura cambia',JSON.stringify(await listar('facturas'))===JSON.stringify(facturas));verificar(caso+' ningun producto cambia',JSON.stringify(await listar('productos'))===JSON.stringify(productos));}

// Comprueba las existencias finales de cada producto indicado.
async function stocks(esperados,caso){const productos=await listar('productos');for(const [id,cantidad] of Object.entries(esperados)){const p=productos.find(p=>p.id===id);verificar(caso+' '+p?.codigo,p?.existenciaActual===cantidad,{esperado:cantidad,actual:p?.existenciaActual});}}

// Conserva la identidad y verifica que la respuesta sea la factura persistida.
async function editarPagada(factura,cuerpo,caso){const r=await solicitar('PUT','/api/facturas/'+factura.numero,cuerpo);verificar(caso+' HTTP200',r.codigo===200,{actual:r.codigo});const f=r.cuerpo;verificar(caso+' cabecera inmutable',f.id===factura.id&&f.numero===factura.numero&&f.fechaEmision===factura.fechaEmision&&f.estado===2);verificar(caso+' GET coincide',JSON.stringify(await obtenerFactura(f.numero))===JSON.stringify(f));return f;}

// Crea y paga un documento para preparar escenarios independientes.
async function crearPagada(cliente,empleado,detalles,caso){const f=await verificarAlta(await solicitar('POST','/api/facturas',{clienteId:cliente.id,empleadoId:empleado.id,metodoPago:1,porcentajeDescuento:0,detalles}),caso);const r=await solicitar('POST','/api/facturas/'+f.numero+'/pagar');verificar(caso+' pago200',r.codigo===200&&r.cuerpo.estado===2);return r.cuerpo;}

// Comprueba que el cambio de unidad rechaza tambien otros cambios del mismo PUT.
async function rechazarUnidad(id,caso){const productos=await listar('productos'),facturas=await listar('facturas');const r=await enviarActualizacionProducto(id,{unidadMedida:2,precioUnidadMedida:999});verificarError(caso,r,409,'UnidadMedida');verificar(caso+' productos intactos',JSON.stringify(await listar('productos'))===JSON.stringify(productos));verificar(caso+' facturas intactas',JSON.stringify(await listar('facturas'))===JSON.stringify(facturas));}

try{
 await puertoLibre();const carpeta=path.join(raiz,'Carniceria.Api');
 proceso=spawn('dotnet',[path.join(carpeta,'bin','Debug','net10.0','Carniceria.Api.dll'),'--urls',base],{cwd:carpeta,env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development'},windowsHide:true,stdio:['ignore','pipe','pipe']});
 proceso.stdout.on('data',datos=>registro+=datos);proceso.stderr.on('data',datos=>registro+=datos);proceso.on('error',error=>registro+=error.message);await esperarApi();
 const cliente=(await listar('clientes'))[0],empleado=(await listar('empleados'))[0],semilla=(await listar('facturas'))[0];
 const a=await crearProducto('PAGA',1000),b=await crearProducto('PAGB',2000),c=await crearProducto('PAGC',3000);
 let factura=await crearPagada(cliente,empleado,[{productoId:a.id,cantidad:3}],'Inicial');const idA=factura.detalles[0].id;
 await stocks({[a.id]:7,[b.id]:10,[c.id]:10},'Pago inicial');
 factura=await editarPagada(factura,solicitudEdicion(factura,{detalles:[{id:idA,productoId:a.id,cantidad:5}]}),'Aumenta3a5');await stocks({[a.id]:5},'Aumento');
 const repetida=factura;factura=await editarPagada(factura,solicitudEdicion(factura),'Sin cambios');verificar('No-op conserva DTO',JSON.stringify(factura)===JSON.stringify(repetida));await stocks({[a.id]:5},'No-op');
 factura=await editarPagada(factura,solicitudEdicion(factura,{detalles:[{id:idA,productoId:a.id,cantidad:2}]}),'Reduce5a2');await stocks({[a.id]:8},'Reduccion');
 await actualizarProducto(a.id,{precioUnidadMedida:1500});
 factura=await editarPagada(factura,solicitudEdicion(factura),'Snapshot por defecto');verificar('Precio historico1000 total2260',factura.detalles[0].precioUnitario===1000&&factura.total===2260);
 factura=await editarPagada(factura,solicitudEdicion(factura,{actualizarPreciosYUnidades:true}),'Opt-in actual');verificar('Precio actual1500 total3390',factura.detalles[0].precioUnitario===1500&&factura.total===3390);await stocks({[a.id]:8},'Precio no cambia stock');
 // Las lineas repetidas descuentan la demanda agrupada.
 factura=await editarPagada(factura,solicitudEdicion(factura,{detalles:[{id:idA,productoId:a.id,cantidad:2},{productoId:b.id,cantidad:2},{productoId:b.id,cantidad:1}]}),'Agrega duplicadas');verificar('Ids nuevos distintos',new Set(factura.detalles.map(d=>d.id)).size===3);await stocks({[a.id]:8,[b.id]:7},'Demanda3');
 const idB=factura.detalles[1].id;
 factura=await editarPagada(factura,solicitudEdicion(factura,{detalles:[{id:idA,productoId:a.id,cantidad:2},{id:idB,productoId:b.id,cantidad:4}]}),'Reagrupa');await stocks({[a.id]:8,[b.id]:6},'Demanda4');
 // La union incluye productos quitados y productos sustituidos.
 factura=await editarPagada(factura,solicitudEdicion(factura,{detalles:[{id:idA,productoId:c.id,cantidad:1},{id:idB,productoId:b.id,cantidad:4}]}),'SustituyeAporC');verificar('Sustitucion conserva Id y toma precio',factura.detalles[0].id===idA&&factura.detalles[0].precioUnitario===3000);await stocks({[a.id]:10,[b.id]:6,[c.id]:9},'Union productos');
 factura=await editarPagada(factura,solicitudEdicion(factura,{detalles:[{id:idA,productoId:c.id,cantidad:1}]}),'QuitaB');await stocks({[b.id]:10,[c.id]:9},'Reintegro vigente');
 // Fallar en el segundo producto no aplica el primer stock preparado.
 await actualizarProducto(a.id,{existenciaActual:1});
 await rechazar(factura.numero,solicitudEdicion(factura,{detalles:[{id:idA,productoId:c.id,cantidad:0.5},{productoId:a.id,cantidad:2}]}),409,'ExistenciaActual','Segundo producto insuficiente');
 await rechazar(factura.numero,solicitudEdicion(factura,{detalles:[{id:idA,productoId:c.id,cantidad:11}]}),409,'ExistenciaActual','Cantidad agregada insuficiente');
 await rechazar(factura.numero,solicitudEdicion(factura,{detalles:[{id:semilla.detalles[0].id,productoId:c.id,cantidad:1}]}),400,'Detalles[0].Id','Id ajeno');
 await rechazar(factura.numero,solicitudEdicion(factura,{detalles:[{id:idA,productoId:c.id,cantidad:1},{id:idA,productoId:c.id,cantidad:1}]}),400,'Detalles[1].Id','Id duplicado');
 await rechazar(factura.numero,solicitudEdicion(factura,{porcentajeDescuento:26}),400,'PorcentajeDescuento','Descuento invalido');
 await rechazarUnidad(c.id,'Guarda Pagada');await rechazarUnidad(semilla.detalles[0].productoId,'Guarda Pendiente');
 const libre=await crearProducto('LIBREU',100);await actualizarProducto(libre.id,{unidadMedida:2});verificar('Unidad libre editable',(await solicitar('GET','/api/productos/'+libre.id)).cuerpo.unidadMedida===2);
 // El JSON crudo conserva decimal.MaxValue sin redondearlo como Number.
 const extremo=await crearProducto('EXTREMO',1);let limite=await crearPagada(cliente,empleado,[{productoId:extremo.id,cantidad:1}],'Limite');const p=(await solicitar('GET','/api/productos/'+extremo.id)).cuerpo;
 const formulario=Object.fromEntries(['proveedorId','codigo','nombre','categoria','unidadMedida','precioUnidadMedida','existenciaActual','existenciaMinima'].map(campo=>[campo,p[campo]]));formulario.existenciaActual='DECIMAL_MAXIMO';
 const ajuste=await solicitar('PUT','/api/productos/'+extremo.id,JSON.stringify(formulario).replace('"DECIMAL_MAXIMO"','79228162514264337593543950335'),true);verificar('Stock decimalMax aceptado',ajuste.codigo===200);
 const antesMax=(await solicitar('GET','/api/productos/'+extremo.id)).cuerpo;limite=await editarPagada(limite,solicitudEdicion(limite),'Max mas1 menos1');verificar('No-op extremo conserva producto',JSON.stringify((await solicitar('GET','/api/productos/'+extremo.id)).cuerpo)===JSON.stringify(antesMax));
 await rechazar(limite.numero,solicitudEdicion(limite,{detalles:[{productoId:b.id,cantidad:1}]}),409,'ExistenciaActual','Overflow reintegro real');
 verificar('Sin respuestas500',solicitudes.every(r=>r.codigo<500));
}catch(error){fallo=error;console.error(error.stack);}
finally{
 try{if(proceso&&proceso.exitCode===null&&proceso.signalCode===null){const cierre=new Promise(resolver=>proceso.once('close',resolver));proceso.kill();await Promise.race([cierre,esperar(5000)]);}if(proceso&&proceso.exitCode===null&&proceso.signalCode===null)throw new Error('No se confirmo cierre API propia.');}catch(error){errorCierre=error.message;}
 let libre=false;try{await puertoLibre();libre=true;}catch{}
 await fs.writeFile(path.join(salida,'solicitudes.json'),JSON.stringify(solicitudes,null,2));await fs.writeFile(path.join(salida,'api.log'),registro);
 const resumen={grupo:'EdicionPagadaHttp',total:resultados.length,aprobadas:resultados.filter(r=>r.aprobado).length,fallo:fallo?.message??null,errorCierre,puerto:Number(opciones.puerto),puertoLibre:libre,resultados};
 await fs.writeFile(path.join(salida,'resumen.json'),JSON.stringify(resumen,null,2));console.log(JSON.stringify({total:resumen.total,aprobadas:resumen.aprobadas,fallo:resumen.fallo,errorCierre,puertoLibre:libre}));
 if(fallo||errorCierre||!libre)process.exitCode=1;
}
