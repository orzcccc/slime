const fs=require('fs'),p=require('path');
const re=new RegExp(process.argv[2],'i');const listOnly=process.argv[3]==='-c';
const roots=process.argv[4]?process.argv[4].split(','):['server/src','server/test','Assets/Scripts','Assets/Editor'];
function walk(d){for(const f of fs.readdirSync(d)){const q=p.join(d,f);const s=fs.statSync(q);
if(s.isDirectory()){if(f!=='node_modules')walk(q)}else if(/\.(js|cs|prefab)$/.test(f)){
const L=fs.readFileSync(q,'utf8').split(/\r?\n/);let n=0;
L.forEach((l,i)=>{if(re.test(l)){n++;if(!listOnly)console.log(q+':'+(i+1)+': '+l.trim().slice(0,200))}});
if(listOnly)console.log(String(n).padStart(4),String(L.length).padStart(6),q)}}}
roots.forEach(walk);
