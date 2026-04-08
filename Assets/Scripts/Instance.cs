using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

public struct InstanceData
{
    public Vector3 Position;
    public Vector3 Rotation;
    public Vector3 Scale;
    public Color Color;
}

[System.Serializable]
public class InstanceProp
{
    public Mesh Mesh;
    public Texture2D Texture;
    [Range(0f, 1f)] public float Alpha;
    public float Scale;
    [Range(0f, 1f)] public float ColorRandomness;
}
public class Instance : MonoBehaviour
{
    [SerializeField] private InstanceProp[] _instanceProps;
    [SerializeField] [Range(0, 20)] private float _size;
    [SerializeField] private Vector3 _scale;
    [SerializeField] private int _count;
    [SerializeField] private Shader _shader;
    [SerializeField] private bool _zwrite;
    [SerializeField] private CompareFunction _compareFunction;
    [SerializeField] private BlendMode _srcFactor0;
    [SerializeField] private BlendMode _dstFactor0;
    [SerializeField] private BlendMode _srcFactor1;
    [SerializeField] private BlendMode _dstFactor1;
    [SerializeField] private bool _ascendingDrawOrder;
    private GraphicsBuffer[] _dataBuffers;
    private GraphicsBuffer _argsBuffer;
    private GraphicsBuffer[][] _argsBuffers;
    private CommandBuffer _commandBuffer;
    private Material[] _materials;
    private Material[] _linkedListMaterials;
    private Shader _cachedLinkedListShader;
    private DepthPeelingType _depthPeelingType;
    private ComopsiteType _compositeType;
    private readonly uint[] _args = { 0, 0, 0, 0, 0 };
    public DepthPeelingType DepthPeelingType { get => _depthPeelingType; set => _depthPeelingType = value; }
    public ComopsiteType ComopsiteType { get => _compositeType; set => _compositeType = value; }
    public bool ZWrite { get => _zwrite; set => _zwrite = value; }
    public CompareFunction CompareFunction { get => _compareFunction; set => _compareFunction = value; }

    void Start()
    {
        _materials  = new Material[_instanceProps.Length];
        for (var i = 0; i < _instanceProps.Length; i++)
        {
            _materials[i] = new Material(_shader);
        }
        InitBuffer();
    }

    void InitBuffer()
    {
        ReleaseBuffer();
        _dataBuffers  = new GraphicsBuffer[_instanceProps.Length];
        for (var i = 0; i < _instanceProps.Length; i++)
        {
            _dataBuffers[i] = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _count, Marshal.SizeOf<InstanceData>());
            var instanceDatas = new InstanceData[_count];
            var colorRandomness = _instanceProps[i].ColorRandomness;
            for (var j = 0; j < _count; j++)
            {
                float r = colorRandomness;
                float hue = Random.value;
                Color randomColor = Color.HSVToRGB(hue, 1f, 1f);
                instanceDatas[j].Color = Color.Lerp(Color.white, randomColor, r);
                instanceDatas[j].Position = Random.insideUnitSphere * _size + transform.position;
                instanceDatas[j].Rotation = Random.insideUnitSphere;
                instanceDatas[j].Scale = _scale;
            }
            _dataBuffers[i].SetData(instanceDatas);
        }

