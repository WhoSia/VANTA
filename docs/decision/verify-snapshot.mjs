import fs from "node:fs";
import assert from "node:assert/strict";
const actual=JSON.parse(fs.readFileSync(process.argv[2],"utf8"));
const expected=JSON.parse(fs.readFileSync(new URL("./results.json",import.meta.url),"utf8"));
function compare(a,b){
 if(typeof a==="number"&&typeof b==="number"){assert.ok(Number.isFinite(a)&&Math.abs(a-b)<1e-10,`numeric mismatch ${a} ${b}`);return;}
 if(a===null||b===null||typeof a!=="object"||typeof b!=="object"){assert.equal(a,b);return;}
 assert.deepEqual(Object.keys(a),Object.keys(b));for(const k of Object.keys(a))compare(a[k],b[k]);
}
compare(actual,expected);console.log("Decision snapshot matches (numeric tolerance 1e-10).");
