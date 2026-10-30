import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import net from 'node:net';
import http from 'node:http';
import {randomUUID} from 'node:crypto';
import {spawn} from 'node:child_process';
import {setTimeout as esperar} from 'node:timers/promises';
import {parseArgs} from 'node:util';

// Opciones de ejecución para aislar puertos y conservar evidencia fuera de la aplicación.
const {values: opciones} = parseArgs({options:{grupo:{type:'string',default:'Migracion'},raiz:{type:'string',default:process.cwd()},salida:{type:'string'},'puerto-api':{type:'string',default:'5234'},'puerto-web':{type:'string',default:'5235'},'solo-cierre':{type:'boolean',default:false}}});
const raiz = path.resolve(opciones.raiz);
const carpetaSalida = opciones.salida ? path.resolve(opciones.salida) : await fs.mkdtemp(path.join(os.tmpdir(),'carniceria-qa-f4-'));
await fs.mkdir(carpetaSalida,{recursive:true});
const baseApi=`http://127.0.0.1:${opciones['puerto-api']}`;
const baseWeb=`http://127.0.0.1:${opciones['puerto-web']}`;
const procesos=[];let servidorStub=null;const temporizadoresStub=new Set();
const resultados=[];
const cookies=new Map();

// Registra una condición comprobable y detiene el grupo al primer fallo.
function confirmar(caso,condicion,detalle={}) {resultados.push({caso,aprobado:!!condicion,...detalle});if(!condicion)throw new Error(caso);}

