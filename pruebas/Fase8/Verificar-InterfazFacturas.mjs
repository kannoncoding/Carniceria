import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import net from 'node:net';
import http from 'node:http';
import {spawn} from 'node:child_process';
import {parseArgs} from 'node:util';
import {setTimeout as esperar} from 'node:timers/promises';

// Reserva puertos separados y una carpeta de evidencia indicada por quien ejecuta.
const {values: opciones}=parseArgs({options:{raiz:{type:'string',default:process.cwd()},salida:{type:'string'},navegador:{type:'string'},'puerto-api':{type:'string',default:'5234'},'puerto-web':{type:'string',default:'5235'},'puerto-proxy':{type:'string',default:'5236'},'puerto-depuracion':{type:'string',default:'5237'}}});
const raiz=path.resolve(opciones.raiz);
const salida=opciones.salida?path.resolve(opciones.salida):await fs.mkdtemp(path.join(os.tmpdir(),'carniceria-interfaz-evidencia-'));
await fs.mkdir(salida,{recursive:true});
const baseApi='http://127.0.0.1:'+opciones['puerto-api'],baseWeb='http://127.0.0.1:'+opciones['puerto-web'];
const procesos=[],resultados=[],solicitudes=[],solicitudesNavegador=[],excepciones=[],consola=[];
let proxy,perfil,navegador,cdpPagina,cdpNavegador,fallo=null;
let navegaciones=0;

// Registra cada condicion observable y detiene el recorrido si falla.
function confirmar(caso,condicion,detalle={}){resultados.push({caso,aprobado:!!condicion,...detalle});if(!condicion)throw new Error(caso);}

// Espera una condicion observable con un limite de tiempo.
async function hasta(condicion,descripcion,limite=15000){const fin=Date.now()+limite;while(Date.now()<fin){try{const valor=await condicion();if(valor)return valor;}catch{}await esperar(100);}throw new Error('No se cumplio: '+descripcion);}

// Comprueba que un puerto este libre antes de iniciar un servicio propio.
async function puertoLibre(puerto){const servidor=net.createServer();await new Promise((resolver,rechazar)=>{servidor.once('error',rechazar);servidor.listen(Number(puerto),'127.0.0.1',resolver);});await new Promise(resolver=>servidor.close(resolver));}

// Conserva los registros de un proceso y sus senales de salida.
function iniciarProceso(nombre,comando,argumentos,configuracion={}){const proceso=spawn(comando,argumentos,{windowsHide:true,stdio:['ignore','pipe','pipe'],...configuracion});procesos.push(proceso);let registro='';proceso.stdout.on('data',datos=>registro+=datos);proceso.stderr.on('data',datos=>registro+=datos);proceso.on('error',error=>registro+=error.message);proceso.on('close',()=>{void fs.writeFile(path.join(salida,nombre+'.log'),registro);});return proceso;}

