# Unity OBJ Asset Loader

Unity에서 OBJ 파일을 효율적으로 로드하기 위한 고성능 비동기 로더 시스템입니다.

## 🚀 주요 기능

- **비동기 로딩**: 메인 스레드 블로킹 없이 OBJ 파일을 백그라운드에서 로드
- **배치 로딩**: 폴더 내 모든 OBJ 파일을 동시에 로드
- **성능 최적화**: 
  - 해시 기반 중복 정점 제거
  - 메모리 사전 할당으로 GC 압박 최소화
  - 커스텀 파싱 로직으로 빠른 처리 속도
- **렌더 파이프라인 지원**: Built-in, URP, HDRP 지원
- **로드 시간 측정**: 디버깅을 위한 로드 시간 표시

## 📋 시스템 요구사항

- Unity 2019.4 이상
- .NET Standard 2.0
- 모든 렌더 파이프라인 호환

## 🛠️ 설치 방법

1. `AssetLoader.cs`와 `LoaderModule.cs` 파일을 Unity 프로젝트에 추가
2. 빈 GameObject를 생성하고 `AssetLoader` 컴포넌트 추가
3. Inspector에서 `LoaderModule` 필드에 LoaderModule 컴포넌트 연결

## 🎮 사용 방법

### 기본 설정

```csharp
public class AssetLoader : MonoBehaviour
{
    [field: SerializeField]
    public LoaderModule LoaderModule { get; set; }
}
```

### 단일 파일 로드

```csharp
// 동기 로드
assetLoader.Load("path/to/model.obj");

// 비동기 로드
assetLoader.AsyncLoad("path/to/model.obj");
```

### 폴더 전체 로드

```csharp
List<string> objFiles = GetObjFiles("/Resources/Models");
assetLoader.Load(objFiles);  // 모든 파일을 비동기로 동시 로드
```

### LoaderModule 설정

Inspector에서 다음 옵션들을 설정할 수 있습니다:

- **Render Pipeline**: Built-in, URP, HDRP 선택
- **Default Material**: 기본 머티리얼 설정
- **Preallocate Collections**: 성능 향상을 위한 메모리 사전 할당
- **Expectation Vertex Count**: 예상 정점 수 (기본값: 10,000)
- **Vertex Merge Threshold**: 정점 병합 임계값 (기본값: 0.0001)

## 📊 성능 최적화

### 1. 해시 기반 정점 중복 제거
```csharp
public struct HashVertexKey : IEquatable<HashVertexKey>
{
    // 위치, 법선, UV를 해시로 비교하여 중복 정점 제거
}
```

### 2. 메모리 사전 할당
```csharp
List<Vector3> vertices = _preallocateCollections ? 
    new List<Vector3>(_expectationVertexCount) : new List<Vector3>();
```

### 3. 비동기 파일 파싱
```csharp
private Task<ObjFileParseData> AsyncOBJFileParse(string assetPath)
{
    return Task.Run(() => {
        // 백그라운드 스레드에서 파일 파싱
    });
}
```

## 📁 프로젝트 구조

```
Assets/
├── Scripts/
│   ├── AssetLoader.cs          # 메인 로더 컨트롤러
│   └── LoaderModule.cs         # OBJ 파싱 및 메시 생성 모듈
└── Resources/
    └── Models/                 # OBJ 파일들이 위치할 폴더
        ├── model1.obj
        ├── model2.obj
        └── ...
```

## 🔧 지원하는 OBJ 기능

- ✅ 정점 (v)
- ✅ 법선 (vn) 
- ✅ 텍스처 좌표 (vt)
- ✅ 면 (f)
- ✅ 오브젝트/그룹 (o/g)
- ✅ 다각형 자동 삼각분할
- ⏳ 머티리얼 라이브러리 (mtl) - 개발 예정

## 📈 성능 벤치마크

로드 시간이 콘솔에 자동으로 출력됩니다:

```
OBJ 파일 로딩 시작: model.obj
OBJ 로딩 완료! 소요 시간: 0.235초
메시 생성: model_Mesh (버텍스: 15420, 삼각형: 8540)
```

## 🐛 문제 해결

### 일반적인 문제들

1. **파일을 찾을 수 없음**
   - 파일 경로가 올바른지 확인
   - Unity 프로젝트의 Assets 폴더 기준 경로 사용

2. **성능이 느림**
   - `_preallocateCollections`을 true로 설정
   - `_expectationVertexCount`를 적절히 조정
   - `_vertexMergeThreshold` 값 조정

3. **메모리 사용량이 높음**
   - 큰 모델의 경우 배치 로딩 대신 개별 로딩 사용
   - 불필요한 모델은 언로드

## 🤝 기여하기

버그 리포트나 기능 개선 제안은 Issues 탭에서 해주세요.

## 📄 라이선스

이 프로젝트는 MIT 라이선스 하에 배포됩니다.

---

Made with ❤️ for Unity Developers