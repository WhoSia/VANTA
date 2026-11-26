// Research-only, no file I/O, no encoder bypass, no output publication.
// sourceCurrent is supplied by a separate source reinspection; never infer it from size alone.
export function advise(x){
  if(!Number.isSafeInteger(x.sourceBytes)||x.sourceBytes<=0||!Number.isSafeInteger(x.maxBytes)||x.maxBytes<=0)throw new RangeError("Positive exact byte counts required");
  if(!["size-only","transform","unknown"].includes(x.intent)||typeof x.sourceCurrent!=="boolean"||typeof x.requiresNewOutput!=="boolean")throw new TypeError("Explicit intent and source state required");
  if(!x.sourceCurrent)return "reinspect-source";
  if(x.sourceBytes>x.maxBytes)return "target-not-met";
  if(x.intent!=="size-only"||x.requiresNewOutput)return "target-met-but-transform-requested";
  return "offer-keep-original";
}
const cases=[
  [
    100,
    100,
    "size-only",
    true,
    false,
    "offer-keep-original"
  ],
  [
    99,
    100,
    "size-only",
    true,
    false,
    "offer-keep-original"
  ],
  [
    101,
    100,
    "size-only",
    true,
    false,
    "target-not-met"
  ],
  [
    100,
    100,
    "transform",
    true,
    false,
    "target-met-but-transform-requested"
  ],
  [
    100,
    100,
    "unknown",
    true,
    false,
    "target-met-but-transform-requested"
  ],
  [
    100,
    100,
    "size-only",
    true,
    true,
    "target-met-but-transform-requested"
  ],
  [
    99,
    100,
    "size-only",
    false,
    false,
    "reinspect-source"
  ],
  [
    101,
    100,
    "size-only",
    false,
    false,
    "reinspect-source"
  ],
  [
    9007199254740991,
    9007199254740991,
    "size-only",
    true,
    false,
    "offer-keep-original"
  ],
  [
    9007199254740991,
    9007199254740990,
    "size-only",
    true,
    false,
    "target-not-met"
  ]
];
const run=(x)=>advise({sourceBytes:x[0],maxBytes:x[1],intent:x[2],sourceCurrent:x[3],requiresNewOutput:x[4]});
for(const c of cases)if(run(c)!==c[5])throw Error("boundary case "+JSON.stringify(c));
const invalid=[{sourceBytes:0},{sourceBytes:-1},{sourceBytes:1.5},{maxBytes:Infinity},{maxBytes:NaN},{sourceBytes:Number.MAX_SAFE_INTEGER+1},{intent:""},{sourceCurrent:1},{requiresNewOutput:null}];
for(const patch of invalid){let failed=false;try{advise({sourceBytes:100,maxBytes:100,intent:"size-only",sourceCurrent:true,requiresNewOutput:false,...patch})}catch(e){failed=e instanceof RangeError||e instanceof TypeError}if(!failed)throw Error("invalid not rejected");}
let n=0,keep=0;for(const sourceBytes of [1,99,100,101,1000,1000000000,Number.MAX_SAFE_INTEGER])for(const maxBytes of [1,99,100,101,1000,1000000000,Number.MAX_SAFE_INTEGER])for(const intent of ["size-only","transform","unknown"])for(const sourceCurrent of [true,false])for(const requiresNewOutput of [true,false]){const r=advise({sourceBytes,maxBytes,intent,sourceCurrent,requiresNewOutput});n++;if(r==="offer-keep-original"){keep++;if(!(sourceCurrent&&sourceBytes<=maxBytes&&intent==="size-only"&&!requiresNewOutput))throw Error("unsafe offer");}else if(sourceCurrent&&sourceBytes<=maxBytes&&intent==="size-only"&&!requiresNewOutput)throw Error("missing offer")}
console.log(JSON.stringify({boundaries:cases.length,invalid:invalid.length,grid:n,keepOffers:keep}));