// Inicia una DLL existente sin restaurar ni compilar.
function iniciarProyecto(nombre,puerto,entorno={}){return iniciarProceso(nombre,'dotnet',[path.join(raiz,nombre,'bin','Debug','net10.0',nombre+'.dll'),'--urls','http://127.0.0.1:'+puerto],{cwd:path.join(raiz,nombre),env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',...entorno}});}

// Transporta comandos CDP y distribuye eventos sin bibliotecas externas.
class ConexionCdp{
 // Mantiene respuestas pendientes y observadores de eventos de esta conexion.
 constructor(socket){this.socket=socket;this.contador=0;this.pendientes=new Map();this.observadores=[];socket.addEventListener('message',evento=>{const mensaje=JSON.parse(evento.data);if(mensaje.id){const pendiente=this.pendientes.get(mensaje.id);if(pendiente){this.pendientes.delete(mensaje.id);clearTimeout(pendiente.temporizador);mensaje.error?pendiente.rechazar(new Error(JSON.stringify(mensaje.error))):pendiente.resolver(mensaje.result);}}else{for(const observar of this.observadores)observar(mensaje);}});}
 // Abre una conexion local al navegador o a una pagina.
 static async abrir(url){const socket=new WebSocket(url);await new Promise((resolver,rechazar)=>{socket.addEventListener('open',resolver,{once:true});socket.addEventListener('error',rechazar,{once:true});});return new ConexionCdp(socket);}
 // Envia un comando con un plazo para evitar esperas indefinidas.
 async enviar(method,params={}){const id=++this.contador;return await new Promise((resolver,rechazar)=>{const temporizador=setTimeout(()=>{this.pendientes.delete(id);rechazar(new Error('Tiempo CDP '+method));},15000);this.pendientes.set(id,{resolver,rechazar,temporizador});this.socket.send(JSON.stringify({id,method,params}));});}
 // Cierra el canal y cancela los plazos pendientes.
 cerrar(){for(const pendiente of this.pendientes.values()){clearTimeout(pendiente.temporizador);pendiente.rechazar(new Error('Conexion cerrada'));}this.pendientes.clear();this.socket.close();}
}

// Obtiene valores serializables del contexto de la pagina.
async function evaluar(expression){const respuesta=await cdpPagina.enviar('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(respuesta.exceptionDetails)throw new Error(JSON.stringify(respuesta.exceptionDetails));return respuesta.result.value;}

// Navega y espera que el documento nuevo este completamente cargado.
async function navegar(ruta){const anterior=navegaciones;await cdpPagina.enviar('Page.navigate',{url:baseWeb+ruta});await hasta(async()=>navegaciones>anterior&&await evaluar("document.readyState==='complete'"),'documento '+ruta);}

// Completa campos y dispara eventos reales del DOM, incluidos cambio y perdida de foco.
async function completar(valores){return await evaluar('(()=>{const valores='+JSON.stringify(valores)+';for(const [nombre,valor] of Object.entries(valores)){const campo=document.querySelector(\'[name="\'+nombre+\'"]\');if(!campo)throw new Error("Campo ausente "+nombre);if(campo.type==="radio"){const radio=[...document.querySelectorAll(\'[name="\'+nombre+\'"]\')].find(opcion=>opcion.value===valor);radio.click();}else{campo.focus();campo.value=valor;campo.dispatchEvent(new Event("input",{bubbles:true}));campo.dispatchEvent(new Event("change",{bubbles:true}));campo.blur();}}return true;})()');}

// Envia el formulario mediante su boton y espera la navegacion de la respuesta.
async function enviar(){const anterior=navegaciones;confirmar('Formulario contiene token antifalsificacion',await evaluar("!!document.querySelector('input[name=__RequestVerificationToken]')?.value"));await evaluar("document.querySelector('button[type=submit]').click()");await hasta(async()=>navegaciones>anterior&&await evaluar("document.readyState==='complete'"),'respuesta del formulario');}

// Obtiene los registros de la API para contrastar los valores enviados por el navegador.
async function datos(recurso){const respuesta=await fetch(baseApi+'/api/'+recurso,{signal:AbortSignal.timeout(5000)});if(!respuesta.ok)throw new Error('Consulta API '+respuesta.status);return await respuesta.json();}

// Captura el documento y una imagen de la pagina como evidencia del estado observado.
async function capturar(nombre){await fs.writeFile(path.join(salida,nombre+'.html'),await evaluar('document.documentElement.outerHTML'));const imagen=await cdpPagina.enviar('Page.captureScreenshot',{format:'png',captureBeyondViewport:true});await fs.writeFile(path.join(salida,nombre+'.png'),Buffer.from(imagen.data,'base64'));}

// Pulsa una accion MVC y espera la nueva pagina.
async function pulsar(selector){const anterior=navegaciones;await evaluar('document.querySelector('+JSON.stringify(selector)+').click()');await hasta(async()=>navegaciones>anterior&&await evaluar("document.readyState==='complete'"),'respuesta formulario');}
// Revisa las paginas de factura con controles y presentacion reales.
async function probarFacturas(){
 await cdpPagina.enviar('Emulation.setDeviceMetricsOverride',{width:1440,height:1000,deviceScaleFactor:1,mobile:false});await navegar('/Facturas');confirmar('Listado semilla',await evaluar("document.body.innerText.includes('FAC000001')"));await capturar('listado-escritorio');
 await navegar('/Facturas/Detalles/FAC000001');confirmar('Detalle CRC11187',await evaluar("document.body.innerText.replace(/\\s/g,'').includes('₡11187,00')"));confirmar('Fecha visible',await evaluar("/\\d{2}\\/\\d{2}\\/\\d{4}/.test(document.body.innerText)"));
 const cliente=(await datos('clientes'))[0],empleado=(await datos('empleados'))[0],producto=(await datos('productos'))[0];await navegar('/Facturas/Crear');const clave=await evaluar("document.querySelector('[name=\"Detalles.Index\"]').value");const cantidad=`Detalles[${clave}].Cantidad`;await completar({ClienteId:cliente.id,EmpleadoId:empleado.id,MetodoPago:'1',PorcentajeDescuento:'10',[`Detalles[${clave}].ProductoId`]:producto.id,[cantidad]:'0,5'});confirmar('Etiquetas asociadas',await evaluar("[...document.querySelectorAll('#formulario-factura label[for]')].every(etiqueta=>document.getElementById(etiqueta.htmlFor))"));await pulsar('button[name=accion][value=calcular]');confirmar('Calculo API visible',await evaluar("!!document.querySelector('#resumen-calculo')"));confirmar('Precio CRC visible',await evaluar("[...document.querySelectorAll('.valores-linea')].some(elemento=>elemento.innerText.includes('₡'))"));await capturar('crear-calculo');await completar({[cantidad]:'cantidad_invalida'});confirmar('JS limpia calculo previo',await evaluar("!document.querySelector('#resumen-calculo')"));await pulsar('button[name=accion][value=guardar]');confirmar('Error visible conserva entrada',await evaluar('document.querySelector('+JSON.stringify('[name="'+cantidad+'"]')+').value==="cantidad_invalida"&&!!document.querySelector(".validation-summary-errors")'));await capturar('crear-error');
 await cdpPagina.enviar('Emulation.setDeviceMetricsOverride',{width:390,height:844,deviceScaleFactor:1,mobile:true});await capturar('crear-movil');confirmar('Movil sin overflow global',await evaluar('document.documentElement.scrollWidth<=window.innerWidth+1'));await navegar('/Facturas');await capturar('listado-movil');confirmar('Tabla en contenedor movil',await evaluar("!!document.querySelector('.tabla-contenedor')"));
 await cdpPagina.enviar('Emulation.setDeviceMetricsOverride',{width:1440,height:1000,deviceScaleFactor:1,mobile:false});await navegar('/Facturas/Editar/FAC000001');confirmar('Historicos desmarcado',await evaluar("!document.querySelector('#ActualizarPreciosYUnidades').checked"));confirmar('Productos seleccionados',await evaluar("[...document.querySelectorAll('select[name$=\".ProductoId\"]')].every(campo=>!!campo.value)"));await evaluar("document.querySelector('#ActualizarPreciosYUnidades').click()");confirmar('Checkbox invalida calculo',await evaluar("document.querySelector('#ActualizarPreciosYUnidades').checked&&!document.querySelector('#resumen-calculo')"));await evaluar('document.body.style.zoom="200%"');await capturar('editar-zoom200');confirmar('Zoom conserva controles',await evaluar("document.querySelector('#ClienteId').getBoundingClientRect().width>0"));await evaluar('document.body.style.zoom="100%"');await evaluar("document.querySelector('#ClienteId').focus()");await cdpPagina.enviar('Input.dispatchKeyEvent',{type:'keyDown',key:'Tab',code:'Tab',windowsVirtualKeyCode:9});await cdpPagina.enviar('Input.dispatchKeyEvent',{type:'keyUp',key:'Tab',code:'Tab',windowsVirtualKeyCode:9});confirmar('Tab avanza foco',await evaluar("document.activeElement.id==='EmpleadoId'"));
 const antes=JSON.stringify({facturas:await datos('facturas'),productos:await datos('productos')});await navegar('/Facturas/Pagar/FAC000001');confirmar('Confirmacion identifica cantidades',await evaluar("document.body.innerText.includes('Existencias afectadas')&&document.querySelectorAll('tbody tr').length===2"));await capturar('pagar-confirmacion');const cancelar=await evaluar("[...document.querySelectorAll('a')].find(enlace=>enlace.textContent.trim()==='Cancelar').getAttribute('href')");await navegar(cancelar);confirmar('Cancelar sin mutacion',JSON.stringify({facturas:await datos('facturas'),productos:await datos('productos')})===antes);
}
// Cierra solamente los procesos iniciados por la verificacion.
async function cerrarProcesos(){const errores=[];for(const proceso of [...procesos].reverse()){try{if(proceso.exitCode===null&&proceso.signalCode===null){const cierre=new Promise(resolver=>proceso.once('close',resolver));proceso.kill();await Promise.race([cierre,esperar(5000)]);}if(proceso.exitCode===null&&proceso.signalCode===null)throw new Error('Proceso propio pendiente '+proceso.pid);}catch(error){errores.push(error.message);}}return errores;}

try{
 for(const puerto of [opciones['puerto-api'],opciones['puerto-web'],opciones['puerto-proxy'],opciones['puerto-depuracion']])await puertoLibre(puerto);
 iniciarProyecto('Carniceria.Api',opciones['puerto-api']);
 await hasta(async()=>(await fetch(baseApi+'/api/estado',{signal:AbortSignal.timeout(1000)})).ok,'API disponible');
 // El proxy conserva bytes JSON y registra solamente llamadas del servidor MVC.
 proxy=http.createServer(async(peticion,respuesta)=>{try{const partes=[];for await(const parte of peticion)partes.push(parte);const cuerpo=Buffer.concat(partes);const observacion={metodo:peticion.method,ruta:new URL(peticion.url,baseApi).pathname,json:cuerpo.length?JSON.parse(cuerpo):null};solicitudes.push(observacion);const cabeceras={'Content-Type':peticion.headers['content-type']??'application/json',Accept:'application/json'};const destino=await fetch(baseApi+peticion.url,{method:peticion.method,headers:cabeceras,body:['GET','HEAD'].includes(peticion.method)?undefined:cuerpo,redirect:'manual',signal:AbortSignal.timeout(10000)});observacion.codigo=destino.status;respuesta.writeHead(destino.status,{'Content-Type':destino.headers.get('content-type')??'application/json',...(destino.headers.get('location')?{Location:destino.headers.get('location')}:{})});respuesta.end(Buffer.from(await destino.arrayBuffer()));}catch(error){respuesta.writeHead(502);respuesta.end(error.message);}});
 await new Promise((resolver,rechazar)=>{proxy.once('error',rechazar);proxy.listen(Number(opciones['puerto-proxy']),'127.0.0.1',resolver);});
 iniciarProyecto('Carniceria.Web',opciones['puerto-web'],{Api__DireccionBase:'http://127.0.0.1:'+opciones['puerto-proxy']+'/'});
 await hasta(async()=>(await fetch(baseWeb,{signal:AbortSignal.timeout(1000)})).ok,'Web disponible');
 perfil=await fs.mkdtemp(path.join(os.tmpdir(),'carniceria-interfaz-perfil-'));
 const candidatos=[opciones.navegador,path.join(process.env['ProgramFiles(x86)']??'C:/Program Files (x86)','Microsoft/Edge/Application/msedge.exe'),path.join(process.env.ProgramFiles??'C:/Program Files','Google/Chrome/Application/chrome.exe')].filter(Boolean);
 let ejecutable;for(const candidato of candidatos){try{await fs.access(candidato);ejecutable=candidato;break;}catch{}}
 if(!ejecutable)throw new Error('No se encontro Edge o Chrome instalado.');
 navegador=iniciarProceso('navegador',ejecutable,['--headless=new','--no-first-run','--no-default-browser-check','--remote-debugging-address=127.0.0.1','--remote-debugging-port='+opciones['puerto-depuracion'],'--user-data-dir='+perfil,'about:blank']);
 const depuracion='http://127.0.0.1:'+opciones['puerto-depuracion'];
 const version=await hasta(async()=>(await fetch(depuracion+'/json/version',{signal:AbortSignal.timeout(1000)})).json(),'canal CDP');
 await fs.writeFile(path.join(salida,'navegador-version.json'),JSON.stringify({navegador:version.Browser,protocolo:version['Protocol-Version']},null,2));
 cdpNavegador=await ConexionCdp.abrir(version.webSocketDebuggerUrl);
 const paginas=await(await fetch(depuracion+'/json/list')).json();const pagina=paginas.find(p=>p.type==='page'&&p.url==='about:blank');if(!pagina)throw new Error('No hay pagina del perfil propio.');
 cdpPagina=await ConexionCdp.abrir(pagina.webSocketDebuggerUrl);
 cdpPagina.observadores.push(mensaje=>{if(mensaje.method==='Page.frameNavigated'&&!mensaje.params.frame.parentId)navegaciones++;if(mensaje.method==='Network.requestWillBeSent')solicitudesNavegador.push({metodo:mensaje.params.request.method,url:mensaje.params.request.url});if(mensaje.method==='Runtime.exceptionThrown')excepciones.push(mensaje.params.exceptionDetails);if(mensaje.method==='Runtime.consoleAPICalled')consola.push(mensaje.params);});
 for(const dominio of ['Page','Runtime','Network'])await cdpPagina.enviar(dominio+'.enable');
 await probarFacturas();
 const llamadasDirectas=solicitudesNavegador.filter(s=>[opciones['puerto-api'],opciones['puerto-proxy']].includes(new URL(s.url).port));
 confirmar('Navegador no solicita API ni proxy directamente',llamadasDirectas.length===0);
 confirmar('JavaScript sin excepciones no controladas',excepciones.length===0);
 await fs.writeFile(path.join(salida,'solicitudes-servidor.json'),JSON.stringify(solicitudes,null,2));
 await fs.writeFile(path.join(salida,'solicitudes-navegador.json'),JSON.stringify(solicitudesNavegador,null,2));
}catch(error){fallo=error;console.error(error.stack);if(cdpPagina){try{await capturar('estado-fallo');}catch{}}}
finally{
 await fs.writeFile(path.join(salida,'solicitudes-servidor.json'),JSON.stringify(solicitudes,null,2));
 await fs.writeFile(path.join(salida,'solicitudes-navegador.json'),JSON.stringify(solicitudesNavegador,null,2));
 const erroresCierre=[];
 try{if(cdpNavegador){await cdpNavegador.enviar('Browser.close');await hasta(()=>navegador.exitCode!==null||navegador.signalCode!==null,'cierre del navegador propio',5000);}}catch(error){if(navegador?.exitCode===null&&navegador?.signalCode===null)erroresCierre.push(error.message);}
 try{cdpPagina?.cerrar();cdpNavegador?.cerrar();}catch(error){erroresCierre.push(error.message);}
 erroresCierre.push(...await cerrarProcesos());
 if(proxy){try{proxy.closeAllConnections();await new Promise(resolver=>proxy.close(resolver));}catch(error){erroresCierre.push(error.message);}}
 const puertos=[];for(const puerto of [opciones['puerto-api'],opciones['puerto-web'],opciones['puerto-proxy'],opciones['puerto-depuracion']]){try{await puertoLibre(puerto);puertos.push({puerto:Number(puerto),libre:true});}catch{puertos.push({puerto:Number(puerto),libre:false});}}
 // El perfil temporal se conserva para diagnostico y nunca se elimina recursivamente.
 await fs.writeFile(path.join(salida,'consola.json'),JSON.stringify({excepciones,consola},null,2));
 await fs.writeFile(path.join(salida,'puertos.json'),JSON.stringify(puertos,null,2));
 await fs.writeFile(path.join(salida,'resumen.json'),JSON.stringify({grupo:'Interfaz',total:resultados.length,aprobadas:resultados.filter(r=>r.aprobado).length,fallo:fallo?.message??null,erroresCierre,perfilTemporalConservado:!!perfil,resultados},null,2));
 console.log(JSON.stringify({total:resultados.length,aprobadas:resultados.filter(r=>r.aprobado).length,fallo:fallo?.message??null,erroresCierre,puertos}));
 if(fallo||erroresCierre.length||puertos.some(p=>!p.libre))process.exitCode=1;
}
