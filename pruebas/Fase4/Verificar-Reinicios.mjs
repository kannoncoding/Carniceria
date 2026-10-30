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

// Detiene unicamente un proceso creado por el recorrido y confirma la liberacion de su puerto.
async function detenerPropio(proceso,puerto,descripcion){if(proceso.exitCode===null&&proceso.signalCode===null){const cierre=new Promise(resolver=>proceso.once('close',resolver));proceso.kill();await Promise.race([cierre,esperar(5000)]);}confirmar('Proceso detenido '+descripcion,proceso.exitCode!==null||proceso.signalCode!==null);await comprobarPuerto(puerto);confirmar('Puerto liberado '+descripcion,true);}

// Comprueba que una respuesta MVC muestra el registro de la API y sus campos actuales.
async function comprobarRegistroMvc(modulo,registro,campos,etapa){const pagina=await solicitarWeb('/'+modulo+'/Detalles/'+registro.id);confirmar(etapa+' detalle MVC '+modulo,pagina.codigo===200);for(const campo of campos)confirmar(etapa+' campo MVC '+modulo+' '+campo,decodificar(pagina.html).includes(String(registro[campo])));}

// Registra reinicios separados para demostrar que Web no es propietario de los datos de negocio.
let fallo=null;
const identidad={};
try{
 await comprobarPuerto(opciones['puerto-api']);await comprobarPuerto(opciones['puerto-web']);
 let procesoApi=await iniciarProyecto('Carniceria.Api',opciones['puerto-api']);await esperarServicio(baseApi+'/api/estado');
 let procesoWeb=await iniciarProyecto('Carniceria.Web',opciones['puerto-web'],{Api__DireccionBase:baseApi+'/'});await esperarServicio(baseWeb);
 identidad.inicial={api:procesoApi.pid,web:procesoWeb.pid};
 const semillasIniciales=await imagenDatos();
 confirmar('Semillas iniciales 1 cliente 1 empleado 1 proveedor 2 productos',JSON.stringify(Object.values(semillasIniciales).map(lista=>lista.length))===JSON.stringify([1,1,1,2]));
 // Altas MVC con proteccion antifalsificacion y relacion real al proveedor de la API.
 const formularioCliente={TipoIdentificacion:'1',Identificacion:'9-4949-0001',Nombre:'ElenaReinicio',PrimerApellido:'Prueba',SegundoApellido:'Mora',FechaNacimiento:'1992-01-02'};
 const formularioProducto={ProveedorId:semillasIniciales.proveedores[0].id,Codigo:'F4REINICIO',Nombre:'ProductoReinicio',Categoria:'1',UnidadMedida:'1',PrecioUnidadMedida:'1250,50',ExistenciaActual:'5,25',ExistenciaMinima:'1,5'};
 const altaCliente=await enviarFormulario('/Clientes/Crear',formularioCliente);confirmar('Alta cliente MVC redirige',altaCliente.codigo===302&&new URL(altaCliente.ubicacion,baseWeb).pathname==='/Clientes');
 const altaProducto=await enviarFormulario('/Productos/Crear',formularioProducto);confirmar('Alta producto MVC redirige',altaProducto.codigo===302&&new URL(altaProducto.ubicacion,baseWeb).pathname==='/Productos');
 const antes=await imagenDatos();
 const cliente=antes.clientes.find(c=>c.identificacion===formularioCliente.Identificacion),producto=antes.productos.find(p=>p.codigo===formularioProducto.Codigo);
 confirmar('Alta cliente registrada en API con Id',!!cliente?.id&&cliente.nombre===formularioCliente.Nombre);
 confirmar('Alta producto mantiene proveedor y decimales',!!producto?.id&&producto.proveedorId===formularioProducto.ProveedorId&&producto.precioUnidadMedida===1250.5&&producto.existenciaActual===5.25&&producto.existenciaMinima===1.5);
 await comprobarRegistroMvc('Clientes',cliente,['nombre','identificacion'],'Antes');
 await comprobarRegistroMvc('Productos',producto,['nombre','codigo','proveedorRazonSocial','proveedorIdentificacion'],'Antes');
 await fs.writeFile(path.join(carpetaSalida,'datos-antes.json'),JSON.stringify(antes,null,2));
 // Reinicia solo Web y exige que sobrevivan exactamente los identificadores y datos de API.
 await detenerPropio(procesoWeb,opciones['puerto-web'],'Web primero');
 procesoWeb=await iniciarProyecto('Carniceria.Web',opciones['puerto-web'],{Api__DireccionBase:baseApi+'/'});await esperarServicio(baseWeb);
 identidad.trasWeb={api:procesoApi.pid,web:procesoWeb.pid};
 confirmar('Reinicio Web cambia su PID',identidad.trasWeb.web!==identidad.inicial.web);
 confirmar('Reinicio Web conserva PID API',identidad.trasWeb.api===identidad.inicial.api&&procesoApi.exitCode===null&&procesoApi.signalCode===null);
 const trasWeb=await imagenDatos();confirmar('API conserva todos los Id y campos tras reinicio Web',JSON.stringify(trasWeb)===JSON.stringify(antes));
 await comprobarRegistroMvc('Clientes',cliente,['nombre','identificacion'],'Tras Web');
 await comprobarRegistroMvc('Productos',producto,['nombre','codigo','proveedorRazonSocial','proveedorIdentificacion'],'Tras Web');
 for(const [modulo,registro] of [['Clientes',cliente],['Productos',producto]]){const pagina=await solicitarWeb('/'+modulo);confirmar('Listado conserva enlace Id tras Web '+modulo,pagina.codigo===200&&pagina.html.includes('/Detalles/'+registro.id));}
 // Reinicia solo API y exige semillas nuevas sin los registros creados por el recorrido.
 await detenerPropio(procesoApi,opciones['puerto-api'],'API primero');
 procesoApi=await iniciarProyecto('Carniceria.Api',opciones['puerto-api']);await esperarServicio(baseApi+'/api/estado');
 identidad.trasApi={api:procesoApi.pid,web:procesoWeb.pid};
 confirmar('Reinicio API cambia su PID',identidad.trasApi.api!==identidad.trasWeb.api);
 confirmar('Reinicio API conserva PID Web',identidad.trasApi.web===identidad.trasWeb.web&&procesoWeb.exitCode===null&&procesoWeb.signalCode===null);
 const trasApi=await imagenDatos();
 confirmar('Reinicio API restaura conteos 1 1 1 2',JSON.stringify(Object.values(trasApi).map(lista=>lista.length))===JSON.stringify([1,1,1,2]));
 for(const recurso of ['clientes','empleados','proveedores','productos']){
  confirmar('Semillas nuevas con Id distintos '+recurso,trasApi[recurso].every(registro=>!semillasIniciales[recurso].some(semilla=>semilla.id===registro.id)));
  const modulo=recurso[0].toUpperCase()+recurso.slice(1);const pagina=await solicitarWeb('/'+modulo);
  confirmar('MVC consulta semillas actuales '+modulo,pagina.codigo===200);
  for(const registro of trasApi[recurso])confirmar('MVC enlaza semilla actual '+recurso+' '+registro.id,pagina.html.includes('/Detalles/'+registro.id));
  confirmar('MVC sin enlaces a Id anteriores '+modulo,antes[recurso].every(registro=>!pagina.html.includes('/Detalles/'+registro.id)));
 }
 confirmar('Productos semilla conservan proveedor existente',trasApi.productos.every(p=>trasApi.proveedores.some(proveedor=>proveedor.id===p.proveedorId)));
 confirmar('Inventario semilla nunca negativo',trasApi.productos.every(p=>p.existenciaActual>=0&&p.existenciaMinima>=0));
 for(const [recurso,modulo,registro] of [['clientes','Clientes',cliente],['productos','Productos',producto]]){
  const respuesta=await fetch(baseApi+'/api/'+recurso+'/'+registro.id);confirmar('API antiguo Id devuelve 404 '+recurso,respuesta.status===404);
  const pagina=await solicitarWeb('/'+modulo+'/Detalles/'+registro.id);confirmar('MVC antiguo Id devuelve 404 '+recurso,pagina.codigo===404);
 }
 await fs.writeFile(path.join(carpetaSalida,'datos-tras-api.json'),JSON.stringify(trasApi,null,2));
}catch(error){fallo=error;console.error(error.stack);}
finally{
 let errorCierre=null;try{await cerrarProcesos();}catch(error){errorCierre=error.message;}
 const puertos=[];for(const puerto of [opciones['puerto-api'],opciones['puerto-web']]){try{await comprobarPuerto(puerto);puertos.push({puerto:Number(puerto),libre:true});}catch{puertos.push({puerto:Number(puerto),libre:false});}}
 await fs.writeFile(path.join(carpetaSalida,'resumen.json'),JSON.stringify({grupo:'Reinicios',total:resultados.length,aprobadas:resultados.filter(r=>r.aprobado).length,fallo:fallo?.message??null,errorCierre,identidad,resultados,puertos},null,2));
 console.log(JSON.stringify({total:resultados.length,aprobadas:resultados.filter(r=>r.aprobado).length,fallo:fallo?.message??null,errorCierre,identidad,puertos}));
 if(fallo||errorCierre||puertos.some(p=>!p.libre))process.exitCode=1;
}


