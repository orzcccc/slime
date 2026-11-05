const fs=require('fs');
const [file,...ids]=process.argv.slice(2);const txt=fs.readFileSync(file,'utf8');
const parts=txt.split(/^--- /m);parts.shift();
for(const p of parts){const m=/^!u!(\d+) &(-?\d+)/.exec(p);if(m[1]!=='224')continue;const go=/m_GameObject: \{fileID: (-?\d+)\}/.exec(p)[1];if(!ids.includes(go))continue;
const g=k=>(new RegExp(k+': (.*)').exec(p)||[])[1];console.log(go,'min',g('m_AnchorMin'),'max',g('m_AnchorMax'),'pos',g('m_AnchoredPosition'),'size',g('m_SizeDelta'))}
