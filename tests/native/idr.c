#include <stdint.h>
#include <stddef.h>
#include <limits.h>
static int sample(const uint8_t *p, size_t n, uint32_t width) {
    if (!n || (width != 1 && width != 2 && width != 4)) return -1;
    int idr=0, other=0;
    while(n) {
        if(n<width) return -1;
        uint32_t len=0;
        for(uint32_t i=0;i<width;i++) len=(len<<8)|p[i];
        p+=width; n-=width;
        if(!len || len>n || (p[0]&128)) return -1;
        int type=p[0]&31;
        if(type==5) idr=1;
        else if(type<6 || type>12) other=1;
        p+=len; n-=len;
    }
    return idr&&!other;
}
int64_t vanta_count_idr(const uint8_t *p, size_t n, uint32_t width) {
    if(!p || !n || n>128u*1024*1024 || (width!=1 && width!=2 && width!=4)) return -1;
    int64_t total=0;
    while(n) {
        if(n<4) return -1;
        uint32_t len=(uint32_t)p[0]|((uint32_t)p[1]<<8)|((uint32_t)p[2]<<16)|((uint32_t)p[3]<<24);
        p+=4; n-=4;
        if(!len || len>n) return -1;
        int result=sample(p,len,width);
        if(result<0) return -1;
        total+=result; p+=len; n-=len;
    }
    return total;
}
