const fs=require('fs');const [f,re]=process.argv.slice(2);fs.readFileSync(f,'utf8').split(/\n/).forEach((l,i)=>{if(new RegExp(re).test(l))console.log(i+1,l.slice(0,300))})
