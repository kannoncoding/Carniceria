import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import net from 'node:net';
import {spawn} from 'node:child_process';
import {parseArgs} from 'node:util';
import {setTimeout as esperar} from 'node:timers/promises';

// Reserva un puerto y conserva evidencia de los dos inicios de API.
const {values: opciones}=parseArgs({options:{raiz:{type:'string',default:process.cwd()},salida:{type:'string'},puerto:{type:'string',default:'5234'}}});
const raiz=path.resolve(opciones.raiz);
const salida=opciones.salida?path.resolve(opciones.salida):await fs.mkdtemp(path.join(os.tmpdir(),'carniceria-semillas-'));
await fs.mkdir(salida,{recursive:true});
const base='http://127.0.0.1:'+opciones.puerto;
const resultados=[],procesos=[],solicitudes=[];
let fallo=null;

// Registra una condicion semantica de la semilla o del reinicio.
function verificar(caso,condicion,detalle={}){resultados.push({caso,aprobado:!!condicion,...detalle});if(!condicion)throw new Error(caso);}

// Comprueba disponibilidad del puerto sin intervenir en procesos ajenos.
async function puertoLibre(){const servidor=net.createServer();await new Promise((resolver,rechazar)=>{servidor.once('error',rechazar);servidor.listen(Number(opciones.puerto),'127.0.0.1',resolver);});await new Promise(resolver=>servidor.close(resolver));}

