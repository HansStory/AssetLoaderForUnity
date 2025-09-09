//using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public class AssetLoader : MonoBehaviour
{
    [SerializeField] private bool _showLoadTime = true;
    private float _loadStartTime;

    [field: SerializeField]
    public LoaderModule LoaderModule { get; set; }

    private void Start()
    {
        //string selectedAssetName = EditorUtility.OpenFilePanel("Select obj model", "", "obj");
        //Load(selectedAssetName);
        //AsyncLoad(selectedAssetName);

        List<string> selectedAssetNames = GetObjFiles("/Resources/Models");
        Load(selectedAssetNames);
    }

    public void Load(string assetName)
    {
        if (_showLoadTime)
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

    public async void AsyncLoad(string assetName)
    {
        Debug.Log($"OBJ 파일 비동기 로딩 시작: {assetName}");
        _loadStartTime = Time.realtimeSinceStartup;

        GameObject loadedAsset = await LoaderModule.LoadAssetAsync(assetName);

        if (loadedAsset != null)
        {
            if (_showLoadTime)
            {
                float loadTime = Time.realtimeSinceStartup - _loadStartTime;
                Debug.Log($"OBJ 로딩 완료! 소요 시간: {loadTime:F3}초");
            }

            loadedAsset.transform.SetParent(transform);
        }
    }

    private List<string> GetObjFiles(string directory)
    {
        // Unity 프로젝트 Assets 폴더 기준으로 경로 설정
        string fullPath = Path.Combine(Application.dataPath, directory.TrimStart('/'));

        List<string> objFiles = new List<string>();

        if (Directory.Exists(fullPath))
        {
            string[] files = Directory.GetFiles(fullPath, "*.obj", SearchOption.AllDirectories);
            objFiles.AddRange(files);
        }
        else
        {
            Debug.LogWarning($"[GetObjFiles] 경로를 찾을 수 없음: {fullPath}");
        }

        return objFiles;
    }

    public async void Load(List<string> assetNames)
    {
        Debug.Log("폴더의 모든 OBJ 파일 로드 시작");
        float loadStartTime = Time.realtimeSinceStartup;

        List<Task<GameObject>> tasks = new();

        foreach (string assetName in assetNames)
        {
            tasks.Add(LoadAndPlace(assetName));
        }

        await Task.WhenAll(tasks);

        if (_showLoadTime)
        {
            float loadTime = Time.realtimeSinceStartup - loadStartTime;
            Debug.Log($"모든 OBJ 로딩 완료! 소요 시간: {loadTime:F3}초");
        }
    }

    private async Task<GameObject> LoadAndPlace(string assetName)
    {
        Debug.Log($"OBJ 파일 비동기 로딩 시작: {assetName}");
        float loadStartTime = Time.realtimeSinceStartup;

        GameObject loadedAsset = await LoaderModule.LoadAssetAsync(assetName);

        if (_showLoadTime)
        {
            float loadTime = Time.realtimeSinceStartup - loadStartTime;
            Debug.Log($"OBJ 로딩 완료! 소요 시간: {loadTime:F3}초");
        }

        loadedAsset.transform.SetParent(transform);
        return loadedAsset;
    }
}
