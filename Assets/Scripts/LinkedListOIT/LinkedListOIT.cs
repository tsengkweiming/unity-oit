using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

public struct FragmentAndLinkBuffer
{
    public uint uuid;
    public float depth;
    public uint next;
    public uint color;
};
public class LinkedListOIT : MonoBehaviour
{
    private const int FragmentNodeStride = 16; // uuid(4) + depth(4) + next(4) + color(4)
    private const uint InvalidNodeIndex = 0xFFFFFFFF;

    [SerializeField] private ComopsiteType _compositeType;
    [SerializeField] private ComputeShader _computeShader;
    [SerializeField] private Shader _instanceShader;
    [SerializeField] private Shader _compositeShader;
    [SerializeField] private Instance _instance;
    [SerializeField] private bool _enable;
    [SerializeField] [Range(1, 4)] private int _resolutionScale = 1;
    [SerializeField] private int _maxNodesPerPixel = 4;

    const int THREADS_X = 512;
    const int MAX_GROUPS = 65535;
    private CommandBuffer _commandBuffer;
    private Material _compositeMaterial;
    private RenderTexture _depthTexture;
    private RenderTexture _dummyColorTarget;
    private GraphicsBuffer _headBuffer;
    private GraphicsBuffer _nodeBuffer;
    private GraphicsBuffer _perPixelSlotBuffer;
    private uint[] _headClearData;
    private int _bufferWidth;
    private int _bufferHeight;
    private int _pixelCount;

    private void Start()
    {
        _compositeMaterial = new Material(_compositeShader);
    }

