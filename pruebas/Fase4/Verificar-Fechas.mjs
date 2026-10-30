import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import net from 'node:net';
import http from 'node:http';
import {randomUUID} from 'node:crypto';
import {spawn} from 'node:child_process';
import {setTimeout as esperar} from 'node:timers/promises';
import {parseArgs} from 'node:util';

// Opciones de ejecucion para aislar puertos y conservar evidencia fuera de la aplicacion.
const {values: opciones} = parseArgs({options:{grupo:{type:'string',default:'Migracion'},raiz:{type:'string',default:process.cwd()},salida:{type:'string'},'puerto-api':{type:'string',default:'5234'},'puerto-web':{type:'string',default:'5235'},'solo-cierre':{type:'boolean',default:false}}});
const raiz = path.resolve(opciones.raiz);
const carpetaSalida = opciones.salida ? path.resolve(opciones.salida) : await fs.mkdtemp(path.join(os.tmpdir(),'carniceria-qa-f4-'));
await fs.mkdir(carpetaSalida,{recursive:true});
const baseApi=`http://127.0.0.1:${opciones['puerto-api']}`;
const baseWeb=`http://127.0.0.1:${opciones['puerto-web']}`;
const procesos=[];let servidorStub=null;const temporizadoresStub=new Set();
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

// Envia el formulario con el token y la cookie obtenidos desde su pagina GET.
async function enviarFormulario(ruta,formulario) {const pagina=await solicitarWeb(ruta);confirmar(`GET formulario ${ruta}`,pagina.codigo===200);return await solicitarWeb(ruta,{metodo:'POST',formulario:{...formulario,__RequestVerificationToken:tokenAntifalsificacion(pagina.html)},seguir:false});}

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

