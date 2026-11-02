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
const carpetaSalida = opciones.salida ? path.resolve(opciones.salida) : await fs.mkdtemp(path.join(os.tmpdir(),'carniceria-qa-f8-'));
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


// Lee los atributos HTML sin depender de su orden ni del tipo de comillas.
function atributos(texto){return Object.fromEntries([...texto.matchAll(/([\w:-]+)\s*=\s*(?:"([^"]*)"|'([^']*)')/g)].map(par=>[par[1].toLowerCase(),decodificar(par[2]??par[3])]));}

// Conserva campos repetidos y valores seleccionados, incluidos los indices de detalle.
function camposFormulario(html){const campos=[];for(const coincidencia of html.matchAll(/<input\b([^>]*)>/gi)){const control=atributos(coincidencia[1]),tipo=(control.type??'text').toLowerCase();if(!control.name||['submit','button','reset'].includes(tipo)||/\bdisabled\b/i.test(coincidencia[1]))continue;if(['checkbox','radio'].includes(tipo)&&!/\bchecked\b/i.test(coincidencia[1]))continue;campos.push([control.name,control.value??'']);}for(const coincidencia of html.matchAll(/<select\b([^>]*)>([\s\S]*?)<\/select>/gi)){const control=atributos(coincidencia[1]);if(!control.name)continue;const opciones=[...coincidencia[2].matchAll(/<option\b([^>]*)>([\s\S]*?)<\/option>/gi)];const elegida=opciones.find(opcion=>/\bselected\b/i.test(opcion[1]))??opciones[0];campos.push([control.name,elegida?(atributos(elegida[1]).value??decodificar(elegida[2])):'']);}return campos;}

// Reemplaza campos especificos conservando las demas filas y claves repetidas.
function cambiarCampos(campos,cambios){const nombres=new Set(Object.keys(cambios));return [...campos.filter(([nombre])=>!nombres.has(nombre)),...Object.entries(cambios).flatMap(([nombre,valor])=>(Array.isArray(valor)?valor:[valor]).map(parte=>[nombre,String(parte)]))];}

// Recupera las claves estables del enlazador, separadas de los Id del negocio.
function clavesDetalles(campos){return campos.filter(([nombre])=>nombre==='Detalles.Index').map(([,valor])=>valor);}const baseProxy=`http://127.0.0.1:${opciones['puerto-proxy']}`;
const trafico=[],conexiones=new Set();
let proxy,modo='Normal',fallo=null,errorCierre=null;




// Captura los documentos y productos completos para detectar escrituras inesperadas.
async function imagen(){return JSON.stringify({facturas:await consultarApi('/api/facturas'),productos:await consultarApi('/api/productos')});}

// Recupera un valor sin perder los nombres repetidos de otros campos.
function valor(campos,nombre){return campos.find(([campo])=>campo===nombre)?.[1];}

// Envia una accion explicita conservando el token y todas las filas del formulario.
async function enviar(ruta,campos,cambios={}){const enviados=cambiarCampos(campos,cambios);const respuesta=await solicitarWeb(ruta,{metodo:'POST',formulario:enviados,seguir:false});await fs.writeFile(path.join(carpetaSalida,'ultimo-post.json'),JSON.stringify({ruta,campos:enviados,codigo:respuesta.codigo},null,2));await fs.writeFile(path.join(carpetaSalida,'ultimo-post.html'),respuesta.html);return respuesta;}




// Observa los cuerpos JSON enviados desde MVC y simula errores por ordinal de fila.
async function iniciarProxy(){proxy=http.createServer(async(entrada,respuesta)=>{try{const partes=[];for await(const parte of entrada)partes.push(parte);const cuerpo=Buffer.concat(partes),url=new URL(entrada.url,baseApi);let datos;try{datos=JSON.parse(cuerpo.toString());}catch{}const evento={metodo:entrada.method,ruta:url.pathname,cuerpo:datos,modo};trafico.push(evento);
 if(modo==='DemoraCalculo'&&entrada.method==='POST'&&url.pathname.endsWith('/calcular'))return;
 if(modo==='GuardadoAjeno'&&((entrada.method==='POST'&&url.pathname==='/api/facturas')||entrada.method==='PUT')){const consulta=await fetch(entrada.method==='POST'?baseApi+'/api/facturas':url,{signal:AbortSignal.timeout(5000)});let factura=await consulta.json();if(Array.isArray(factura))factura=factura[0];else factura.detalles[0].cantidad+=1;respuesta.writeHead(entrada.method==='POST'?201:200,{'Content-Type':'application/json'});respuesta.end(JSON.stringify(factura));return;}
 if(modo==='ErrorFila'&&entrada.method==='PUT'&&url.pathname.startsWith('/api/facturas/')){respuesta.writeHead(400,{'Content-Type':'application/json'});respuesta.end(JSON.stringify({codigo:'ValidacionFallida',mensaje:'Fallo de fila',erroresPorCampo:{'$.detalles[1].cantidad':['Cantidad rechazada en fila dos']}}));return;}
 if(modo==='ListaVacia'&&entrada.method==='GET'&&url.pathname==='/api/facturas'){respuesta.writeHead(200,{'Content-Type':'application/json'});respuesta.end('[]');return;} const real=await fetch(url,{method:entrada.method,headers:cuerpo.length?{'Content-Type':entrada.headers['content-type']??'application/json'}:{},body:cuerpo.length?cuerpo:undefined,signal:AbortSignal.timeout(5000)});let texto=await real.text();evento.codigo=real.status;
 if(modo==='CalculoNoCorresponde'&&url.pathname.endsWith('/calcular')&&real.status===200){const calculo=JSON.parse(texto);calculo.detalles[0].productoId='aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';texto=JSON.stringify(calculo);}
 respuesta.writeHead(real.status,{'Content-Type':real.headers.get('content-type')??'application/json'});respuesta.end(texto);}catch(error){trafico.push({error:error.message});respuesta.destroy();}});proxy.on('connection',conexion=>{conexiones.add(conexion);conexion.on('close',()=>conexiones.delete(conexion));});await new Promise((resolver,rechazar)=>{proxy.once('error',rechazar);proxy.listen(Number(opciones['puerto-proxy']),'127.0.0.1',resolver);});}

try{
 for(const puerto of [opciones['puerto-api'],opciones['puerto-web'],opciones['puerto-proxy']])await comprobarPuerto(puerto);
 let procesoApi=await iniciarProyecto('Carniceria.Api',opciones['puerto-api']);await esperarServicio(baseApi+'/api/estado');await iniciarProxy();await iniciarProyecto('Carniceria.Web',opciones['puerto-web'],{Api__DireccionBase:baseProxy+'/'});await esperarServicio(baseWeb+'/');
 const cliente=(await consultarApi('/api/clientes'))[0],empleado=(await consultarApi('/api/empleados'))[0],producto=(await consultarApi('/api/productos'))[0];
 let pagina=await solicitarWeb('/Facturas/Crear'),campos=camposFormulario(pagina.html),clave=clavesDetalles(campos)[0];campos=cambiarCampos(campos,{ClienteId:cliente.id,EmpleadoId:empleado.id,MetodoPago:'1',PorcentajeDescuento:'0',[`Detalles[${clave}].ProductoId`]:producto.id,[`Detalles[${clave}].Cantidad`]:'1,25'});
 const creada=await enviar('/Facturas/Crear',campos,{accion:'guardar'});confirmar('Fixture alta redirige',creada.codigo===302);const numero=new URL(creada.ubicacion,baseWeb).pathname.split('/').pop();confirmar('Fixture FAC2',numero==='FAC000002');
 const edicion=await solicitarWeb('/Facturas/Editar/'+numero),camposEditar=cambiarCampos(camposFormulario(edicion.html),{ActualizarPreciosYUnidades:'true'});
 modo='ListaVacia';pagina=await solicitarWeb('/Facturas');confirmar('Lista vacia controlada',pagina.codigo===200&&/no hay facturas/i.test(decodificar(pagina.html)));modo='Normal';
 async function detener(){const cierre=new Promise(resolver=>procesoApi.once('close',resolver));procesoApi.kill();await cierre;}
 await detener();pagina=await enviar('/Facturas/Crear',campos,{accion:'guardar'});confirmar('API apagada conserva200',pagina.codigo===200&&!pagina.ubicacion);let retenidos=camposFormulario(pagina.html);confirmar('API apagada conserva cabecera',valor(retenidos,'ClienteId')===cliente.id&&valor(retenidos,'MetodoPago')==='1');confirmar('API apagada conserva fila',valor(retenidos,`Detalles[${clave}].Cantidad`)==='1,25');confirmar('API apagada informa resultado incierto',decodificar(pagina.html).includes('No se pudo confirmar'));
 procesoApi=await iniciarProyecto('Carniceria.Api',opciones['puerto-api']);await esperarServicio(baseApi+'/api/estado');const antes=await imagen();
 pagina=await enviar('/Facturas/Crear',campos,{accion:'guardar'});confirmar('Crear obsoleto muestra error sin redirigir',pagina.codigo===200&&!pagina.ubicacion&&/validation-summary-errors/.test(pagina.html));retenidos=camposFormulario(pagina.html);confirmar('Crear obsoleto conserva IDs y cantidad',valor(retenidos,'ClienteId')===cliente.id&&valor(retenidos,`Detalles[${clave}].ProductoId`)===producto.id&&valor(retenidos,`Detalles[${clave}].Cantidad`)==='1,25');confirmar('Crear obsoleto no muta',await imagen()===antes);
 pagina=await enviar('/Facturas/Editar/'+numero,camposEditar,{accion:'guardar'});confirmar('Editar obsoleto404 controlado',pagina.codigo===404&&!pagina.ubicacion);confirmar('Editar obsoleto no sobrescribe semilla',await imagen()===antes);
 const actuales=(await consultarApi('/api/clientes'))[0];confirmar('Reinicio renueva IDs',actuales.id!==cliente.id);
}catch(error){fallo=error;console.error(error.stack);}
finally{
 try{for(const conexion of conexiones)conexion.destroy();if(proxy?.listening)await new Promise(resolver=>proxy.close(resolver));}catch(error){errorCierre='Proxy: '+error.message;}try{await cerrarProcesos();}catch(error){errorCierre=(errorCierre?errorCierre+'; ':'')+error.message;}
 const puertos=[];for(const puerto of [opciones['puerto-api'],opciones['puerto-web'],opciones['puerto-proxy']]){try{await comprobarPuerto(puerto);puertos.push({puerto:Number(puerto),libre:true});}catch{puertos.push({puerto:Number(puerto),libre:false});}}await fs.writeFile(path.join(carpetaSalida,'trafico-api.json'),JSON.stringify(trafico,null,2));const resumen={grupo:'FallosReinicioMvc',total:resultados.length,aprobadas:resultados.filter(resultado=>resultado.aprobado).length,fallo:fallo?.message??null,errorCierre,puertos,resultados};await fs.writeFile(path.join(carpetaSalida,'resumen.json'),JSON.stringify(resumen,null,2));console.log(JSON.stringify({total:resumen.total,aprobadas:resumen.aprobadas,fallo:resumen.fallo,errorCierre,puertos}));if(fallo||errorCierre||puertos.some(puerto=>!puerto.libre))process.exitCode=1;
}
