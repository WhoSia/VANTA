namespace Vanta.Core;

// Research-only minimax frame-count partition over observed source keyframes.
// A keyframe is a boundary candidate, not proof of safe direct seeking.
internal static class SegmentPartition
{
    internal const int MaxKeyframes=512;
    internal static long[]? Balanced(long frames,int segments,long minimum,IReadOnlyList<long> keyframes)
    {
        if(frames<1||segments<2||segments>16||minimum<1||minimum>frames/segments)
            return null;
        if(keyframes.Count>MaxKeyframes)return null;
        var points=new List<long>{0};
        long previous=-1;
        foreach(long k in keyframes)
        {
            if(k<0||k>=frames||k<=previous)throw new ArgumentException("Invalid keyframe sequence");
            previous=k;
            if(k!=0)points.Add(k);
        }
        points.Add(frames);
        int count=points.Count;
        var costs=new long[segments+1,count];
        var parents=new int[segments+1,count];
        for(int n=0;n<=segments;n++)
        for(int j=0;j<count;j++){costs[n,j]=long.MaxValue;parents[n,j]=-1;}
        costs[0,0]=0;
        for(int n=1;n<=segments;n++)
        for(int j=1;j<count;j++)
        {
            for(int k=0;k<j;k++)
            {
                long length=points[j]-points[k];
                if(length<minimum||costs[n-1,k]==long.MaxValue)continue;
                long cost=Math.Max(length,costs[n-1,k]);
                if(cost<costs[n,j]){costs[n,j]=cost;parents[n,j]=k;}
            }
        }
        if(costs[segments,count-1]==long.MaxValue)return null;
        var result=new long[segments+1];
        int end=count-1;
        for(int n=segments;n>=0;n--)
        {
            result[n]=points[end];
            if(n>0)end=parents[n,end];
        }
        return result;
    }
}
