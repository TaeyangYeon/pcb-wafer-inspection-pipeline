# 면접 준비 가이드
## PCB Wafer Inspection Pipeline (Project 2)

---

## 1. 1분 프로젝트 소개 (엘리베이터 피치)

### 30초 버전
저는 기존 비전 검사 경험을 바탕으로 **PatchCore 논문을 직접 구현**하여 **2단계 검사 파이프라인**을 개발했습니다. **비지도 이상 탐지**로 먼저 스크리닝하고, **YOLOv8-seg**로 정밀 분류하는 구조입니다. **FastAPI 서버**와 **C# Avalonia 클라이언트**를 분리하여 **실제 현장 배포**를 고려한 **엔터프라이즈 아키텍처**로 설계했습니다.

### 1분 버전
저는 기존 비전 검사 경험을 바탕으로 **PatchCore 논문을 직접 구현**하여 **산업용 PCB 검사 시스템**을 개발했습니다. 핵심은 **2단계 파이프라인** 구조인데요, **PatchCore로 이상 탐지**를 먼저 수행하고, 이상이 감지되면 **YOLOv8-seg**로 **6종류 결함을 픽셀 단위로 분류**합니다. 아키텍처는 **Python FastAPI 서버**와 **C# Avalonia 클라이언트**로 분리하여 **ML 엔지니어**와 **현장 엔지니어**가 각각 담당할 수 있도록 했습니다. **EF Core Repository 패턴**으로 **검사 이력을 체계적으로 관리**하고, **KS-test**를 통한 **모델 드리프트 모니터링**까지 구현했습니다. 결과적으로 **Box mAP50 0.96, Mask mAP50 0.91**을 달성했고, **92개 NUnit 테스트**로 **코드 품질**을 검증했습니다.

---

## 2. 핵심 기술 질문 & 모범 답변

### Q1. PatchCore를 직접 구현했다고 했는데, anomalib 같은 라이브러리를 쓰면 되지 않나요?

**모범 답변:**
네, anomalib을 사용하는 것이 더 빠르고 간편합니다만, 저는 **두 가지 이유**로 직접 구현했습니다. 첫째, **알고리즘에 대한 깊은 이해**를 얻기 위해서입니다. **timm WideResNet50 백본으로 피처 추출**하고, **greedy coreset subsampling**으로 메모리 뱅크를 구성하는 과정을 직접 구현하면서 **하이퍼파라미터 튜닝 포인트**를 정확히 알 수 있었습니다. 둘째, **PCB 검사에 특화된 최적화**를 위해서입니다. **메모리 사용량**과 **추론 속도**를 현장 요구사항에 맞게 조절하려면 내부 구현을 완전히 컨트롤할 수 있어야 했습니다.

**핵심 키워드:** timm WideResNet50, greedy coreset subsampling, 하이퍼파라미터 튜닝, 현장 최적화

**주의: 이렇게 말하면 안 됨** "anomalib이 복잡해서요" / "그냥 공부하려고요" → 목적성 없이 들림

### Q2. Detection이 아닌 Anomaly Detection을 선택한 이유가 무엇인가요?

**모범 답변:**
**실제 현장 상황**을 고려했습니다. **정상 제품은 많지만 결함 샘플은 적고**, 특히 **새로운 타입의 결함**이 계속 나타날 수 있습니다. **비지도 학습인 PatchCore**는 **정상 샘플만으로 학습**하기 때문에 **unknown 결함도 탐지**할 수 있습니다. 또한 **2단계 파이프라인**에서 **빠른 스크리닝 역할**을 합니다. 모든 이미지에 **YOLOv8-seg를 돌리면 연산량이 크**지만, **PatchCore로 먼저 필터링**하면 **60% 이상 연산량을 절약**할 수 있습니다.

**핵심 키워드:** 정상 샘플만 학습, unknown 결함 탐지, 빠른 스크리닝, 연산량 절약

**주의: 이렇게 말하면 안 됨** "더 쉬워서요" → 기술 선택에 대한 이해 부족으로 들림

### Q3. 2단계 파이프라인 구조를 설명해주세요.

