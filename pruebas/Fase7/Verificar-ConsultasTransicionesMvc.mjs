import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import net from 'node:net';
import http from 'node:http';
import {spawn} from 'node:child_process';
import {setTimeout as esperar} from 'node:timers/promises';
import {parseArgs} from 'node:util';

// Opciones de ejecucion para aislar puertos y conservar evidencia fuera de la aplicacion.
const {values: opciones} = parseArgs({options:{raiz:{type:'string',default:process.cwd()},salida:{type:'string'},'puerto-api':{type:'string',default:'5234'},'puerto-web':{type:'string',default:'5235'},'puerto-proxy':{type:'string',default:'5236'}}});
const raiz = path.resolve(opciones.raiz);
const carpetaSalida = opciones.salida ? path.resolve(opciones.salida) : await fs.mkdtemp(path.join(os.tmpdir(),'carniceria-qa-f7-'));
await fs.mkdir(carpetaSalida,{recursive:true});
const baseApi=`http://127.0.0.1:${opciones['puerto-api']}`;
const baseWeb=`http://127.0.0.1:${opciones['puerto-web']}`;
const procesos=[];
const resultados=[];
const cookies=new Map();

// Registra una condicion comprobable y detiene el grupo al primer fallo.
function confirmar(caso,condicion,detalle={}) {resultados.push({caso,aprobado:!!condicion,...detalle});if(!condicion)throw new Error(caso);}

