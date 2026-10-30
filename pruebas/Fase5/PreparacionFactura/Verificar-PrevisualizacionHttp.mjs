import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import net from 'node:net';
import {spawn} from 'node:child_process';
import {parseArgs} from 'node:util';
import {setTimeout as esperar} from 'node:timers/promises';

// Ejecuta previsualizaciones contra una API propia sin paquetes adicionales.
const {values: opciones}=parseArgs({options:{raiz:{type:'string',default:process.cwd()},salida:{type:'string'},puerto:{type:'string',default:'5234'}}});
const raiz=path.resolve(opciones.raiz);
const salida=opciones.salida?path.resolve(opciones.salida):await fs.mkdtemp(path.join(os.tmpdir(),'carniceria-previsualizacion-'));
await fs.mkdir(salida,{recursive:true});
const base='http://127.0.0.1:'+opciones.puerto;
const resultados=[],solicitudes=[];
let proceso,registro='',fallo=null,errorCierre=null;

// Registra una condición observable del cálculo y sus efectos.
function verificar(caso,condicion,detalle={}){resultados.push({caso,aprobado:!!condicion,...detalle});if(!condicion)throw new Error(caso);}

// Reserva temporalmente el puerto para comprobar que no pertenece a otro proceso.
async function puertoLibre(){const servidor=net.createServer();await new Promise((resolver,rechazar)=>{servidor.once('error',rechazar);servidor.listen(Number(opciones.puerto),'127.0.0.1',resolver);});await new Promise(resolver=>servidor.close(resolver));}

// Conserva las respuestas HTTP para revisar los contratos reales.
async function solicitar(metodo,ruta,cuerpo){const respuesta=await fetch(new URL(ruta,base),{method:metodo,headers:cuerpo===undefined?{}:{'Content-Type':'application/json'},body:cuerpo===undefined?undefined:JSON.stringify(cuerpo),redirect:'manual',signal:AbortSignal.timeout(10000)});const texto=await respuesta.text();let json;try{json=JSON.parse(texto);}catch{}const observacion={metodo,ruta,codigo:respuesta.status,cuerpo:json??texto,cabeceras:Object.fromEntries(respuesta.headers)};solicitudes.push(observacion);return observacion;}

// Obtiene una imagen completa del almacén mediante sus colecciones públicas.
async function imagen(){const datos={};for(const recurso of ['clientes','empleados','proveedores','productos','facturas']){const respuesta=await solicitar('GET','/api/'+recurso);if(respuesta.codigo!==200||!Array.isArray(respuesta.cuerpo))throw new Error('No se pudo listar '+recurso);datos[recurso]=respuesta.cuerpo;}return datos;}

// Espera la salud HTTP antes de comenzar el grupo.
async function esperarApi(){const fin=Date.now()+15000;while(Date.now()<fin){try{if((await fetch(base+'/api/estado',{signal:AbortSignal.timeout(1000)})).ok)return;}catch{}await esperar(100);}throw new Error('La API propia no inicio.');}

// Comprueba que la respuesta no anuncie identidad definitiva de factura o detalle.
function verificarSinIdentidad(caso,calculo){verificar(caso+' sin identidad de factura',['id','numero','fechaEmision','estado'].every(campo=>!Object.hasOwn(calculo,campo)));verificar(caso+' sin identidad de detalle',Array.isArray(calculo.detalles)&&calculo.detalles.every(detalle=>!Object.hasOwn(detalle,'id')));}

// Conserva solo los datos calculados comparables entre una vista previa y un alta.
function proyectarCalculo(factura){return {porcentajeDescuento:factura.porcentajeDescuento,subtotal:factura.subtotal,montoDescuento:factura.montoDescuento,baseGravable:factura.baseGravable,montoIva:factura.montoIva,total:factura.total,detalles:factura.detalles.map(({id,...detalle})=>detalle)};}

// Comprueba errores de contrato o disponibilidad con su propiedad exacta.
function verificarError(caso,respuesta,codigo,campo){verificar(caso+' HTTP',respuesta.codigo===codigo,{actual:respuesta.codigo});verificar(caso+' ErrorApi',typeof respuesta.cuerpo?.codigo==='string'&&typeof respuesta.cuerpo?.mensaje==='string'&&respuesta.cuerpo.mensaje.trim().length>0&&typeof respuesta.cuerpo.erroresPorCampo==='object'&&respuesta.cuerpo.erroresPorCampo!==null);if(campo){const claves=Object.keys(respuesta.cuerpo.erroresPorCampo).map(c=>c.toLowerCase());verificar(caso+' campo exacto',claves.includes(campo.toLowerCase())||claves.includes('$.'+campo.toLowerCase()),{claves});}}