**모범 답변:**
네, 화이트보드로 그려서 설명드리겠습니다. **[손으로 그리는 시늉]** 먼저 **이미지가 입력**되면 **PatchCore가 anomaly score**를 계산합니다. **threshold 이하면 정상**으로 바로 패스하고, **이상이면 2단계로 진입**합니다. 2단계에서는 **YOLOv8-seg가 6종류 결함을 픽셀 단위로 분류**하고 **confidence score**를 함께 출력합니다. 이 구조의 장점은 **정상 제품 99%는 빠르게 처리**하고, **의심 제품만 정밀 분석**한다는 점입니다. **실제 현장 워크플로우와 동일**하게 설계했습니다.

**핵심 키워드:** anomaly score threshold, 6종류 결함 픽셀 분류, 정상 제품 빠른 처리, 현장 워크플로우

**주의: 이렇게 말하면 안 됨** "그냥 두 개를 연결했어요" → 설계 의도 없이 들림

### Q4. FastAPI 서버와 C# 클라이언트를 분리한 이유는 무엇인가요?

**모범 답변:**
**현장 배포 상황**을 고려했습니다. **ML 엔지니어**는 **Python 생태계**에서 모델을 개선하고, **현장 엔지니어**는 **C# 환경**에서 UI와 데이터베이스를 관리하는 것이 자연스럽습니다. 또한 **여러 클라이언트**가 **하나의 추론 서버**를 공유할 수 있어 **자원 효율성**이 높습니다. **모델 업데이트**도 **서버만 재배포**하면 되므로 **유지보수**가 편합니다. 첫 번째 프로젝트에서는 **C#에 ONNX를 직접 임베드**했는데, 이번에는 **확장성**을 고려해서 분리했습니다.

**핵심 키워드:** 역할 분리, 자원 효율성, 모델 업데이트 편의성, 확장성

**주의: 이렇게 말하면 안 됨** "요즘 트렌드라서요" → 기술적 근거 없이 들림

### Q5. EF Core와 Repository 패턴을 사용한 이유는?

**모범 답변:**
**엔터프라이즈 환경**에서 요구되는 **데이터 관리**를 고려했습니다. **CSV 파일 저장**만으로는 **검사 이력 추적, 통계 분석, 드리프트 모니터링**이 어렵습니다. **EF Core**는 **타입 세이프한 쿼리**와 **자동 마이그레이션**을 제공하고, **Repository 패턴**은 **비즈니스 로직과 데이터 레이어를 분리**해서 **단위 테스트**와 **유지보수**를 용이하게 합니다. 실제로 **92개 NUnit 테스트 중 Repository 테스트**가 **핵심 비즈니스 로직을 검증**하는 역할을 합니다.

**핵심 키워드:** 검사 이력 추적, 타입 세이프 쿼리, 비즈니스 로직 분리, 단위 테스트 용이성

**주의: 이렇게 말하면 안 됨** "ORM이 편해서요" → 아키텍처 설계 이해 부족으로 들림

### Q6. KS-test 드리프트 모니터링이 실제 현장에서 왜 필요한가요?

**모범 답변:**
**제조 현장은 시간이 지나면서 조건이 변합니다**. **원자재 변경, 공정 파라미터 조정, 환경 변화** 등으로 **데이터 분포가 달라지면** 모델 성능이 저하됩니다. **단순 threshold 모니터링**은 **급격한 변화만 잡을 수 있지만**, **KS-test는 confidence score 분포의 미세한 변화**도 **통계적으로 유의미하게** 감지합니다. **p-value가 0.05 미만**이면 **분포가 유의미하게 변했다는 신호**이므로 **재학습이 필요함**을 조기에 알 수 있습니다.

**핵심 키워드:** 제조 현장 조건 변화, confidence score 분포 변화, 통계적 유의성, p-value 0.05, 조기 재학습 신호

**주의: 이렇게 말하면 안 됨** "논문에서 봐서요" → 현장 적용성 이해 부족으로 들림

### Q7. 테스트를 92개나 작성한 이유는?

