# ANT Mission Manager

WPF .NET 8.0 기반 ANT (Autonomous Navigation Technology) 미션 관리 시스템

## 주요 기능

### 🔐 로그인 시스템
- 기본 계정: `admin` / `123456`
- 간단한 인증 시스템

### 📊 미션 대시보드
- 실시간 미션 현황 모니터링
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
- 차량 목록 실시간 조회
- 차량 Insert/Extract 명령
- 차량 상태 모니터링:
  - 운영 상태 (Idle, Running, Charging, Error, Maintenance)
  - 배터리 레벨 (프로그레스 바 표시)
  - 현재 위치
  - 미션 ID
  - 알람 상태
- 배터리 부족 경고 (30% 이하)

## 기술 스택

- **Framework**: WPF .NET 8.0
- **UI Library**: Material Design In XAML
- **Pattern**: MVVM
- **Data Format**: CSV/TXT
- **HTTP Client**: HttpClient (ANT API 통신)
- **JSON**: Newtonsoft.Json
- **CSV**: CsvHelper

## 프로젝트 구조

```
AntMissionManager/
├── AntMissionManager.csproj        # 프로젝트 파일 및 패키지 참조
├── App.xaml                        # 전역 리소스/스타일 정의
├── App.xaml.cs                     # 애플리케이션 시작 진입점
├── Models/                         # 도메인/DTO 클래스
│   ├── AlarmInfo.cs                # 알람 로그 정보 및 상태 매핑
│   ├── MissionInfo.cs              # 미션 현황 데이터 모델
│   ├── MissionRoute.cs             # 노드 경로 및 미션 타입 정의
│   ├── NodeInfo.cs                 # ANT 노드(포인트) 정보
│   └── Vehicle.cs                  # 차량 상태, 배터리, 좌표 등 상세 정보
├── Services/                       # 백엔드/API/파일 서비스
│   ├── AntApiService.cs            # 로그인, 차량/미션/알람 REST API 호출
│   └── FileService.cs              # 미션 라우터 CSV Import/Export
├── Utilities/                      # 보조 유틸리티
│   ├── Converters.cs               # 배터리/상태 → 색상/가시성 변환기
│   └── DialogService.cs            # 커스텀 메시지/파일 다이얼로그 헬퍼
├── ViewModels/                     # MVVM ViewModel 계층
│   ├── LoginViewModel.cs           # 로그인 로직 및 서버 연결 초기화
│   ├── MainViewModel.cs            # 대시보드·라우터·차량 관리 상태 관리
│   ├── RelayCommand.cs             # ICommand 래퍼
│   └── ViewModelBase.cs            # INotifyPropertyChanged 기본 구현
├── Views/                          # XAML 뷰 및 코드 비하인드
│   ├── CommonDialogWindow.xaml     # 공용 다이얼로그 쉘
│   ├── CommonDialogWindow.xaml.cs
│   ├── LoginWindow.xaml            # 로그인 화면
│   ├── LoginWindow.xaml.cs
│   ├── MainWindow.xaml             # 메인 대시보드/탭 UI
│   ├── MainWindow.xaml.cs
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
| `Models/` | ANT 서버에서 내려오는 차량·미션·알람 데이터를 표현하는 POCO 모델 모음. |
| `Services/` | 싱글톤 `AntApiService`로 REST 통신을 캡슐화하고, `FileService`로 CSV 입출력을 담당. |
| `Utilities/` | WPF 전용 컨버터와 커스텀 다이얼로그 서비스 등 View에서 재사용되는 헬퍼. |
| `ViewModels/` | MVVM 패턴의 핵심 로직. 로그인 이후 메인 탭 상태 관리, 커맨드 바인딩 제공. |
| `Views/` | Material Design 테마를 적용한 XAML 화면과 코드 비하인드. 공통 메시지 다이얼로그 포함. |
| `bin/`, `obj/` | .NET 빌드시 자동 생성되는 출력/캐시 폴더. 배포 소스에서 제외. |

### 주요 흐름

1. `App.xaml.cs`가 실행되면서 `Views/LoginWindow`를 최초로 띄워 로그인 절차를 시작합니다.
2. `LoginViewModel`이 `AntApiService`를 통해 토큰을 발급받고 연결 상태를 공유합니다.
3. 로그인 성공 시 `MainWindow`가 열리고, `MainViewModel`이 미션/차량/알람 데이터를 주기적으로 동기화합니다.
4. `Utilities/DialogService`와 `Views/Common/*` 구성 요소가 사용자 피드백(메시지, 파일 선택)을 담당합니다.

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

### 2. 미션 라우터 등록
1. **미션 라우터** 탭 선택
2. 라우터 이름 입력
3. 미션 유형 선택 (Transport, Move, PickAndDrop)
4. 노드 경로 설정:
   - 노드 선택 후 **추가** 버튼 클릭
   - 여러 노드 추가로 멀티 라우터 생성
5. **라우터 저장** 버튼 클릭

### 3. 차량 관리
1. **차량 관리** 탭 선택
2. 차량 선택 및 삽입 노드 선택
3. **차량 삽입** 또는 **차량 추출** 실행
4. 차량 상태 모니터링

### 4. 파일 관리
- **Export**: 현재 라우터를 CSV 파일로 내보내기
- **Import**: CSV 파일에서 라우터 가져오기

## 데이터 파일 형식

### mission_routes.csv
```csv
Id,Name,Nodes,MissionType,CreatedAt,IsActive
1,PickupToDropoff,"NodeA,NodeB",Transport,2024-01-01T10:00:00,true
2,MultiRoute,"NodeA,NodeB,NodeC",Transport,2024-01-01T11:00:00,true
```

## ANT API 연동

### 지원 기능
- 로그인/인증
- 차량 정보 조회
- 미션 생성/취소/조회
- 차량 Insert/Extract 명령
- 실시간 상태 모니터링

### API 엔드포인트
- `POST /wms/rest/login` - 로그인
- `GET /wms/rest/{version}/vehicles` - 차량 조회
- `GET /wms/rest/{version}/missions` - 미션 조회
- `POST /wms/rest/{version}/missions` - 미션 생성
- `DELETE /wms/rest/{version}/missions/{id}` - 미션 취소
- `POST /wms/rest/{version}/vehicles/{name}/command` - 차량 명령

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

## 라이선스

이 프로젝트는 MIT 라이선스 하에 배포됩니다.

## 버전 히스토리

### v1.0.0
- 초기 릴리스
- 기본 미션 관리 기능
- 차량 Insert/Extract 기능
- CSV 파일 Import/Export
- Material Design UI