// Conserva los valores de texto y los caracteres escapados del formulario HTML.
function decodificar(texto) {return texto.replace(/&#x([0-9a-f]+);/gi,(_,valor)=>String.fromCodePoint(parseInt(valor,16))).replace(/&#(\d+);/g,(_,valor)=>String.fromCodePoint(Number(valor))).replace(/&quot;/g,'"').replace(/&#39;|&apos;/g,"'").replace(/&lt;/g,'<').replace(/&gt;/g,'>').replace(/&amp;/g,'&');}

// Extrae el token del campo oculto sin depender del orden de sus atributos.
function tokenAntifalsificacion(html) {for(const coincidencia of html.matchAll(/<input\b[^>]*>/gi)){const atributos=Object.fromEntries([...coincidencia[0].matchAll(/([\w:-]+)\s*=\s*(?:"([^"]*)"|'([^']*)')/g)].map(par=>[par[1].toLowerCase(),decodificar(par[2]??par[3])]));if(atributos.name==='__RequestVerificationToken')return atributos.value;}throw new Error('El formulario no contiene token antifalsificacion.');}

// Envía cookies y sigue redirecciones con las mismas reglas de cambio de método del navegador.
async function solicitarWeb(ruta,{metodo='GET',formulario,seguir=true}={}) {let url=new URL(ruta,baseWeb);let cuerpo=formulario?new URLSearchParams(formulario).toString():undefined;let metodoActual=metodo;for(let vuelta=0;vuelta<8;vuelta++){const cabeceras={};if(cookies.size)cabeceras.Cookie=[...cookies].map(([nombre,valor])=>`${nombre}=${valor}`).join('; ');if(cuerpo!==undefined)cabeceras['Content-Type']='application/x-www-form-urlencoded';const respuesta=await fetch(url,{method:metodoActual,headers:cabeceras,body:cuerpo,redirect:'manual',signal:AbortSignal.timeout(15000)});for(const cookie of respuesta.headers.getSetCookie()){const par=cookie.split(';',1)[0];const separador=par.indexOf('=');cookies.set(par.slice(0,separador),par.slice(separador+1));}const html=await respuesta.text();const ubicacion=respuesta.headers.get('location');if(seguir&&ubicacion&&[301,302,303,307,308].includes(respuesta.status)){url=new URL(ubicacion,url);if(respuesta.status===303||([301,302].includes(respuesta.status)&&metodoActual==='POST')){metodoActual='GET';cuerpo=undefined;}continue;}return {codigo:respuesta.status,html,url:url.href,ubicacion};}throw new Error('Se excedio el limite de redirecciones.');}

// Obtiene JSON de la API para contrastar el resultado de las acciones MVC.
async function consultarApi(ruta) {const respuesta=await fetch(baseApi+ruta,{signal:AbortSignal.timeout(10000)});if(!respuesta.ok)throw new Error(`API ${ruta} devolvio ${respuesta.status}`);return await respuesta.json();}

// Envía el formulario con el token y la cookie obtenidos desde su página GET.
async function enviarFormulario(ruta,formulario) {const pagina=await solicitarWeb(ruta);confirmar(`GET formulario ${ruta}`,pagina.codigo===200);return await solicitarWeb(ruta,{metodo:'POST',formulario:{...formulario,__RequestVerificationToken:tokenAntifalsificacion(pagina.html)},seguir:false});}

// Comprueba que los puertos reservados estén libres antes de crear procesos propios.
async function comprobarPuerto(puerto) {const servidor=net.createServer();await new Promise((resolver,rechazar)=>{servidor.once('error',rechazar);servidor.listen(Number(puerto),'127.0.0.1',resolver);});await new Promise(resolver=>servidor.close(resolver));}

// Inicia una DLL previamente compilada sin ejecutar restauración ni compilación.
async function iniciarProyecto(nombre,puerto,entorno={}) {const ejecutable=path.join(raiz,nombre,'bin','Debug','net10.0',`${nombre}.dll`);await fs.access(ejecutable);const proceso=spawn('dotnet',[ejecutable,'--urls',`http://127.0.0.1:${puerto}`],{cwd:path.join(raiz,nombre),env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development',...entorno},windowsHide:true,stdio:['ignore','pipe','pipe']});procesos.push(proceso);let salida='',error='';proceso.stdout.on('data',datos=>salida+=datos);proceso.stderr.on('data',datos=>error+=datos);proceso.on('error',fallo=>error+=fallo.message);proceso.on('close',()=>{void fs.writeFile(path.join(carpetaSalida,`${nombre}.log`),salida+'\n'+error);});return proceso;}

// Espera una respuesta HTTP de salud antes de iniciar el recorrido.
async function esperarServicio(url) {for(let intento=0;intento<60;intento++){try{const respuesta=await fetch(url,{signal:AbortSignal.timeout(1000)});if(respuesta.ok)return;}catch{}await esperar(100);}throw new Error(`No inicio ${new URL(url).pathname}`);}

// Cierra solamente los procesos creados por esta ejecución.
async function cerrarProcesos() {const pendientes=[];for(const proceso of procesos.reverse()){try{if(proceso.exitCode===null&&proceso.signalCode===null){const cierre=new Promise(resolver=>proceso.once('close',resolver));proceso.kill();await Promise.race([cierre,esperar(5000)]);}if(proceso.exitCode===null&&proceso.signalCode===null)pendientes.push(proceso.pid);}catch(error){pendientes.push(proceso.pid);}}if(pendientes.length)throw new Error('No se confirmo el cierre de un proceso propio.');}

// Obtiene una imagen completa de los cuatro módulos para detectar escrituras inesperadas.
async function imagenDatos() {const datos={};for(const recurso of ['clientes','empleados','proveedores','productos'])datos[recurso]=await consultarApi(`/api/${recurso}`);return datos;}

// Obtiene todas las colecciones para contrastar bajas bloqueadas desde MVC.
async function imagenCompleta(){const datos=await imagenDatos();datos.facturas=await consultarApi('/api/facturas');return datos;}

// Crea únicamente los recursos auxiliares libres que usa el recorrido.
async function crearAuxiliarApi(recurso,formulario){const respuesta=await fetch(baseApi+'/api/'+recurso,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(formulario),signal:AbortSignal.timeout(5000)});confirmar('Alta auxiliar API '+recurso,respuesta.status===201);return await respuesta.json();}

// Verifica confirmaciones MVC con cookies y antifalsificación sobre relaciones de factura.
let fallo=null;
try{
 await comprobarPuerto(opciones['puerto-api']);await comprobarPuerto(opciones['puerto-web']);
 await iniciarProyecto('Carniceria.Api',opciones['puerto-api']);await esperarServicio(baseApi+'/api/estado');
 await iniciarProyecto('Carniceria.Web',opciones['puerto-web'],{Api__DireccionBase:baseApi+'/'});await esperarServicio(baseWeb);
 const inicial=await imagenCompleta();confirmar('Factura semilla disponible para guardas MVC',inicial.facturas.length===1);
 // Las tres bajas relacionadas permanecen en confirmación con resumen y el mismo Id.
 for(const [modulo,recurso,registro,campo] of [['Clientes','clientes',inicial.clientes[0],'identificacion'],['Empleados','empleados',inicial.empleados[0],'identificacion'],['Productos','productos',inicial.productos.find(p=>p.codigo==='RES001'),'codigo']]){
  const ruta='/'+modulo+'/Eliminar/'+registro.id;const pagina=await solicitarWeb(ruta);
  confirmar('Confirmacion relacionada200 '+modulo,pagina.codigo===200&&decodificar(pagina.html).includes(registro[campo]));
  const cancelacion=[...pagina.html.matchAll(/<a\b([^>]*)>([\s\S]*?)<\/a>/gi)].find(par=>decodificar(par[2]).trim()==='Cancelar');const enlace=cancelacion&&/href="([^"]+)"/i.exec(cancelacion[1])?.[1];
  confirmar('Cancelar tiene enlace de lectura '+modulo,!!enlace&&new URL(decodificar(enlace),baseWeb).pathname==='/'+modulo);
  const cancelada=await solicitarWeb(decodificar(enlace));confirmar('Cancelar no modifica datos '+modulo,cancelada.codigo===200&&JSON.stringify(await imagenCompleta())===JSON.stringify(inicial));
  const rechazo=await solicitarWeb('/'+modulo+'/EliminarConfirmado/'+registro.id,{metodo:'POST',formulario:{__RequestVerificationToken:tokenAntifalsificacion(pagina.html)},seguir:false});
  confirmar('Baja bloqueada permanece200 sin Location '+modulo,rechazo.codigo===200&&!rechazo.ubicacion);
  confirmar('Baja bloqueada muestra confirmacion e Id '+modulo,rechazo.html.includes('Confirmar eliminación')&&rechazo.html.includes('/'+modulo+'/EliminarConfirmado/'+registro.id)&&decodificar(rechazo.html).includes(registro[campo]));
  confirmar('Baja bloqueada muestra error de factura '+modulo,/validation-summary-errors/.test(rechazo.html)&&/factura/i.test(decodificar(rechazo.html)));
  confirmar('Baja bloqueada no muestra falso exito '+modulo,!decodificar(rechazo.html).includes('se eliminó correctamente'));
  confirmar('Baja bloqueada conserva todas las colecciones '+modulo,JSON.stringify(await imagenCompleta())===JSON.stringify(inicial));
  await fs.writeFile(path.join(carpetaSalida,'bloqueo-'+recurso+'.html'),rechazo.html);
 }
 // Los registros auxiliares no relacionados siguen siendo eliminables desde MVC.
 const cliente=await crearAuxiliarApi('clientes',{tipoIdentificacion:1,identificacion:'9-5101-0001',nombre:'ClienteLibre',primerApellido:'Prueba',segundoApellido:'Mora',fechaNacimiento:'1990-01-02'});
 const empleado=await crearAuxiliarApi('empleados',{tipoIdentificacion:1,identificacion:'9-5101-0002',nombre:'EmpleadoLibre',primerApellido:'Prueba',segundoApellido:'Mora',fechaNacimiento:'1990-01-02',salarioMensual:650000.5,fechaIngreso:'2020-01-02',areaTrabajo:2,puesto:'Cajero',turno:1});
 const producto=await crearAuxiliarApi('productos',{proveedorId:inicial.proveedores[0].id,codigo:'F5MVCLIBRE',nombre:'ProductoLibre',categoria:1,unidadMedida:1,precioUnidadMedida:1000,existenciaActual:5,existenciaMinima:1});
 for(const [modulo,recurso,registro] of [['Clientes','clientes',cliente],['Empleados','empleados',empleado],['Productos','productos',producto]]){
  const pagina=await solicitarWeb('/'+modulo+'/Eliminar/'+registro.id);confirmar('Confirmacion libre200 '+modulo,pagina.codigo===200);
  const respuesta=await solicitarWeb('/'+modulo+'/EliminarConfirmado/'+registro.id,{metodo:'POST',formulario:{__RequestVerificationToken:tokenAntifalsificacion(pagina.html)},seguir:false});
  confirmar('Baja libre PRG302 al listado '+modulo,respuesta.codigo===302&&!!respuesta.ubicacion&&new URL(respuesta.ubicacion,baseWeb).pathname==='/'+modulo);
  const ausente=await fetch(baseApi+'/api/'+recurso+'/'+registro.id);confirmar('Baja MVC libre elimina recurso API '+modulo,ausente.status===404);
  const listado=await solicitarWeb(respuesta.ubicacion);confirmar('Baja libre muestra exito '+modulo,listado.codigo===200&&decodificar(listado.html).includes('se eliminó correctamente'));
 }
 confirmar('Bajas libres no alteran semillas ni factura',JSON.stringify(await imagenCompleta())===JSON.stringify(inicial));
}catch(error){fallo=error;console.error(error.stack);}
finally{
 let errorCierre=null;try{await cerrarProcesos();}catch(error){errorCierre=error.message;}
 const puertos=[];for(const puerto of [opciones['puerto-api'],opciones['puerto-web']]){try{await comprobarPuerto(puerto);puertos.push({puerto:Number(puerto),libre:true});}catch{puertos.push({puerto:Number(puerto),libre:false});}}
 const resumen={grupo:'IntegridadMvc',total:resultados.length,aprobadas:resultados.filter(r=>r.aprobado).length,fallo:fallo?.message??null,errorCierre,puertos,resultados};
 await fs.writeFile(path.join(carpetaSalida,'resumen.json'),JSON.stringify(resumen,null,2));console.log(JSON.stringify({total:resumen.total,aprobadas:resumen.aprobadas,fallo:resumen.fallo,errorCierre,puertos}));
 if(fallo||errorCierre||puertos.some(p=>!p.libre))process.exitCode=1;
}


