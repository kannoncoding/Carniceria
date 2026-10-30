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

// Conserva los registros de un proceso y sus señales de salida.
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

// Verifica controles de fecha, catalogo de puestos, radios y datos enviados al servidor.
async function probarEmpleado(){
 await navegar('/Empleados/Crear');
 confirmar('Fecha nacimiento usa control date',await evaluar("document.querySelector('#FechaNacimiento').type==='date'"));
 confirmar('Fecha ingreso usa control date',await evaluar("document.querySelector('#FechaIngreso').type==='date'"));
 await completar({FechaNacimiento:'1991-02-03',FechaIngreso:'2020-04-05'});
 confirmar('Resumen nacimiento dd/MM/yyyy',await evaluar("document.querySelector('#resumen-fecha-nacimiento').textContent.trim()==='Fecha seleccionada: 03/02/1991.'"));
 confirmar('Resumen ingreso dd/MM/yyyy',await evaluar("document.querySelector('#resumen-fecha-ingreso').textContent.trim()==='Fecha seleccionada: 05/04/2020.'"));
 await completar({FechaNacimiento:''});
 confirmar('Fecha vacia actualiza resumen',await evaluar("document.querySelector('#resumen-fecha-nacimiento').textContent.trim()==='Fecha seleccionada: sin seleccionar.'"));
 await completar({AreaTrabajo:''});
 confirmar('Sin area puesto deshabilitado',await evaluar("document.querySelector('#Puesto').disabled&&document.querySelector('#Puesto').options.length===1"));
 const catalogo=await evaluar("JSON.parse(document.querySelector('#catalogo-puestos-empleado').textContent)");
 let previo='';
 for(const [area,puestos] of Object.entries(catalogo)){
  await completar({AreaTrabajo:area});
  const estado=await evaluar("({opciones:[...document.querySelector('#Puesto').options].filter(o=>o.value).map(o=>o.value),valor:document.querySelector('#Puesto').value,deshabilitado:document.querySelector('#Puesto').disabled})");
  confirmar('Puestos exactos del area '+area,JSON.stringify(estado.opciones)===JSON.stringify(puestos)&&!estado.deshabilitado);
  confirmar('Cambio de area elimina puesto ajeno '+area,(!previo||!puestos.includes(previo))?estado.valor==='':true);
  await completar({Puesto:puestos[0]});previo=puestos[0];
 }
 const formulario={TipoIdentificacion:'1',Identificacion:'9-4747-0001',Nombre:'Mario',PrimerApellido:'Prueba',SegundoApellido:'Mora',FechaNacimiento:'1991-02-03',SalarioMensual:'650000,50',FechaIngreso:'2020-04-05',AreaTrabajo:'2',Puesto:'Cajero',Turno:'2'};
 await completar(formulario);
 confirmar('Turno radio selecciona un solo valor',await evaluar("document.querySelectorAll('input[name=Turno]:checked').length===1&&document.querySelector('input[name=Turno]:checked').value==='2'"));
 confirmar('Puesto seleccionado queda en area valida',await evaluar("document.querySelector('#Puesto').value==='Cajero'"));
 await capturar('empleado-antes-enviar');
 const desde=solicitudes.length;await enviar();
 confirmar('Crear empleado redirige al listado',await evaluar("location.pathname==='/Empleados'"));
 const registro=(await datos('empleados')).find(e=>e.identificacion===formulario.Identificacion);
 confirmar('Empleado conserva salario fraccionario',registro?.salarioMensual===650000.5);
 confirmar('Empleado conserva fechas ISO y turno',registro?.fechaNacimiento.startsWith('1991-02-03')&&registro?.fechaIngreso.startsWith('2020-04-05')&&registro?.turno===2);
 const peticion=solicitudes.slice(desde).find(s=>s.metodo==='POST'&&s.ruta==='/api/empleados');
 confirmar('JSON salario es number fraccionario',typeof peticion?.json?.salarioMensual==='number'&&peticion.json.salarioMensual===650000.5);
 confirmar('JSON fechas son ISO sin alteracion',/^1991-02-03(?:T|$)/.test(peticion?.json?.fechaNacimiento)&&/^2020-04-05(?:T|$)/.test(peticion?.json?.fechaIngreso));
 await navegar('/Empleados/Editar/'+registro.id);
 confirmar('Editar fecha ISO del control',await evaluar("document.querySelector('#FechaNacimiento').value==='1991-02-03'&&document.querySelector('#FechaIngreso').value==='2020-04-05'"));
 confirmar('Editar conserva area puesto y radio',await evaluar("document.querySelector('#AreaTrabajo').value==='2'&&document.querySelector('#Puesto').value==='Cajero'&&document.querySelector('input[name=Turno]:checked')?.value==='2'"));
 // Un duplicado validado por la API comprueba la restauracion de controles tras error de servidor.
 await navegar('/Empleados/Crear');await completar({...formulario,AreaTrabajo:'3',Puesto:'Carnicero',Turno:'3'});
 const antesRechazo=solicitudes.length;await enviar();
 confirmar('Duplicado permanece en formulario',await evaluar("location.pathname==='/Empleados/Crear'"));
 confirmar('Duplicado API devuelve 409',solicitudes.slice(antesRechazo).some(s=>s.metodo==='POST'&&s.codigo===409));
 confirmar('Error muestra mensaje campo',await evaluar("document.querySelector('[data-valmsg-for=Identificacion]').textContent.trim().length>0"));
 confirmar('Error conserva area puesto y turno',await evaluar("document.querySelector('#AreaTrabajo').value==='3'&&document.querySelector('#Puesto').value==='Carnicero'&&document.querySelector('input[name=Turno]:checked')?.value==='3'"));
 confirmar('Error conserva salario con coma',await evaluar("document.querySelector('#SalarioMensual').value==='650000,50'"));
 await capturar('empleado-error-servidor');
}

