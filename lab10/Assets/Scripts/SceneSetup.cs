using UnityEngine;

public class SceneSetup : MonoBehaviour
{
    public Camera sceneCamera;
    public Transform[] demoObjects;
    public Transform[] samplePoints;

    void Start()
    {
        CreateScene();
        ViewingPipelineController controller=gameObject.AddComponent<ViewingPipelineController>();
        controller.Initialize(sceneCamera, samplePoints);
        ViewingPipelineUI ui=gameObject.AddComponent<ViewingPipelineUI>();
        ui.Initialize(controller);
    }

    Material Mat(string name, Color color)
    {
        Shader s=Shader.Find("Standard"); Material m=new Material(s); m.name=name; m.color=color; return m;
    }
    GameObject Primitive(PrimitiveType type,string name,Vector3 pos,Vector3 scale,Color color)
    {
        GameObject g=GameObject.CreatePrimitive(type); g.name=name; g.transform.position=pos; g.transform.localScale=scale; g.GetComponent<Renderer>().material=Mat(name+"Material",color); return g;
    }
    void CreateScene()
    {
        sceneCamera=new GameObject("Pipeline Camera").AddComponent<Camera>();
        sceneCamera.transform.position=new Vector3(8f,5.5f,10f); sceneCamera.transform.LookAt(new Vector3(0,1,0));
        sceneCamera.clearFlags=CameraClearFlags.SolidColor; sceneCamera.backgroundColor=new Color(0.055f,0.07f,0.1f); sceneCamera.fieldOfView=55f;
        GameObject light=new GameObject("Directional Light"); Light dl=light.AddComponent<Light>(); dl.type=LightType.Directional; dl.intensity=1.2f; light.transform.rotation=Quaternion.Euler(50,-30,0);
        Primitive(PrimitiveType.Plane,"Ground",new Vector3(0,0,0),new Vector3(3,1,3),new Color(.18f,.2f,.23f));
        demoObjects=new Transform[3];
        demoObjects[0]=Primitive(PrimitiveType.Cube,"Red Cube",new Vector3(-2,1,0),Vector3.one*2,new Color(.85f,.12f,.1f)).transform;
        demoObjects[1]=Primitive(PrimitiveType.Sphere,"Blue Sphere",new Vector3(1,1.25f,2),Vector3.one*2.5f,new Color(.1f,.35f,.9f)).transform;
        demoObjects[2]=Primitive(PrimitiveType.Cylinder,"Yellow Cylinder",new Vector3(3,1.5f,-2),new Vector3(1.4f,1.5f,1.4f),new Color(.95f,.7f,.08f)).transform;
        CreateAxes();
        samplePoints=new Transform[3];
        for(int i=0;i<demoObjects.Length;i++){ GameObject marker=GameObject.CreatePrimitive(PrimitiveType.Sphere); marker.name="SamplePoint_"+i; marker.transform.position=demoObjects[i].position+Vector3.up*1.2f; marker.transform.localScale=Vector3.one*.12f; marker.GetComponent<Renderer>().material=Mat("Marker"+i,Color.white); samplePoints[i]=marker.transform; }
    }
    void CreateAxes()
    {
        GameObject x=Primitive(PrimitiveType.Cube,"World X Axis",new Vector3(4,0.08f,0),new Vector3(8,.04f,.04f),Color.red);
        GameObject y=Primitive(PrimitiveType.Cube,"World Y Axis",new Vector3(0,4,0),new Vector3(.04f,8,.04f),Color.green);
        GameObject z=Primitive(PrimitiveType.Cube,"World Z Axis",new Vector3(0,.08f,4),new Vector3(.04f,.04f,8),Color.blue);
        x.transform.SetParent(transform); y.transform.SetParent(transform); z.transform.SetParent(transform);
    }
}
