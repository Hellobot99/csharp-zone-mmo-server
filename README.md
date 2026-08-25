# C# 존(Zone) 기반 MMO 서버

C#/.NET 8로 만든 존(Zone) 방식 MMO 서버입니다. `SocketAsyncEventArgs`(Windows에서는
IOCP로 동작) 기반 TCP 실시간 게임 서버와, JWT 인증을 처리하는 ASP.NET Core HTTP API를
같은 프로세스에서 호스팅 서비스로 함께 띄웁니다. 로비에서 대기열에 들어가면 4인이
매칭돼 아레나 존에서 최후의 1인을 가리는 PvP를 하고, 끝나면 다시 로비로 돌아오는
실시간 전투 MMO입니다.

## 왜 이 프로젝트인가

포트폴리오의 다른 두 서버 프로젝트가 각각 C++ raw IOCP, boost.asio라면, 이 프로젝트는
**같은 비동기 소켓 모델(SAEA)을 C#/.NET 생태계에서** 다뤄본 것입니다. `TcpServer`가
`BufferManager`로 수신 버퍼를 미리 통짜로 할당해 슬라이스로 나눠 쓰고, `SAEAPool`로
`SocketAsyncEventArgs` 객체를 재사용하며, `SemaphoreSlim`으로 동시 접속 수를 제한하는
구조를 직접 짰습니다 — 언어만 다를 뿐 C++ IOCP 프로젝트와 같은 문제(버퍼/오브젝트
재사용, 백프레셔)를 C#에서 어떻게 푸는지 비교해볼 수 있습니다.

또한 세 프로젝트 모두 **"실시간은 TCP, 트랜잭션은 HTTP"** 로 계층을 분리하는 패턴을
일관되게 적용했습니다 (이동/전투/매칭은 TCP, 로그인/회원가입은 JWT 발급 HTTP API).

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
 ├─ TcpServer         : SAEA 기반 accept/receive 루프, BufferManager+SAEAPool로 재사용
 ├─ SessionManager     : 접속 중인 PlayerSession registry
 ├─ PacketHandlerManager : 패킷 타입 → 핸들러 라우팅
 │   (Move/Chat/Skill/ZoneTransfer/Matchmake/Ping...)
 ├─ ZoneManager/Zone   : 로비 1개 + 3x3 아레나(존 9개). 존 하나 = 플레이어 집합 +
 │                        AOI 브로드캐스트
 ├─ MatchmakingManager : 대기열 → 4인 매칭 → 빈 아레나 배정 → 30초 타임아웃/승패 판정
 └─ HeartbeatService   : 별도 호스티드 서비스로 접속 상태 점검

