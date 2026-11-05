const fs=require('fs');
const [file,idsArg,dropFieldsArg]=process.argv.slice(2);
const txt=fs.readFileSync(file,'utf8');
const parts=txt.split(/^(?=--- !u!)/m);const head=parts[0].startsWith('--- ')?'':parts.shift();
const docs=parts.map(p=>{const m=/^--- !u!(\d+) &(-?\d+)/.exec(p);return {cls:+m[1],id:m[2],body:p}});
const byId=new Map(docs.map(d=>[d.id,d]));
const goOf=d=>(/m_GameObject: \{fileID: (-?\d+)\}/.exec(d.body)||[])[1];
const comps=g=>[...g.body.matchAll(/component: \{fileID: (-?\d+)\}/g)].map(x=>x[1]);
const tfOf=g=>comps(g).map(c=>byId.get(c)).find(c=>c&&(c.cls===224||c.cls===4));
const kids=t=>{const m=/m_Children:\s*\n((?:\s*- \{fileID: -?\d+\}\n)*)/.exec(t.body);return m?[...m[1].matchAll(/fileID: (-?\d+)/g)].map(x=>x[1]):[]};
const removed=new Set();
function collect(goId){const g=byId.get(goId);removed.add(goId);for(const c of comps(g))removed.add(c);const t=tfOf(g);for(const k of kids(t))collect(goOf(byId.get(k)))}
const rootGos=idsArg.split(',');
for(const id of rootGos){const t=tfOf(byId.get(id));const father=/m_Father: \{fileID: (-?\d+)\}/.exec(t.body)[1];const f=byId.get(father);
f.body=f.body.replace(new RegExp('\\n\\s*- \\{fileID: '+t.id+'\\}'),'');collect(id)}
let out=docs.filter(d=>!removed.has(d.id));
const dropFields=(dropFieldsArg||'').split(',').filter(Boolean);
for(const d of out){for(const f of dropFields)d.body=d.body.replace(new RegExp('\\n  '+f+': \\{fileID: -?\\d+\\}'),'');}
const result=head+out.map(d=>d.body).join('');
const dangling=[...removed].filter(id=>new RegExp('fileID: '+id+'\\b').test(result));
fs.writeFileSync(file,result);
console.log(file,'removed docs:',removed.size,'remaining:',out.length,'dangling:',dangling.join(',')||'none');
