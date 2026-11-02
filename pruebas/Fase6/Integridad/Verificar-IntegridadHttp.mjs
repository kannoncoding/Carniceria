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

let consecutivo=100;
const carreras=[];

// Crea personas nuevas sin referencias para las carreras de baja.
async function crearPersona(recurso){const n=String(++consecutivo).padStart(4,'0');const formulario={tipoIdentificacion:1,identificacion:'9-0000-'+n,nombre:'Prueba',primerApellido:'Mora',segundoApellido:'Solis',fechaNacimiento:'1990-01-01'};if(recurso==='empleados')Object.assign(formulario,{salarioMensual:500000,fechaIngreso:'2020-01-01',areaTrabajo:2,puesto:'Cajero',turno:1});return await crearAuxiliar(recurso,formulario);}

// Crea un proveedor sin productos previos.
async function crearProveedor(){return await crearAuxiliar('proveedores',{tipoIdentificacion:4,identificacion:'3-101-'+String(++consecutivo).padStart(6,'0'),razonSocial:'Proveedor'+consecutivo,nombreContacto:'Ana Mora',telefono:'22223333',correoElectronico:'prueba@example.com',tipoSuministro:8,frecuenciaEntrega:3});}

// Define un producto valido antes de su alta o reasignacion.
function formularioProducto(proveedorId){return {proveedorId,codigo:'INTEG'+(++consecutivo),nombre:'ProductoPrueba',categoria:1,unidadMedida:1,precioUnidadMedida:1000,existenciaActual:10,existenciaMinima:1};}

// Define la entrada de factura sin campos asignados por el servidor.
function formularioFactura(clienteId,empleadoId,productoId){return {clienteId,empleadoId,metodoPago:1,porcentajeDescuento:0,detalles:[{productoId,cantidad:1}]};}

// Captura todas las colecciones que deben quedar intactas al rechazar una baja.
async function capturar(){const valores={};for(const recurso of ['clientes','empleados','proveedores','productos','facturas'])valores[recurso]=await listar(recurso);return JSON.stringify(valores);}

// Comprueba codigo, campo exacto y ausencia de cualquier cambio parcial.
async function comprobarRechazo(recurso,id,campo,caso){const antes=await capturar();verificarError(caso,await solicitar('DELETE','/api/'+recurso+'/'+id),409,campo);verificar(caso+' colecciones intactas',await capturar()===antes);}

// Revisa referencias finales mediante los contratos publicos de API.
async function sinHuerfanos(caso){const clientes=await listar('clientes'),empleados=await listar('empleados'),productos=await listar('productos'),proveedores=await listar('proveedores'),facturas=await listar('facturas');verificar(caso+' facturas sin huerfanos',facturas.every(f=>clientes.some(c=>c.id===f.clienteId)&&empleados.some(e=>e.id===f.empleadoId)&&f.detalles.every(d=>productos.some(p=>p.id===d.productoId))));verificar(caso+' productos sin proveedor huerfano',productos.every(p=>proveedores.some(v=>v.id===p.proveedorId)));}