**모범 답변:**
**포트폴리오라고 해서 품질을 포기할 수는 없다**고 생각했습니다. **실제 현장 배포를 가정**하면 **Repository, API 호출, ViewModel 로직** 등 **핵심 비즈니스 로직**에 대한 **신뢰성 확보**가 필수입니다. **92개 테스트로 70.5% 라인 커버리지**를 달성했고, **0 failures**를 유지하면서 **리팩토링과 기능 추가를 안전하게** 진행할 수 있었습니다. **5개 통합 테스트**는 **end-to-end 시나리오**를 검증하여 **실제 동작을 보장**합니다.

**핵심 키워드:** 현장 배포 가정, 핵심 비즈니스 로직 신뢰성, 70.5% 라인 커버리지, 안전한 리팩토링

**주의: 이렇게 말하면 안 됨** "완벽주義자라서요" → 개발 효율성 무시하는 것으로 들림

### Q8. 첫 번째 프로젝트와 무엇이 다른가요?

**모범 답변:**
**기술 깊이와 시스템 복잡도** 면에서 크게 발전했습니다. **첫 번째 프로젝트**는 **YOLOv8 detection만 사용한 단일 데스크톱 앱**이었고, **두 번째 프로젝트**는 **PatchCore + YOLOv8-seg 파이프라인**에 **서버-클라이언트 분산 아키텍처**입니다. **데이터 관리**도 **메모리 저장**에서 **EF Core + Repository 패턴**으로, **모니터링**도 **수동 확인**에서 **통계적 드리프트 탐지**로 발전했습니다. **논문 구현 역량**과 **엔터프라이즈 아키텍처 설계 역량**을 보여주는 프로젝트입니다.

**핵심 키워드:** 기술 깊이, 시스템 복잡도, 분산 아키텍처, 논문 구현 역량, 엔터프라이즈 설계

**주의: 이렇게 말하면 안 됨** "더 복잡하게 만들었어요" → 복잡도 증가의 목적 없이 들림

### Q9. CPU 추론을 선택한 이유는?

**모범 답변:**
**현실적 제약과 배포 환경**을 고려했습니다. **Intel Mac 개발 환경**과 **Google Colab T4 학습 제약** 상황에서 **CPU 최적화**에 집중했습니다. **실제 현장**에서도 **GPU는 비용과 전력 소모**가 크고, **여러 검사 라인에 배포**하려면 **CPU 기반이 현실적**입니다. **PatchCore 45ms + YOLOv8-seg 120ms = 165ms**로 **초당 6개 처리**가 가능하며, **일반적인 컨베이어 속도**에 충분합니다.

**핵심 키워드:** 현실적 제약, 배포 환경 고려, 비용과 전력 효율성, 165ms 처리 시간, 컨베이어 속도 대응

**주의: 이렇게 말하면 안 됨** "GPU가 없어서요" → 기술 선택의 전략적 사고 부족으로 들림

### Q10. 실제 현장 배포 시 어떤 점을 개선해야 한다고 생각하나요?

**모범 답변:**
**세 가지 핵심 개선점**이 있습니다. 첫째, **학습 데이터 확장**입니다. 현재 **클래스당 200장**으로 제한되어 있는데, **실제 현장 다양성**을 반영하려면 **클래스당 1000장 이상** 필요합니다. 둘째, **실시간 카메라 연동**입니다. 현재는 **File Watcher 시뮬레이션**이지만 **GigE Vision 카메라**와의 **직접 연동**이 필요합니다. 셋째, **PatchCore 전체 학습 완료**입니다. **Colab 환경 제약**으로 **20장 로컬 테스트**만 완료했는데, **전체 데이터셋 학습**과 **AUROC 검증**이 필요합니다.

**핵심 키워드:** 학습 데이터 확장, 실시간 카메라 연동, GigE Vision, 전체 데이터셋 학습, AUROC 검증

**주의: 이렇게 말하면 안 됨** "완벽합니다" → 현실 인식 부족으로 들림

---

## 3. 약점 & 솔직한 답변 준비

### 약점 1: 학습 데이터가 적음 (1,200장, 클래스당 200장)

**예상 질문:** "데이터가 너무 적지 않나요?"

