const fs=require('fs');const file='Assets/Resources/UI/MainUI.prefab';let txt=fs.readFileSync(file,'utf8');
const parts=txt.split(/^(?=--- !u!)/m);let n=0;
const out=parts.map(p=>{if(/^--- !u!224 /.test(p)&&/m_GameObject: \{fileID: 5941629754620406446\}/.test(p)){n++;return p.replace('m_AnchorMax: {x: 0.98, y: 0.82}','m_AnchorMax: {x: 0.98, y: 0.94}')}return p});
fs.writeFileSync(file,out.join(''));console.log('anchor patched',n);
for(const f of ['Assets/Resources/UI/MainUI.prefab','Assets/Resources/UI/InventoryItem.prefab']){const t=fs.readFileSync(f,'utf8');
console.log(f,'head ok',t.startsWith('%YAML'),'残渣',/\\u6B8B\\u6E23|残渣/.test(t),'融合',/\\u878D\\u5408|融合/.test(t),'排行',/\\u6392\\u884C|排行/.test(t),'装备',/\\u88C5\\u5907|装备/.test(t),'fields',(t.match(/(equipmentContent|leaderboard\w*|inventoryEquipment\w*|inventorySlimeTab|mergeButton):/g)||[]).join(','));}
