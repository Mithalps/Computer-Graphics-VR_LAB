using System.Text;
using UnityEngine;

public class ViewingPipelineController : MonoBehaviour
{
    public enum ProjectionMode { Perspective, Orthographic }
    public ProjectionMode mode=ProjectionMode.Perspective;
    public float fieldOfView=60f, orthographicWidth=12f, nearPlane=.1f, farPlane=100f;
    public int viewportWidth=800, viewportHeight=450; public Vector2 viewportOrigin=Vector2.zero;
    public Camera sceneCamera; public Transform[] samplePoints; public string Report { get; private set; }
    public Matrix4x4 ModelMatrix {get;private set;} public Matrix4x4 ViewMatrix {get;private set;} public Matrix4x4 ProjectionMatrix {get;private set;}
    public ViewingPipelineMath.PipelineResult[] Results {get;private set;}

    // Set by Calculate(): how many times the CALCULATE button has run, and a one-paragraph result summary for the UI.
    public int CalculationCount { get; private set; }
    public string LastCalculationSummary { get; private set; }

    public void Initialize(Camera cam,Transform[] points){sceneCamera=cam;samplePoints=points;Recalculate();}

    // Full pipeline recomputation (unchanged algorithm): view matrix, projection matrix, then for every sample point
    // world -> view -> clip -> NDC (perspective division) -> viewport/screen coordinates + frustum inclusion.
    public void Recalculate()
    {
        if(sceneCamera==null)return; nearPlane=Mathf.Max(.001f,nearPlane); farPlane=Mathf.Max(nearPlane+.01f,farPlane); viewportWidth=Mathf.Max(1,viewportWidth); viewportHeight=Mathf.Max(1,viewportHeight);
        Vector3 target=new Vector3(0,1,0); ViewMatrix=ViewingPipelineMath.LookAt(sceneCamera.transform.position,target,Vector3.up);
        float aspect=(float)viewportWidth/viewportHeight; ProjectionMatrix=mode==ProjectionMode.Perspective?ViewingPipelineMath.Perspective(fieldOfView,aspect,nearPlane,farPlane):ViewingPipelineMath.Orthographic(orthographicWidth,orthographicWidth/aspect,nearPlane,farPlane);
        ModelMatrix=Matrix4x4.identity; Rect vp=new Rect(viewportOrigin.x,viewportOrigin.y,viewportWidth,viewportHeight); Results=new ViewingPipelineMath.PipelineResult[samplePoints.Length];
        StringBuilder b=new StringBuilder(); b.AppendLine("EXPLICIT VIEWING PIPELINE"); b.AppendLine("Convention: column vectors, p' = M * p"); b.AppendLine("Mode: "+mode); b.AppendLine(ViewingPipelineMath.MatrixText("View matrix",ViewMatrix)); b.AppendLine(ViewingPipelineMath.MatrixText("Projection matrix",ProjectionMatrix));
        for(int i=0;i<samplePoints.Length;i++){Vector3 local=samplePoints[i].position; Results[i]=ViewingPipelineMath.ProjectPoint(local,ModelMatrix,ViewMatrix,ProjectionMatrix,vp); b.AppendLine($"\nSample {i}: "+Results[i].Summary());} Report=b.ToString();
        sceneCamera.fieldOfView=fieldOfView; sceneCamera.nearClipPlane=nearPlane; sceneCamera.farClipPlane=farPlane; sceneCamera.orthographic=mode==ProjectionMode.Orthographic; if(sceneCamera.orthographic)sceneCamera.orthographicSize=orthographicWidth/aspect;
    }

    static bool NotFinite(float v){return float.IsNaN(v)||float.IsInfinity(v);}

