using System.Windows.Media;
namespace VWP;
public static class SceneActors
{
    public static string Description(int id)=>SceneAtmosphere.Description(id);
    public static void Draw(DrawingContext drawing,int id,double seconds,bool economy)=>SceneAtmosphere.Draw(drawing,id,seconds,economy);
}
