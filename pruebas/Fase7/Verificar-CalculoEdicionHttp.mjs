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

// Crea productos propios para controlar precios y disponibilidad.
async function producto(codigo,precio){const r=await solicitar('POST','/api/productos',{proveedorId:(await listar('proveedores'))[0].id,codigo,nombre:codigo,categoria:1,unidadMedida:1,precioUnidadMedida:precio,existenciaActual:10,existenciaMinima:1});verificar('Producto propio '+codigo,r.codigo===201);return r.cuerpo;}

// Actualiza el precio actual sin reinterpretar la unidad del producto.
async function precioActual(id,precio){const p=(await solicitar('GET','/api/productos/'+id)).cuerpo;const formulario=Object.fromEntries(['proveedorId','codigo','nombre','categoria','unidadMedida','precioUnidadMedida','existenciaActual','existenciaMinima'].map(c=>[c,c==='precioUnidadMedida'?precio:p[c]]));const r=await solicitar('PUT','/api/productos/'+id,formulario);verificar('Cambio precio actual200',r.codigo===200);}

// Construye un contrato editable con los Id de detalle que pertenecen a la factura.
function edicion(f,cambios={}){return {clienteId:f.clienteId,empleadoId:f.empleadoId,metodoPago:f.metodoPago,porcentajeDescuento:f.porcentajeDescuento,detalles:f.detalles.map(d=>({id:d.id,productoId:d.productoId,cantidad:d.cantidad})),...cambios};}

// Captura factura, precios y todas las existencias para detectar efectos del calculo.
async function imagen(){return JSON.stringify({facturas:await listar('facturas'),productos:await listar('productos')});}

// La vista previa valida o rechaza sin guardar ningun cambio de negocio.
async function calcular(numero,solicitud,codigo,caso,campo){const antes=await imagen(),r=await solicitar('POST','/api/facturas/'+numero+'/calcular',solicitud);if(codigo===200){verificar(caso+' HTTP200',r.codigo===200,{actual:r.codigo});verificar(caso+' sin identidad definitiva',['id','numero','fechaEmision','estado'].every(k=>!Object.hasOwn(r.cuerpo,k))&&r.cuerpo.detalles.every(d=>!Object.hasOwn(d,'id')));}else verificarError(caso,r,codigo,campo);verificar(caso+' sin efectos',await imagen()===antes);return r.cuerpo;}

// Proyecta el documento guardado al contrato publico de calculo para compararlos.
function calculoGuardado(f){return Object.fromEntries(['porcentajeDescuento','subtotal','montoDescuento','baseGravable','montoIva','total','detalles'].map(k=>[k,k==='detalles'?f.detalles.map(({id,...d})=>d):f[k]]));}

// Crea una factura completa sin asumir el numero salvo en la prueba de secuencia.
async function alta(cliente,empleado,detalles){const r=await solicitar('POST','/api/facturas',{clienteId:cliente.id,empleadoId:empleado.id,metodoPago:1,porcentajeDescuento:0,detalles});verificar('Alta201',r.codigo===201);return r.cuerpo;}

