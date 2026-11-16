using System.Buffers.Binary;

namespace Engine.Core;

// Keep only a page of source frames per voice. RIFF chunks may be padded or in arbitrary order.
internal sealed record WaveInfo(string Path,int Channels,int Rate,int Bits,bool Floating,long Offset,long Frames) : AudioFile(Path, Channels, Rate, Frames)
{
    public int Stride => Channels*(Bits/8);
    internal override IAudioStream Open() => new WaveStream(this);
    public static WaveInfo ReadFile(string path)
    {
        using var f=File.OpenRead(path);
        using var r=new BinaryReader(f);
        InvalidDataException Invalid(string reason)=>new($"{path}: {reason}");
        if(f.Length<12 || r.ReadUInt32()!=0x46464952) throw Invalid("expected a little-endian RIFF WAV.");
        long end=8L+r.ReadUInt32();
        if(end>f.Length || r.ReadUInt32()!=0x45564157) throw Invalid("invalid or truncated WAVE header.");
        int type=0,channels=0,hz=0,bits=0,align=0; long data=-1,length=0;
        while(f.Position+8<=end)
        {
            uint chunk=r.ReadUInt32(),size=r.ReadUInt32(); long start=f.Position;
            if(start+size>end) throw Invalid("truncated WAV chunk.");
            if(chunk==0x20746D66)
            {
                if(size<16) throw Invalid("invalid format chunk.");
                type=r.ReadUInt16();channels=r.ReadUInt16();hz=r.ReadInt32();r.ReadUInt32();align=r.ReadUInt16();bits=r.ReadUInt16();
                if(type==0xfffe && size>=40)
                {
                    int ext=r.ReadUInt16();r.ReadUInt16();r.ReadUInt32();
                    var guid=new Guid(r.ReadBytes(16));
                    type=ext<22?0:guid==new Guid("00000001-0000-0010-8000-00aa00389b71")?1:guid==new Guid("00000003-0000-0010-8000-00aa00389b71")?3:0;
                }
            }
            else if(chunk==0x61746164 && data<0) { data=start;length=size; }
            f.Position=start+size+(size&1);
        }
        if(channels is not (1 or 2) || hz is <1000 or >384000 ||
            !(type==1 && bits is 8 or 16 or 24 or 32 || type==3 && bits==32) || align!=channels*(bits/8))
            throw Invalid("use mono or stereo PCM (8/16/24/32 bit) or float32 WAV.");
        if(data<0 || length<align || length%align!=0) throw Invalid("missing or partial sample frames.");
        return new(path,channels,hz,bits,type==3,data,length/align);
    }
}

internal sealed class WaveStream(WaveInfo info) : IAudioStream
{
    private readonly FileStream file=new(info.Path,FileMode.Open,FileAccess.Read,FileShare.Read,1);
    private readonly byte[] page=new byte[1024*info.Stride];
    private long start=-1;
    private int count;
    public (float L,float R) Sample(long index)
    {
        if(index<start || index>=start+count)
        {
            start=index;count=(int)Math.Min(1024,info.Frames-index);
            file.Position=info.Offset+index*info.Stride; file.ReadExactly(page.AsSpan(0,count*info.Stride));
        }
        int at=(int)(index-start)*info.Stride;
        float l=Value(at);
        return (l,info.Channels==1?l:Value(at+info.Bits/8));
    }
    private float Value(int at)
    {
        var s=page.AsSpan(at);
        if(info.Floating)
        {
            float v=BinaryPrimitives.ReadSingleLittleEndian(s);
            return float.IsFinite(v)?Math.Clamp(v,-1,1):0;
        }
        return info.Bits switch
        {
            8=>(s[0]-128)/128f,
            16=>BinaryPrimitives.ReadInt16LittleEndian(s)/32768f,
            24=>((s[0]<<8|s[1]<<16|s[2]<<24)>>8)/8388608f,
            _=>BinaryPrimitives.ReadInt32LittleEndian(s)/2147483648f
        };
    }
    public void Dispose()=>file.Dispose();
}