// Comprueba etiquetas dinamicas, decimales y alertas del inventario en tres vistas.
async function probarProducto(){
 const proveedor=(await datos('proveedores'))[0];
 const formulario={ProveedorId:proveedor.id,Codigo:'F4INTERFAZ',Nombre:'ProductoInterfaz',Categoria:'1',UnidadMedida:'1',PrecioUnidadMedida:'1250,50',ExistenciaActual:'1,25',ExistenciaMinima:'1,25'};
 await navegar('/Productos/Crear');await completar(formulario);
 for(const unidad of ['1','2','3','4','5']){
  await completar({UnidadMedida:unidad});
  const estado=await evaluar("({nombre:document.querySelector('#UnidadMedida').selectedOptions[0].textContent.trim().toLowerCase(),precio:document.querySelector('#etiqueta-precio').textContent,actual:document.querySelector('#etiqueta-existencia-actual').textContent,minima:document.querySelector('#etiqueta-existencia-minima').textContent,ayuda:document.querySelector('#ayuda-unidad-medida').textContent,valores:['PrecioUnidadMedida','ExistenciaActual','ExistenciaMinima'].map(id=>document.getElementById(id).value)})");
  const plural=estado.nombre==='unidad'?'unidades':estado.nombre+'s';
  confirmar('Etiquetas unidad '+unidad,estado.precio==='Precio por '+estado.nombre+' (₡)'&&estado.actual==='Existencia actual ('+plural+')'&&estado.minima==='Existencia mínima ('+plural+')');
  confirmar('Ayuda unidad '+unidad,estado.ayuda.includes('colones por '+estado.nombre)&&estado.ayuda.includes(plural));
  confirmar('Cambio unidad no convierte valores '+unidad,JSON.stringify(estado.valores)===JSON.stringify(['1250,50','1,25','1,25']));
 }
 await completar({UnidadMedida:'1'});const desde=solicitudes.length;await enviar();
 confirmar('Crear producto redirige al listado',await evaluar("location.pathname==='/Productos'"));
 const registro=(await datos('productos')).find(p=>p.codigo===formulario.Codigo);
 confirmar('Producto conserva fracciones',registro?.precioUnidadMedida===1250.5&&registro?.existenciaActual===1.25&&registro?.existenciaMinima===1.25);
 const peticion=solicitudes.slice(desde).find(s=>s.metodo==='POST'&&s.ruta==='/api/productos');
 for(const [campo,valor] of Object.entries({precioUnidadMedida:1250.5,existenciaActual:1.25,existenciaMinima:1.25}))confirmar('JSON number '+campo,typeof peticion?.json?.[campo]==='number'&&peticion.json[campo]===valor);
 for(const [etapa,actual,esperado] of [['igual','1,25',true],['inferior','0,75',true],['superior','2,5',false]]){
  if(etapa!=='igual'){await navegar('/Productos/Editar/'+registro.id);await completar({ExistenciaActual:actual});await enviar();confirmar('Editar stock '+etapa+' redirige',await evaluar("location.pathname==='/Productos'"));}
  for(const ruta of ['/Productos','/Productos/Detalles/'+registro.id,'/Productos/Buscar?criterio=Codigo&consulta=F4INTERFAZ']){
   await navegar(ruta);
   confirmar('Producto visible stock '+etapa+' en '+ruta,await evaluar("document.body.textContent.includes('F4INTERFAZ')"));
   const alerta=ruta.includes('/Detalles/')?await evaluar("!!document.querySelector('.mensaje-advertencia')"):await evaluar("(()=>{const fila=[...document.querySelectorAll('tbody tr')].find(f=>f.textContent.includes('F4INTERFAZ'));return !!fila?.querySelector('.estado-stock-bajo');})()");
   confirmar('Alerta stock '+etapa+' en '+ruta,alerta===esperado);
   if(etapa==='igual')await capturar('stock-igual-'+(ruta.includes('/Detalles/')?'detalle':ruta.includes('/Buscar')?'busqueda':'listado'));
  }
 }
}

// Recorre controles de cliente para comprobar fechas y persistencia con el navegador.
async function probarCliente(){
 await navegar('/Clientes/Crear');
 const formulario={TipoIdentificacion:'1',Identificacion:'9-4747-0002',Nombre:'Elena',PrimerApellido:'Prueba',SegundoApellido:'Mora',FechaNacimiento:'1992-12-31'};
 await completar(formulario);
 confirmar('Cliente fecha date y resumen',await evaluar("document.querySelector('#FechaNacimiento').type==='date'&&document.querySelector('#FechaNacimiento').value==='1992-12-31'&&document.querySelector('#resumen-fecha-nacimiento').textContent.trim()==='Fecha seleccionada: 31/12/1992.'"));
 await enviar();confirmar('Cliente creado desde navegador',await evaluar("location.pathname==='/Clientes'"));
 const registro=(await datos('clientes')).find(c=>c.identificacion===formulario.Identificacion);
 confirmar('Cliente API recibe fecha ISO',registro?.fechaNacimiento.startsWith('1992-12-31'));
}

// Cierra todos los procesos propios aunque falle el cierre de otro servicio.
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
 await probarEmpleado();await probarProducto();await probarCliente();
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