// Inicia una DLL ya compilada y guarda su registro de salida.
function iniciarApi(etapa){const carpeta=path.join(raiz,'Carniceria.Api');const proceso=spawn('dotnet',[path.join(carpeta,'bin','Debug','net10.0','Carniceria.Api.dll'),'--urls',base],{cwd:carpeta,env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development'},windowsHide:true,stdio:['ignore','pipe','pipe']});procesos.push(proceso);let registro='';proceso.stdout.on('data',datos=>registro+=datos);proceso.stderr.on('data',datos=>registro+=datos);proceso.on('error',error=>registro+=error.message);proceso.on('close',()=>{void fs.writeFile(path.join(salida,'api-'+etapa+'.log'),registro);});return proceso;}

// Espera salud HTTP antes de leer las semillas.
async function esperarApi(){const fin=Date.now()+15000;while(Date.now()<fin){try{if((await fetch(base+'/api/estado',{signal:AbortSignal.timeout(1000)})).ok)return;}catch{}await esperar(100);}throw new Error('La API propia no inicio.');}

// Envia una solicitud y registra su resultado JSON.
async function solicitar(metodo,ruta,cuerpo){const respuesta=await fetch(new URL(ruta,base),{method:metodo,headers:cuerpo?{'Content-Type':'application/json'}:{},body:cuerpo?JSON.stringify(cuerpo):undefined,signal:AbortSignal.timeout(5000)});const texto=await respuesta.text();let json;try{json=JSON.parse(texto);}catch{}const registro={metodo,ruta,codigo:respuesta.status,cuerpo:json??texto,cabeceras:Object.fromEntries(respuesta.headers)};solicitudes.push(registro);return registro;}

// Obtiene las cinco colecciones de datos como una imagen verificable.
async function imagen(){const datos={};for(const recurso of ['clientes','empleados','proveedores','productos','facturas']){const respuesta=await solicitar('GET','/api/'+recurso);if(respuesta.codigo!==200||!Array.isArray(respuesta.cuerpo))throw new Error('No se pudo leer '+recurso);datos[recurso]=respuesta.cuerpo;}return datos;}

// Comprueba cabecera, relaciones, calculo, dos detalles y ausencia de descuento de stock.
async function comprobarSemilla(datos,etapa,inicio){
 verificar(etapa+' conteos1/1/1/2/1',JSON.stringify(Object.values(datos).map(lista=>lista.length))===JSON.stringify([1,1,1,2,1]));
 const factura=datos.facturas[0],res=datos.productos.find(p=>p.codigo==='RES001'),pol=datos.productos.find(p=>p.codigo==='POL001');
 verificar(etapa+' numeroFAC1 y Pendiente',factura.numero==='FAC000001'&&factura.estado===1);
 verificar(etapa+' cliente y empleado reales',factura.clienteId===datos.clientes[0].id&&factura.empleadoId===datos.empleados[0].id);
 verificar(etapa+' metodoEfectivo descuento10',factura.metodoPago===1&&factura.porcentajeDescuento===10);
 verificar(etapa+' cabecera calculada11187',factura.subtotal===11000&&factura.montoDescuento===1100&&factura.baseGravable===9900&&factura.montoIva===1287&&factura.total===11187);
 verificar(etapa+' emision actual Costa Rica',/-06:00$/.test(factura.fechaEmision)&&Date.parse(factura.fechaEmision)>=inicio-1000&&Date.parse(factura.fechaEmision)<=Date.now()+1000);
 verificar(etapa+' Id factura asignado',/^[0-9a-f-]{36}$/i.test(factura.id)&&factura.id!=='00000000-0000-0000-0000-000000000000');
 verificar(etapa+' dos detalles con Id unicos',factura.detalles.length===2&&factura.detalles.every(d=>/^[0-9a-f-]{36}$/i.test(d.id)&&d.id!=='00000000-0000-0000-0000-000000000000')&&new Set(factura.detalles.map(d=>d.id)).size===2);
 const detalleRes=factura.detalles.find(d=>d.productoId===res.id),detallePol=factura.detalles.find(d=>d.productoId===pol.id);
 verificar(etapa+' lineaRES2 usa precio unidad y subtotal',detalleRes?.cantidad===2&&detalleRes.precioUnitario===4000&&detalleRes.unidadMedida===res.unidadMedida&&detalleRes.subtotalLinea===8000);
 verificar(etapa+' lineaPOL1 usa precio unidad y subtotal',detallePol?.cantidad===1&&detallePol.precioUnitario===3000&&detallePol.unidadMedida===pol.unidadMedida&&detallePol.subtotalLinea===3000);
 verificar(etapa+' stock10/10 intacto',res.existenciaActual===10&&pol.existenciaActual===10);
 const consulta=await solicitar('GET','/api/facturas/'+factura.numero);verificar(etapa+' consulta reproduce semilla',consulta.codigo===200&&JSON.stringify(consulta.cuerpo)===JSON.stringify(factura));
 return factura;
}

// Detiene unicamente un proceso propio y confirma su salida.
async function detener(proceso){if(proceso.exitCode===null&&proceso.signalCode===null){const cierre=new Promise(resolver=>proceso.once('close',resolver));proceso.kill();await Promise.race([cierre,esperar(5000)]);}if(proceso.exitCode===null&&proceso.signalCode===null)throw new Error('No se confirmo cierre de API propia.');}

try{
 await puertoLibre();
 const inicio=Date.now();const primeraApi=iniciarApi('inicial');await esperarApi();
 const inicial=await imagen();const semilla=await comprobarSemilla(inicial,'Inicial',inicio);
 await fs.writeFile(path.join(salida,'datos-iniciales.json'),JSON.stringify(inicial,null,2));
 // Una alta real debe continuar despues del numero usado por la semilla.
 const alta=await solicitar('POST','/api/facturas',{clienteId:inicial.clientes[0].id,empleadoId:inicial.empleados[0].id,metodoPago:4,porcentajeDescuento:0,detalles:[{productoId:inicial.productos[0].id,cantidad:1}]});
 verificar('Alta posterior201 con FAC2',alta.codigo===201&&alta.cuerpo.numero==='FAC000002'&&alta.cuerpo.estado===1);
 const trasAlta=await imagen();verificar('Alta posterior agrega una factura',trasAlta.facturas.length===2);
 verificar('Alta posterior tampoco altera stock',JSON.stringify(trasAlta.productos)===JSON.stringify(inicial.productos));
 await detener(primeraApi);await puertoLibre();
 const inicioReinicio=Date.now();const segundaApi=iniciarApi('reinicio');await esperarApi();
 verificar('Reinicio usa PID nuevo',segundaApi.pid!==primeraApi.pid);
 const reinicio=await imagen();const nuevaSemilla=await comprobarSemilla(reinicio,'Reinicio',inicioReinicio);
 verificar('Reinicio genera Id nuevo de factura semilla',nuevaSemilla.id!==semilla.id);
 verificar('Reinicio genera Id nuevos de detalles',nuevaSemilla.detalles.every(d=>!semilla.detalles.some(anterior=>anterior.id===d.id)));
 for(const recurso of ['clientes','empleados','proveedores','productos'])verificar('Reinicio genera Id nuevos '+recurso,reinicio[recurso].every(r=>!inicial[recurso].some(anterior=>anterior.id===r.id)));
 const ausente=await solicitar('GET','/api/facturas/'+alta.cuerpo.numero);verificar('Reinicio elimina altaFAC2',ausente.codigo===404);
 verificar('Reinicio conserva solo semilla nueva',reinicio.facturas.length===1&&reinicio.facturas[0].id!==alta.cuerpo.id);
 await fs.writeFile(path.join(salida,'datos-reinicio.json'),JSON.stringify(reinicio,null,2));
}catch(error){fallo=error;console.error(error.stack);}
finally{
 const erroresCierre=[];for(const proceso of [...procesos].reverse()){try{await detener(proceso);}catch(error){erroresCierre.push(error.message);}}
 let libre=false;try{await puertoLibre();libre=true;}catch{}
 await fs.writeFile(path.join(salida,'solicitudes.json'),JSON.stringify(solicitudes,null,2));
 const resumen={grupo:'SemillasHttp',total:resultados.length,aprobadas:resultados.filter(r=>r.aprobado).length,fallo:fallo?.message??null,erroresCierre,pids:procesos.map(p=>p.pid),puerto:Number(opciones.puerto),puertoLibre:libre,resultados};
 await fs.writeFile(path.join(salida,'resumen.json'),JSON.stringify(resumen,null,2));
 console.log(JSON.stringify({total:resumen.total,aprobadas:resumen.aprobadas,fallo:resumen.fallo,erroresCierre,pids:resumen.pids,puertoLibre:libre}));
 if(fallo||erroresCierre.length||!libre)process.exitCode=1;
}