**솔직하지만 강점으로 전환하는 답변:**
맞습니다. **현실적 제약**이었습니다. **Google Colab T4 4시간 제한** 상황에서 **전체 10,668장을 모두 학습**하기는 어려웠습니다. 하지만 **제한된 데이터로도 mAP50 0.91을 달성**한 것은 **데이터 전처리와 augmentation이 적절했다**는 증거입니다. **실제 프로젝트**에서는 **데이터 수집 전략**과 **점진적 학습**을 통해 **데이터 품질을 점진적으로 개선**하는 것이 더 중요하다고 생각합니다.

### 약점 2: 실제 카메라 없음 (File Watcher 시뮬레이션)

**예상 질문:** "실제 카메라 없이 어떻게 산업용이라고 할 수 있나요?"

**솔직하지만 강점으로 전환하는 답변:**
**File Watcher는 시뮬레이션**이 맞습니다. 하지만 **아키텍처 설계**에서 **이미지 입력 추상화**를 통해 **실제 카메라 연동이 쉽도록** 구성했습니다. **IImageService 인터페이스**를 통해 **File Watcher, HTTP API, GigE Vision** 등 **다양한 입력 소스**를 **플러그인 방식**으로 교체할 수 있습니다. **3년간 현장 경험**으로 **카메라 연동 요구사항**을 잘 알고 있어서 **확장 가능한 설계**를 했습니다.

### 약점 3: 회사에서 딥러닝 프로젝트 경험 없음

**예상 질문:** "실무 딥러닝 경험이 없는데 괜찮나요?"

**솔직하지만 강점으로 전환하는 답변:**
**전통적 비전 경험**이 오히려 **강점**이라고 생각합니다. **OpenCV 기반 전처리, 임계값 최적화, 현장 노이즈 처리** 등 **실무 노하우**가 있어서 **딥러닝 모델이 실패할 때의 대안**을 제시할 수 있습니다. **15일간 두 개 포트폴리오**를 완성한 것은 **빠른 학습 능력**과 **실행력**의 증거입니다. **기존 현장 경험 + 새로운 AI 기술**을 조합하는 것이 실제로 더 가치 있다고 봅니다.

### 약점 4: PatchCore 전체 학습 미완료

**예상 질문:** "PatchCore 결과가 TBD인데 정말 구현한 건가요?"

**솔직하지만 강점으로 전환하는 답변:**
**로컬에서 20장으로 proof-of-concept는 완료**했고, **전체 학습은 Colab에서 진행 중**입니다. **중요한 것은 알고리즘 구현 역량**입니다. **timm WideResNet50 백본, greedy coreset subsampling, nearest neighbor search** 등 **핵심 구현부는 완료**했고, **메모리 뱅크 구조**도 확인했습니다. **AUROC 수치보다는 논문을 읽고 직접 구현한 과정**에서 얻은 **deep understanding**이 더 중요하다고 생각합니다.

---

## 4. 숫자로 말하는 포트폴리오

### Project 1: vision-inspection-portfolio
- **YOLOv8 Detection mAP50:** bottle 0.869 / tile 0.946
- **NUnit Tests:** 150 tests, 0 failures  
- **추론 속도:** ~36ms / ~27 FPS (CPU, Intel Mac)
- **Model Size:** ~6MB (ONNX)
- **Dataset:** MVTec AD

### Project 2: pcb-wafer-inspection-pipeline  
- **YOLOv8-seg Box mAP50:** 0.9609
- **YOLOv8-seg Mask mAP50:** 0.9147
- **추론 속도:** 5.0ms (T4 GPU), 165ms (CPU 파이프라인)
- **Model Size:** 6.5MB
- **NUnit Tests:** 92 tests, 0 failures
- **Core Coverage:** 70.5%
- **개발 기간:** 15일
- **학습 데이터:** 1,200장 (클래스당 200장)

---

## 5. 회사별 어필 포인트

### SFA / 한화 장비 대기업
**중요하게 보는 것:** 실시간 처리, 산업 현장 적응력, C# 개발 역량, 시스템 안정성

