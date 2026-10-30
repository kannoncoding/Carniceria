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

try{
 await puertoLibre();
 const carpeta=path.join(raiz,'Carniceria.Api');
 proceso=spawn('dotnet',[path.join(carpeta,'bin','Debug','net10.0','Carniceria.Api.dll'),'--urls',base],{cwd:carpeta,env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development'},windowsHide:true,stdio:['ignore','pipe','pipe']});
 proceso.stdout.on('data',datos=>registro+=datos);proceso.stderr.on('data',datos=>registro+=datos);proceso.on('error',error=>registro+=error.message);
 await esperarApi();
 const clientes=await listar('clientes'),empleados=await listar('empleados'),productos=await listar('productos'),facturas=await listar('facturas');
 const producto=productos.find(p=>p.codigo==='RES001');
 const solicitud={clienteId:clientes[0].id,empleadoId:empleados[0].id,metodoPago:1,porcentajeDescuento:0,detalles:[{productoId:producto.id,cantidad:1}]};
 // Casos publicos faltantes del rechazo completo, sin alterar la primera linea valida.
 const casos=[
 {nombre:'Cero lineas',cuerpo:{...solicitud,detalles:[]},campo:'Detalles'},
 {nombre:'Cantidad cero',cuerpo:{...solicitud,detalles:[{productoId:producto.id,cantidad:0}]},campo:'Detalles[0].Cantidad'},
 {nombre:'Empleado inexistente',cuerpo:{...solicitud,empleadoId:'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'},campo:'EmpleadoId'},
 {nombre:'Descuento negativo',cuerpo:{...solicitud,porcentajeDescuento:-0.01},campo:'PorcentajeDescuento'},
 {nombre:'Primera linea valida segunda invalida',cuerpo:{...solicitud,detalles:[{productoId:producto.id,cantidad:1},{productoId:producto.id,cantidad:0}]},campo:'Detalles[1].Cantidad'}
 ];
 for(const caso of casos){const respuesta=await solicitar('POST','/api/facturas',caso.cuerpo);verificarError(caso.nombre,respuesta,400,caso.campo);verificar(caso.nombre+' no guarda factura parcial',JSON.stringify(await listar('facturas'))===JSON.stringify(facturas));verificar(caso.nombre+' no cambia inventario',JSON.stringify(await listar('productos'))===JSON.stringify(productos));}
 // Las dos lineas suman exactamente stock y siguen siendo pendientes sin reserva.
 const exacta=await solicitar('POST','/api/facturas',{...solicitud,detalles:[{productoId:producto.id,cantidad:6},{productoId:producto.id,cantidad:4}]});
 verificar('Demanda agregada exacta201 y siguienteFAC2',exacta.codigo===201&&exacta.cuerpo.numero==='FAC000002');
 verificar('Demanda exacta conserva ambas lineas',exacta.cuerpo.detalles.length===2&&exacta.cuerpo.detalles[0].cantidad===6&&exacta.cuerpo.detalles[1].cantidad===4);
 verificar('Demanda exacta calcula40000masIVA',exacta.cuerpo.subtotal===40000&&exacta.cuerpo.montoDescuento===0&&exacta.cuerpo.montoIva===5200&&exacta.cuerpo.total===45200);
 verificar('Demanda exacta no reserva ni descuenta stock',JSON.stringify(await listar('productos'))===JSON.stringify(productos));
 verificar('Solo la factura valida se registra',(await listar('facturas')).length===facturas.length+1);
 verificar('Ninguna respuesta produce500',solicitudes.every(s=>s.codigo<500));
}catch(error){fallo=error;console.error(error.stack);}
finally{
 try{if(proceso&&proceso.exitCode===null&&proceso.signalCode===null){const cierre=new Promise(resolver=>proceso.once('close',resolver));proceso.kill();await Promise.race([cierre,esperar(5000)]);}if(proceso&&proceso.exitCode===null&&proceso.signalCode===null)throw new Error('No se confirmo cierre API propia.');}catch(error){errorCierre=error.message;}
 let libre=false;try{await puertoLibre();libre=true;}catch{}
 await fs.writeFile(path.join(salida,'solicitudes.json'),JSON.stringify(solicitudes,null,2));await fs.writeFile(path.join(salida,'api.log'),registro);
 const resumen={grupo:'LimitesFacturaHttp',total:resultados.length,aprobadas:resultados.filter(r=>r.aprobado).length,fallo:fallo?.message??null,errorCierre,puerto:Number(opciones.puerto),puertoLibre:libre,resultados};
 await fs.writeFile(path.join(salida,'resumen.json'),JSON.stringify(resumen,null,2));console.log(JSON.stringify({total:resumen.total,aprobadas:resumen.aprobadas,fallo:resumen.fallo,errorCierre,puertoLibre:libre}));
 if(fallo||errorCierre||!libre)process.exitCode=1;
}