        InitArgsBuffers();
    }
    
    void InitArgsBuffers()
    {
        _argsBuffers = new GraphicsBuffer[_instanceProps.Length][];
        for (int i = 0; i < _instanceProps.Length; i++)
        {
            var mesh = _instanceProps[i].Mesh;
            _argsBuffers[i] = new GraphicsBuffer[mesh.subMeshCount];

            for (int sm = 0; sm < mesh.subMeshCount; sm++)
            {
                _argsBuffers[i][sm] =
                    new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, _args.Length, sizeof(uint));
            }
        }
    }

    public void UpdateCommandBuffer(RenderTargetIdentifier[] colorIds, RenderTargetIdentifier depthId,
        Color? depthClearColor = null, Color? backgroundColor = null, RTClearFlags clearFlags = RTClearFlags.ColorDepth, int pass = 0)
    {
        _commandBuffer ??= new CommandBuffer { name = "Renderer" };
        _commandBuffer.Clear();
        var clearColor = depthClearColor ?? Color.clear;
        var clearFlagsInner = depthClearColor.HasValue ? clearFlags :  RTClearFlags.Depth;
        _commandBuffer.SetRenderTarget(colorIds[0]);
        _commandBuffer.ClearRenderTarget(clearFlagsInner, clearColor, 1, 0);
        for (var i = 1; i < colorIds.Length; i++)
        {
            _commandBuffer.SetRenderTarget(colorIds[i], depthId);
            clearColor = backgroundColor ?? Color.clear;
            clearFlagsInner = backgroundColor.HasValue ? clearFlags :  RTClearFlags.Depth;
            _commandBuffer.ClearRenderTarget(clearFlagsInner, clearColor, 1, 0);
        }
        
        _commandBuffer.SetRenderTarget(colorIds, depthId);
        
        for (var j = 0; j < _instanceProps.Length; j++)
        {
            int i = _ascendingDrawOrder ? j : _instanceProps.Length - j - 1;
            _materials[i].SetFloat("_ZWrite", _zwrite ? 1 : 0);
            _materials[i].SetFloat("_ZTest", (int)_compareFunction);
            _materials[i].SetFloat("_Scale", _instanceProps[i].Scale);
            _materials[i].SetFloat("_Alpha", _instanceProps[i].Alpha);
            _materials[i].SetTexture("_MainTex", _instanceProps[i].Texture);
            _materials[i].SetBuffer("_InstanceBuffer", _dataBuffers[i]);
            switch (_depthPeelingType)
            {
                case DepthPeelingType.Front2Back:
                default:
                    _materials[i].SetFloat("_BlendOp0", (int)BlendOp.Add);
                    _materials[i].SetFloat("_SrcFactor0", (int)BlendMode.One);
                    _materials[i].SetFloat("_DstFactor0", (int)BlendMode.Zero);
                    _materials[i].SetFloat("_SrcFactor1", (int)BlendMode.One);
                    _materials[i].SetFloat("_DstFactor1", (int)BlendMode.Zero);
                    break;
                case DepthPeelingType.DualPeeling:
                    _materials[i].SetFloat("_ZWrite", 0);
                    _materials[i].SetFloat("_BlendOp0", (int)BlendOp.Max);
                    _materials[i].SetFloat("_SrcFactor0", (int)BlendMode.One);
                    _materials[i].SetFloat("_DstFactor0", (int)BlendMode.One);
                    switch (_compositeType)
                    {
                        case ComopsiteType.AlphaBlend:
                        default:
                            _materials[i].SetFloat("_SrcFactor1", (int)BlendMode.OneMinusDstAlpha);
                            _materials[i].SetFloat("_DstFactor1", (int)BlendMode.One);
                            _materials[i].SetFloat("_SrcFactor2", (int)BlendMode.One);
                            _materials[i].SetFloat("_DstFactor2", (int)BlendMode.OneMinusSrcAlpha);
                            break;
                        case ComopsiteType.Additive:
                            _materials[i].SetFloat("_SrcFactor1", (int)BlendMode.One);
                            _materials[i].SetFloat("_DstFactor1", (int)BlendMode.One);
                            _materials[i].SetFloat("_SrcFactor2", (int)BlendMode.One);
                            _materials[i].SetFloat("_DstFactor2", (int)BlendMode.One);
                            break;
                    }
                    break;
            }

            var mesh = _instanceProps[i].Mesh;
            for (int sm = 0; sm < mesh.subMeshCount; sm++)
            {
                var smInfo = mesh.GetSubMesh(sm);
                // 0 == number of triangle indices, 1 == population, others are only relevant if drawing submeshes.
                _args[0] = (uint)smInfo.indexCount;
                _args[1] = (uint)_count;
                _args[2] = (uint)smInfo.indexStart;
                _args[3] = (uint)smInfo.baseVertex;
                _argsBuffers[i][sm].SetData(_args);
                _commandBuffer.DrawMeshInstancedIndirect(mesh, sm, _materials[i], pass, _argsBuffers[i][sm]);
            }
        }
    }

    public void ExecuteCommandBuffer()
    {
        Graphics.ExecuteCommandBuffer(_commandBuffer);
    }

    public void AddLinkedListDrawCalls(CommandBuffer cmd, Shader linkedListShader, int width, int height, int maxNodes)
    {
        if (linkedListShader == null || _instanceProps == null) return;

        if (_linkedListMaterials == null || _cachedLinkedListShader != linkedListShader)
        {
            if (_linkedListMaterials != null)
            {
                foreach (var m in _linkedListMaterials)
                {
                    if (m != null)
                    {
                        if (Application.isEditor) DestroyImmediate(m);
                        else Destroy(m);
                    }
                }
            }
            _cachedLinkedListShader = linkedListShader;
            _linkedListMaterials = new Material[_instanceProps.Length];
            for (int i = 0; i < _instanceProps.Length; i++)
                _linkedListMaterials[i] = new Material(linkedListShader);
        }

        for (int j = 0; j < _instanceProps.Length; j++)
        {
            int i = _ascendingDrawOrder ? j : _instanceProps.Length - j - 1;
            var material = _linkedListMaterials[i];
            material.SetFloat("_ZWrite", 0);
            material.SetFloat("_ZTest", (int)_compareFunction);
            material.SetFloat("_Scale", _instanceProps[i].Scale);
            material.SetFloat("_Alpha", _instanceProps[i].Alpha);
            material.SetTexture("_MainTex", _instanceProps[i].Texture);
            material.SetBuffer("_InstanceBuffer", _dataBuffers[i]);
            material.SetVector("_OIT_Size", new Vector4(width, height, 0, 0));
            material.SetInt("_MaxNodes", maxNodes);

            var mesh = _instanceProps[i].Mesh;
            for (int sm = 0; sm < mesh.subMeshCount; sm++)
            {
                var smInfo = mesh.GetSubMesh(sm);
                _args[0] = (uint)smInfo.indexCount;
                _args[1] = (uint)_count;
                _args[2] = (uint)smInfo.indexStart;
                _args[3] = (uint)smInfo.baseVertex;
                _argsBuffers[i][sm].SetData(_args);
                cmd.DrawMeshInstancedIndirect(mesh, sm, material, 0, _argsBuffers[i][sm]);
            }
        }
    }
    private void RemoveCommandBuffer()
    {
        _commandBuffer?.Release();
        _commandBuffer = null;
    }
    
    void ReleaseBuffer()
    {
        if (_dataBuffers != null)
        {
            for (int i = 0; i < _dataBuffers.Length; i++)
            {
                _dataBuffers[i]?.Release();
                _dataBuffers[i] = null;
            }
        }
        if (_argsBuffers != null)
        {
            for (int i = 0; i < _argsBuffers.Length; i++)
            {
                for (int j = 0; j < _argsBuffers[i].Length; j++)
                {
                    _argsBuffers[i][j]?.Release();
                    _argsBuffers[i][j] = null;
                }
            }
        }
        _argsBuffer?.Release();
        _argsBuffer = null;
    }
    
    void DeleteMaterial(Material material)
    {
        if (material != null)
        {
            if (Application.isEditor)
                DestroyImmediate(material);
            else
                Destroy(material);
        }
    }
    
    private void OnDestroy()
    {
        RemoveCommandBuffer();
        
        ReleaseBuffer();
        if (_materials != null)
        {
            for (int i = 0; i < _materials.Length; i++)
            {
                DeleteMaterial(_materials[i]);
            }
        }
        if (_linkedListMaterials != null)
        {
            foreach (var m in _linkedListMaterials)
            {
                if (m != null)
                {
                    if (Application.isEditor) DestroyImmediate(m);
                    else Destroy(m);
                }
            }
            _linkedListMaterials = null;
        }
    }
}
