# ANT Mission Manager

WPF .NET 8.0 기반 ANT (Autonomous Navigation Technology) 미션 관리 시스템

## 주요 기능

### 🔐 로그인 시스템
- 기본 계정: `admin` / `123456`
- 간단한 인증 시스템

### 📊 미션 대시보드
- 실시간 미션 현황 모니터링 (1초마다 자동 갱신)
- 미션 상태별 통계 (총 미션, 실행중, 대기중, 완료됨)
- 미션 목록 DataGrid 표시
- 미션 상태별 필터링
- 미션 취소 기능

### 🛣️ 미션 라우터 관리
- **단일 라우터**: A → B 경로 설정
- **멀티 라우터**: A → B → C 순차 경로 설정
- 라우터 이름 및 미션 유형 설정
- CSV 파일 기반 저장/로드
- Import/Export 기능
- 라우터 수정/삭제

### 🚗 차량 관리
- 차량 목록 실시간 조회 (1초마다 자동 갱신)
- 차량 Insert/Extract 명령
- 차량 상태 모니터링:
  - 운영 상태 (Idle, Running, Charging, Error, Maintenance)
  - 배터리 레벨 (프로그레스 바 표시)
  - 현재 위치
  - 미션 ID
  - 알람 상태
- 배터리 부족 경고 (30% 이하)

### 🗺️ 실시간 맵 뷰 (신규 추가)
- **고성능 렌더링**: 노드/링크 캐싱으로 부드러운 이동
- **인터랙티브 맵 조작**:
  - 마우스 휠: 확대/축소
  - 마우스 드래그: 전방향 맵 이동 (위/아래/좌/우)
- **지게차 실시간 추적**:
  - Extract 상태: 맵에서 숨김
  - Insert/Running 상태: 상태별 색상 구분
  - 미션 수행 중: 주황색
  - 충전 중: 시안색
  - 주차 중: 회색
  - 이동 중: 노란색
- **노드 정보 스낵바**: 노드 마우스 오버 시 이름 표시
- **설정 저장 기능**: 
  - 맵 오프셋 (X, Y 위치) 자동 저장
  - 회전 각도 설정 가능
  - 프로그램 재시작 시 이전 설정 복원

### 🚨 알람 로그 (신규 추가)
- 실시간 알람 모니터링 (1초마다 자동 갱신)
- 알람 검색 및 필터링
- 정렬 기능 (시간, 상태, 타입별)
- 상세 알람 정보 표시

## 기술 스택

- **Framework**: WPF .NET 8.0
- **UI Library**: Material Design In XAML
- **Pattern**: MVVM
- **Data Format**: CSV/JSON
- **HTTP Client**: HttpClient (ANT API 통신)
- **JSON**: Newtonsoft.Json
- **CSV**: CsvHelper
- **Rendering**: WPF Canvas (고성능 2D 렌더링)

## 프로젝트 구조

```
AntMissionManager/
├── AntMissionManager.csproj        # 프로젝트 파일 및 패키지 참조
├── App.xaml                        # 전역 리소스/스타일 정의
├── App.xaml.cs                     # 애플리케이션 시작 진입점
├── Models/                         # 도메인/DTO 클래스
│   ├── AlarmInfo.cs                # 알람 로그 정보 및 상태 매핑
│   ├── MapData.cs                  # 맵 레이어, 노드, 링크 데이터 모델
│   ├── MissionInfo.cs              # 미션 현황 데이터 모델
│   ├── MissionRoute.cs             # 노드 경로 및 미션 타입 정의
│   ├── NodeInfo.cs                 # ANT 노드(포인트) 정보
│   └── Vehicle.cs                  # 차량 상태, 배터리, 좌표 등 상세 정보
├── Services/                       # 백엔드/API/파일 서비스
│   ├── AntApiService.cs            # REST API 통신 (로그인, 차량/미션/알람/맵 데이터)
│   ├── FileService.cs              # 미션 라우터 CSV Import/Export
│   ├── MapLogger.cs                # 맵 렌더링 디버그 로깅
│   └── MapSettingsService.cs       # 맵 설정 영구 저장/로드
├── Utilities/                      # 보조 유틸리티
│   ├── Converters.cs               # 배터리/상태 → 색상/가시성 변환기
│   └── DialogService.cs            # 커스텀 메시지/파일 다이얼로그 헬퍼
├── ViewModels/                     # MVVM ViewModel 계층
│   ├── LoginViewModel.cs           # 로그인 로직 및 서버 연결 초기화
│   ├── MainViewModel.cs            # 대시보드·라우터·차량 관리 상태 관리
│   ├── RelayCommand.cs             # ICommand 래퍼 (동기/비동기)
│   └── ViewModelBase.cs            # INotifyPropertyChanged 기본 구현
├── Views/                          # XAML 뷰 및 코드 비하인드
│   ├── CommonDialogWindow.xaml     # 공용 다이얼로그 쉘
│   ├── CommonDialogWindow.xaml.cs
│   ├── LoginWindow.xaml            # 로그인 화면
│   ├── LoginWindow.xaml.cs
│   ├── MainWindow.xaml             # 메인 대시보드/탭 UI
│   ├── MainWindow.xaml.cs
│   ├── MapView.xaml                # 실시간 맵 렌더링 UserControl
│   ├── MapView.xaml.cs             # 맵 인터랙션 및 최적화 로직
│   ├── VehicleDetailWindow.xaml    # 차량 상세 팝업
│   ├── VehicleDetailWindow.xaml.cs
│   └── Common/
│       ├── MessageDialog.xaml      # Material 스타일 메시지 다이얼로그
│       └── MessageDialog.xaml.cs
├── bin/                            # 빌드 산출물 (자동 생성)
└── obj/                            # 중간 빌드 결과 (자동 생성)
```

