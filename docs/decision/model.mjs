// Deterministic scenario sensitivity; no claim of calibrated posterior.
import fs from "node:fs";
import assert from "node:assert/strict";
function calculate(d) {
 const N=d.candidates.length,K=d.axes.length;
 if(K!==12||d.groups.length!==4||d.samples<1||d.samples>100000||new Set(d.candidates.map(c=>c.id)).size!==N)throw Error("invalid model dimensions");
 for(const c of d.candidates)if(c.center.length!==K||c.center.some(x=>!Number.isFinite(x)||x<0||x>1)||!Number.isFinite(c.uncertainty)||c.uncertainty<0||c.uncertainty>1)throw Error("invalid scores");
 const profiles=Object.entries(d.profiles);
 for(const [,v] of profiles)if(v.length!==4||v.some(x=>!Number.isFinite(x)||x<0)||Math.abs(v.reduce((a,b)=>a+b,0)-1)>1e-9)throw Error("invalid group weights");
 let seed=d.seed>>>0;const rng=()=>{seed=(Math.imul(1664525,seed)+1013904223)>>>0;return (seed+.5)/4294967296};
 const clip=x=>Math.max(0,Math.min(1,x));
 const means=Array(N).fill(0),regrets=Array.from({length:N},()=>[]),rank=Array.from({length:N},()=>Array(N).fill(0)),pair=Array.from({length:N},()=>Array(N).fill(0)),scores=Array.from({length:N},()=>[]),profileFirst=Object.fromEntries(profiles.map(([k])=>[k,Array(N).fill(0)])),profileCount=Object.fromEntries(profiles.map(([k])=>[k,0]));
 let bestSum=0;
 for(let s=0;s<d.samples;s++){
  const [name,p]=profiles[s%profiles.length];profileCount[name]++;
  const groupNoise=p.map(x=>x*(.5+rng()));const total=groupNoise.reduce((a,b)=>a+b,0);
  const w=groupNoise.flatMap(g=>{const v=[-Math.log(rng()),-Math.log(rng()),-Math.log(rng())];const z=v.reduce((a,b)=>a+b);return v.map(x=>g/total*x/z)});
  const shared=Array.from({length:4},()=>2*rng()-1);
  const utilities=d.candidates.map(c=>{const idio=2*rng()-1;return c.center.reduce((sum,x,k)=>sum+w[k]*clip(x+c.uncertainty*(.5*shared[Math.floor(k/3)]+.3*idio+.2*(2*rng()-1))),0)});
  const order=utilities.map((x,i)=>i).sort((a,b)=>utilities[b]-utilities[a]||a-b);
  const best=utilities[order[0]];bestSum+=best;
  profileFirst[name][order[0]]++;
  for(let j=0;j<N;j++){rank[order[j]][j]++;means[j]+=utilities[j];scores[j].push(utilities[j]);regrets[j].push(best-utilities[j]);for(let k=0;k<N;k++)pair[j][k]+=utilities[j]>utilities[k]?1:utilities[j]===utilities[k]?.5:0}
 }
 const q=(arr,p)=>{const a=[...arr].sort((x,y)=>x-y);return a[Math.max(0,Math.ceil(p*a.length)-1)]};
 const rows=d.candidates.map((c,i)=>({id:c.id,gate:c.gate,mean:means[i]/d.samples,q05:q(scores[i],.05),q95:q(scores[i],.95),first:rank[i][0]/d.samples,top3:rank[i].slice(0,3).reduce((a,b)=>a+b,0)/d.samples,meanRegret:regrets[i].reduce((a,b)=>a+b,0)/d.samples,p95Regret:q(regrets[i],.95),maxSampleRegret:Math.max(...regrets[i]),profiles:Object.fromEntries(profiles.map(([name])=>[name,profileFirst[name][i]/profileCount[name]])),ranks:rank[i].map(v=>v/d.samples)}));
 const dominates=(a,b)=>a.center.every((x,i)=>x>=b.center[i])&&a.center.some((x,i)=>x>b.center[i]);
 return {schema:1,samples:d.samples,seed:d.seed,interpretation:"scenario sensitivity, not calibrated probabilities; required gates override ranking",rows:rows.sort((a,b)=>b.mean-a.mean),pairwise:pair.map(row=>row.map(x=>x/d.samples)),pairwiseIds:d.candidates.map(c=>c.id),centralPareto:d.candidates.filter(a=>!d.candidates.some(b=>dominates(b,a))).map(c=>c.id),scenarioPerfectInformationGap:bestSum/d.samples-Math.max(...means.map(x=>x/d.samples))};
}
const data=JSON.parse(fs.readFileSync(process.argv[2]??new URL("./inputs.json",import.meta.url),"utf8"));
const result=calculate(data);
assert.deepEqual(result,calculate(data));
for(const row of result.rows){assert.ok(Math.abs(row.ranks.reduce((a,b)=>a+b,0)-1)<1e-9);assert.ok(row.q05<=row.q95);assert.ok(row.meanRegret>=0);}
assert.ok(Math.abs(result.rows.reduce((a,b)=>a+b.first,0)-1)<1e-9);
for(let i=0;i<result.pairwise.length;i++)for(let j=0;j<result.pairwise.length;j++)assert.ok(Math.abs(result.pairwise[i][j]+result.pairwise[j][i]-1)<1e-9);
const invalid=structuredClone(data);invalid.candidates[0].center[0]=1.1;assert.throws(()=>calculate(invalid));
console.log(JSON.stringify(result,null,2));
