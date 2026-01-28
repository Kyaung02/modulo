# **🏭 Modulo (모듈로)**

## **🏭 핵심 한눈에 보기**

**Modulo**는 단어를 자원처럼 가공하고 조립하여 목표를 달성하는 **무한 확장형 팩토리 자동화 게임**입니다.
단순한 공장 건설을 넘어, 공장 자체를 하나의 모듈로 캡슐화하고 재귀적으로 사용하는 깊이 있는 설계를 제공합니다.

- **🧩 재귀적 설계 (Recursion)**: 내가 만든 공장 내부를 하나의 '부품'으로 압축하여 상위 공장에서 사용하는 무한 계층 구조를 지원합니다.
- **🔡 단어 가공 시스템**: 컨베이어 벨트 위의 자원은 '물건'이 아닌 '단어(String)'입니다. 글자를 합치고(Combiner), 분배(Distributer)하여 목표 단어를 완성하세요.
- **🤝 멀티플레이어 협동**: 친구들과 함께 접속하여 실시간으로 거대한 자동화 공장을 설계하고 운영할 수 있습니다.
- **💾 블루프린트 공유**: 자신만의 효율적인 공장 설계를 저장하고 프리뷰 이미지와 함께 쉽게 복제(Copy & Paste)할 수 있습니다.

---

## **🏭 기능 소개**

### **🏗️ 팩토리 자동화 (Factory Automation)**

가장 기초적인 부품부터 복잡한 로직까지, 당신만의 라인을 구축하세요.

- **코어 모듈**: Mover(이동), Combiner(합성), Balancer(분배), Port(입출력) 등 다양한 기능성 타일 제공.
- **그리드 시스템**: 직관적인 2D 그리드 위에서 마우스 드래그로 손쉽게 기계를 배치하고 연결합니다.
- **실시간 로직 연산**: 모든 아이템의 이동과 가공이 실시간 물리/로직 연산으로 시뮬레이션됩니다.

### **📦 모듈화 & 재귀 (Modular & Recursive)**

복잡한 공장을 깔끔하게 정리하는 Modulo만의 핵심 기능입니다.

- **모듈 압축**: 복잡한 생산 라인을 하나의 `Recursive Module`로 만들어 인벤도로에 저장합니다.
- **블랙박스 설계**: 내부는 복잡하지만 외부는 심플한 입출력 포트만 남겨, 거대한 시스템을 블록 쌓기처럼 설계할 수 있습니다.

### **💾 블루프린트 & 건설 관리 (Blueprints)**

반복되는 작업은 줄이고, 효율은 극대화하세요.

- **블루프린트 시스템**: 클립보드 처럼 간편하게 모듈들을 저장하고 불러올 수 있습니다.

- **블루프린트 UI**: 반복하여 사용되는 모듈들은 저장해 이름을 적어둔 다음, 다음에 활용하세요!

### **🌐 협동 멀티플레이 (Co-op Multiplayer)**

혼자가 힘들다면 동료와 함께하세요.

- **로비 & 매치메이킹**: 방 코드를 통해 친구를 초대하거나 공개된 방에 참여할 수 있습니다.
- **Unity Relay 지원**: 복잡한 포트포워딩 없이, 전 세계 어디서든 안정적인 P2P 연결을 지원합니다.
- **상태 동기화**: 모든 공장의 상태와 아이템의 흐름이 호스트와 클라이언트 간에 정확하게 동기화됩니다.

---

## **🏭 데이터베이스 & 저장 시스템**

- **JSON Serialization**: 복잡한 계층 구조의 공장 데이터를 JSON 포맷으로 직렬화하여 관리합니다.
- **Base64 Image Encoding**: 블루프린트의 썸네일 이미지를 텍스트(Base64)로 변환하여 단일 세이브 파일에 통합 저장합니다.
- **Local Persistence**: 로컬 파일 시스템을 통해 안전하게 데이터를 보존하며, 언제든 이어서 플레이할 수 있습니다.

---

## **🏭 개발 스택**

### **Engine & Core**
- **Engine**: Unity 6 (or 2022 LTS+)
- **Language**: C# (.NET Standard 2.1)
- **Architecture**: Component-based Architecture, Lazy Initialization

### **Network**
- **Framework**: Unity Netcode for GameObjects (NGO)
- **Transport**: Unity Transport Protocol (UTP)
- **Services**: Unity Relay, Unity Lobby

### **System**
- **Serialization**: `UnityEngine.JsonUtility`, Custom JSON Logic
- **UI**: uGUI, TextMeshPro
- **Async**: `UniTask` or Native `async/await`

---

## **🏭 팀원 소개**

<aside>
⚙

**[이름 입력]**

- [소속/학과 입력]
- **Core System / Network**
- [GitHub Link]
</aside>

<aside>
🎨

**[이름 입력]**

- [소속/학과 입력]
- **Gameplay / UI / Design**
- [GitHub Link]
</aside>