try{
 await puertoLibre();const carpeta=path.join(raiz,'Carniceria.Api');proceso=spawn('dotnet',[path.join(carpeta,'bin','Debug','net10.0','Carniceria.Api.dll'),'--urls',base],{cwd:carpeta,env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development'},windowsHide:true,stdio:['ignore','pipe','pipe']});proceso.stdout.on('data',d=>registro+=d);proceso.stderr.on('data',d=>registro+=d);proceso.on('error',e=>registro+=e.message);await esperarApi();
 const cliente=(await listar('clientes'))[0],empleado=(await listar('empleados'))[0],semilla=(await listar('facturas'))[0];const a=await producto('PREVIA',1000),b=await producto('PREVIB',2000),c=await producto('PREVIC',3000);let pendiente=await alta(cliente,empleado,[{productoId:a.id,cantidad:2},{productoId:b.id,cantidad:1}]);
 await precioActual(a.id,1500);await precioActual(b.id,2500);
 const solicitud=edicion(pendiente,{porcentajeDescuento:25,detalles:pendiente.detalles.map((d,i)=>({id:d.id,productoId:d.productoId,cantidad:i===0?3:2}))});
 const historico=await calcular(pendiente.numero,solicitud,200,'Pendiente historico');verificar('Historico importes5932.5',historico.subtotal===7000&&historico.total===5932.5&&historico.detalles[0].precioUnitario===1000);
 const actual=await calcular(pendiente.numero,{...solicitud,actualizarPreciosYUnidades:true},200,'Pendiente opt-in');verificar('Actual importes8051.25',actual.subtotal===9500&&actual.total===8051.25&&actual.detalles[0].precioUnitario===1500);
 const nueva=await calcular(pendiente.numero,{...solicitud,detalles:[solicitud.detalles[0],{productoId:b.id,cantidad:0.5}]},200,'Nueva linea sin Id');verificar('Linea nueva toma precio actual',nueva.detalles[1].precioUnitario===2500&&nueva.detalles[1].cantidad===0.5);
 const repeticiones=await Promise.all([calcular(pendiente.numero,solicitud,200,'Repetida1'),calcular(pendiente.numero,solicitud,200,'Repetida2')]);verificar('Previews repetidas identicas',repeticiones.every(r=>JSON.stringify(r)===JSON.stringify(historico)));
 const guardada=await solicitar('PUT','/api/facturas/'+pendiente.numero,solicitud);verificar('PUT Pendiente200 tras preview',guardada.codigo===200);verificar('Preview coincide PUT historico',JSON.stringify(calculoGuardado(guardada.cuerpo))===JSON.stringify(historico));pendiente=guardada.cuerpo;
 // Una Pagada puede reutilizar su demanda anterior aunque el stock actual sea menor.
 let pagada=await alta(cliente,empleado,[{productoId:c.id,cantidad:6}]);const pago=await solicitar('POST','/api/facturas/'+pagada.numero+'/pagar');verificar('Pago fixture200',pago.codigo===200);pagada=pago.cuerpo;await precioActual(c.id,3500);
 const ocho=edicion(pagada,{detalles:[{id:pagada.detalles[0].id,productoId:c.id,cantidad:8}]});const previaPagada=await calcular(pagada.numero,ocho,200,'Pagada disponibilidad efectiva');verificar('Pagada historico total27120',previaPagada.total===27120&&previaPagada.detalles[0].precioUnitario===3000);
 const actualPagada=await calcular(pagada.numero,{...ocho,actualizarPreciosYUnidades:true},200,'Pagada opt-in');verificar('Pagada actual total31640',actualPagada.total===31640&&actualPagada.detalles[0].precioUnitario===3500);
 await calcular(pagada.numero,{...ocho,detalles:[{...ocho.detalles[0],cantidad:11}]},409,'Pagada exceso efectivo','ExistenciaActual');
 for(const [nombre,detalles,campo] of [['Id ajeno',[{...ocho.detalles[0],id:semilla.detalles[0].id}],'Detalles[0].Id'],['Id vacio',[{...ocho.detalles[0],id:'00000000-0000-0000-0000-000000000000'}],'Detalles[0].Id'],['Id repetido',[ocho.detalles[0],ocho.detalles[0]],'Detalles[1].Id'],['Cantidad invalida',[{...ocho.detalles[0],cantidad:0}],'Detalles[0].Cantidad']])await calcular(pagada.numero,{...ocho,detalles},400,nombre,campo);
 await calcular('FAC999999',ocho,404,'Factura ausente');await calcular(pagada.numero,{...ocho,detalles:null},400,'Detalles nulos','Detalles');
 const aplicada=await solicitar('PUT','/api/facturas/'+pagada.numero,{...ocho,actualizarPreciosYUnidades:true});verificar('PUT Pagada200 tras preview',aplicada.codigo===200);verificar('Preview coincide PUT Pagada',JSON.stringify(calculoGuardado(aplicada.cuerpo))===JSON.stringify(actualPagada));verificar('PUT aplica solo delta real',(await listar('productos')).find(p=>p.id===c.id).existenciaActual===2);
 const anulada=await solicitar('POST','/api/facturas/'+pagada.numero+'/anular');verificar('Fixture Anulada200',anulada.codigo===200);await calcular(pagada.numero,ocho,409,'Anulada no editable','Estado');
 const siguiente=await alta(cliente,empleado,[{productoId:a.id,cantidad:0.5}]);verificar('Preview y rechazos no consumen secuencia',siguiente.numero==='FAC000004');verificar('Sin respuestas500',solicitudes.every(s=>s.codigo<500));
}catch(error){fallo=error;console.error(error.stack);}
finally{
 try{if(proceso&&proceso.exitCode===null&&proceso.signalCode===null){const cierre=new Promise(resolver=>proceso.once('close',resolver));proceso.kill();await Promise.race([cierre,esperar(5000)]);}if(proceso&&proceso.exitCode===null&&proceso.signalCode===null)throw new Error('No se confirmo cierre API propia.');}catch(error){errorCierre=error.message;}
 let libre=false;try{await puertoLibre();libre=true;}catch{}
 await fs.writeFile(path.join(salida,'solicitudes.json'),JSON.stringify(solicitudes,null,2));await fs.writeFile(path.join(salida,'api.log'),registro);
 const resumen={grupo:'CalculoEdicionHttp',total:resultados.length,aprobadas:resultados.filter(r=>r.aprobado).length,fallo:fallo?.message??null,errorCierre,puerto:Number(opciones.puerto),puertoLibre:libre,resultados};
 await fs.writeFile(path.join(salida,'resumen.json'),JSON.stringify(resumen,null,2));console.log(JSON.stringify({total:resumen.total,aprobadas:resumen.aprobadas,fallo:resumen.fallo,errorCierre,puertoLibre:libre}));
 if(fallo||errorCierre||!libre)process.exitCode=1;
}