**어필 포인트:**
- **3년 현장 경험 + C# .NET 10 숙련도**
- **SOLID 원칙, MVVM 패턴, DI 컨테이너** 적용한 엔터프라이즈 아키텍처  
- **92개 NUnit 테스트, 70.5% 커버리지**로 검증된 코드 품질
- **CPU 165ms 처리 속도**로 실시간 대응 가능
- **드리프트 모니터링**으로 장기 운영 안정성 확보

### 삼성/SK 반도체 벤더  
**중요하게 보는 것:** 웨이퍼/PCB 도메인 이해, 정밀도, 재현율, 반도체 공정 지식

**어필 포인트:**
- **PCB 6종 결함** (missing_hole, short, open_circuit 등) **도메인 특화**
- **Mask mAP50 0.915**로 **픽셀 단위 정밀 분류** 역량 입증
- **비지도 anomaly detection**으로 **unknown 결함 탐지** 가능
- **2단계 파이프라인**으로 **정상품 99% 빠른 처리, 이상품만 정밀 분석**
- **통계적 품질 관리** (KS-test) 역량

### AI 비전 스타트업
**중요하게 보는 것:** 최신 모델 구현력, 논문 이해도, 빠른 프로토타이핑, 풀스택 역량

**어필 포인트:**  
- **PatchCore 논문 직접 구현** (anomalib 미사용)으로 알고리즘 이해도 입증
- **15일 프로젝트 완성**으로 빠른 실행력 증명
- **FastAPI + Streamlit + C# Avalonia** 풀스택 역량
- **GitHub 포트폴리오 2개**로 꾸준한 학습 의지 표현
- **전통 비전 + AI 융합** 능력으로 차별화

---

## 6. 포트폴리오 발표 순서 (5분 발표 시나리오)

### 1분: README 아키텍처 다이어그램 (30초)
"**2단계 파이프라인 구조**를 보시면, **PatchCore anomaly screening** 후 **YOLOv8-seg 정밀 분류**로 이어집니다. **FastAPI 서버**와 **C# 클라이언트 분리**로 **현장 배포**를 고려했습니다."

### 2-4분: 핵심 코드 3곳 (2분 30초)

**2-1. PatchCore 구현** (45초)
```csharp
// 01_training/src/patchcore_runner.py - greedy coreset subsampling
def reduce_via_greedy_coreset_selection(self, features, num_select):
    # 논문의 greedy subsampling 직접 구현
```
"**논문의 coreset subsampling을 직접 구현**했습니다. **anomalib 대신 직접 구현**한 이유는..."

**2-2. 파이프라인 로직** (45초)  
```csharp
// 02_inspection/InspectionPipeline.Core/Services/InspectionService.cs
public async Task<InspectionResult> ProcessImageAsync(string imagePath)
{
    // 1단계: PatchCore anomaly detection
    var anomalyResult = await _apiClient.DetectAnomalyAsync(imagePath);
    
    if (anomalyResult.IsAnomalous)
    {
        // 2단계: YOLOv8-seg segmentation
        var segResult = await _apiClient.SegmentAsync(imagePath);
    }
}
```
"**2단계 파이프라인의 핵심 로직**입니다. **threshold 기반 분기**로..."

**2-3. EF Core Repository** (60초)
```csharp
// InspectionPipeline.Core/Repositories/InspectionRepository.cs  
public async Task<IEnumerable<InspectionRecord>> GetRecentInspectionsAsync(int count)
{
    return await _context.InspectionRecords
        .OrderByDescending(x => x.Timestamp)
        .Take(count)
        .ToListAsync();
}
```
"**Repository 패턴**으로 **데이터 레이어를 분리**했고, **92개 테스트**로 검증했습니다."

### 4-5분: 결과 수치 & 테스트 데모 (1분)
"**Box mAP50 0.96, Mask mAP50 0.91** 달성했고, **92개 NUnit 테스트 모두 통과**입니다."

```bash
dotnet test InspectionPipeline.Tests/ --verbosity minimal
# 실제 테스트 실행 화면 보여주기
```

---

## 7. 예상 코딩 테스트 / 기술 질문

### Q: PatchCore coreset subsampling 알고리즘을 수도코드로 설명하시오

