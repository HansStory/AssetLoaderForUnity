using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public enum RenderPipeline
{
    BUILTIN,
    URP,
    HDRP
}

[System.Serializable]
public struct MeshVertex
{
    public int VertexIndex;
    public int UvIndex;
    public int NormalIndex;

    public MeshVertex(int vIndex, int uvIdx, int nIndex)
    {
        VertexIndex = vIndex;
        UvIndex = uvIdx;
        NormalIndex = nIndex;
    }
}

[System.Serializable]
public struct ObjFace
{
    public MeshVertex[] Vertices;
}

[System.Serializable]
public class ObjObject
{
    public string Name;
    public List<ObjFace> Faces;
}

public struct OptimizedMeshData
{
    public List<Vector3> Vertices;
    public List<Vector3> Normals;
    public List<Vector2> Uvs;
    public List<int> Triangles;
}

public class LoaderModule : MonoBehaviour
{
    public Action<GameObject> OnLoadCompleted;

    [SerializeField] private RenderPipeline _renderPipeline = RenderPipeline.BUILTIN;
    [SerializeField] private Material _defaultMaterial;

    [SerializeField] private bool _preallocateCollections = true; // 컬렉션 미리 할당 여부
    [SerializeField] private int _expectationVertexCount = 10000; // 예상 버텍스 수
    [SerializeField] private float _vertexMergeThreshold = 0.0001f; // 임계값을 높여 해시 충돌 감소

    private void Awake()
    {
        InitDefaultMaterial();
    }

    private void InitDefaultMaterial()
    {
        if (_defaultMaterial == null)
        {
            switch (_renderPipeline)
            {
                case RenderPipeline.URP:
                    _defaultMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    break;
                case RenderPipeline.HDRP:
                    _defaultMaterial = new Material(Shader.Find("HDRP/Lit"));
                    break;
                case RenderPipeline.BUILTIN:
                default:
                    _defaultMaterial = new Material(Shader.Find("Standard"));
                    break;
            }
        }
    }

    public void LoadAsset(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            Debug.LogError("Asset path is null or empty!");
            return;
        }

        if (!File.Exists(assetPath))
        {
            Debug.LogError($"File not found: {assetPath}");
            return;
        }

        List<Vector3> vertices = _preallocateCollections ? new List<Vector3>(_expectationVertexCount) : new List<Vector3>();
        List<Vector3> normals = _preallocateCollections ? new List<Vector3>(_expectationVertexCount) : new List<Vector3>();
        List<Vector2> uvs = _preallocateCollections ? new List<Vector2>(_expectationVertexCount) : new List<Vector2>();

        string fileName = Path.GetFileNameWithoutExtension(assetPath);

        var objects = new List<ObjObject>();
        var currentObject = new ObjObject { Name = fileName, Faces = new List<ObjFace>() };

        try
        {
            StreamReader reader = new StreamReader(assetPath);
            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine().Trim();
                if (string.IsNullOrEmpty(line) || line[0] == '#') continue;

                ParseObjFile(line, vertices, normals, uvs, objects, ref currentObject);
            }

            // 마지막 오브젝트 추가
            if (currentObject.Faces.Count > 0)
            {
                objects.Add(currentObject);
            }

            if (objects.Count == 0)
            {
                objects.Add(currentObject);
            }