AuthController(HTTP, 8080)  : 회원가입/로그인 → JWT 발급 (MySQL/EF Core)
TcpAuthHandler(TCP, 7000)   : 접속 직후 JWT를 검증해 세션을 인증 상태로 전환
```

## 매치메이킹 + PvP 아레나
로비(존 10번)에서 매칭 요청을 보내면 대기열에 들어가고, 4명이 모이면 9개 아레나 존
중 비어있는 곳에 배정돼 동시에 여러 매치가 병렬로 돌아간다. 아레나 안에서는:
- 이동 중 공격 판정(사거리 내 상대 자동 공격, 쿨다운 있음), 데미지 적용, 체력 0 시
  사망 처리
- 스폰/리스폰 직후 3초 무적 시간
- 최후의 1인이 남으면 승리 처리하고, 3초 뒤 전원 로비로 복귀시킨 다음 바로 다음 매칭을 시도
- 매치가 30초 안에 안 끝나면 강제 종료(타임아웃)
- 매치 중 플레이어가 접속을 끊으면 남은 인원 기준으로 승패를 재계산

## AOI(관심 영역) 브로드캐스트
`Zone.Broadcast`는 존 전체에 패킷을 뿌리지만, 이동처럼 빈번한 패킷은
`Zone.BroadcastNearby`로 **좌표 기준 반경(150 유닛) 이내의 플레이어에게만** 보낸다.
존 단위로 한 번 걸러내고, 그 안에서 다시 거리로 걸러내는 2단계 구조라 존이 커지거나
인원이 몰려도 불필요한 패킷 전송을 줄인다.

이 150이라는 값 자체가 임의로 정한 게 아니라 **부하테스트로 실측하며 조정한 결과**다.
처음엔 range=300으로 시작했는데 로비에 150명 이상 몰리는 상황에서는 오히려 RTT가
악화됐다 — 자세한 과정은 [LOADTEST.md](LOADTEST.md) 참고.

## 프로토콜
- `proto/auth.proto`, `proto/game.proto` — Protocol Buffers로 정의.
- 와이어 포맷: `[size(2B)][type(2B)][protobuf body]` — C++ IOCP 프로젝트와 동일하게
  헤더+바디 구조를 직접 파싱한다 (자체 프레이밍, 프레임워크 의존 없음).
- 흐름: `POST /api/auth/register`로 가입하고 `POST /api/auth/login`으로 JWT를 발급받는다.
  그 JWT를 들고 TCP로 접속해서 `TcpAuthRequest`(원문 전송)로 세션을 인증한 뒤, 로비에서
  매칭을 요청하고 `MatchStarted`를 받으면 아레나로 이동해 이동/공격을 주고받다가
  `MatchEnded`로 끝난다.

## 빌드 및 실행
```bash
cd Server
docker compose up --build -d
```
게임 서버(TCP 7000) + HTTP API(8080) + MySQL + Redis가 한 번에 뜬다. `main` 브랜치에
`Server/`나 `proto/`가 바뀌어 푸시되면 GitHub Actions가 SSH로 Azure VM에 접속해
`docker compose up --build -d gameserver`로 자동 재배포한다 (MySQL/Redis 데이터는 유지).

## 부하테스트 도구
`Server/src/TestClient`에 봇 클라이언트가 있다. 로그인 후 로비에서 매칭 요청을
반복하며 실제 매치메이킹 → 아레나 이동 → 매치 종료 → 재매칭 흐름을 그대로 타고,
이동(Move)/존이동(ZoneTransfer)/채팅(Chat) 각각의 RTT(평균/최소/최대)와 플레이한
매치 수를 봇별·전체로 집계해서 리포트를 낸다.
```bash
cd Server/src/TestClient
dotnet run -- 100   # 봇 100개, 120초
```
50~300봇까지 Azure 배포 서버에 대고 실제로 돌렸다. 300봇에서 브로드캐스트 O(N²)
증폭으로 실제 한계(Move RTT 2초대, ZoneTransfer 성공률 84.8%)를 관측했고, 이후
Channel 전환 + AOI 튜닝으로 최적화한 전체 과정은 [LOADTEST.md](LOADTEST.md)에 정리했다.

## 프로젝트 구조
```
Server/src/GameServer/
 ├─ Network/     TcpServer, TcpSession, SAEAPool, BufferManager
 ├─ Packets/     PacketHandlerManager + 핸들러들(Move/Chat/Skill/ZoneTransfer/Matchmake/Ping/...)
 ├─ Game/        Zone, ZoneManager, MatchmakingManager, SessionManager, PlayerSession
 ├─ Api/         AuthController(HTTP), AuthService
 ├─ Database/    GameDbContext(EF Core), PlayerRepository
 └─ Cache/       RedisService
Server/src/TestClient/   부하테스트용 봇 클라이언트 (매칭까지 자동으로 태움)
proto/                    공용 패킷 정의
```

## 알려진 제약사항 / 향후 개선 방향
- 공격은 이동 시 사거리 내 상대를 자동으로 때리는 근접 판정만 있다 — 스킬별
  사거리/투사체/범위 공격 등은 아직 없음.
- 클라이언트(Unity) 쪽 리소스는 실제 아트 대신 도형(구체 등)으로 대체돼 있다 —
  기능 검증 위주로 만든 프로젝트라 완성도보다 구조에 집중했다.
- AOI range=150은 300봇까지의 실측 기준 튜닝값이다. 인원이 더 늘어나면 존 자체를
  더 잘게 쪼개거나 range를 동적으로 조절하는 식의 추가 튜닝이 필요할 것으로 보인다.
