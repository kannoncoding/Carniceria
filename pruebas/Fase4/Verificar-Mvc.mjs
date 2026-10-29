import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import net from 'node:net';
import {spawn} from 'node:child_process';
import {setTimeout as esperar} from 'node:timers/promises';
import {parseArgs} from 'node:util';

// Opciones de ejecución para aislar puertos y conservar evidencia fuera de la aplicación.
const {values: opciones} = parseArgs({options:{raiz:{type:'string',default:process.cwd()},salida:{type:'string'},'puerto-api':{type:'string',default:'5234'},'puerto-web':{type:'string',default:'5235'},'solo-cierre':{type:'boolean',default:false}}});
const raiz = path.resolve(opciones.raiz);
const carpetaSalida = opciones.salida ? path.resolve(opciones.salida) : await fs.mkdtemp(path.join(os.tmpdir(),'carniceria-qa-f4-'));
await fs.mkdir(carpetaSalida,{recursive:true});
const baseApi=`http://127.0.0.1:${opciones['puerto-api']}`;
const baseWeb=`http://127.0.0.1:${opciones['puerto-web']}`;
const procesos=[];
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

let fallo=null;
try {
 await comprobarPuerto(opciones['puerto-api']);await comprobarPuerto(opciones['puerto-web']);
 await iniciarProyecto('Carniceria.Api',opciones['puerto-api']);await esperarServicio(baseApi+'/api/estado');
 await iniciarProyecto('Carniceria.Web',opciones['puerto-web'],{Api__DireccionBase:baseApi+'/'});await esperarServicio(baseWeb+'/');
 if(!opciones['solo-cierre']) {
 const proveedores=await consultarApi('/api/proveedores');
 const casos=[
 {modulo:'Clientes',recurso:'clientes',clave:'Identificacion',valor:'9-4242-0001',formulario:{TipoIdentificacion:'1',Identificacion:'9-4242-0001',Nombre:'Elena',PrimerApellido:'Prueba',SegundoApellido:'Mora',FechaNacimiento:'1992-01-02'}},
 {modulo:'Empleados',recurso:'empleados',clave:'Identificacion',valor:'9-4242-0002',formulario:{TipoIdentificacion:'1',Identificacion:'9-4242-0002',Nombre:'Mario',PrimerApellido:'Prueba',SegundoApellido:'Mora',FechaNacimiento:'1990-01-02',SalarioMensual:'650000,50',FechaIngreso:'2020-02-03',AreaTrabajo:'2',Puesto:'Cajero',Turno:'1'}},
 {modulo:'Proveedores',recurso:'proveedores',clave:'Identificacion',valor:'3-424-000003',formulario:{TipoIdentificacion:'4',Identificacion:'3-424-000003',RazonSocial:'ProveedorQA',NombreContacto:'Mario Mora',Telefono:'88887777',CorreoElectronico:'prueba@example.com',TipoSuministro:'8',FrecuenciaEntrega:'3'}},
 {modulo:'Productos',recurso:'productos',clave:'Codigo',valor:'F4SMOKE',formulario:{ProveedorId:proveedores[0].id,Codigo:'F4SMOKE',Nombre:'ProductoQA',Categoria:'1',UnidadMedida:'1',PrecioUnidadMedida:'2500,50',ExistenciaActual:'8,25',ExistenciaMinima:'2,5'}}
 ];
 for(const caso of casos){const lista=await solicitarWeb(`/${caso.modulo}`);confirmar(`Listado ${caso.modulo}`,lista.codigo===200);const alta=await enviarFormulario(`/${caso.modulo}/Crear`,caso.formulario);confirmar(`Alta MVC ${caso.modulo}`,alta.codigo===302&&!!alta.ubicacion,{codigo:alta.codigo});const registros=await consultarApi(`/api/${caso.recurso}`);const registro=registros.find(valor=>valor[caso.clave[0].toLowerCase()+caso.clave.slice(1)]===caso.valor);confirmar(`Alta persiste en API ${caso.modulo}`,!!registro);caso.id=registro.id;const detalle=await solicitarWeb(`/${caso.modulo}/Detalles/${registro.id}`);confirmar(`Detalle ${caso.modulo}`,detalle.codigo===200&&decodificar(detalle.html).includes(caso.valor));const edicion=await solicitarWeb(`/${caso.modulo}/Editar/${registro.id}`);confirmar(`Consulta edicion ${caso.modulo}`,edicion.codigo===200&&decodificar(edicion.html).includes(caso.valor));const confirmacion=await solicitarWeb(`/${caso.modulo}/Eliminar/${registro.id}`);confirmar(`Confirmacion baja ${caso.modulo}`,confirmacion.codigo===200&&decodificar(confirmacion.html).includes(caso.valor));const criterio=caso.recurso==='productos'?'Codigo':caso.recurso==='proveedores'?'Identificacion':null;const parametros=new URLSearchParams({consulta:caso.valor,...(criterio?{criterio}:{})});const busqueda=await solicitarWeb(`/${caso.modulo}/Buscar?${parametros}`);confirmar(`Busqueda ${caso.modulo}`,busqueda.codigo===200&&decodificar(busqueda.html).includes(caso.valor));}
 // Contrasta rechazos de validación local y duplicados API sin perder valores del formulario.
 for(const caso of casos){const invalido={...caso.formulario};const campo=caso.recurso==='proveedores'?'RazonSocial':'Nombre';invalido[campo]='';const rechazo=await enviarFormulario(`/${caso.modulo}/Crear`,invalido);confirmar(`400 MVC conserva formulario ${caso.modulo}`,rechazo.codigo===200&&rechazo.html.includes(`data-valmsg-for="${campo}"`)&&decodificar(rechazo.html).includes(caso.valor));const duplicado=await enviarFormulario(`/${caso.modulo}/Crear`,caso.formulario);confirmar(`409 API muestra error campo ${caso.modulo}`,duplicado.codigo===200&&/class="field-validation-error"/.test(duplicado.html)&&decodificar(duplicado.html).includes(caso.valor));const registros=await consultarApi(`/api/${caso.recurso}`);confirmar(`Rechazos no duplican datos ${caso.modulo}`,registros.filter(registro=>registro[caso.clave[0].toLowerCase()+caso.clave.slice(1)]===caso.valor).length===1);}
 // Modifica cada registro desde MVC y verifica el mismo identificador en API.
 for(const caso of casos){const campo=caso.recurso==='proveedores'?'RazonSocial':'Nombre';caso.formulario[campo]='EditadoQA';const cambio=await enviarFormulario(`/${caso.modulo}/Editar/${caso.id}`,caso.formulario);confirmar(`Edicion MVC ${caso.modulo}`,cambio.codigo===302);const registro=await consultarApi(`/api/${caso.recurso}/${caso.id}`);confirmar(`Edicion persiste mismo Id ${caso.modulo}`,registro.id===caso.id&&registro[campo[0].toLowerCase()+campo.slice(1)]==='EditadoQA');const inexistente=await solicitarWeb(`/${caso.modulo}/Detalles/11111111-1111-1111-1111-111111111111`);confirmar(`Detalle inexistente 404 ${caso.modulo}`,inexistente.codigo===404);}
 // Mantiene la confirmación cuando API bloquea eliminar un proveedor relacionado.
 const confirmacionProveedor=await solicitarWeb(`/Proveedores/Eliminar/${proveedores[0].id}`);const bloqueo=await solicitarWeb(`/Proveedores/EliminarConfirmado/${proveedores[0].id}`,{metodo:'POST',formulario:{__RequestVerificationToken:tokenAntifalsificacion(confirmacionProveedor.html)},seguir:false});confirmar('409 baja proveedor permanece en confirmacion',bloqueo.codigo===200&&decodificar(bloqueo.html).includes('tiene productos relacionados')&&bloqueo.html.includes('Confirmar eliminación'));
 // Elimina registros libres y verifica 404 al repetir la confirmación con el token válido.
 for(const caso of [...casos].reverse()){const pagina=await solicitarWeb(`/${caso.modulo}/Eliminar/${caso.id}`);const formulario={__RequestVerificationToken:tokenAntifalsificacion(pagina.html)};const baja=await solicitarWeb(`/${caso.modulo}/EliminarConfirmado/${caso.id}`,{metodo:'POST',formulario,seguir:false});confirmar(`Baja MVC ${caso.modulo}`,baja.codigo===302);const repetida=await solicitarWeb(`/${caso.modulo}/EliminarConfirmado/${caso.id}`,{metodo:'POST',formulario,seguir:false});confirmar(`Baja repetida 404 ${caso.modulo}`,repetida.codigo===404);}
 }
} catch(error) {fallo={mensaje:error.message,pila:error.stack};process.exitCode=1;}
finally {try{await cerrarProcesos();}catch(error){fallo??={mensaje:error.message};process.exitCode=1;}await fs.writeFile(path.join(carpetaSalida,'resumen.json'),JSON.stringify({aprobadas:resultados.filter(r=>r.aprobado).length,resultados,fallo},null,2));}
console.log(JSON.stringify({aprobadas:resultados.filter(r=>r.aprobado).length,fallo}));



