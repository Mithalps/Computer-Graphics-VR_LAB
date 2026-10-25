using System;
using System.Text;
using UnityEngine;

public static class ViewingPipelineMath
{
    public struct PipelineResult
    {
        public Vector3 world, view, ndc, screen;
        public Vector4 clip;
        public bool validW, insideFrustum;
        public string Summary()
        {
            return $"World {world}\nView {view}\nClip {clip}\nNDC {ndc}\nScreen BL {screen}\nInside frustum: {insideFrustum}";
        }
    }

    // Column-vector convention: p' = M * p. Unity Matrix4x4 uses m[row,column].
    public static Matrix4x4 Translation(Vector3 t)
    {
        Matrix4x4 m = Matrix4x4.identity; m.m03=t.x; m.m13=t.y; m.m23=t.z; return m;
    }
    public static Matrix4x4 Scale(Vector3 s)
    {
        Matrix4x4 m = Matrix4x4.identity; m.m00=s.x; m.m11=s.y; m.m22=s.z; return m;
    }
    public static Matrix4x4 Model(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        return Translation(position) * Matrix4x4.Rotate(rotation) * Scale(scale);
    }

    public static Matrix4x4 LookAt(Vector3 eye, Vector3 target, Vector3 up)
    {
        Vector3 forward = (target-eye).normalized;
        if (forward.sqrMagnitude < 0.000001f) forward = Vector3.forward;
        Vector3 right = Vector3.Cross(up.normalized, forward).normalized;
        if (right.sqrMagnitude < 0.000001f) right = Vector3.right;
        Vector3 trueUp = Vector3.Cross(forward, right);
        Matrix4x4 m = Matrix4x4.identity;
        m.m00=right.x; m.m01=right.y; m.m02=right.z; m.m03=-Vector3.Dot(right,eye);
        m.m10=trueUp.x; m.m11=trueUp.y; m.m12=trueUp.z; m.m13=-Vector3.Dot(trueUp,eye);
        m.m20=-forward.x; m.m21=-forward.y; m.m22=-forward.z; m.m23=Vector3.Dot(forward,eye);
        return m;
    }

    public static Matrix4x4 Perspective(float fovDegrees, float aspect, float near, float far)
    {
        float n=Mathf.Max(0.001f,near), f=Mathf.Max(n+0.001f,far);
        float a=Mathf.Max(0.001f,aspect), q=1f/Mathf.Tan(fovDegrees*Mathf.Deg2Rad*0.5f);
        Matrix4x4 m=new Matrix4x4();
        m.m00=q/a; m.m11=q; m.m22=(f+n)/(n-f); m.m23=(2f*f*n)/(n-f); m.m32=-1f; m.m33=0f;
        return m;
    }
    public static Matrix4x4 Orthographic(float width, float height, float near, float far)
    {
        float w=Mathf.Max(0.001f,width), h=Mathf.Max(0.001f,height), n=Mathf.Max(0.001f,near), f=Mathf.Max(n+0.001f,far);
        Matrix4x4 m=Matrix4x4.identity;
        m.m00=2f/w; m.m11=2f/h; m.m22=-2f/(f-n); m.m23=-(f+n)/(f-n); return m;
    }
    public static Vector4 Multiply(Matrix4x4 m, Vector4 v)
    {
        return new Vector4(m.m00*v.x+m.m01*v.y+m.m02*v.z+m.m03*v.w,
            m.m10*v.x+m.m11*v.y+m.m12*v.z+m.m13*v.w,
            m.m20*v.x+m.m21*v.y+m.m22*v.z+m.m23*v.w,
            m.m30*v.x+m.m31*v.y+m.m32*v.z+m.m33*v.w);
    }
    public static PipelineResult ProjectPoint(Vector3 local, Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection,
        Rect viewport)
    {
        PipelineResult r=new PipelineResult();
        Vector4 world4=Multiply(model,new Vector4(local.x,local.y,local.z,1));
        Vector4 view4=Multiply(view,world4); r.world=new Vector3(world4.x,world4.y,world4.z); r.view=new Vector3(view4.x,view4.y,view4.z);
        r.clip=Multiply(projection,view4); r.validW=Mathf.Abs(r.clip.w)>0.000001f;
        if (!r.validW) { r.ndc=Vector3.zero; r.screen=Vector3.zero; r.insideFrustum=false; return r; }
        r.ndc=new Vector3(r.clip.x/r.clip.w,r.clip.y/r.clip.w,r.clip.z/r.clip.w);
        r.screen=new Vector3(viewport.x+((r.ndc.x+1f)*0.5f)*viewport.width,
            viewport.y+((r.ndc.y+1f)*0.5f)*viewport.height,r.ndc.z);
        r.insideFrustum=r.ndc.x>=-1f&&r.ndc.x<=1f&&r.ndc.y>=-1f&&r.ndc.y<=1f&&r.ndc.z>=-1f&&r.ndc.z<=1f;
        return r;
    }
    public static string MatrixText(string title, Matrix4x4 m)
    {
        return $"{title}\n[{m.m00,8:0.000} {m.m01,8:0.000} {m.m02,8:0.000} {m.m03,8:0.000}]\n[{m.m10,8:0.000} {m.m11,8:0.000} {m.m12,8:0.000} {m.m13,8:0.000}]\n[{m.m20,8:0.000} {m.m21,8:0.000} {m.m22,8:0.000} {m.m23,8:0.000}]\n[{m.m30,8:0.000} {m.m31,8:0.000} {m.m32,8:0.000} {m.m33,8:0.000}]";
    }
}