try{
 await puertoLibre();
 const carpeta=path.join(raiz,'Carniceria.Api');
 proceso=spawn('dotnet',[path.join(carpeta,'bin','Debug','net10.0','Carniceria.Api.dll'),'--urls',base],{cwd:carpeta,env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development'},windowsHide:true,stdio:['ignore','pipe','pipe']});
 proceso.stdout.on('data',datos=>registro+=datos);proceso.stderr.on('data',datos=>registro+=datos);proceso.on('error',error=>registro+=error.message);
 await esperarApi();
 const inicial=await imagen();verificar('Inicio con una factura semilla',inicial.facturas.length===1&&inicial.facturas[0].numero==='FAC000001');
 const res=inicial.productos.find(p=>p.codigo==='RES001'),pol=inicial.productos.find(p=>p.codigo==='POL001');
 // Las referencias son reales; precios, unidades e importes proceden de API.
 const solicitud={clienteId:inicial.clientes[0].id,empleadoId:inicial.empleados[0].id,metodoPago:1,porcentajeDescuento:10,detalles:[{productoId:res.id,cantidad:2},{productoId:pol.id,cantidad:1}]};
 let esperado;
 for(let indice=0;indice<3;indice++){const respuesta=await solicitar('POST','/api/facturas/calcular',solicitud);verificar('Preview repetido200 '+indice,respuesta.codigo===200);verificar('Preview sin Location '+indice,!respuesta.cabeceras.location);verificarSinIdentidad('Preview repetido '+indice,respuesta.cuerpo);verificar('Preview ejemplo11187 '+indice,respuesta.cuerpo.subtotal===11000&&respuesta.cuerpo.montoDescuento===1100&&respuesta.cuerpo.baseGravable===9900&&respuesta.cuerpo.montoIva===1287&&respuesta.cuerpo.total===11187);if(!esperado)esperado=respuesta.cuerpo;else verificar('Preview repetido determinista '+indice,JSON.stringify(respuesta.cuerpo)===JSON.stringify(esperado));}
 verificar('Preview repetido no cambia ningun recurso',JSON.stringify(await imagen())===JSON.stringify(inicial));
 // Todos los cálculos concurrentes deben ser completos y no persistentes.
 const concurrentes=await Promise.all(Array.from({length:8},()=>solicitar('POST','/api/facturas/calcular',solicitud)));
 for(let indice=0;indice<concurrentes.length;indice++){verificar('Preview concurrente completo '+indice,concurrentes[indice].codigo===200&&JSON.stringify(concurrentes[indice].cuerpo)===JSON.stringify(esperado));verificarSinIdentidad('Preview concurrente '+indice,concurrentes[indice].cuerpo);}
 verificar('Preview concurrente no cambia recursos ni stock',JSON.stringify(await imagen())===JSON.stringify(inicial));
 // Un cálculo inválido debe compartir los rechazos del alta sin dejar efectos.
 const casos=[
 {nombre:'Solicitud nula',cuerpo:null,codigo:400},
 {nombre:'Metodo ausente',cuerpo:Object.fromEntries(Object.entries(solicitud).filter(([campo])=>campo!=='metodoPago')),codigo:400,campo:'MetodoPago'},
 {nombre:'Descuento fuera de rango',cuerpo:{...solicitud,porcentajeDescuento:25.01},codigo:400,campo:'PorcentajeDescuento'},
 {nombre:'Detalle nulo',cuerpo:{...solicitud,detalles:[null]},codigo:400,campo:'Detalles[0]'},
 {nombre:'Producto inexistente',cuerpo:{...solicitud,detalles:[{productoId:'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',cantidad:1}]},codigo:400,campo:'Detalles[0].ProductoId'},
 {nombre:'Cantidad negativa',cuerpo:{...solicitud,detalles:[{productoId:res.id,cantidad:-1}]},codigo:400,campo:'Detalles[0].Cantidad'},
 {nombre:'Demanda duplicada excedida',cuerpo:{...solicitud,detalles:[{productoId:res.id,cantidad:6},{productoId:res.id,cantidad:5}]},codigo:409,campo:'Detalles[0].Cantidad'}
 ];
 for(const caso of casos){const respuesta=await solicitar('POST','/api/facturas/calcular',caso.cuerpo);verificarError(caso.nombre,respuesta,caso.codigo,caso.campo);verificar(caso.nombre+' no cambia estado',JSON.stringify(await imagen())===JSON.stringify(inicial));}
 // Los campos de servidor inventados nunca deben reemplazar el cálculo real.
 const manipulada=await solicitar('POST','/api/facturas/calcular',{...solicitud,id:'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',numero:'SUPLANTADO',fechaEmision:'2000-01-01T00:00:00Z',estado:3,total:0.01,detalles:solicitud.detalles.map(d=>({...d,id:'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',precioUnitario:0.01,unidadMedida:999,subtotalLinea:0.01}))});
 verificar('Preview extras ignorados o rechazados',manipulada.codigo===200||manipulada.codigo===400);
 if(manipulada.codigo===200){verificar('Preview extras no alteran calculo',JSON.stringify(manipulada.cuerpo)===JSON.stringify(esperado));verificarSinIdentidad('Preview con extras',manipulada.cuerpo);}
 verificar('Extras no alteran recursos',JSON.stringify(await imagen())===JSON.stringify(inicial));
 // La siguiente alta confirma que los cálculos no consumieron número definitivo.
 const alta=await solicitar('POST','/api/facturas',solicitud);verificar('Alta posterior sigueFAC2',alta.codigo===201&&alta.cuerpo.numero==='FAC000002');
 verificar('Preview y alta comparten importes y snapshots',JSON.stringify(proyectarCalculo(alta.cuerpo))===JSON.stringify(esperado));
 const trasAlta=await imagen();verificar('Solo el alta agrega una factura',trasAlta.facturas.length===2);
 verificar('Alta pendiente no descuenta stock',JSON.stringify(trasAlta.productos)===JSON.stringify(inicial.productos));
 const fraccionaria={...solicitud,porcentajeDescuento:25,detalles:[{productoId:res.id,cantidad:0.125}]};
 const previewFraccionario=await solicitar('POST','/api/facturas/calcular',fraccionaria);verificar('Preview fraccionario y25 calcula423.75',previewFraccionario.codigo===200&&previewFraccionario.cuerpo.total===423.75&&previewFraccionario.cuerpo.subtotal===500&&previewFraccionario.cuerpo.montoDescuento===125);verificarSinIdentidad('Preview fraccionario',previewFraccionario.cuerpo);
 verificar('Preview tras alta conserva factura e inventario',JSON.stringify(await imagen())===JSON.stringify(trasAlta));
 const segundaAlta=await solicitar('POST','/api/facturas',fraccionaria);verificar('Segunda alta sigueFAC3',segundaAlta.codigo===201&&segundaAlta.cuerpo.numero==='FAC000003');
 verificar('Alta fraccionaria coincide con preview',JSON.stringify(proyectarCalculo(segundaAlta.cuerpo))===JSON.stringify(previewFraccionario.cuerpo));
 verificar('Ninguna respuesta produce500',solicitudes.every(s=>s.codigo<500));
}catch(error){fallo=error;console.error(error.stack);}
finally{
 try{if(proceso&&proceso.exitCode===null&&proceso.signalCode===null){const cierre=new Promise(resolver=>proceso.once('close',resolver));proceso.kill();await Promise.race([cierre,esperar(5000)]);}if(proceso&&proceso.exitCode===null&&proceso.signalCode===null)throw new Error('No se confirmo cierre API propia.');}catch(error){errorCierre=error.message;}
 let libre=false;try{await puertoLibre();libre=true;}catch{}
 await fs.writeFile(path.join(salida,'solicitudes.json'),JSON.stringify(solicitudes,null,2));await fs.writeFile(path.join(salida,'api.log'),registro);
 const resumen={grupo:'PrevisualizacionHttp',total:resultados.length,aprobadas:resultados.filter(r=>r.aprobado).length,fallo:fallo?.message??null,errorCierre,puerto:Number(opciones.puerto),puertoLibre:libre,resultados};
 await fs.writeFile(path.join(salida,'resumen.json'),JSON.stringify(resumen,null,2));
 console.log(JSON.stringify({total:resumen.total,aprobadas:resumen.aprobadas,fallo:resumen.fallo,errorCierre,puertoLibre:libre}));
 if(fallo||errorCierre||!libre)process.exitCode=1;
}