**모범 답변:**
```
Algorithm: Greedy Coreset Subsampling
Input: features (N x D), num_select (K)
Output: selected_indices (K,)

1. selected = []
2. remaining = [0, 1, ..., N-1]
3. 
4. // 첫 번째는 랜덤 선택
5. first_idx = random(remaining)
6. selected.append(first_idx)
7. remaining.remove(first_idx)
8. 
9. // Greedy selection
10. for i in range(1, K):
11.     max_min_dist = -1
12.     best_idx = -1
13.     
14.     for candidate in remaining:
15.         // candidate와 기존 selected들 간의 최소 거리
16.         min_dist = min(distance(features[candidate], features[s]) 
17.                       for s in selected)
18.         
19.         // 최소 거리가 가장 큰 candidate 선택
20.         if min_dist > max_min_dist:
21.             max_min_dist = min_dist
22.             best_idx = candidate
23.     
24.     selected.append(best_idx)
25.     remaining.remove(best_idx)
26. 
27. return selected
```

**핵심:** **각 단계마다 기존 선택된 점들과 가장 먼 점을 선택**하여 **다양성을 최대화**하는 greedy 알고리즘

### Q: KS-test p-value가 낮을수록 drift가 크다는 것을 설명하시오

**모범 답변:**
**KS-test의 귀무가설은 "두 분포가 동일하다"**입니다. **p-value가 낮다는 것은 귀무가설을 기각**한다는 뜻이므로, **"두 분포가 다르다"**는 증거가 강하다는 의미입니다.

구체적으로:
- **p-value > 0.05**: 두 분포 간 차이가 통계적으로 유의하지 않음 → **drift 없음**
- **p-value < 0.05**: 두 분포가 유의미하게 다름 → **drift 발생**  
- **p-value < 0.01**: 매우 강한 증거 → **심각한 drift**

**실제 적용**: **baseline confidence score 분포**와 **현재 confidence score 분포**를 비교하여 **모델 성능 변화를 조기 감지**

### Q: Repository 패턴 없이 DbContext를 ViewModel에서 직접 쓰면 어떤 문제가 생기나요?

**모범 답변:**
**세 가지 주요 문제**가 있습니다:

1. **강결합 (Tight Coupling)**: ViewModel이 특정 DB 기술 (EF Core)에 직접 의존하게 되어 **DB 변경 시 ViewModel도 수정** 필요

2. **테스트 어려움**: DbContext를 Mock하기 어려워 **단위 테스트가 실제 DB에 의존**하게 됨. **테스트 속도 저하**와 **격리 실패**

3. **단일 책임 원칙 위반**: ViewModel이 **UI 로직**과 **데이터 액세스 로직**을 모두 담당하게 되어 **책임이 분산**됨

**Repository 패턴의 장점**:
```csharp
// 테스트 시
var mockRepo = new Mock<IInspectionRepository>();
mockRepo.Setup(r => r.GetRecentAsync(5))
       .ReturnsAsync(testData);

var viewModel = new DashboardViewModel(mockRepo.Object);
// DB 없이 독립적 테스트 가능
```

### Q: SemaphoreSlim(1,1)을 쓴 이유와 lock 키워드와의 차이는?

**모범 답변:**
**비동기 메서드에서는 lock 키워드를 사용할 수 없기** 때문입니다.

```csharp
// ❌ 컴파일 에러
public async Task ProcessAsync()
{
    lock (_lockObject)  // CS1996: Cannot await inside lock
    {
        await SomeAsyncMethod();
    }
}

// ✅ SemaphoreSlim 사용  
private readonly SemaphoreSlim _semaphore = new(1, 1);

public async Task ProcessAsync()
{
    await _semaphore.WaitAsync();
    try
    {
        await SomeAsyncMethod();
    }
    finally
    {
        _semaphore.Release();
    }
}
```

**차이점:**
- **lock**: 동기 메서드만, OS 레벨 뮤텍스, 더 빠름
- **SemaphoreSlim(1,1)**: 비동기 지원, 카운팅 세마포어, 취소 토큰 지원

**적용 이유**: **InspectionService의 ProcessImageAsync**에서 **동시 요청 방지**를 위해 사용

---