### 디렉터리별 설명

| 경로 | 주요 내용 |
|------|-----------|
| `Models/` | ANT 서버에서 내려오는 차량·미션·알람·맵 데이터를 표현하는 POCO 모델 모음. |
| `Services/` | 싱글톤 `AntApiService`로 REST 통신을 캡슐화하고, `FileService`로 CSV 입출력, `MapLogger`로 디버깅, `MapSettingsService`로 설정 관리. |
| `Utilities/` | WPF 전용 컨버터와 커스텀 다이얼로그 서비스 등 View에서 재사용되는 헬퍼. |
| `ViewModels/` | MVVM 패턴의 핵심 로직. 로그인 이후 메인 탭 상태 관리, 커맨드 바인딩 제공. |
| `Views/` | Material Design 테마를 적용한 XAML 화면과 코드 비하인드. 실시간 맵 뷰 포함. |

### 아키텍처 및 최적화

#### 데이터 로드 전략
- **맵 데이터**: 정적 데이터로 프로그램 시작 시 1회만 로드하여 메모리에 캐싱
- **차량 데이터**: 1초마다 자동 갱신 (실시간 위치, 상태, 배터리)
- **미션 데이터**: 1초마다 자동 갱신 (미션 진행 상황)
- **알람 데이터**: 1초마다 자동 갱신 (새로운 알람 발생)

#### 맵 렌더링 최적화
- **렌더링 스케줄링**: 60FPS 타이머로 불필요한 렌더링 방지
- **요소 캐싱**: 노드/링크 UI 요소를 딕셔너리에 캐시
- **지연 렌더링**: 실제 변경이 있을 때만 Canvas 업데이트
- **메모리 효율성**: 노드 라벨 제거로 UI 요소 수 최소화

#### 실시간 업데이트 플로우
```
Timer (1초) → API 호출 → 데이터 비교 → UI 업데이트 (변경분만)
            ↓
        맵 뷰 자동 갱신 (차량 위치, 상태 색상)
```

## 설치 및 실행

### 요구사항
- .NET 8.0 Runtime
- Windows 10/11
- ANT Server (테스트용)

### 빌드
```bash
dotnet build AntMissionManager.csproj
```

### 실행
```bash
dotnet run
```

## 사용법

### 1. 로그인
- 기본 계정으로 로그인: `admin` / `123456`

### 2. 실시간 맵 뷰 사용법
1. **맵 탭** 선택
2. **맵 조작**:
   - 마우스 휠: 확대/축소
   - 마우스 드래그: 맵 이동 (모든 방향)
3. **차량 모니터링**:
   - 지게차 상태별 색상으로 구분
   - Extract 상태 차량은 자동으로 숨김
4. **노드 정보 확인**:
   - 노드에 마우스 오버 시 스낵바로 이름 표시
5. **설정 저장**:
   - 회전 각도 텍스트박스에서 직접 입력 (Enter 키)
   - "Save Settings" 버튼으로 현재 맵 위치/각도 저장

### 3. 미션 라우터 등록
1. **미션 라우터** 탭 선택
2. 라우터 이름 입력
3. 미션 유형 선택 (Transport, Move, PickAndDrop)
4. 노드 경로 설정:
   - 노드 선택 후 **추가** 버튼 클릭
   - 여러 노드 추가로 멀티 라우터 생성
5. **라우터 저장** 버튼 클릭

### 4. 차량 관리
1. **차량 관리** 탭 선택
2. 차량 선택 및 삽입 노드 선택
3. **차량 삽입** 또는 **차량 추출** 실행
4. 차량 상태 모니터링