// Distingue fechas minimas validas de propiedades omitidas en el JSON de respuesta.
let fallo=null;
const observaciones=[];
try{
 await comprobarPuerto(opciones['puerto-api']);await comprobarPuerto(opciones['puerto-web']);
 const procesoApi=await iniciarProyecto('Carniceria.Api',opciones['puerto-api']);await esperarServicio(baseApi+'/api/estado');
 await iniciarProyecto('Carniceria.Web',opciones['puerto-web'],{Api__DireccionBase:baseApi+'/'});await esperarServicio(baseWeb);
 // Casos independientes para nacimiento de cliente, nacimiento e ingreso de empleado.
 const persona={tipoIdentificacion:1,nombre:'Minimo',primerApellido:'Prueba',segundoApellido:'Mora'};
 const empleo={salarioMensual:650000.5,areaTrabajo:2,puesto:'Cajero',turno:2};
 const casos=[
 {recurso:'clientes',modulo:'Clientes',campo:'fechaNacimiento',formulario:{...persona,identificacion:'9-4747-0091',fechaNacimiento:'0001-01-01'}},
 {recurso:'empleados',modulo:'Empleados',campo:'fechaNacimiento',formulario:{...persona,...empleo,identificacion:'9-4747-0092',fechaNacimiento:'0001-01-01',fechaIngreso:'2020-01-02'}},
 {recurso:'empleados',modulo:'Empleados',campo:'fechaIngreso',formulario:{...persona,...empleo,identificacion:'9-4747-0093',fechaNacimiento:'1990-01-02',fechaIngreso:'0001-01-01'}}
 ];
 for(const caso of casos){
  const respuesta=await fetch(baseApi+'/api/'+caso.recurso,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(caso.formulario)});const registro=await respuesta.json();caso.registro=registro;
  confirmar('API acepta fecha minima '+caso.recurso+' '+caso.campo,respuesta.status===201);
  for(const ruta of ['/'+caso.modulo,'/'+caso.modulo+'/Detalles/'+registro.id,'/'+caso.modulo+'/Editar/'+registro.id]){
   const pagina=await solicitarWeb(ruta);observaciones.push({etapa:'fecha-presente',campo:caso.campo,ruta,codigo:pagina.codigo});confirmar('MVC lee fecha minima '+caso.campo+' '+ruta,pagina.codigo===200);
   if(ruta.includes('/Editar/')){const nombre=caso.campo[0].toUpperCase()+caso.campo.slice(1);const entrada=[...pagina.html.matchAll(/<input\b[^>]*>/gi)].find(par=>par[0].includes('name="'+nombre+'"'));confirmar('Control date conserva 0001 '+caso.campo,!!entrada&&entrada[0].includes('type="date"')&&entrada[0].includes('value="0001-01-01"'));await fs.writeFile(path.join(carpetaSalida,caso.recurso+'-'+caso.campo+'-editar.html'),pagina.html);}
  }
  // Los formularios MVC usan coma decimal y envian el token obtenido del formulario real.
  const formulario=Object.fromEntries(Object.entries(caso.formulario).map(([clave,valor])=>[clave[0].toUpperCase()+clave.slice(1),typeof valor==='number'?String(valor).replace('.',','):valor]));
  formulario.Identificacion=caso.formulario.identificacion.replace('-00','-01');
  const creacion=await enviarFormulario('/'+caso.modulo+'/Crear',formulario);confirmar('MVC POST permite fecha minima '+caso.campo,creacion.codigo===302);
  const creado=(await consultarApi('/api/'+caso.recurso)).find(r=>r.identificacion===formulario.Identificacion);confirmar('MVC POST conserva fecha en API '+caso.campo,creado?.[caso.campo]?.startsWith('0001-01-01'));
  const edicion=await enviarFormulario('/'+caso.modulo+'/Editar/'+creado.id,{...formulario,Nombre:'MinimoEditado'});confirmar('MVC PUT permite fecha minima '+caso.campo,edicion.codigo===302);
  const editado=await consultarApi('/api/'+caso.recurso+'/'+creado.id);confirmar('MVC PUT conserva fecha e Id '+caso.campo,editado.id===creado.id&&editado.nombre==='MinimoEditado'&&editado[caso.campo].startsWith('0001-01-01'));
 }
 // Detiene la API propia y conserva Web para simular DTO con un campo requerido ausente.
 const cierre=new Promise(resolver=>procesoApi.once('close',resolver));procesoApi.kill();await Promise.race([cierre,esperar(5000)]);confirmar('API cerrada antes de fixture',procesoApi.exitCode!==null||procesoApi.signalCode!==null);
 let fixture=null;const solicitudesStub=[];
 servidorStub=http.createServer((peticion,respuesta)=>{const ruta=new URL(peticion.url,baseApi).pathname;solicitudesStub.push({ruta,campoOmitido:fixture.campo});respuesta.writeHead(200,{'Content-Type':'application/json'});respuesta.end(JSON.stringify(ruta.split('/').filter(Boolean).length===2?[fixture.json]:fixture.json));});
 await new Promise((resolver,rechazar)=>{servidorStub.once('error',rechazar);servidorStub.listen(Number(opciones['puerto-api']),'127.0.0.1',resolver);});
 for(const caso of casos){
  const json={...caso.registro};delete json[caso.campo];fixture={campo:caso.campo,json};
  for(const ruta of ['/'+caso.modulo,'/'+caso.modulo+'/Detalles/'+caso.registro.id,'/'+caso.modulo+'/Editar/'+caso.registro.id]){
   const pagina=await solicitarWeb(ruta);observaciones.push({etapa:'fecha-omitida',campo:caso.campo,ruta,codigo:pagina.codigo});confirmar('MVC rechaza JSON sin '+caso.campo+' '+ruta,pagina.codigo===503);confirmar('JSON incompleto nunca aparenta 404 '+caso.campo+' '+ruta,!pagina.html.includes('No se encontró el registro'));
  }
 }
 await fs.writeFile(path.join(carpetaSalida,'solicitudes-stub.json'),JSON.stringify(solicitudesStub,null,2));
}catch(error){fallo=error;console.error(error.stack);}
finally{
 const erroresCierre=[];try{await cerrarProcesos();}catch(error){erroresCierre.push(error.message);}
 if(servidorStub){try{servidorStub.closeAllConnections();await new Promise(resolver=>servidorStub.close(resolver));}catch(error){erroresCierre.push(error.message);}}
 const puertos=[];for(const puerto of [opciones['puerto-api'],opciones['puerto-web']]){try{await comprobarPuerto(puerto);puertos.push({puerto:Number(puerto),libre:true});}catch{puertos.push({puerto:Number(puerto),libre:false});}}
 await fs.writeFile(path.join(carpetaSalida,'resumen.json'),JSON.stringify({grupo:'FechasLimite',total:resultados.length,aprobadas:resultados.filter(r=>r.aprobado).length,fallo:fallo?.message??null,erroresCierre,observaciones,resultados,puertos},null,2));
 console.log(JSON.stringify({total:resultados.length,aprobadas:resultados.filter(r=>r.aprobado).length,fallo:fallo?.message??null,erroresCierre,puertos}));
 if(fallo||erroresCierre.length||puertos.some(p=>!p.libre))process.exitCode=1;
}


