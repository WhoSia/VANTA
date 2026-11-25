// Research-only C ABI. The caller must supply a valid readable region for n bytes.
fn sample(mut p: &[u8], width: usize) -> Option<bool> {
    if p.is_empty() || !matches!(width,1|2|4) { return None; }
    let (mut idr,mut other)=(false,false);
    while !p.is_empty() {
        if p.len()<width { return None; }
        let mut len=0u32;
        for &b in &p[..width] { len=(len<<8)|u32::from(b); }
        p=&p[width..];
        let n=len as usize;
        if n==0 || n>p.len() || p[0]&128!=0 { return None; }
        match p[0]&31 { 5=>idr=true,6..=12=>{},_=>other=true }
        p=&p[n..];
    }
    Some(idr&&!other)
}
fn count(mut p:&[u8],width:usize)->Option<i64> {
    if p.is_empty() || p.len()>128*1024*1024 || !matches!(width,1|2|4) { return None; }
    let mut total=0i64;
    while !p.is_empty() {
        let header:[u8;4]=p.get(..4)?.try_into().ok()?;
        let n=u32::from_le_bytes(header) as usize;
        p=&p[4..];
        if n==0 || n>p.len() { return None; }
        if sample(&p[..n],width)? { total+=1; }
        p=&p[n..];
    }
    Some(total)
}
#[no_mangle]
pub unsafe extern "C" fn vanta_count_idr(p:*const u8,n:usize,width:u32)->i64 {
    if p.is_null() || n==0 || n>128*1024*1024 { return -1; }
    // SAFETY: caller must pin and provide a valid readable buffer of the stated length.
    let data=unsafe { std::slice::from_raw_parts(p,n) };
    count(data,width as usize).unwrap_or(-1)
}
#[cfg(test)]
mod tests {
    use super::*;
    #[test] fn examples() {
        assert_eq!(count(&[6,0,0,0,0,0,0,2,0x65,0x88],4),Some(1));
        assert_eq!(count(&[3,0,0,0,2,0x61,0x88],1),Some(0));
        assert_eq!(count(&[2,0,0,0,0,0],1),None);
        assert_eq!(count(&[255,255,255,255],4),None);
        assert_eq!(count(&[],4),None);
    }
}