// Conserva los valores de texto y los caracteres escapados del formulario HTML.
function decodificar(texto) {return texto.replace(/&#x([0-9a-f]+);/gi,(_,valor)=>String.fromCodePoint(parseInt(valor,16))).replace(/&#(\d+);/g,(_,valor)=>String.fromCodePoint(Number(valor))).replace(/&quot;/g,'"').replace(/&#39;|&apos;/g,"'").replace(/&lt;/g,'<').replace(/&gt;/g,'>').replace(/&amp;/g,'&');}

// Extrae el token del campo oculto sin depender del orden de sus atributos.
function tokenAntifalsificacion(html) {for(const coincidencia of html.matchAll(/<input\b[^>]*>/gi)){const atributos=Object.fromEntries([...coincidencia[0].matchAll(/([\w:-]+)\s*=\s*(?:"([^"]*)"|'([^']*)')/g)].map(par=>[par[1].toLowerCase(),decodificar(par[2]??par[3])]));if(atributos.name==='__RequestVerificationToken')return atributos.value;}throw new Error('El formulario no contiene token antifalsificacion.');}

// Envia cookies y sigue redirecciones con las mismas reglas de cambio de metodo del navegador.
async function solicitarWeb(ruta,{metodo='GET',formulario,seguir=true}={}) {let url=new URL(ruta,baseWeb);let cuerpo=formulario?new URLSearchParams(formulario).toString():undefined;let metodoActual=metodo;for(let vuelta=0;vuelta<8;vuelta++){const cabeceras={};if(cookies.size)cabeceras.Cookie=[...cookies].map(([nombre,valor])=>`${nombre}=${valor}`).join('; ');if(cuerpo!==undefined)cabeceras['Content-Type']='application/x-www-form-urlencoded';const respuesta=await fetch(url,{method:metodoActual,headers:cabeceras,body:cuerpo,redirect:'manual',signal:AbortSignal.timeout(15000)});for(const cookie of respuesta.headers.getSetCookie()){const par=cookie.split(';',1)[0];const separador=par.indexOf('=');cookies.set(par.slice(0,separador),par.slice(separador+1));}const html=await respuesta.text();const ubicacion=respuesta.headers.get('location');if(seguir&&ubicacion&&[301,302,303,307,308].includes(respuesta.status)){url=new URL(ubicacion,url);if(respuesta.status===303||([301,302].includes(respuesta.status)&&metodoActual==='POST')){metodoActual='GET';cuerpo=undefined;}continue;}return {codigo:respuesta.status,html,url:url.href,ubicacion};}throw new Error('Se excedio el limite de redirecciones.');}

// Obtiene JSON de la API para contrastar el resultado de las acciones MVC.
async function consultarApi(ruta) {const respuesta=await fetch(baseApi+ruta,{signal:AbortSignal.timeout(10000)});if(!respuesta.ok)throw new Error(`API ${ruta} devolvio ${respuesta.status}`);return await respuesta.json();}

// Comprueba que los puertos reservados esten libres antes de crear procesos propios.
async function comprobarPuerto(puerto) {const servidor=net.createServer();await new Promise((resolver,rechazar)=>{servidor.once('error',rechazar);servidor.listen(Number(puerto),'127.0.0.1',resolver);});await new Promise(resolver=>servidor.close(resolver));}

// Inicia una DLL previamente compilada sin ejecutar restauracion ni compilacion.
async function iniciarProyecto(nombre,puerto,entorno={}) {const ejecutable=path.join(raiz,nombre,'bin','Debug','net10.0',`${nombre}.dll`);await fs.access(ejecutable);const proceso=spawn('dotnet',[ejecutable,'--urls',`http://127.0.0.1:${puerto}`],{cwd:path.join(raiz,nombre),env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',...entorno},windowsHide:true,stdio:['ignore','pipe','pipe']});procesos.push(proceso);let salida='',error='';proceso.stdout.on('data',datos=>salida+=datos);proceso.stderr.on('data',datos=>error+=datos);proceso.on('error',fallo=>error+=fallo.message);proceso.on('close',()=>{void fs.writeFile(path.join(carpetaSalida,`${nombre}.log`),salida+'\n'+error);});return proceso;}

// Espera una respuesta HTTP de salud antes de iniciar el recorrido.
async function esperarServicio(url) {for(let intento=0;intento<60;intento++){try{const respuesta=await fetch(url,{signal:AbortSignal.timeout(1000)});if(respuesta.ok)return;}catch{}await esperar(100);}throw new Error(`No inicio ${new URL(url).pathname}`);}

// Cierra solamente los procesos creados por esta ejecucion.
async function cerrarProcesos() {const pendientes=[];for(const proceso of procesos.reverse()){try{if(proceso.exitCode===null&&proceso.signalCode===null){const cierre=new Promise(resolver=>proceso.once('close',resolver));proceso.kill();await Promise.race([cierre,esperar(5000)]);}if(proceso.exitCode===null&&proceso.signalCode===null)pendientes.push(proceso.pid);}catch(error){pendientes.push(proceso.pid);}}if(pendientes.length)throw new Error('No se confirmo el cierre de un proceso propio.');}

// Obtiene una imagen completa de los cuatro modulos para detectar escrituras inesperadas.
async function imagenDatos() {const datos={};for(const recurso of ['clientes','empleados','proveedores','productos'])datos[recurso]=await consultarApi(`/api/${recurso}`);return datos;}

const baseProxy=`http://127.0.0.1:${opciones['puerto-proxy']}`;
const observaciones=[],conexiones=new Set();
let proxy,modo='Normal',fallo=null,errorCierre=null;

// Registra el contrato observable de API sin reutilizar datos de negocio en Web.
async function api(metodo,ruta,cuerpo){const respuesta=await fetch(baseApi+ruta,{method:metodo,headers:cuerpo?{'Content-Type':'application/json'}:{},body:cuerpo?JSON.stringify(cuerpo):undefined,signal:AbortSignal.timeout(5000)});const texto=await respuesta.text();let datos;try{datos=JSON.parse(texto);}catch{}return {codigo:respuesta.status,datos,texto};}

// Incluye facturas en la imagen de datos que deben preservar las consultas.
async function imagenCompleta(){return JSON.stringify({...await imagenDatos(),facturas:await consultarApi('/api/facturas')});}

// Contrasta PRG y el mensaje exacto visible sin repetir la operacion en GET.
async function confirmarPrg(respuesta,ruta,mensaje,caso){confirmar(caso+' redireccion302',respuesta.codigo===302&&new URL(respuesta.ubicacion,baseWeb).pathname===ruta);const primera=await solicitarWeb(respuesta.ubicacion);confirmar(caso+' mensaje exacto',primera.codigo===200&&decodificar(primera.html).includes(mensaje));const antes=await imagenCompleta();const segunda=await solicitarWeb(respuesta.ubicacion);confirmar(caso+' mensaje consumido',segunda.codigo===200&&!decodificar(segunda.html).includes(mensaje));confirmar(caso+' GET no repite mutacion',await imagenCompleta()===antes);}

// Obtiene el token desde la confirmacion y envia solamente el POST MVC.
async function confirmarOperacion(accion,numero){const pagina=await solicitarWeb(`/Facturas/${accion}/${numero}`);confirmar('GET confirmacion '+accion,pagina.codigo===200);return await solicitarWeb(`/Facturas/${accion}Confirmado/${numero}`,{metodo:'POST',formulario:{__RequestVerificationToken:tokenAntifalsificacion(pagina.html)},seguir:false});}

// Crea una factura por API para preparar escenarios sin exigir el formulario pendiente.
async function crearFactura(cantidad=1,descuento=0){const cliente=(await consultarApi('/api/clientes'))[0],empleado=(await consultarApi('/api/empleados'))[0],producto=(await consultarApi('/api/productos'))[0];const alta=await api('POST','/api/facturas',{clienteId:cliente.id,empleadoId:empleado.id,metodoPago:1,porcentajeDescuento:descuento,detalles:[{productoId:producto.id,cantidad}]});confirmar('Fixture factura201',alta.codigo===201);return alta.datos;}

// Reenvia solicitudes a la API y permite fallos controlados solo en rutas de facturas.
async function iniciarProxy(){proxy=http.createServer(async(entrada,respuesta)=>{try{const url=new URL(entrada.url,baseApi),partes=[];for await(const parte of entrada)partes.push(parte);const cuerpo=Buffer.concat(partes);const evento={metodo:entrada.method,ruta:url.pathname,consulta:[...url.searchParams],url:entrada.url,modo};observaciones.push(evento);const lectura=entrada.method==='GET',facturas=url.pathname.startsWith('/api/facturas');
 if(facturas&&lectura&&modo==='Corte'){entrada.socket.destroy();return;}
 if(facturas&&lectura&&modo==='Demora'){return;}
 if(facturas&&lectura&&modo==='JsonInvalido'){respuesta.writeHead(200,{'Content-Type':'application/json'});respuesta.end('{mal');return;}
 if(facturas&&lectura&&modo==='Error500'){respuesta.writeHead(500,{'Content-Type':'application/json'});respuesta.end('{"codigo":"ErrorInterno","mensaje":"Fallo controlado","erroresPorCampo":{}}');return;}
 if(facturas&&!lectura&&(modo==='Error400'||modo==='Error409'||modo==='ErrorJson')){const codigo=modo==='Error409'?409:400;evento.codigo=codigo;respuesta.writeHead(codigo,{'Content-Type':'application/json'});respuesta.end(modo==='ErrorJson'?'{mal':JSON.stringify({codigo:'ValidacionFallida',mensaje:'Rechazo de factura visible',erroresPorCampo:{Estado:['Estado rechazado por API']}}));return;}
 const real=await fetch(url,{method:entrada.method,headers:cuerpo.length?{'Content-Type':entrada.headers['content-type']??'application/json'}:{},body:cuerpo.length?cuerpo:undefined,signal:AbortSignal.timeout(5000)});let texto=await real.text();evento.codigo=real.status;
 if(facturas&&lectura&&modo==='EstadoInvalido'){const datos=JSON.parse(texto);if(Array.isArray(datos))datos[0].estado=999;else datos.estado=999;texto=JSON.stringify(datos);}
 if(facturas&&!lectura&&modo==='PerderPago'&&url.pathname.endsWith('/pagar')){evento.respuestaOculta=true;return;}
 respuesta.writeHead(real.status,{'Content-Type':real.headers.get('content-type')??'application/json'});respuesta.end(texto);
 }catch(error){observaciones.push({error:error.message});if(!respuesta.destroyed)respuesta.destroy();}});proxy.on('connection',conexion=>{conexiones.add(conexion);conexion.on('close',()=>conexiones.delete(conexion));});await new Promise((resolver,rechazar)=>{proxy.once('error',rechazar);proxy.listen(Number(opciones['puerto-proxy']),'127.0.0.1',resolver);});}

try{
 for(const puerto of [opciones['puerto-api'],opciones['puerto-web'],opciones['puerto-proxy']])await comprobarPuerto(puerto);
 await iniciarProyecto('Carniceria.Api',opciones['puerto-api']);await esperarServicio(baseApi+'/api/estado');await iniciarProxy();await iniciarProyecto('Carniceria.Web',opciones['puerto-web'],{Api__DireccionBase:baseProxy+'/'});await esperarServicio(baseWeb+'/');
 const inicial=await imagenCompleta(),semilla=(await consultarApi('/api/facturas'))[0];
 for(const modulo of ['Clientes','Empleados','Proveedores','Productos','Facturas']){const pagina=await solicitarWeb('/'+modulo);confirmar('Listado y ruta '+modulo,pagina.codigo===200);confirmar('Menu Facturas en '+modulo,/href="\/Facturas"/.test(pagina.html));}
 const detalle=await solicitarWeb('/Facturas/Detalles/'+semilla.numero);confirmar('Detalles identifica factura',detalle.codigo===200&&detalle.html.includes(semilla.numero));confirmar('Moneda local y total11187',decodificar(detalle.html).replace(/\s/g,'').includes('₡11187,00'));
 for(const ruta of ['/Facturas','/Facturas/Detalles/'+semilla.numero,'/Facturas/Buscar','/Facturas/Buscar?criterio=Numero&consulta='+semilla.numero,'/Facturas/Pagar/'+semilla.numero,'/Facturas/Eliminar/'+semilla.numero]){const pagina=await solicitarWeb(ruta);confirmar('GET200 '+ruta,pagina.codigo===200);confirmar('GET sin mutacion '+ruta,await imagenCompleta()===inicial);}
 const confirmacion=await solicitarWeb('/Facturas/Pagar/'+semilla.numero),token=tokenAntifalsificacion(confirmacion.html);confirmar('Pago detalla cantidades antes de confirmar',decodificar(confirmacion.html).includes('RES001')&&decodificar(confirmacion.html).includes('POL001')&&confirmacion.html.includes('<table>'));
 const cancelar=[...confirmacion.html.matchAll(/<a\b([^>]*)>([\s\S]*?)<\/a>/gi)].find(a=>decodificar(a[2]).trim()==='Cancelar');const destino=cancelar&&/href="([^"]+)"/.exec(cancelar[1])?.[1];confirmar('Cancelar lleva a factura identificada',destino==='/Facturas/Detalles/'+semilla.numero);await solicitarWeb(destino);confirmar('Cancelar no muta',await imagenCompleta()===inicial);
 for(const accion of ['Pagar','Anular','Eliminar']){const rechazo=await solicitarWeb(`/Facturas/${accion}Confirmado/${semilla.numero}`,{metodo:'POST',formulario:{},seguir:false});confirmar('Sin antiforgery400 '+accion,rechazo.codigo===400);}confirmar('POST sin token no muta',await imagenCompleta()===inicial);
 const nada=await solicitarWeb('/Facturas/Buscar?criterio=Numero&consulta=FAC999999');confirmar('Busqueda vacia explicita',nada.codigo===200&&decodificar(nada.html).includes('No se encontraron facturas para el criterio ingresado.'));
 const especial='A&B+C#% ñ &criterio=Otro',antesConsulta=observaciones.length;await solicitarWeb('/Facturas/Buscar?'+new URLSearchParams({criterio:'Numero',consulta:especial}));const enviada=observaciones.slice(antesConsulta).find(o=>o.ruta==='/api/facturas/buscar');const parametros=new URLSearchParams(enviada?.consulta);confirmar('Busqueda conserva texto codificado',parametros.get('consulta')===especial);confirmar('Busqueda no inyecta criterio',parametros.getAll('criterio').length===1&&parametros.get('criterio')==='Numero');
 const ausente=await solicitarWeb('/Facturas/Detalles/FAC999999');confirmar('Numero ausente404',ausente.codigo===404);
 const fraccion=await crearFactura(0.00001,0.00001);for(const accion of ['Detalles','Pagar']){const pagina=await solicitarWeb(`/Facturas/${accion}/${fraccion.numero}`);confirmar('Cantidad pequena completa '+accion,pagina.codigo===200&&decodificar(pagina.html).includes('0,00001'));if(accion==='Detalles')confirmar('Descuento pequeno completo',decodificar(pagina.html).includes('0,00001%'));}
 // Los rechazos de estado mantienen confirmacion identificable sin falso exito.
 const pago=await solicitarWeb(`/Facturas/PagarConfirmado/${semilla.numero}`,{metodo:'POST',formulario:{__RequestVerificationToken:token},seguir:false});await confirmarPrg(pago,'/Facturas/Detalles/'+semilla.numero,`El pago de la factura ${semilla.numero} se confirmó correctamente.`,'Pago MVC');confirmar('API confirma Pagada',(await consultarApi('/api/facturas/'+semilla.numero)).estado===2);
 const previo=await imagenCompleta(),repetido=await solicitarWeb(`/Facturas/PagarConfirmado/${semilla.numero}`,{metodo:'POST',formulario:{__RequestVerificationToken:token},seguir:false});confirmar('409 pago conserva confirmacion',repetido.codigo===200&&!repetido.ubicacion&&repetido.html.includes(semilla.numero)&&/validation-summary-errors/.test(repetido.html));confirmar('409 no cambia datos',await imagenCompleta()===previo);
 const anulacion=await confirmarOperacion('Anular',semilla.numero);await confirmarPrg(anulacion,'/Facturas/Detalles/'+semilla.numero,`La factura ${semilla.numero} se anuló correctamente.`,'Anulacion MVC');confirmar('API confirma Anulada',(await consultarApi('/api/facturas/'+semilla.numero)).estado===3);
 const eliminar=await confirmarOperacion('Eliminar',fraccion.numero);await confirmarPrg(eliminar,'/Facturas',`La factura ${fraccion.numero} se eliminó correctamente.`,'Baja MVC');confirmar('Baja queda ausente API',(await api('GET','/api/facturas/'+fraccion.numero)).codigo===404);
 // Las respuestas invalidas y fallos de comunicacion nunca se muestran como colecciones vacias.
 for(const falloLectura of ['JsonInvalido','EstadoInvalido','Error500','Corte','Demora']){modo=falloLectura;const inicio=Date.now(),pagina=await solicitarWeb('/Facturas');confirmar('Fallo lectura controlado '+modo,pagina.codigo===503&&!pagina.html.includes('Todavia no hay facturas registradas.'));if(modo==='Demora')confirmar('Timeout acotado',Date.now()-inicio<8000);modo='Normal';}
 const errores=await crearFactura();for(const tipo of ['Error400','Error409']){modo=tipo;const pagina=await confirmarOperacion('Pagar',errores.numero);confirmar('ErrorApi visible '+tipo,pagina.codigo===200&&!pagina.ubicacion&&decodificar(pagina.html).includes('Rechazo de factura visible')&&decodificar(pagina.html).includes('Estado rechazado por API'));modo='Normal';confirmar('Rechazo API no paga '+tipo,(await consultarApi('/api/facturas/'+errores.numero)).estado===1);}
 modo='ErrorJson';const errorJson=await confirmarOperacion('Pagar',errores.numero);confirmar('ErrorApi JSON invalido controlado',errorJson.codigo===200&&!errorJson.ubicacion&&decodificar(errorJson.html).includes('La API rechazó la operación.'));await fs.writeFile(path.join(carpetaSalida,'error-json.html'),errorJson.html);modo='Normal';
 // El pago aplicado con respuesta perdida se confirma por GET sin segundo POST automatico.
 const incierta=await crearFactura(),inicioPerdida=observaciones.length;modo='PerderPago';const perdido=await confirmarOperacion('Pagar',incierta.numero);modo='Normal';const trafico=observaciones.slice(inicioPerdida),mutaciones=trafico.filter(o=>o.metodo==='POST'&&o.ruta===`/api/facturas/${incierta.numero}/pagar`),indice=trafico.findIndex(o=>o.respuestaOculta);confirmar('Respuesta perdida un unico pago aplicado',mutaciones.length===1&&mutaciones[0].codigo===200);confirmar('Tras perdida consulta estado antes de resultado',indice>=0&&trafico.slice(indice+1).some(o=>o.metodo==='GET'&&o.ruta===`/api/facturas/${incierta.numero}`));await confirmarPrg(perdido,'/Facturas/Detalles/'+incierta.numero,`Se verificó que el pago de la factura ${incierta.numero} fue aplicado.`,'Pago con respuesta perdida');confirmar('API conserva Pagada tras perdida',(await consultarApi('/api/facturas/'+incierta.numero)).estado===2);
 confirmar('No se ofrecen formularios pendientes',!(await solicitarWeb('/Facturas')).html.match(/href="\/Facturas\/(Crear|Editar)/));
}catch(error){fallo=error;console.error(error.stack);}
finally{
 try{for(const conexion of conexiones)conexion.destroy();if(proxy?.listening)await new Promise(resolver=>proxy.close(resolver));}catch(error){errorCierre='Proxy: '+error.message;}
 try{await cerrarProcesos();}catch(error){errorCierre=(errorCierre?errorCierre+'; ':'')+error.message;}
 const puertos=[];for(const puerto of [opciones['puerto-api'],opciones['puerto-web'],opciones['puerto-proxy']]){try{await comprobarPuerto(puerto);puertos.push({puerto:Number(puerto),libre:true});}catch{puertos.push({puerto:Number(puerto),libre:false});}}
 await fs.writeFile(path.join(carpetaSalida,'proxy.json'),JSON.stringify(observaciones,null,2));const resumen={grupo:'ConsultasYTransicionesMvc',total:resultados.length,aprobadas:resultados.filter(resultado=>resultado.aprobado).length,fallo:fallo?.message??null,errorCierre,puertos,resultados};await fs.writeFile(path.join(carpetaSalida,'resumen.json'),JSON.stringify(resumen,null,2));console.log(JSON.stringify({total:resumen.total,aprobadas:resumen.aprobadas,fallo:resumen.fallo,errorCierre,puertos}));if(fallo||errorCierre||puertos.some(puerto=>!puerto.libre))process.exitCode=1;
}
