using System;
using NAudio.Wave;
namespace VWP;
public sealed class AudioReaction : IDisposable
{
    WasapiLoopbackCapture? capture;
    float level;
    long updated;
    public string? Error {get;private set;}
    public double Level=>Environment.TickCount64-updated>300?0:level;
    public void SetEnabled(bool enabled)
    {
        if(enabled && capture is null)
        {
            try
            {
                capture=new WasapiLoopbackCapture();capture.WaveFormat=WaveFormat.CreateIeeeFloatWaveFormat(capture.WaveFormat.SampleRate,capture.WaveFormat.Channels);
                capture.DataAvailable+=(_,e)=>{level=Measure(e.Buffer,e.BytesRecorded);updated=Environment.TickCount64;};
                capture.RecordingStopped+=(_,e)=>{if(e.Exception is not null)Error=e.Exception.Message;};
                capture.StartRecording();Error=null;
            }
            catch(Exception e){Error=e.Message;Dispose();}
        }
        else if(!enabled)Dispose();
    }
    internal static float Measure(byte[] buffer,int count)
    {
        double sum=0;int samples=count/4;for(int i=0;i<samples;i++){float value=BitConverter.ToSingle(buffer,i*4);if(float.IsFinite(value))sum+=value*value;}
        return samples==0?0:(float)Math.Clamp(Math.Sqrt(sum/samples)*3,0,1);
    }
    public void Dispose(){var old=capture;capture=null;if(old is not null){old.StopRecording();old.Dispose();}level=0;}
}