### 5. 파일 관리
- **Export**: 현재 라우터를 CSV 파일로 내보내기
- **Import**: CSV 파일에서 라우터 가져오기

## 데이터 파일 형식

### mission_routes.csv
```csv
Id,Name,Nodes,MissionType,CreatedAt,IsActive
1,PickupToDropoff,"NodeA,NodeB",Transport,2024-01-01T10:00:00,true
2,MultiRoute,"NodeA,NodeB,NodeC",Transport,2024-01-01T11:00:00,true
```

### 맵 설정 파일 (자동 생성)
`%APPDATA%/AntMissionManager/mapsettings.json`
```json
{
  "OffsetX": 150.5,
  "OffsetY": -75.2,
  "RotationAngle": 15.0,
  "ZoomLevel": 1.5,
  "ShowNodeLabels": false,
  "LastSaved": "2024-10-30T10:30:00"
}
```

## ANT API 연동

### 지원 기능
- 로그인/인증
- 차량 정보 조회
- 미션 생성/취소/조회
- 차량 Insert/Extract 명령
- 맵 데이터 조회 (노드, 링크)
- 실시간 상태 모니터링
- 알람 로그 조회

### API 엔드포인트
- `POST /wms/rest/login` - 로그인
- `GET /wms/rest/{version}/vehicles` - 차량 조회
- `GET /wms/rest/{version}/missions` - 미션 조회
- `POST /wms/rest/{version}/missions` - 미션 생성
- `DELETE /wms/rest/{version}/missions/{id}` - 미션 취소
- `POST /wms/rest/{version}/vehicles/{name}/command` - 차량 명령
- `GET /wms/rest/{version}/maps/level/1/data` - 맵 데이터 조회
- `GET /wms/rest/{version}/alarms` - 알람 조회

## 상태 코드 설명

### 네비게이션 상태 (NavigationState)
- `0`: 수신됨 (Received)
- `1`: 승인됨 (Accepted/Planned)
- `2`: 거부됨 (Rejected)
- `3`: 시작됨 (Started/Running)
- `4`: 완료됨 (Completed/Successful)
- `5`: 취소됨 (Cancelled)

### 운송 상태 (TransportState)
- `0`: 새로운 운송작업 (New)
- `1`: 운송작업 승인됨 (Accepted)
- `3`: 차량에 할당됨 (Assigned)
- `4`: 이동중 (Moving)
- `8`: 운송 완료 (Completed)
- `10`: 오류 발생 (Error)

### 차량 운영 상태 (OperatingState)
- `0`: Idle (대기)
- `1`: Running (실행중)
- `2`: Charging (충전중)
- `3`: Error (오류)
- `4`: Maintenance (정비)

### 차량 상태 (VehicleState) - 맵 표시용
- `"extracted"`: 맵에서 숨김 ❌
- `"runningamission"`: 주황색 🟠 (미션 수행 중)
- `"charging"`: 시안색 🔵 (충전 중)
- `"parking"`: 회색 ⚫ (주차 중)
- `"movingtonode"`: 노란색 🟡 (노드로 이동 중)

## 성능 최적화

### 렌더링 최적화
- 노드 라벨 제거로 UI 요소 50% 감소
- 60FPS 렌더링 스케줄러로 부드러운 애니메이션
- 맵 데이터 메모리 캐싱으로 API 호출 최소화

### 메모리 관리
- 정적 맵 데이터는 1회 로드 후 재사용
- 실시간 데이터만 주기적 갱신
- UI 요소 캐싱으로 GC 압박 최소화

### 네트워크 효율성
- 차량/미션/알람만 1초마다 갱신
- 맵 데이터는 프로그램 시작 시 1회 로드
- 실패한 요청에 대한 자동 재시도 없이 로깅만 수행

## 라이선스

이 프로젝트는 MIT 라이선스 하에 배포됩니다.

## 버전 히스토리

### v2.0.0 (최신)
- **실시간 맵 뷰 추가**: 지게차 위치/상태 실시간 추적
- **맵 인터랙션 개선**: 드래그로 전방향 이동, 회전 기능
- **성능 최적화**: 렌더링 스케줄링, 데이터 캐싱
- **설정 영구 저장**: 맵 위치/각도 자동 저장 및 복원
- **노드 스낵바**: 마우스 오버로 노드 정보 표시
- **알람 로그 시스템**: 실시간 알람 모니터링 및 필터링

### v1.0.0
- 초기 릴리스
- 기본 미션 관리 기능
- 차량 Insert/Extract 기능
- CSV 파일 Import/Export
- Material Design UI