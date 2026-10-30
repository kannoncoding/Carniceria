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
 const clientes=await listar('clientes'),empleados=await listar('empleados'),productos=await listar('productos');
 const cliente=clientes[0],empleado=empleados[0],res=productos.find(p=>p.codigo==='RES001'),pol=productos.find(p=>p.codigo==='POL001');
 const inicial=await listar('facturas');verificar('Factura semilla única FAC000001',inicial.length===1&&inicial[0].numero==='FAC000001');
 verificar('Factura semilla pendiente con datos calculados',inicial[0]?.estado===1&&inicial[0]?.clienteId===cliente.id&&inicial[0]?.empleadoId===empleado.id&&inicial[0]?.metodoPago===1&&inicial[0]?.porcentajeDescuento===10&&inicial[0]?.subtotal===11000&&inicial[0]?.montoDescuento===1100&&inicial[0]?.baseGravable===9900&&inicial[0]?.montoIva===1287&&inicial[0]?.total===11187);
 verificar('Factura semilla conserva cantidades y referencias',inicial[0]?.detalles?.length===2&&inicial[0].detalles[0].productoId===res.id&&inicial[0].detalles[0].cantidad===2&&inicial[0].detalles[1].productoId===pol.id&&inicial[0].detalles[1].cantidad===1);
 const configuracion=await solicitar('GET','/api/facturas/configuracion');verificar('Ruta configuracion sigue estatica',configuracion.codigo===200&&configuracion.cuerpo.porcentajeDescuentoMaximo===25&&configuracion.cuerpo.porcentajeIva===13);
 // Solicitud base con productos y cantidades; precios e importes pertenecen al servidor.
 const solicitud={clienteId:cliente.id,empleadoId:empleado.id,metodoPago:1,porcentajeDescuento:10,detalles:[{productoId:res.id,cantidad:2},{productoId:pol.id,cantidad:1}]};
 const inicio=Date.now();const primera=await verificarAlta(await solicitar('POST','/api/facturas',solicitud),'Factura posterior a semilla');
 verificar('Ejemplo academico11187 calculado por API',primera.subtotal===11000&&primera.montoDescuento===1100&&primera.baseGravable===9900&&primera.montoIva===1287&&primera.total===11187);
 verificar('Emision actual con offset Costa Rica',/-06:00$/.test(primera.fechaEmision)&&Date.parse(primera.fechaEmision)>=inicio-1000&&Date.parse(primera.fechaEmision)<=Date.now()+1000);
 verificar('Detalle usa precios y unidades del producto',primera.detalles[0].precioUnitario===res.precioUnidadMedida&&primera.detalles[0].unidadMedida===res.unidadMedida&&primera.detalles[1].precioUnitario===pol.precioUnidadMedida);
 verificar('Datos descriptivos cliente empleado y productos',primera.clienteIdentificacion===cliente.identificacion&&primera.empleadoIdentificacion===empleado.identificacion&&primera.detalles[0].productoCodigo===res.codigo&&primera.detalles[1].productoNombre===pol.nombre);
 verificar('Detalle tiene Id distintos no vacios',primera.detalles.every(d=>/^[0-9a-f-]{36}$/i.test(d.id)&&d.id!=='00000000-0000-0000-0000-000000000000')&&new Set(primera.detalles.map(d=>d.id)).size===2);
 const porMinusculas=await solicitar('GET','/api/facturas/'+primera.numero.toLowerCase());verificar('Consulta numero exacto ignora mayusculas',porMinusculas.codigo===200&&porMinusculas.cuerpo.id===primera.id);
 verificarError('Numero inexistente',await solicitar('GET','/api/facturas/FAC999999'),404);
 // Campos extras pueden ignorarse o rechazarse; nunca deben gobernar datos asignados por API.
 const idFalso='aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
 const manipulada={...solicitud,id:idFalso,numero:'SUPLANTADA',fechaEmision:'2000-01-01T00:00:00Z',estado:3,subtotal:0.01,montoDescuento:0,baseGravable:0,montoIva:0,total:0.01,detalles:solicitud.detalles.map(d=>({...d,id:idFalso,precioUnitario:0.01,unidadMedida:999,subtotalLinea:0.01}))};
 const respuestaManipulada=await solicitar('POST','/api/facturas',manipulada);
 verificar('Campos extras se ignoran o rechazan',respuestaManipulada.codigo===201||respuestaManipulada.codigo===400);
 if(respuestaManipulada.codigo===201){const factura=await verificarAlta(respuestaManipulada,'Campos extra ignorados');verificar('Campos servidor no se suplantan',factura.id!==idFalso&&factura.numero!=='SUPLANTADA'&&factura.fechaEmision!=='2000-01-01T00:00:00Z'&&factura.estado===1&&factura.total===11187&&factura.detalles.every(d=>d.id!==idFalso&&d.precioUnitario>0.01&&d.unidadMedida!==999));}
 else verificarError('Campos extra rechazados',respuestaManipulada,400);
 const antesRechazos=await listar('facturas');
 // Casos de enlace JSON y validacion de negocio sobre el pipeline publico.
 const invalidos=[
 {nombre:'JSON malformado',cuerpo:'{"clienteId":',crudo:true,codigo:400},
 {nombre:'Solicitud nula',cuerpo:null,codigo:400},
 {nombre:'Metodo ausente',cuerpo:Object.fromEntries(Object.entries(solicitud).filter(([clave])=>clave!=='metodoPago')),codigo:400,campo:'MetodoPago'},
 {nombre:'Metodo indefinido',cuerpo:{...solicitud,metodoPago:999},codigo:400,campo:'MetodoPago'},
 {nombre:'Cliente inexistente',cuerpo:{...solicitud,clienteId:idFalso},codigo:400,campo:'ClienteId'},
 {nombre:'Guid mal formado',cuerpo:{...solicitud,empleadoId:'no-guid'},codigo:400,campo:'EmpleadoId'},
 {nombre:'Detalles nulos',cuerpo:{...solicitud,detalles:null},codigo:400,campo:'Detalles'},
 {nombre:'Linea nula',cuerpo:{...solicitud,detalles:[null]},codigo:400,campo:'Detalles[0]'},
 {nombre:'Cantidad negativa',cuerpo:{...solicitud,detalles:[{productoId:res.id,cantidad:-0.01}]},codigo:400,campo:'Detalles[0].Cantidad'},
 {nombre:'Producto inexistente',cuerpo:{...solicitud,detalles:[{productoId:idFalso,cantidad:1}]},codigo:400,campo:'Detalles[0].ProductoId'},
 {nombre:'Descuento mayor maximo',cuerpo:{...solicitud,porcentajeDescuento:25.01},codigo:400,campo:'PorcentajeDescuento'},
 {nombre:'Demanda duplicada excede stock',cuerpo:{...solicitud,detalles:[{productoId:res.id,cantidad:6},{productoId:res.id,cantidad:5}]},codigo:409,campo:'Detalles[0].Cantidad'},
 {nombre:'Suma de demanda desbordada',cuerpo:'{"clienteId":"'+cliente.id+'","empleadoId":"'+empleado.id+'","metodoPago":1,"detalles":[{"productoId":"'+res.id+'","cantidad":79228162514264337593543950335},{"productoId":"'+res.id+'","cantidad":1}]}',crudo:true,codigo:400,campo:'Detalles[1].Cantidad'}
 ];
 for(const caso of invalidos){const respuesta=await solicitar('POST','/api/facturas',caso.cuerpo,caso.crudo);verificarError(caso.nombre,respuesta,caso.codigo,caso.campo);verificar(caso.nombre+' no persiste factura',JSON.stringify(await listar('facturas'))===JSON.stringify(antesRechazos));verificar(caso.nombre+' no altera stock',JSON.stringify(await listar('productos'))===JSON.stringify(productos));}
 const adicional=await verificarAlta(await solicitar('POST','/api/facturas',{...solicitud,porcentajeDescuento:25,detalles:[{productoId:res.id,cantidad:0.125}]}),'Factura tras rechazos');
 verificar('Fraccion y descuento25 calculados',adicional.detalles[0].cantidad===0.125&&adicional.subtotal===500&&adicional.montoDescuento===125&&adicional.total===423.75);
 // Altas paralelas deben conservar identidades unicas y no reservar stock pendiente.
 const paralelas=await Promise.all(Array.from({length:8},()=>solicitar('POST','/api/facturas',{...solicitud,porcentajeDescuento:0,detalles:[{productoId:res.id,cantidad:1}]})));
 for(let indice=0;indice<paralelas.length;indice++)await verificarAlta(paralelas[indice],'Alta paralela '+indice);
 const todas=await listar('facturas');verificar('Listado contiene semilla y todas las altas',todas.length===creadas.length+1);
 const numeros=todas.map(f=>f.numero).sort();verificar('Rechazos no consumen numero y concurrencia no duplica',JSON.stringify(numeros)===JSON.stringify(Array.from({length:creadas.length+1},(_,indice)=>'FAC'+String(indice+1).padStart(6,'0'))));
 verificar('Todos los Id de factura son unicos',new Set(todas.map(f=>f.id)).size===todas.length);
 const idsDetalles=todas.flatMap(f=>f.detalles.map(d=>d.id));verificar('Todos los Id de detalle son unicos',new Set(idsDetalles).size===idsDetalles.length);
 const buscarCliente=await solicitar('GET','/api/facturas/buscar?'+new URLSearchParams({criterio:'IdentificacionCliente',consulta:' '+cliente.identificacion+' '}));verificar('Busqueda cliente devuelve semilla y altas',buscarCliente.codigo===200&&buscarCliente.cuerpo.length===creadas.length+1&&buscarCliente.cuerpo.every(f=>f.clienteId===cliente.id));
 const buscarNumero=await solicitar('GET','/api/facturas/buscar?'+new URLSearchParams({criterio:'numero',consulta:' '+primera.numero.toLowerCase()+' '}));verificar('Busqueda numero exacto normalizado',buscarNumero.codigo===200&&buscarNumero.cuerpo.length===1&&buscarNumero.cuerpo[0].id===primera.id);
 const parcial=await solicitar('GET','/api/facturas/buscar?criterio=Numero&consulta=FAC');verificar('Numero parcial no coincide',parcial.codigo===200&&parcial.cuerpo.length===0);
 const sinCoincidencias=await solicitar('GET','/api/facturas/buscar?criterio=IdentificacionCliente&consulta=9-9999-9999');verificar('Busqueda cliente sin coincidencias lista vacia',sinCoincidencias.codigo===200&&sinCoincidencias.cuerpo.length===0);
 verificarError('Criterio ausente',await solicitar('GET','/api/facturas/buscar'),400,'Criterio');
 verificarError('Criterio invalido',await solicitar('GET','/api/facturas/buscar?criterio=Estado&consulta=1'),400,'Criterio');
 verificar('Pendientes no descuentan ni reservan stock',JSON.stringify(await listar('productos'))===JSON.stringify(productos));
 // Las bajas relacionadas deben mantener factura y entidades completas.
 for(const [recurso,id,campo] of [['clientes',cliente.id,'ClienteId'],['empleados',empleado.id,'EmpleadoId'],['productos',res.id,'ProductoId']]){
  const antes=await solicitar('GET','/api/'+recurso+'/'+id);const baja=await solicitar('DELETE','/api/'+recurso+'/'+id);
  verificarError('Baja relacionada '+recurso,baja,409,campo);
  const despues=await solicitar('GET','/api/'+recurso+'/'+id);verificar('Baja bloqueada conserva entidad '+recurso,despues.codigo===200&&JSON.stringify(despues.cuerpo)===JSON.stringify(antes.cuerpo));
  const factura=await solicitar('GET','/api/facturas/'+primera.numero);verificar('Baja bloqueada conserva DTO factura '+recurso,factura.codigo===200&&JSON.stringify(factura.cuerpo)===JSON.stringify(primera));
 }
 // Recursos sin referencias siguen siendo eliminables.
 const auxiliarCliente=await crearAuxiliar('clientes',{tipoIdentificacion:1,identificacion:'9-5757-0001',nombre:'ClienteLibre',primerApellido:'Prueba',segundoApellido:'Mora',fechaNacimiento:'1990-01-02'});
 const auxiliarEmpleado=await crearAuxiliar('empleados',{tipoIdentificacion:1,identificacion:'9-5757-0002',nombre:'EmpleadoLibre',primerApellido:'Prueba',segundoApellido:'Mora',fechaNacimiento:'1990-01-02',salarioMensual:650000.5,fechaIngreso:'2020-01-02',areaTrabajo:2,puesto:'Cajero',turno:1});
 const auxiliarProducto=await crearAuxiliar('productos',{proveedorId:res.proveedorId,codigo:'F5LIBRE',nombre:'ProductoLibre',categoria:1,unidadMedida:1,precioUnidadMedida:1000,existenciaActual:5,existenciaMinima:1});
 for(const [recurso,registroAuxiliar] of [['clientes',auxiliarCliente],['empleados',auxiliarEmpleado],['productos',auxiliarProducto]]){const baja=await solicitar('DELETE','/api/'+recurso+'/'+registroAuxiliar.id);verificar('Baja libre204 '+recurso,baja.codigo===204);const ausente=await solicitar('GET','/api/'+recurso+'/'+registroAuxiliar.id);verificar('Baja libre luego404 '+recurso,ausente.codigo===404);}
 verificar('Ninguna respuesta del recorrido produce500',solicitudes.every(s=>s.codigo<500));
}catch(error){fallo=error;console.error(error.stack);}
finally{
 try{if(proceso&&proceso.exitCode===null&&proceso.signalCode===null){const cierre=new Promise(resolver=>proceso.once('close',resolver));proceso.kill();await Promise.race([cierre,esperar(5000)]);}if(proceso&&proceso.exitCode===null&&proceso.signalCode===null)throw new Error('No se confirmo cierre API propia.');}catch(error){errorCierre=error.message;}
 let libre=false;try{await puertoLibre();libre=true;}catch{}
 await fs.writeFile(path.join(salida,'solicitudes.json'),JSON.stringify(solicitudes,null,2));
 await fs.writeFile(path.join(salida,'api.log'),registro);
 const resumen={grupo:'FacturasHttp',total:resultados.length,aprobadas:resultados.filter(r=>r.aprobado).length,fallo:fallo?.message??null,errorCierre,puerto:Number(opciones.puerto),puertoLibre:libre,facturasCreadas:creadas.length,resultados};
 await fs.writeFile(path.join(salida,'resumen.json'),JSON.stringify(resumen,null,2));
 console.log(JSON.stringify({total:resumen.total,aprobadas:resumen.aprobadas,fallo:resumen.fallo,errorCierre,puertoLibre:libre,facturasCreadas:creadas.length}));
 if(fallo||errorCierre||!libre)process.exitCode=1;
}

