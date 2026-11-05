const fs=require('fs');
const file=process.argv[2];const txt=fs.readFileSync(file,'utf8');
const parts=txt.split(/^--- /m);const head=parts.shift();
const docs=parts.map(p=>{const m=/^!u!(\d+) &(-?\d+)( stripped)?/.exec(p);return {cls:+m[1],id:m[2],body:p}});
const byId=new Map(docs.map(d=>[d.id,d]));
const name=d=>(/m_Name: (.*)/.exec(d.body)||[])[1];
const goOf=d=>(/m_GameObject: \{fileID: (-?\d+)\}/.exec(d.body)||[])[1];
const kids=d=>{const m=/m_Children:\s*\n((?:\s*- \{fileID: -?\d+\}\n)*)/.exec(d.body);return m?[...m[1].matchAll(/fileID: (-?\d+)/g)].map(x=>x[1]):[]};
const comps=g=>[...g.body.matchAll(/component: \{fileID: (-?\d+)\}/g)].map(x=>x[1]);
const scriptGuid=d=>(/m_Script: \{fileID: \d+, guid: (\w+)/.exec(d.body)||[])[1];
const metaGuids={};for(const f of require('child_process').execSync('dir /s /b Assets\\Scripts\\*.cs.meta',{encoding:'utf8'}).trim().split(/\r?\n/)){const g=/guid: (\w+)/.exec(fs.readFileSync(f,'utf8'))[1];metaGuids[g]=require('path').basename(f,'.cs.meta')}
const roots=docs.filter(d=>(d.cls===224||d.cls===4)&&/m_Father: \{fileID: 0\}/.test(d.body));
function show(t,depth){const g=byId.get(goOf(t));const cs=comps(g).map(c=>byId.get(c)).filter(c=>c&&c.id!==t.id).map(c=>c.cls===114?(metaGuids[scriptGuid(c)]||'MB:'+scriptGuid(c)?.slice(0,6)):c.cls);
const active=/m_IsActive: 0/.test(g.body)?' (inactive)':'';
console.log('  '.repeat(depth)+name(g)+active+' ['+g.id+'] '+cs.join(','));for(const k of kids(t))show(byId.get(k),depth+1)}
roots.forEach(r=>show(r,0));
