import fs from "node:fs";
import assert from "node:assert/strict";
function summarize(rows){
 const group=id=>id.startsWith("earth-")?"earth":id;
 const mean=xs=>xs.reduce((a,b)=>a+b,0)/xs.length;
 const q=(xs,p)=>[...xs].sort((a,b)=>a-b)[Math.max(0,Math.ceil(p*xs.length)-1)];
 const modes=[...new Set(rows.map(r=>r.candidate))];
 return modes.map(candidate=>{
  const rs=rows.filter(r=>r.candidate===candidate);
  const groups=[...new Set(rs.map(r=>group(r.id)))];
  const clusters=groups.map(id=>{const a=rs.filter(r=>group(r.id)===id);return {id,n:a.length,meanTimeReduction:mean(a.map(r=>r.timeReduction)),matched:mean(a.map(r=>+r.matched)),jointWin:mean(a.map(r=>+(r.matched&&r.timeReduction>0))),jointTarget:mean(a.map(r=>+(r.matched&&r.timeReduction>=.15)))}});
  const loo=clusters.map((_,i)=>mean(clusters.filter((_,j)=>i!==j).map(x=>x.meanTimeReduction)));
  const boot=[];function rec(i,sum){if(i===clusters.length){boot.push(sum/clusters.length);return;}for(const g of clusters)rec(i+1,sum+g.meanTimeReduction)}rec(0,0);
  return {candidate,n:rs.length,independentContentGroups:groups.length,matched:rs.filter(r=>r.matched).length,jointWin:rs.filter(r=>r.matched&&r.timeReduction>0).length,jointTarget:rs.filter(r=>r.matched&&r.timeReduction>=.15).length,contentWeightedMeanTimeReduction:mean(clusters.map(x=>x.meanTimeReduction)),contentWeightedJointWin:mean(clusters.map(x=>x.jointWin)),leaveOneContentOut:[Math.min(...loo),Math.max(...loo)],clusterResampleSensitivity:[q(boot,.05),q(boot,.95)],resampleCount:boot.length,clusters};
 });
}
const rows=JSON.parse(fs.readFileSync(new URL("./public-pairs.json",import.meta.url),"utf8"));
assert.equal(rows.length,30);assert.equal(new Set(rows.map(r=>[r.id,r.rep,r.candidate].join("/"))).size,30);
const result=summarize(rows);for(const r of result){assert.equal(r.n,10);assert.equal(r.independentContentGroups,4);assert.equal(r.resampleCount,256);assert.ok(r.jointTarget<=r.jointWin&&r.jointWin<=r.matched);}
console.log(JSON.stringify(result,null,2));