    // Called by the CALCULATE button. Validates the current parameters (reporting every problem instead of silently
    // clamping), runs Recalculate(), logs the outcome, and returns the problems found ("" when everything was valid).
    public string Calculate()
    {
        const string tag="[ViewingPipelineController] ";
        if(sceneCamera==null){LastCalculationSummary="ERROR: scene camera is missing.";Debug.LogError(tag+"CALCULATE failed: sceneCamera is null.");return LastCalculationSummary;}
        if(samplePoints==null||samplePoints.Length==0){LastCalculationSummary="ERROR: there are no sample points.";Debug.LogError(tag+"CALCULATE failed: samplePoints is null or empty.");return LastCalculationSummary;}

        StringBuilder issues=new StringBuilder();
        if(NotFinite(fieldOfView)){issues.AppendLine(" - Field of View is not a finite number; reset to 60.");fieldOfView=60f;}
        else if(fieldOfView<1f||fieldOfView>179f){float f=Mathf.Clamp(fieldOfView,1f,179f);issues.AppendLine(" - Field of View "+fieldOfView+" is outside 1..179 degrees; using "+f+".");fieldOfView=f;}
        if(NotFinite(orthographicWidth)){issues.AppendLine(" - Orthographic Width is not a finite number; reset to 12.");orthographicWidth=12f;}
        else if(orthographicWidth<.01f){issues.AppendLine(" - Orthographic Width "+orthographicWidth+" must be positive; using 0.01.");orthographicWidth=.01f;}
        if(NotFinite(nearPlane)){issues.AppendLine(" - Near Plane is not a finite number; reset to 0.1.");nearPlane=.1f;}
        else if(nearPlane<=0f){issues.AppendLine(" - Near Plane "+nearPlane+" must be greater than 0; using 0.001.");nearPlane=.001f;}
        if(NotFinite(farPlane)){issues.AppendLine(" - Far Plane is not a finite number; reset to 100.");farPlane=100f;}
        else if(farPlane<=nearPlane){issues.AppendLine(" - Far Plane "+farPlane+" must be greater than Near Plane "+nearPlane+"; using "+(nearPlane+.01f)+".");farPlane=nearPlane+.01f;}
        if(viewportWidth<1){issues.AppendLine(" - Viewport Width "+viewportWidth+" must be at least 1 pixel; using 1.");viewportWidth=1;}
        if(viewportHeight<1){issues.AppendLine(" - Viewport Height "+viewportHeight+" must be at least 1 pixel; using 1.");viewportHeight=1;}

        Recalculate();
        CalculationCount++;

        int inside=0,invalidW=0;
        for(int i=0;i<Results.Length;i++){if(Results[i].insideFrustum)inside++;if(!Results[i].validW)invalidW++;}
        LastCalculationSummary=$"Mode {mode} | FOV {fieldOfView:0.##} | Ortho W {orthographicWidth:0.##} | Near {nearPlane:0.###} | Far {farPlane:0.##} | Viewport {viewportWidth}x{viewportHeight}\n"
            +$"{Results.Length} sample points recalculated: {inside} inside frustum, {Results.Length-inside} outside"+(invalidW>0?$", {invalidW} with clip w = 0":"")+".";

        Debug.Log(tag+"CALCULATE #"+CalculationCount+": "+LastCalculationSummary.Replace("\n"," -> "));
        string problems=issues.ToString().TrimEnd();
        if(problems.Length>0)Debug.LogWarning(tag+"Invalid parameter values were corrected:\n"+problems);
        return problems;
    }

    public void ResetValues(){mode=ProjectionMode.Perspective;fieldOfView=60;orthographicWidth=12;nearPlane=.1f;farPlane=100;viewportWidth=800;viewportHeight=450;Recalculate();}
    public void SetPerspective(){mode=ProjectionMode.Perspective;Recalculate();} public void SetOrthographic(){mode=ProjectionMode.Orthographic;Recalculate();}
    void OnGUI(){if(sceneCamera==null)return; GUI.color=Color.white; GUI.Label(new Rect(15,15,430,28),"3D VIEWING PIPELINE  |  "+mode.ToString().ToUpper(),new GUIStyle(GUI.skin.label){fontSize=18,fontStyle=FontStyle.Bold});}
}