            GameObject objGameObject = CreateGameObjectsOptimized(vertices, normals, uvs, objects, fileName);
            OnLoadCompleted?.Invoke(objGameObject);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error reading OBJ file: {e.Message}");
            return;
        }

    }

    private void ParseObjFile(string line, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
                              List<ObjObject> objects, ref ObjObject currentObject)
    {
        switch (line[0])
        {
            case 'v':
                if (line.Length > 1)
                {
                    switch (line[1])
                    {
                        case ' ': // v
                            ParseVertex(line, vertices);
                            break;
                        case 'n': // vn
                            ParseNormal(line, normals);
                            break;
                        case 't': // vt
                            ParseUV(line, uvs);
                            break;
                    }
                }
                break;

            case 'f': // face
                if (line.Length > 1 && line[1] == ' ')
                {
                    ParseFace(line, currentObject.Faces);
                }
                break;

            case 'o': // object
            case 'g': // group
                if (line.Length > 2)
                {
                    AddNewObject(line, objects, ref currentObject);
                }
                break;

            // TO DO : MTL Library
            case 'm': // material library
                break;
            case 'u': // use material
                break;
        }
    }

    private void ParseVertex(string line, List<Vector3> vertices)
    {
        int index = 2; // "v " 건너뛰기

        float x = ParseFloat(line, ref index);
        float y = ParseFloat(line, ref index);
        float z = ParseFloat(line, ref index);

        vertices.Add(new Vector3(x, y, z)); // Unity 좌표계 변환
    }

    private void ParseNormal(string line, List<Vector3> normals)
    {
        int index = 3; // "vn " 건너뛰기

        float x = ParseFloat(line, ref index);
        float y = ParseFloat(line, ref index);
        float z = ParseFloat(line, ref index);

        normals.Add(new Vector3(x, y, z));
    }

    private void ParseUV(string line, List<Vector2> uvs)
    {
        int index = 3; // "vt " 건너뛰기

        float u = ParseFloat(line, ref index);
        float v = ParseFloat(line, ref index);

        uvs.Add(new Vector2(u, v));
    }

    private void ParseFace(string line, List<ObjFace> faces)
    {
        List<MeshVertex> faceVertices = new List<MeshVertex>(8);

        int index = 2;  // "f " 건너뛰기

        while (index < line.Length)
        {
            // 공백 건너뛰기
            while (index < line.Length && line[index] == ' ') index++;

            if (index >= line.Length) break;

            var vertex = new MeshVertex(-1, -1, -1);

            int num = ParseInt(line, ref index);
            vertex.VertexIndex = num > 0 ? num - 1 : -1;

            if (index < line.Length && line[index] == '/')
            {
                index++; // '/' 건너뛰기

                // 두 번째 숫자 (texture index)
                if (index < line.Length && line[index] != '/')
                {
                    num = ParseInt(line, ref index);
                    vertex.UvIndex = num > 0 ? num - 1 : -1;
                }

                if (index < line.Length && line[index] == '/')
                {
                    index++; // '/' 건너뛰기

                    // 세 번째 숫자 (normal index)
                    if (index < line.Length && char.IsDigit(line[index]))
                    {
                        num = ParseInt(line, ref index);
                        vertex.NormalIndex = num > 0 ? num - 1 : -1;
                    }
                }
            }

            faceVertices.Add(vertex);
        }

        // 삼각형으로 분할
        if (faceVertices.Count >= 3)
        {
            for (int i = 1; i < faceVertices.Count - 1; i++)
            {
                var face = new ObjFace();
                face.Vertices = new MeshVertex[3];
                face.Vertices[0] = faceVertices[0];
                face.Vertices[1] = faceVertices[i];
                face.Vertices[2] = faceVertices[i + 1];
                faces.Add(face);
            }
        }
    }

    private void AddNewObject(string line, List<ObjObject> objects, ref ObjObject currentObject)
    {
        if (currentObject.Faces.Count > 0)
        {
            objects.Add(currentObject);
        }

        currentObject = new ObjObject
        {
            Name = ExtractName(line, 2),
            Faces = new List<ObjFace>()
        };
    }

    private string ExtractName(string line, int startIndex)
    {
        int start = startIndex;
        int end = line.Length;

        while (start < end && line[start] == ' ') start++;
        while (end > start && line[end - 1] == ' ') end--;

        return line.Substring(start, end - start);
    }


    private float ParseFloat(string line, ref int index)
    {
        while (index < line.Length && line[index] == ' ') index++;

        bool negative = false;
        if (index < line.Length && line[index] == '-')
        {
            negative = true;
            index++;
        }

        float result = 0f;
        float decimalPart = 0f;
        float decimalMultiplier = 0.1f;

        // 정수 부분
        while (index < line.Length && char.IsDigit(line[index]))
        {
            result = result * 10 + (line[index] - '0');
            index++;
        }

        // 소수 부분
        if (index < line.Length && line[index] == '.')
        {
            index++;
            while (index < line.Length && char.IsDigit(line[index]))
            {
                decimalPart += (line[index] - '0') * decimalMultiplier;
                decimalMultiplier *= 0.1f;
                index++;
            }
        }

        result += decimalPart;
        return negative ? -result : result;
    }

    private int ParseInt(string line, ref int index)
    {
        while (index < line.Length && line[index] == ' ') index++;

        int result = 0;
        while (index < line.Length && char.IsDigit(line[index]))
        {
            result = result * 10 + (line[index] - '0');
            index++;
        }

        return result;
    }

    private GameObject CreateGameObjectsOptimized(List<Vector3> objVertices, List<Vector3> objNormals, List<Vector2> objUVs, List<ObjObject> objects, string fileName)
    {
        GameObject rootObject = new GameObject(fileName);

        if (objects.Count == 1)
        {
            var singleObject = objects[0];
            var meshData = CreateOptimizedMeshData(objVertices, objNormals, objUVs, singleObject.Faces);
            AttachMeshToGameObject(rootObject, meshData, singleObject.Name);
        }
        else
        {
            foreach (var objObject in objects)
            {
                if (objObject.Faces.Count > 0)
                {
                    GameObject childObject = new GameObject(objObject.Name);
                    childObject.transform.SetParent(rootObject.transform);

                    var meshData = CreateOptimizedMeshData(objVertices, objNormals, objUVs, objObject.Faces);
                    AttachMeshToGameObject(childObject, meshData, objObject.Name);
                }
            }
        }

        return rootObject;
    }

    private void AttachMeshToGameObject(GameObject createdObject, OptimizedMeshData meshData, string name)
    {
        Mesh mesh = new Mesh();
        mesh.name = name + "_Mesh";

        if (meshData.Vertices.Count > ushort.MaxValue)
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        // 배열 변환 최적화 - ToArray() 대신 직접 할당
        mesh.SetVertices(meshData.Vertices);
        mesh.SetTriangles(meshData.Triangles, 0);

        if (meshData.Normals.Count == meshData.Vertices.Count)
        {
            mesh.SetNormals(meshData.Normals);
        }
        else
        {
            mesh.RecalculateNormals();
        }

        if (meshData.Uvs.Count == meshData.Vertices.Count)
        {
            mesh.SetUVs(0, meshData.Uvs);
        }

        mesh.RecalculateBounds();
        mesh.RecalculateTangents();

        var meshFilter = createdObject.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;

        var meshRenderer = createdObject.AddComponent<MeshRenderer>();
        meshRenderer.material = _defaultMaterial;

        mesh.Optimize();

        //Debug.Log($"메시 생성: {name} (버텍스: {meshData.Vertices.Count}, 삼각형: {meshData.Triangles.Count/3})");
    }

    private OptimizedMeshData CreateOptimizedMeshData(List<Vector3> objVertices, List<Vector3> objNormals, List<Vector2> objUVs, List<ObjFace> faces)
    {
        // 빠른 해시맵 기반 중복 제거
        var vertexMap = new Dictionary<HashVertexKey, int>(faces.Count);
        var uniqueVertices = new List<Vector3>(faces.Count);
        var uniqueNormals = new List<Vector3>(faces.Count);
        var uniqueUVs = new List<Vector2>(faces.Count);
        var triangles = new List<int>(faces.Count * 3);

        foreach (var face in faces)
        {
            for (int i = 0; i < 3; i++)
            {
                var vertex = face.Vertices[i];

                Vector3 pos = vertex.VertexIndex >= 0 && vertex.VertexIndex < objVertices.Count
                    ? objVertices[vertex.VertexIndex] : Vector3.zero;
                Vector3 normal = vertex.NormalIndex >= 0 && vertex.NormalIndex  < objNormals.Count
                    ? objNormals[vertex.NormalIndex] : Vector3.up;
                Vector2 uv = vertex.UvIndex >= 0 && vertex.UvIndex  < objUVs.Count
                    ? objUVs[vertex.UvIndex] : Vector2.zero;

                var key = new HashVertexKey(pos, normal, uv, _vertexMergeThreshold);

                if (vertexMap.TryGetValue(key, out int existingIndex))
                {
                    triangles.Add(existingIndex);
                }
                else
                {
                    int newIndex = uniqueVertices.Count;
                    uniqueVertices.Add(pos);
                    uniqueNormals.Add(normal);
                    uniqueUVs.Add(uv);
                    vertexMap[key] = newIndex;
                    triangles.Add(newIndex);
                }
            }
        }

        return new OptimizedMeshData
        {
            Vertices = uniqueVertices,
            Normals = uniqueNormals,
            Uvs = uniqueUVs,
            Triangles = triangles
        };
    }

    // 최적화된 해쉬 버텍스 키
    public struct HashVertexKey : IEquatable<HashVertexKey>
    {
        private readonly int _hash;
        private readonly Vector3 _position;
        private readonly Vector3 _normal;
        private readonly Vector2 _uv;

        public HashVertexKey(Vector3 pos, Vector3 norm, Vector2 texCoord, float threshold)
        {
            _position = pos;
            _normal = norm;
            _uv = texCoord;

            if (threshold == 0.0f)
            {
                threshold = 0.0001f; // 기본 임계값 설정
            }

            // 미리 계산된 해시 (성능 최적화)
            unchecked
            {
                int posHash = ((int)(pos.x / threshold) * 73856093) ^
                             ((int)(pos.y / threshold) * 19349663) ^
                             ((int)(pos.z / threshold) * 83492791);
                int normHash = ((int)(norm.x / threshold) * 73856093) ^
                              ((int)(norm.y / threshold) * 19349663) ^
                              ((int)(norm.z / threshold) * 83492791);
                int uvHash = ((int)(texCoord.x / threshold) * 73856093) ^
                            ((int)(texCoord.y / threshold) * 19349663);

                _hash = posHash ^ (normHash << 1) ^ (uvHash << 2);
            }
        }

        public bool Equals(HashVertexKey other)
        {
            const float epsilon = 0.001f;
            return Mathf.Abs(_position.x - other._position.x) < epsilon &&
                   Mathf.Abs(_position.y - other._position.y) < epsilon &&
                   Mathf.Abs(_position.z - other._position.z) < epsilon &&
                   Mathf.Abs(_normal.x - other._normal.x) < epsilon &&
                   Mathf.Abs(_normal.y - other._normal.y) < epsilon &&
                   Mathf.Abs(_normal.z - other._normal.z) < epsilon &&
                   Mathf.Abs(_uv.x - other._uv.x) < epsilon &&
                   Mathf.Abs(_uv.y - other._uv.y) < epsilon;
        }

        public override bool Equals(object obj)
        {
            return obj is HashVertexKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _hash;
        }
    }
}