    private void EnsureBuffers(int width, int height)
    {
        width = Mathf.Max(1, width / _resolutionScale);
        height = Mathf.Max(1, height / _resolutionScale);
        int pixelCount = width * height;

        if (_headBuffer == null || _bufferWidth != width || _bufferHeight != height)
        {
            ReleaseBuffers();

            _bufferWidth = width;
            _bufferHeight = height;
            _pixelCount = pixelCount;

            _nodeBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.Counter, pixelCount * _maxNodesPerPixel, Marshal.SizeOf(typeof(FragmentAndLinkBuffer)));
            _nodeBuffer.name = "OIT_NodeBuffer";
            _headBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, pixelCount, sizeof(uint));
            _headBuffer.name = "OIT_RWByteAddressBuffer";
            _perPixelSlotBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, pixelCount * _maxNodesPerPixel, sizeof(uint)*3); // x: uuid, y: depth, z: nodeIdx
            _perPixelSlotBuffer.name = "OIT_RWPerPixelSlotBuffer";

            _headClearData = new uint[pixelCount];
            for (int i = 0; i < pixelCount; i++)
                _headClearData[i] = InvalidNodeIndex;

            _depthTexture = new RenderTexture(width, height, 24, RenderTextureFormat.Depth);
            _depthTexture.Create();

            _dummyColorTarget = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            _dummyColorTarget.Create();
        }
    }

    private void ReleaseBuffers()
    {
        _headBuffer?.Release();
        _headBuffer = null;
        _nodeBuffer?.Release();
        _nodeBuffer = null;
        _perPixelSlotBuffer?.Release();
        _perPixelSlotBuffer = null;
        _depthTexture?.Release();
        _depthTexture = null;
        _dummyColorTarget?.Release();
        _dummyColorTarget = null;
    }

    private void ResetBuffer()
    {
        ResetCounter();
        ResetAllBuffer();
    }
    
    void ResetCounter()
    {
        _nodeBuffer.SetCounterValue(0);
    }
        
    void ResetAllBuffer()
    {
        var kernelId = _computeShader.FindKernel("Reset");
        int total = _pixelCount * _maxNodesPerPixel;
        int totalGroups = (total + THREADS_X - 1) / THREADS_X;
        int groupsX = Mathf.Min(totalGroups, MAX_GROUPS);
        int groupsY = (totalGroups + MAX_GROUPS - 1) / MAX_GROUPS;  // ceil(totalGroups / 65535)
        int dispatchedX = groupsX * THREADS_X;   
        _computeShader.SetInt("_SlotCount", _maxNodesPerPixel);
        _computeShader.SetInt("_DispatchedX", dispatchedX);
        _computeShader.SetInt("_DispatchedY", groupsY);
        _computeShader.SetInt("_DispatchedZ", 1);
        _computeShader.SetBuffer(kernelId, "_FLBuffer", _nodeBuffer);
        _computeShader.SetBuffer(kernelId, "_StartOffsetBuffer", _headBuffer);
        _computeShader.SetBuffer(kernelId, "_PerPixelSlots", _perPixelSlotBuffer);
        _computeShader.Dispatch(kernelId, groupsX, groupsY, 1);
    }
    
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        EnsureBuffers(Screen.width, Screen.height);

        if (!_enable)
        {
            Graphics.Blit(source, destination);
            _instance.UpdateCommandBuffer(
                new[] { (RenderTargetIdentifier)destination },
                (RenderTargetIdentifier)destination,
                clearFlags: RTClearFlags.None  // don't wipe what we just blitted
            );
            _instance.ExecuteCommandBuffer();
            return;
        }

        _commandBuffer ??= new CommandBuffer { name = "LinkedListOIT" };
        _commandBuffer.Clear();

        _headBuffer.SetData(_headClearData);
        // _perPixelSlotBuffer.SetData(new[] { 0u });
        ResetBuffer();
        
        switch (_compositeType)
        {
            case ComopsiteType.AlphaBlend:
                _compositeMaterial.DisableKeyword("ADDITIVE");
                _compositeMaterial.EnableKeyword("ALPHA_BLEND");
                break;
            case ComopsiteType.Additive:
                _compositeMaterial.DisableKeyword("ALPHA_BLEND");
                _compositeMaterial.EnableKeyword("ADDITIVE");
                break;
        }

        _compositeMaterial.SetBuffer("_HeadBuffer", _headBuffer);
        _compositeMaterial.SetBuffer("_NodeBuffer", _nodeBuffer);
        _compositeMaterial.SetVector("_OIT_Size", new Vector4(_bufferWidth, _bufferHeight, 0, 0));
        _compositeMaterial.SetTexture("_BackgroundTex", source);

        Graphics.SetRandomWriteTarget(2, _headBuffer);
        Graphics.SetRandomWriteTarget(3, _nodeBuffer);
        Graphics.SetRandomWriteTarget(4, _perPixelSlotBuffer);
        
        // Instance draw: render to dummy target (not source) so background stays clean
        _commandBuffer.SetRenderTarget(_dummyColorTarget.colorBuffer, _depthTexture.depthBuffer);
        _commandBuffer.ClearRenderTarget(true, true, Color.clear, 1f);
        _commandBuffer.SetRandomWriteTarget(2, _headBuffer, true);
        _commandBuffer.SetRandomWriteTarget(3, _nodeBuffer, true);
        _commandBuffer.SetRandomWriteTarget(4, _perPixelSlotBuffer, true);

        _instance.AddLinkedListDrawCalls(_commandBuffer, _instanceShader, _bufferWidth, _bufferHeight, _pixelCount * _maxNodesPerPixel, source);

        // Composite in the same CommandBuffer — Unity inserts the UAV barrier for us
        _commandBuffer.Blit(source, destination, _compositeMaterial);

        Graphics.ExecuteCommandBuffer(_commandBuffer);
    }

    private void RemoveCommandBuffer()
    {
        _commandBuffer?.Release();
        _commandBuffer = null;
    }
    private void OnDestroy()
    {
        if (_compositeMaterial != null)
        {
            if (Application.isEditor)
                DestroyImmediate(_compositeMaterial);
            else
                Destroy(_compositeMaterial);
            _compositeMaterial = null;
        }
        ReleaseBuffers();
        RemoveCommandBuffer();
    }
}
