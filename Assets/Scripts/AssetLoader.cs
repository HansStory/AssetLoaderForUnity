using UnityEditor;
using UnityEngine;

public class AssetLoader : MonoBehaviour
{
    [SerializeField] private bool _showLoadTime = true;
    private float _loadStartTime;

    [field: SerializeField]
    public LoaderModule LoaderModule { get; set; }

    private void Start()
    {
        string selectedAssetName = EditorUtility.OpenFilePanel("Select obj model", "", "obj");
        Load(selectedAssetName);
    }

    public void Load(string assetName)
    {
        if(_showLoadTime)
        {
            Debug.Log($"OBJ 파일 로딩 시작: {assetName}");
            _loadStartTime = Time.realtimeSinceStartup;
        }

        LoaderModule.OnLoadCompleted += OnLoadCompleted;
        LoaderModule.LoadAsset(assetName);
    }

    private void OnLoadCompleted(GameObject loadedAsset)
    {
        if (_showLoadTime)
        {
            float loadTime = Time.realtimeSinceStartup - _loadStartTime;
            Debug.Log($"OBJ 로딩 완료! 소요 시간: {loadTime:F3}초");
        }

        loadedAsset.transform.SetParent(transform);
    }
}
