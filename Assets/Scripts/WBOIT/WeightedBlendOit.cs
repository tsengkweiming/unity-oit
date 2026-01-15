using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class WeightedBlendOit : MonoBehaviour
{
    public enum WeightFunction { Weight0,  Weight1, Weight2, Weight3 }
    
    [SerializeField] private ComopsiteType _compositeType;
    [SerializeField] private WeightFunction _weightFunction;
    [SerializeField] private Shader _compositeShader;
    [SerializeField] private RenderTexture _colorTexture;
    [SerializeField] private RenderTexture _accumulationTexture;
    [SerializeField] private RenderTexture _revealageTexture;
    [SerializeField] private bool _enable;
    [SerializeField] private Instance _instance;
    private Material _compositeMaterial;
    private Camera _camera;
    private CommandBuffer _commandBuffer;
    private (RenderTargetIdentifier[] color, RenderTargetIdentifier depth) _renderIds;
    
    // Start is called before the first frame update
    void Start()
    {
        _compositeMaterial = new Material(_compositeShader);
        _commandBuffer = new CommandBuffer() { name = "Clear" };
        CreateTexture();
    }

    void CreateTexture()
    {
        _colorTexture = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32) { name = "Color" };
        _accumulationTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf) { name = "Accumulation" };
        _revealageTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.RHalf) { name = "Revealage" };
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (_enable)
        {
            RenderTargetIdentifier[] colorIds = { new (_revealageTexture.colorBuffer), new (_accumulationTexture.colorBuffer), new (_colorTexture.colorBuffer)};
            RenderTargetIdentifier depthId = new RenderTargetIdentifier(_colorTexture.depthBuffer);
            
            _instance.UpdateCommandBuffer(colorIds, depthId, new Color(1f,1f,1f, 0f), Color.clear, RTClearFlags.ColorDepth);
            _instance.ExecuteCommandBuffer();
            
            switch (_compositeType)
            {
                case ComopsiteType.AlphaBlend:
                default:
                    _compositeMaterial.DisableKeyword("ADDITIVE");
                    _compositeMaterial.EnableKeyword("ALPHA_BLEND");
                    break;
                case ComopsiteType.Additive:
                    _compositeMaterial.DisableKeyword("ALPHA_BLEND");
                    _compositeMaterial.EnableKeyword("ADDITIVE");
                    break;
            }
            Shader.DisableKeyword("WEIGHTED0");
            Shader.DisableKeyword("WEIGHTED1");
            Shader.DisableKeyword("WEIGHTED2");
            Shader.DisableKeyword("WEIGHTED3");
            switch (_weightFunction) {
                case WeightFunction.Weight0:
                    Shader.EnableKeyword("WEIGHTED0");
                    break;
                case WeightFunction.Weight1:
                    Shader.EnableKeyword("WEIGHTED1");
                    break;
                case WeightFunction.Weight2:
                    Shader.EnableKeyword("WEIGHTED2");
                    break;
                case WeightFunction.Weight3:
                    Shader.EnableKeyword("WEIGHTED3");
                    break;
            }
            _compositeMaterial.SetTexture("_AccumulationTex", _accumulationTexture);
            _compositeMaterial.SetTexture("_RevealageTex", _revealageTexture);
            Graphics.Blit(null, destination, _compositeMaterial);
        }
        else
        {
            Graphics.Blit(_colorTexture, destination);
        }
    }

    void Release()
    {
        _colorTexture?.Release();
        _colorTexture = null;
        
        _accumulationTexture?.Release();
        _accumulationTexture = null;
        
        _revealageTexture?.Release();
        _revealageTexture = null;
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
        
        _commandBuffer?.Release();
        _commandBuffer = null;
        Release();
    }
}
