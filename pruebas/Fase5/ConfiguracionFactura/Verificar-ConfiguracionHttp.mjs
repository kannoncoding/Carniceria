import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import net from 'node:net';
import {spawn} from 'node:child_process';
import {parseArgs} from 'node:util';
import {setTimeout as esperar} from 'node:timers/promises';

// Aísla el proceso de API y conserva evidencia en una carpeta indicada.
const {values: opciones}=parseArgs({options:{raiz:{type:'string',default:process.cwd()},salida:{type:'string'},puerto:{type:'string',default:'5234'}}});
const raiz=path.resolve(opciones.raiz);
const salida=opciones.salida?path.resolve(opciones.salida):await fs.mkdtemp(path.join(os.tmpdir(),'carniceria-configuracion-'));
await fs.mkdir(salida,{recursive:true});
const base='http://127.0.0.1:'+opciones.puerto;
const resultados=[];
let proceso,fallo=null,errorCierre=null,registro='';

// Registra una comprobación del contrato HTTP.
function verificar(caso,condicion){resultados.push({caso,aprobado:!!condicion});if(!condicion)throw new Error(caso);}

// Comprueba que el puerto esté libre sin detener procesos externos.
async function puertoLibre(){const servidor=net.createServer();await new Promise((resolver,rechazar)=>{servidor.once('error',rechazar);servidor.listen(Number(opciones.puerto),'127.0.0.1',resolver);});await new Promise(resolver=>servidor.close(resolver));}

// Espera la ruta de salud antes de ejecutar la consulta de configuración.
async function esperarApi(){const fin=Date.now()+15000;while(Date.now()<fin){try{if((await fetch(base+'/api/estado',{signal:AbortSignal.timeout(1000)})).ok)return;}catch{}await esperar(100);}throw new Error('La API propia no inicio.');}

try{
 await puertoLibre();
 const carpeta=path.join(raiz,'Carniceria.Api');
 proceso=spawn('dotnet',[path.join(carpeta,'bin','Debug','net10.0','Carniceria.Api.dll'),'--urls',base],{cwd:carpeta,env:{...process.env,ASPNETCORE_ENVIRONMENT:'Development'},windowsHide:true,stdio:['ignore','pipe','pipe']});
 proceso.stdout.on('data',datos=>registro+=datos);proceso.stderr.on('data',datos=>registro+=datos);proceso.on('error',error=>registro+=error.message);
 await esperarApi();
 const respuesta=await fetch(base+'/api/facturas/configuracion',{signal:AbortSignal.timeout(5000)});
 const texto=await respuesta.text();
 await fs.writeFile(path.join(salida,'respuesta.json'),texto);
 await fs.writeFile(path.join(salida,'cabeceras.json'),JSON.stringify({codigo:respuesta.status,cabeceras:Object.fromEntries(respuesta.headers)},null,2));
 verificar('GET configuracion devuelve 200',respuesta.status===200);
 verificar('Configuracion responde application/json',/^application\/json\b/i.test(respuesta.headers.get('content-type')??''));
 const configuracion=JSON.parse(texto);
 verificar('Descuento se recibe como numero JSON',typeof configuracion.porcentajeDescuentoMaximo==='number');
 verificar('IVA se recibe como numero JSON',typeof configuracion.porcentajeIva==='number');
 verificar('Descuento maximo vigente es 25',configuracion.porcentajeDescuentoMaximo===25);
 verificar('IVA vigente es 13',configuracion.porcentajeIva===13);
}catch(error){fallo=error;console.error(error.stack);}
finally{
 try{if(proceso&&proceso.exitCode===null&&proceso.signalCode===null){const cierre=new Promise(resolver=>proceso.once('close',resolver));proceso.kill();await Promise.race([cierre,esperar(5000)]);}if(proceso&&proceso.exitCode===null&&proceso.signalCode===null)throw new Error('No se confirmo cierre de API propia.');}catch(error){errorCierre=error.message;}
 let libre=false;try{await puertoLibre();libre=true;}catch{}
 const resumen={grupo:'ConfiguracionHttp',total:resultados.length,aprobadas:resultados.filter(r=>r.aprobado).length,fallo:fallo?.message??null,errorCierre,puerto:Number(opciones.puerto),puertoLibre:libre,resultados};
 await fs.writeFile(path.join(salida,'api.log'),registro);
 await fs.writeFile(path.join(salida,'resumen.json'),JSON.stringify(resumen,null,2));
 console.log(JSON.stringify(resumen));
 if(fallo||errorCierre||!libre)process.exitCode=1;
}

