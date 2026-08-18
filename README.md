# C# 존(Zone) 기반 MMO 서버

C#/.NET 8로 만든 존(Zone) 방식 MMO 서버입니다. `SocketAsyncEventArgs`(Windows에서는
IOCP로 동작) 기반 TCP 실시간 게임 서버와, JWT 인증을 처리하는 ASP.NET Core HTTP API를
같은 프로세스에서 호스팅 서비스로 함께 띄웁니다.

## 왜 이 프로젝트인가

포트폴리오의 다른 두 서버 프로젝트가 각각 C++ raw IOCP, boost.asio라면, 이 프로젝트는
**같은 비동기 소켓 모델(SAEA)을 C#/.NET 생태계에서** 다뤄본 것입니다. `TcpServer`가
`BufferManager`로 수신 버퍼를 미리 통짜로 할당해 슬라이스로 나눠 쓰고, `SAEAPool`로
`SocketAsyncEventArgs` 객체를 재사용하며, `SemaphoreSlim`으로 동시 접속 수를 제한하는
구조를 직접 짰습니다 — 언어만 다를 뿐 C++ IOCP 프로젝트와 같은 문제(버퍼/오브젝트
재사용, 백프레셔)를 C#에서 어떻게 푸는지 비교해볼 수 있습니다.

또한 세 프로젝트 모두 **"실시간은 TCP, 트랜잭션은 HTTP"** 로 계층을 분리하는 패턴을
일관되게 적용했습니다 (이동/채팅/존이동은 TCP, 로그인/회원가입은 JWT 발급 HTTP API).

## 기술 스택
| 구분 | 내용 |
|---|---|
| 서버 | C# / .NET 8, `SocketAsyncEventArgs`(SAEA) 기반 TCP, ASP.NET Core(HTTP API) |
| 인증 | JWT (HTTP 발급 → TCP 세션에서 검증) |
| 영속성 | MySQL(EF Core), Redis |
| 배포 | Docker Compose, GitHub Actions(푸시 시 Azure VM 자동 배포) |
| 클라이언트 | Unity (팀 프로젝트 아님 — 리소스는 실제 아트 대신 도형으로 임시 대체) |

## 아키텍처
```
GameServerHostedService (ASP.NET Core Hosted Service로 TcpServer 구동)
 ├─ TcpServer        : SAEA 기반 accept/receive 루프, BufferManager+SAEAPool로 재사용
 ├─ SessionManager    : 접속 중인 PlayerSession registry
 ├─ PacketHandlerManager : 패킷 타입 → 핸들러 라우팅 (Move/Chat/Skill/ZoneTransfer/Ping...)
 ├─ ZoneManager/Zone  : 3x3 그리드로 나뉜 존. 존 하나 = 플레이어 집합 + 브로드캐스트
 └─ HeartbeatService  : 별도 호스티드 서비스로 접속 상태 점검

AuthController(HTTP, 8080)  : 회원가입/로그인 → JWT 발급 (MySQL/EF Core)
TcpAuthHandler(TCP, 7000)   : 접속 직후 JWT를 검증해 세션을 인증 상태로 전환
```

## 프로토콜
- `proto/auth.proto`, `proto/game.proto` — Protocol Buffers로 정의.
- 와이어 포맷: `[size(2B)][type(2B)][protobuf body]` — C++ IOCP 프로젝트와 동일하게
  헤더+바디 구조를 직접 파싱한다 (자체 프레이밍, 프레임워크 의존 없음).
- 흐름: `POST /api/auth/register` → `POST /api/auth/login`(JWT 발급) → TCP 접속 →
  `TcpAuthRequest`(JWT 원문 전송)로 세션 인증 → 이동/채팅/존이동/스킬 패킷 송수신.

## 존(Zone) 시스템
맵을 3×3 그리드(존 9개)로 나누고, 플레이어가 존 경계를 넘으면 `ZoneTransferRequest`로
인접 존으로 옮겨간다. 브로드캐스트는 같은 존에 있는 플레이어(+ 관전자)에게만 나간다 —
전체 브로드캐스트 대신 관심 영역(AOI)을 존 단위로 거칠게 나눈 구조.

## 빌드 및 실행
```bash
cd Server
docker compose up --build -d
```
게임 서버(TCP 7000) + HTTP API(8080) + MySQL + Redis가 한 번에 뜬다. `main` 브랜치에
`Server/`나 `proto/`가 바뀌어 푸시되면 GitHub Actions가 SSH로 Azure VM에 접속해
`docker compose up --build -d gameserver`로 자동 재배포한다 (MySQL/Redis 데이터는 유지).

## 부하테스트 도구
`Server/src/TestClient`에 봇 클라이언트가 있다. 봇마다 랜덤 방향으로 계속 움직이면서
존 경계를 넘으면 자동으로 `ZoneTransferRequest`를 보내고, 이동(Move)/존이동(ZoneTransfer)/
채팅(Chat) 각각의 RTT(평균/최소/최대)를 봇별·전체로 집계해서 리포트를 낸다.
```bash
cd Server/src/TestClient
dotnet run -- 100   # 봇 100개, 120초
```
※ 실제 부하테스트 결과(수치)는 아직 이 문서에 정리하지 않았다 — 배포된 서버에 대고
재현 가능한 형태로 돌린 뒤 추가할 예정.

## 프로젝트 구조
```
Server/src/GameServer/
 ├─ Network/     TcpServer, TcpSession, SAEAPool, BufferManager
 ├─ Packets/     PacketHandlerManager + 핸들러들(Move/Chat/Skill/ZoneTransfer/Ping/...)
 ├─ Game/        Zone, ZoneManager, SessionManager, PlayerSession
 ├─ Api/         AuthController(HTTP), AuthService
 ├─ Database/    GameDbContext(EF Core), PlayerRepository
 └─ Cache/       RedisService
Server/src/TestClient/   부하테스트용 봇 클라이언트
proto/                    공용 패킷 정의
```

## 알려진 제약사항 / 향후 개선 방향
- 전투(`SkillPacketHandler`)는 아직 자기 자신의 공격력을 올리고 브로드캐스트하는
  수준의 최소 구현이다 — 대상 판정/데미지 적용 등은 미구현.
- 클라이언트(Unity) 쪽 리소스는 실제 아트 대신 도형(구체 등)으로 대체돼 있다 —
  기능 검증 위주로 만든 프로젝트라 완성도보다 구조에 집중했다.
- 부하테스트는 도구만 만들어두고 아직 정식으로 돌려서 결과를 기록하지 않았다.
