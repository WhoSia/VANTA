import fs from "node:fs";
import assert from "node:assert/strict";
const read=n=>JSON.parse(fs.readFileSync(new URL(n,import.meta.url),"utf8"));
const data=read("./inputs-v4.json"),gate=read("./gates-v4.json");
assert.deepEqual(gate.researchIds,data.candidates.map(c=>c.id));
assert.ok(data.candidates.every(c=>c.gate==="research"));
const all=[...gate.required,...gate.owner,...gate.blocked,...gate.retired,...data.candidates].map(c=>c.id);
assert.equal(new Set(all).size,all.length);
console.log("Fourth snapshot independent gates verified.");