try{
 await puertoLibre();const carpeta=path.join(raiz,'Carniceria.Api');proceso=spawn('dotnet',[path.join(carpeta,'bin','Debug','net10.0','Carniceria.Api.dll'),'--urls',base],{cwd:carpeta,env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development'},windowsHide:true,stdio:['ignore','pipe','pipe']});proceso.stdout.on('data',d=>registro+=d);proceso.stderr.on('data',d=>registro+=d);proceso.on('error',e=>registro+=e.message);await esperarApi();
 const clienteBase=(await listar('clientes'))[0],empleadoBase=(await listar('empleados'))[0],productoBase=(await listar('productos'))[0],proveedorBase=(await listar('proveedores'))[0];
 // Cada estado dispone de relaciones propias para probar las cuatro guardas.
 for(const estado of [1,2,3]){
  const cliente=await crearPersona('clientes'),empleado=await crearPersona('empleados'),proveedor=await crearProveedor(),producto=await crearAuxiliar('productos',formularioProducto(proveedor.id));
  let factura=await verificarAlta(await solicitar('POST','/api/facturas',formularioFactura(cliente.id,empleado.id,producto.id)),'Matriz estado'+estado);
  if(estado>=2){const pago=await solicitar('POST','/api/facturas/'+factura.numero+'/pagar');verificar('Pago matriz'+estado,pago.codigo===200);factura=pago.cuerpo;}if(estado===3){const anulacion=await solicitar('POST','/api/facturas/'+factura.numero+'/anular');verificar('Anula matriz',anulacion.codigo===200);factura=anulacion.cuerpo;}
  verificar('Estado real matriz'+estado,factura.estado===estado);
  for(const [recurso,id,campo] of [['clientes',cliente.id,'ClienteId'],['empleados',empleado.id,'EmpleadoId'],['productos',producto.id,'ProductoId'],['proveedores',proveedor.id,'ProveedorId']])await comprobarRechazo(recurso,id,campo,'Estado'+estado+' baja '+recurso);
  const antes=await capturar();verificarError('Unidad estado'+estado,await enviarActualizacionProducto(producto.id,{unidadMedida:2}),409,'UnidadMedida');verificar('Unidad rechazada sin cambios'+estado,await capturar()===antes);
  await actualizarProducto(producto.id,{precioUnidadMedida:1200});verificar('Precio permitido estado'+estado,(await obtenerFactura(factura.numero)).detalles[0].precioUnitario===1000);
 }
 // Las entidades nuevas sin vinculacion siguen admitiendo bajas.
 for(const recurso of ['clientes','empleados','proveedores','productos']){const entidad=recurso==='clientes'||recurso==='empleados'?await crearPersona(recurso):recurso==='proveedores'?await crearProveedor():await crearAuxiliar('productos',formularioProducto(proveedorBase.id));const baja=await solicitar('DELETE','/api/'+recurso+'/'+entidad.id);verificar('Baja libre '+recurso,baja.codigo===204&&baja.cuerpo==='');}
 // Carrera acotada entre alta de factura y baja de una referencia nueva.
 for(const recurso of ['clientes','empleados','productos'])for(let i=0;i<2;i++){
  const entidad=recurso==='productos'?await crearAuxiliar('productos',formularioProducto(proveedorBase.id)):await crearPersona(recurso);const entrada=formularioFactura(recurso==='clientes'?entidad.id:clienteBase.id,recurso==='empleados'?entidad.id:empleadoBase.id,recurso==='productos'?entidad.id:productoBase.id);const productosAntes=await listar('productos');
  const [alta,baja]=await Promise.all([solicitar('POST','/api/facturas',entrada),solicitar('DELETE','/api/'+recurso+'/'+entidad.id)]);carreras.push({tipo:'AltaFactura-'+recurso,alta:alta.codigo,baja:baja.codigo});verificar('Carrera alta/baja '+recurso+i,(alta.codigo===201&&baja.codigo===409)||(alta.codigo===400&&baja.codigo===204));
  const restantes=await listar('productos');verificar('Carrera Pending no cambia stock '+recurso+i,restantes.every(p=>productosAntes.find(a=>a.id===p.id)?.existenciaActual===p.existenciaActual));await sinHuerfanos('Alta/baja '+recurso+i);
 }
 // Editar una factura pagada y borrar el nuevo cliente tampoco puede dejar huerfanos.
 let pagada=await crearPagada(clienteBase,empleadoBase,[{productoId:productoBase.id,cantidad:0.25}],'Carrera edicion');
 for(let i=0;i<3;i++){const cliente=await crearPersona('clientes'),antes=pagada,productos=await listar('productos');const [edicion,baja]=await Promise.all([solicitar('PUT','/api/facturas/'+pagada.numero,solicitudEdicion(pagada,{clienteId:cliente.id})),solicitar('DELETE','/api/clientes/'+cliente.id)]);carreras.push({tipo:'EditarPagada-Cliente',edicion:edicion.codigo,baja:baja.codigo});verificar('Carrera edicion/baja'+i,(edicion.codigo===200&&baja.codigo===409)||(edicion.codigo===400&&baja.codigo===204));pagada=await obtenerFactura(pagada.numero);verificar('Edicion carrera resultado atomico'+i,edicion.codigo===200?pagada.clienteId===cliente.id:JSON.stringify(pagada)===JSON.stringify(antes));verificar('Edicion sin delta no cambia stock'+i,JSON.stringify(await listar('productos'))===JSON.stringify(productos));await sinHuerfanos('Edicion/baja'+i);}
 // Alta de producto y baja del proveedor libre deben resolver la relacion atomicamente.
 for(let i=0;i<3;i++){const proveedor=await crearProveedor();const [alta,baja]=await Promise.all([solicitar('POST','/api/productos',formularioProducto(proveedor.id)),solicitar('DELETE','/api/proveedores/'+proveedor.id)]);carreras.push({tipo:'AltaProducto-Proveedor',alta:alta.codigo,baja:baja.codigo});verificar('Carrera producto/proveedor'+i,(alta.codigo===201&&baja.codigo===409)||(alta.codigo===400&&baja.codigo===204));await sinHuerfanos('Producto/proveedor'+i);}
 await fs.writeFile(path.join(salida,'carreras.json'),JSON.stringify(carreras,null,2));verificar('Ninguna respuesta500',solicitudes.every(s=>s.codigo<500));
}catch(error){fallo=error;console.error(error.stack);}
finally{
 try{if(proceso&&proceso.exitCode===null&&proceso.signalCode===null){const cierre=new Promise(resolver=>proceso.once('close',resolver));proceso.kill();await Promise.race([cierre,esperar(5000)]);}if(proceso&&proceso.exitCode===null&&proceso.signalCode===null)throw new Error('No se confirmo cierre API propia.');}catch(error){errorCierre=error.message;}
 let libre=false;try{await puertoLibre();libre=true;}catch{}
 await fs.writeFile(path.join(salida,'solicitudes.json'),JSON.stringify(solicitudes,null,2));await fs.writeFile(path.join(salida,'api.log'),registro);
 const resumen={grupo:'IntegridadFacturaHttp',total:resultados.length,aprobadas:resultados.filter(r=>r.aprobado).length,fallo:fallo?.message??null,errorCierre,puerto:Number(opciones.puerto),puertoLibre:libre,resultados};
 await fs.writeFile(path.join(salida,'resumen.json'),JSON.stringify(resumen,null,2));console.log(JSON.stringify({total:resumen.total,aprobadas:resumen.aprobadas,fallo:resumen.fallo,errorCierre,puertoLibre:libre}));
 if(fallo||errorCierre||!libre)process.exitCode=1;
}
