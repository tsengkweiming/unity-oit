using UnityEngine;
using UnityEngine.Rendering;

public class LinkedListOIT : MonoBehaviour
{
    private const int FragmentNodeStride = 16; // uuid(4) + depth(4) + next(4) + color(4)
    private const uint InvalidNodeIndex = 0xFFFFFFFF;

    [SerializeField] private ComopsiteType _compositeType;
    [SerializeField] private Shader _instanceShader;
    [SerializeField] private Shader _compositeShader;
    [SerializeField] private Instance _instance;
    [SerializeField] private bool _enable;
    [SerializeField] [Range(1, 4)] private int _resolutionScale = 1;
    [SerializeField] private int _maxNodesPerFrame = 1024 * 1024 * 4;

    private Material _compositeMaterial;
    private RenderTexture _depthTexture;
    private RenderTexture _dummyColorTarget;
    private GraphicsBuffer _headBuffer;
    private GraphicsBuffer _nodeBuffer;
    private GraphicsBuffer _counterBuffer;
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

            _headBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, pixelCount, 4);
            _nodeBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _maxNodesPerFrame, FragmentNodeStride);
            _counterBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, 1, 4);

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
        _counterBuffer?.Release();
        _counterBuffer = null;
        _depthTexture?.Release();
        _depthTexture = null;
        _dummyColorTarget?.Release();
        _dummyColorTarget = null;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        EnsureBuffers(Screen.width, Screen.height);

        if (!_enable || _instance == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        var cmd = new CommandBuffer { name = "LinkedListOIT" };

        _headBuffer.SetData(_headClearData);
        _counterBuffer.SetData(new[] { 0u });

        cmd.SetRenderTarget(_dummyColorTarget.colorBuffer, _depthTexture.depthBuffer);
        cmd.ClearRenderTarget(true, true, Color.clear, 1f);
        cmd.SetRandomWriteTarget(0, _headBuffer);
        cmd.SetRandomWriteTarget(1, _nodeBuffer);
        cmd.SetRandomWriteTarget(2, _counterBuffer);

        _instance.AddLinkedListDrawCalls(cmd, _instanceShader, _bufferWidth, _bufferHeight, _maxNodesPerFrame);
        Graphics.ExecuteCommandBuffer(cmd);
        cmd.ClearRandomWriteTargets();
        cmd.Release();

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
        _compositeMaterial.SetVector("_BufferSize", new Vector4(_bufferWidth, _bufferHeight, 0, 0));
        _compositeMaterial.SetTexture("_BackgroundTex", source);

        Graphics.Blit(source, destination, _compositeMaterial);
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
    }
}
