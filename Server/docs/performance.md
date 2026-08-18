# 성능 측정 기록

## 테스트 환경
- **서버**: Azure VM (Korea Central) — docker compose gameserver
- **클라이언트**: TestClient (봇) — devcontainer에서 실행
- **서버-클라 거리**: 한국 내 (로컬 → Azure Korea Central)
- **측정 항목**: Move RTT (avg/p95/max), Chat RTT, 연결 수, 매치 수

---

## 베이스라인 (최적화 전)

### 50봇 — 2026-04-01
```
bots=50  duration=120s  auth_ok=50  auth_fail=0

Move RTT   avg=163ms  p95=675ms  min=33ms  max=4825ms
Chat RTT   avg=56ms   min=34ms   max=847ms
Matches    total=100  wins=0  deaths=69
Connected  50/50
```

**라이브 추이**
| 시간 | lobby | arena | move avg | move p95 | chat avg | matches |
|------|-------|-------|----------|----------|----------|---------|
|  10s |    44 |     6 |   297ms  |   824ms  |   309ms  |       0 |
|  20s |    42 |     8 |   201ms  |  3585ms  |   112ms  |       8 |
|  30s |    22 |    28 |   173ms  |   871ms  |    84ms  |      28 |
|  40s |    14 |    36 |   165ms  |   902ms  |    76ms  |      36 |
|  50s |    14 |    36 |   159ms  |   893ms  |    69ms  |      44 |
|  60s |    34 |    16 |   156ms  |   817ms  |    65ms  |      44 |
|  70s |    30 |    20 |   158ms  |   756ms  |    63ms  |      56 |
|  80s |    22 |    28 |   159ms  |   744ms  |    61ms  |      72 |
|  90s |    14 |    36 |   160ms  |   700ms  |    59ms  |      80 |
| 100s |    22 |    28 |   160ms  |   686ms  |    57ms  |      84 |
| 110s |    34 |    16 |   161ms  |   670ms  |    57ms  |      88 |

**비고**
- wins=0: 봇이 랜덤 이동으로 공격 범위 진입이 드물어 30초 타임아웃으로 매치 종료
- max 스파이크(4825ms)는 매치 전환 시 브로드캐스트 집중으로 추정

---

### 100봇 — 2026-04-01
```
bots=100  duration=120s  auth_ok=99  auth_fail=1

Move RTT   avg=336ms  p95=912ms  min=26ms  max=4526ms
Chat RTT   avg=204ms  min=28ms   max=13334ms
Matches    total=151  wins=0  deaths=214
Connected  99/100
```

**라이브 추이**
| 시간 | lobby | arena | move avg | move p95 | chat avg | matches |
|------|-------|-------|----------|----------|----------|---------|
|  10s |    52 |    29 |   175ms  |   537ms  |   123ms  |       0 |
|  20s |    82 |    18 |   352ms  |  3803ms  |   263ms  |      11 |
|  30s |    63 |    37 |   347ms  |  3697ms  |   277ms  |      35 |
|  40s |    63 |    37 |   338ms  |  3708ms  |   240ms  |      39 |
|  50s |    62 |    38 |   333ms  |  3587ms  |   222ms  |      51 |
|  60s |    62 |    38 |   331ms  |  1255ms  |   211ms  |      75 |
|  70s |    62 |    38 |   330ms  |  1090ms  |   204ms  |      79 |
|  80s |    62 |    38 |   330ms  |   932ms  |   203ms  |      91 |
|  90s |    62 |    38 |   331ms  |   901ms  |   204ms  |     115 |
| 100s |    62 |    38 |   332ms  |   905ms  |   202ms  |     119 |
| 110s |    62 |    38 |   334ms  |   917ms  |   204ms  |     131 |

**비고**
- 50봇 대비 Move avg 2배(163→336ms), Chat avg 3.6배(56→204ms) 상승
- Broken pipe 7개(Bot25, 87, 90, 93, 95, 98, 99) — 연결 불안정 봇 데이터 0
- Chat max 13334ms 이상치는 Bot38 — 브로드캐스트 큐 적체 추정
- 로비 인원 62명 고정: 아레나 최대 36명(9존×4명) 수용, 나머지 대기

---

### 150봇 — 2026-04-01
```
bots=150  duration=120s  auth_ok=146  auth_fail=4

Move RTT   avg=667ms  p95=4125ms  min=28ms  max=5693ms
Chat RTT   avg=209ms  min=30ms    max=4797ms
Matches    total=128  wins=0  deaths=470
Connected  146/150
```

**라이브 추이**
| 시간 | lobby | arena | move avg | move p95 | chat avg | matches |
|------|-------|-------|----------|----------|----------|---------|
|  10s |    18 |    13 |   731ms  |  1802ms  |   616ms  |       0 |
|  20s |    64 |    56 |   324ms  |  1364ms  |   258ms  |       4 |
|  30s |    87 |    48 |   419ms  |  3599ms  |   337ms  |      20 |
|  40s |   106 |    44 |   509ms  |  4168ms  |   334ms  |      36 |
|  50s |   109 |    41 |   540ms  |  3981ms  |   309ms  |      40 |
|  60s |   109 |    41 |   549ms  |  4153ms  |   268ms  |      56 |
|  70s |   109 |    41 |   560ms  |  4206ms  |   246ms  |      72 |
|  80s |   109 |    41 |   591ms  |  4203ms  |   241ms  |      76 |
|  90s |   109 |    41 |   607ms  |  4124ms  |   226ms  |      92 |
| 100s |   109 |    41 |   625ms  |  4030ms  |   216ms  |     108 |
| 110s |   109 |    41 |   639ms  |  4212ms  |   202ms  |     112 |

**비고**
- Broken pipe 11개, auth_fail 4개 — 동시 접속 부하로 일부 연결 불안정
- Move avg 급등: 50봇(163ms) → 100봇(336ms) → 150봇(667ms) — 봇 수 대비 선형 이상 증가
- 로비 109명 고정: 아레나 최대 36명(9존×4명) 수용, 나머지 73명이 로비에서 브로드캐스트 집중
- p95가 4000ms대 고착 — 브로드캐스트 병목 구간 명확

---

## 최적화 후

### Channel 전환 (System.Threading.Channels)
`ConcurrentQueue + _isSending 플래그` 방식 → `Channel<byte[]> + SendLoopAsync` 방식으로 교체.
Send 경합 제거, 전용 write 루프가 순서대로 처리.

#### 50봇 — 2026-04-01
```
bots=50  duration=120s  auth_ok=50  auth_fail=0

Move RTT   avg=177ms  p95=770ms  min=26ms  max=4210ms
Chat RTT   avg=43ms   min=27ms   max=637ms
Matches    total=96   wins=0  deaths=59
Connected  50/50
```

#### 100봇 — 2026-04-01
```
bots=100  duration=120s  auth_ok=99  auth_fail=1

Move RTT   avg=350ms  p95=1260ms  min=26ms  max=45768ms  (Bot36 이상치 포함)
Chat RTT   avg=190ms  min=27ms    max=13221ms
Matches    total=150  wins=0  deaths=234
Connected  99/100
```

#### 150봇 — 2026-04-01
```
bots=150  duration=120s  auth_ok=145  auth_fail=5

Move RTT   avg=298ms  p95=813ms  min=26ms  max=4368ms
Chat RTT   avg=80ms   min=29ms   max=579ms
Matches    total=148  wins=0  deaths=538
Connected  145/150
```

#### 베이스라인 대비 비교 (전체)
| 봇 수 | | Move avg | Move p95 | Chat avg |
|-------|--|----------|----------|----------|
| 50  | 베이스라인 | 163ms | 675ms | 56ms |
| 50  | Channel   | 177ms | 770ms | **43ms** |
| 100 | 베이스라인 | 336ms | 912ms | 204ms |
| 100 | Channel   | 350ms | 1260ms* | **190ms** |
| 150 | 베이스라인 | 667ms | 4125ms | 209ms |
| 150 | Channel   | **298ms** | **813ms** | **80ms** |

\* Bot36 이상치(45768ms) 포함으로 신뢰도 낮음

#### 베이스라인 대비 개선 (150봇 기준)
| 지표 | 베이스라인 | Channel 후 | 개선 |
|------|-----------|------------|------|
| Move avg | 667ms | 298ms | -55% |
| Move p95 | 4125ms | 813ms | -80% |
| Chat avg | 209ms | 80ms | -62% |
| Chat max | 4797ms | 579ms | -88% |

#### 200봇 — 2026-04-01
```
bots=200  duration=120s  auth_ok=191  auth_fail=9

Move RTT   avg=275ms  p95=831ms  min=26ms  max=5033ms
Chat RTT   avg=293ms  min=27ms   max=35830ms  (Bot139 이상치 포함)
Matches    total=178  wins=0  deaths=671
Connected  191/200
```

**라이브 추이**
| 시간 | lobby | arena | move avg | move p95 | chat avg | matches |
|------|-------|-------|----------|----------|----------|---------|
|  10s |    53 |    22 |    44ms  |   148ms  |    39ms  |       0 |
|  20s |    80 |    56 |    83ms  |   161ms  |    42ms  |       6 |
|  30s |    85 |    67 |   112ms  |   254ms  |    49ms  |      34 |
|  40s |    94 |    90 |   132ms  |   299ms  |    59ms  |      34 |
|  50s |   107 |    93 |   143ms  |   315ms  |    62ms  |      46 |
|  60s |   128 |    72 |   155ms  |   416ms  |    74ms  |      74 |
|  70s |   143 |    57 |   176ms  |   482ms  |    87ms  |      74 |
|  80s |   145 |    55 |   202ms  |   583ms  |    89ms  |      94 |
|  90s |   149 |    51 |   220ms  |   602ms  |   290ms  |     122 |
| 100s |   152 |    48 |   236ms  |   616ms  |   289ms  |     122 |
| 110s |   148 |    52 |   258ms  |   814ms  |   293ms  |     150 |

**비고**
- auth_fail 9개, Broken pipe 14개 — 150봇 대비 불안정 증가
- Move avg는 150봇(298ms)보다 오히려 낮음(275ms) — 이상치 봇들이 통계에서 빠진 영향
- Chat max 35830ms(Bot139) 이상치 — 브로드캐스트 큐 심각한 적체
- 로비 150명 이상 적체 시 chat RTT 급등(90s~: 290ms+) 패턴 확인

#### 300봇 — 2026-04-01
```
bots=300  duration=120s  auth_ok=300  auth_fail=0

Move RTT   avg=322ms  p95=1175ms  min=26ms  max=11932ms
Chat RTT   avg=517ms  min=29ms    max=31916ms  (이상치 다수)
Matches    total=281  wins=0  deaths=719
Connected  300/300
```

**라이브 추이**
| 시간 | lobby | arena | move avg | move p95 | chat avg | matches |
|------|-------|-------|----------|----------|----------|---------|
|  10s |    54 |    23 |    49ms  |   158ms  |    40ms  |       0 |
|  20s |    88 |    47 |   106ms  |   181ms  |    50ms  |      14 |
|  30s |    98 |   102 |   167ms  |   314ms  |    85ms  |      38 |
|  40s |   102 |   120 |   166ms  |   389ms  |   187ms  |      46 |
|  50s |   103 |   146 |   183ms  |   404ms  |   399ms  |      82 |
|  60s |   108 |   173 |   197ms  |   416ms  |   581ms  |     106 |
|  70s |   128 |   162 |   200ms  |   510ms  |   571ms  |     114 |
|  80s |   135 |   162 |   220ms  |   582ms  |   554ms  |     170 |
|  90s |   150 |   150 |   245ms  |   695ms  |   546ms  |     194 |
| 100s |   157 |   143 |   266ms  |   864ms  |   523ms  |     206 |
| 110s |   176 |   124 |   288ms  |   929ms  |   512ms  |     258 |

**비고**
- auth_fail 0 — 세마포어 덕분에 300봇도 전원 인증 성공
- Bot250~300 대부분 0/0/0ms — 접속 완료까지 120s 부족, 아직 move 시작 못함
- Chat RTT 급등: 접속자 증가하면서 400~580ms대 고착
- Move p95는 200봇(831ms)과 비슷한 수준(1175ms) — Move 자체는 선형 이하로 증가
- Chat이 병목 — 브로드캐스트 범위가 너무 넓어서 큐 적체 발생

---

## AOI 적용 후 (Move BroadcastNearby, range=300)

Move 브로드캐스트를 전체 Zone 전송 → 300 유닛 이내 플레이어만 전송으로 교체.
PlayerEnter/Leave, Damage, Death, Chat 등 이벤트성 패킷은 기존 전체 브로드캐스트 유지.
Observer는 AOI 무관하게 항상 수신.

### 50봇 — 2026-04-01
```
bots=50  duration=120s  auth_ok=50  auth_fail=0

Move RTT   avg=136ms  p95=284ms  min=26ms  max=5186ms  (Bot39 이상치)
Chat RTT   avg=41ms   min=28ms   max=100ms
Matches    total=91   wins=0  deaths=90
Connected  50/50
```

**라이브 추이**
| 시간 | lobby | arena | move avg | move p95 | chat avg | matches |
|------|-------|-------|----------|----------|----------|---------|
|  10s |    50 |     0 |    35ms  |    64ms  |    36ms  |       0 |
|  20s |    43 |     7 |    91ms  |   149ms  |    37ms  |       7 |
|  30s |    19 |    31 |   101ms  |   245ms  |    38ms  |      31 |
|  40s |    15 |    35 |   107ms  |   252ms  |    39ms  |      35 |
|  50s |    14 |    36 |   109ms  |   252ms  |    38ms  |      43 |
|  60s |    40 |    10 |   113ms  |   267ms  |    39ms  |      43 |
|  70s |    36 |    14 |   121ms  |   270ms  |    41ms  |      51 |
|  80s |    28 |    22 |   124ms  |   279ms  |    41ms  |      67 |
|  90s |    20 |    30 |   127ms  |   281ms  |    41ms  |      75 |
| 100s |    28 |    22 |   130ms  |   280ms  |    40ms  |      75 |
| 110s |    36 |    14 |   133ms  |   283ms  |    41ms  |      83 |

#### Channel 대비 비교 (50봇)
| 지표 | Channel | AOI | 개선 |
|------|---------|-----|------|
| Move avg | 177ms | 136ms | -23% |
| Move p95 | 770ms | 284ms | -63% |
| Chat avg | 43ms | 41ms | -5% |
| Chat max | 637ms | 100ms | -84% |

---

### 100봇 — 2026-04-01
```
bots=100  duration=120s  auth_ok=100  auth_fail=0

Move RTT   avg=264ms  p95=891ms  min=26ms  max=4538ms
Chat RTT   avg=588ms  min=28ms   max=44729ms  (Bot25: 40244ms, Bot45: 10528ms 이상치)
Matches    total=150  wins=0  deaths=276
Connected  100/100
```

**라이브 추이**
| 시간 | lobby | arena | move avg | move p95 | chat avg | matches |
|------|-------|-------|----------|----------|----------|---------|
|  10s |    57 |    29 |   200ms  |   597ms  |   210ms  |       0 |
|  20s |    92 |     8 |   276ms  |  3817ms  |   200ms  |       6 |
|  30s |    66 |    34 |   255ms  |  1098ms  |   164ms  |      34 |
|  40s |    66 |    34 |   249ms  |  1138ms  |   172ms  |      34 |
|  50s |    57 |    43 |   249ms  |  1044ms  |   545ms  |      70 |
|  60s |    73 |    27 |   246ms  |   929ms  |   545ms  |      82 |
|  70s |    61 |    39 |   248ms  |   937ms  |   561ms  |      94 |
|  80s |    79 |    21 |   249ms  |   855ms  |   570ms  |     114 |
|  90s |    79 |    21 |   252ms  |   921ms  |   579ms  |     118 |
| 100s |    67 |    33 |   255ms  |   863ms  |   590ms  |     130 |
| 110s |    77 |    23 |   259ms  |   835ms  |   591ms  |     142 |

#### Channel 대비 비교 (100봇)
| 지표 | Channel | AOI | 개선 |
|------|---------|-----|------|
| Move avg | 350ms | 264ms | -25% |
| Move p95 | 1260ms | 891ms | -29% |
| Chat avg | 190ms | 588ms* | **악화** |
| Chat max | 13221ms | 44729ms* | **악화** |

\* Bot25(40244ms), Bot45(10528ms) 이상치로 Chat avg 왜곡. 이상치 제외 시 대부분 봇의 Chat avg는 50~110ms 수준.
Move는 AOI 효과로 개선됐으나 Chat은 여전히 전체 브로드캐스트 — 로비 70~80명 구간에서 Chat 큐 적체가 심각.

---

### 150봇 — 2026-04-01
```
bots=150  duration=120s  auth_ok=149  auth_fail=1

Move RTT   avg=190ms  p95=694ms  min=26ms  max=4027ms
Chat RTT   avg=62ms   min=28ms   max=640ms
Matches    total=187  wins=0  deaths=517
Connected  149/150
```

**라이브 추이**
| 시간 | lobby | arena | move avg | move p95 | chat avg | matches |
|------|-------|-------|----------|----------|----------|---------|
|  10s |    57 |    24 |   168ms  |   544ms  |   220ms  |       0 |
|  20s |    83 |    67 |   108ms  |   591ms  |    69ms  |      11 |
|  30s |    94 |    56 |   100ms  |   374ms  |    62ms  |      39 |
|  40s |   110 |    40 |   113ms  |   363ms  |    64ms  |      39 |
|  50s |   111 |    39 |   125ms  |   389ms  |    62ms  |      51 |
|  60s |   107 |    43 |   132ms  |   434ms  |    60ms  |     111 |
|  70s |   104 |    46 |   141ms  |   470ms  |    59ms  |     115 |
|  80s |   105 |    45 |   150ms  |   562ms  |    59ms  |     127 |
|  90s |   126 |    24 |   159ms  |   570ms  |    60ms  |     151 |
| 100s |   114 |    36 |   171ms  |   666ms  |    61ms  |     163 |
| 110s |   102 |    48 |   181ms  |   685ms  |    62ms  |     187 |

#### Channel 대비 비교 (150봇)
| 지표 | Channel | AOI | 개선 |
|------|---------|-----|------|
| Move avg | 298ms | 190ms | -36% |
| Move p95 | 813ms | 694ms | -15% |
| Chat avg | 80ms | 62ms | -23% |
| Chat max | 579ms | 640ms | +10% |

#### 베이스라인 대비 비교 (150봇)
| 지표 | 베이스라인 | Channel | AOI | 누적 개선 |
|------|-----------|---------|-----|----------|
| Move avg | 667ms | 298ms | 190ms | -72% |
| Move p95 | 4125ms | 813ms | 694ms | -83% |
| Chat avg | 209ms | 80ms | 62ms | -70% |
| Chat max | 4797ms | 579ms | 640ms | -87% |

---

### 200봇 — 2026-04-01
```
bots=200  duration=120s  auth_ok=194  auth_fail=6

Move RTT   avg=704ms  p95=4517ms  min=27ms  max=6660ms
Chat RTT   avg=203ms  min=29ms    max=13752ms  (Bot87: 9354ms 이상치)
Matches    total=241  wins=0  deaths=826
Connected  194/200
```

**라이브 추이**
| 시간 | lobby | arena | move avg | move p95 | chat avg | matches |
|------|-------|-------|----------|----------|----------|---------|
|  10s |    58 |    28 |   202ms  |  3576ms  |   143ms  |       0 |
|  20s |    97 |    69 |   318ms  |  3709ms  |   211ms  |       9 |
|  30s |   116 |    74 |   404ms  |  3925ms  |   256ms  |      33 |
|  40s |   147 |    53 |   516ms  |  4087ms  |   293ms  |      33 |
|  50s |   148 |    52 |   566ms  |  4509ms  |   262ms  |      81 |
|  60s |   149 |    51 |   584ms  |  4158ms  |   242ms  |     105 |
|  70s |   149 |    51 |   599ms  |  4237ms  |   232ms  |     105 |
|  80s |   153 |    47 |   618ms  |  4230ms  |   224ms  |     181 |
|  90s |   149 |    51 |   646ms  |  4385ms  |   222ms  |     197 |
| 100s |   138 |    62 |   665ms  |  4576ms  |   212ms  |     209 |
| 110s |   165 |    35 |   680ms  |  4518ms  |   206ms  |     233 |

#### Channel 대비 비교 (200봇)
| 지표 | Channel | AOI | 변화 |
|------|---------|-----|------|
| Move avg | 275ms | 704ms | **+156% 악화** |
| Move p95 | 831ms | 4517ms | **+443% 악화** |
| Chat avg | 293ms | 203ms | -31% 개선 |
| Chat max | 35830ms | 13752ms | -62% 개선 |

**비고**
- Move가 Channel 대비 크게 악화 — Channel 200봇 때는 Broken pipe 14개로 실제 부하가 낮았던 반면, AOI 200봇은 Broken pipe가 적어 실제 참가 봇이 더 많음
- 로비에 149~165명 고착: AOI range=300이지만 밀도 높은 로비에서 여전히 많은 플레이어가 수신 범위 내에 있어 Move 부하 지속
- Move avg가 시간이 지날수록 꾸준히 증가(202ms→680ms) — 로비 적체가 심화될수록 악화되는 패턴

---

### 300봇 — 2026-04-01
```
bots=300  duration=120s  auth_ok=300  auth_fail=0

Move RTT   avg=641ms  p95=4566ms  min=26ms  max=6264ms
Chat RTT   avg=368ms  min=29ms    max=28997ms  (Bot179: 27435ms 이상치)
Matches    total=294  wins=0  deaths=1013
Connected  300/300
```

**라이브 추이**
| 시간 | lobby | arena | move avg | move p95 | chat avg | matches |
|------|-------|-------|----------|----------|----------|---------|
|  10s |    60 |    15 |   242ms  |   611ms  |   329ms  |       0 |
|  20s |    90 |    61 |   221ms  |  3613ms  |   139ms  |      10 |
|  30s |   111 |    82 |   197ms  |   826ms  |   122ms  |      48 |
|  40s |   118 |   134 |   207ms  |   795ms  |   112ms  |      70 |
|  50s |   138 |   162 |   220ms  |   779ms  |   115ms  |      82 |
|  60s |   175 |   125 |   252ms  |   855ms  |   127ms  |     134 |
|  70s |   168 |   132 |   285ms  |   918ms  |   140ms  |     158 |
|  80s |   173 |   127 |   321ms  |   983ms  |   264ms  |     178 |
|  90s |   209 |    91 |   391ms  |  3330ms  |   290ms  |     210 |
| 100s |   203 |    97 |   470ms  |  3736ms  |   318ms  |     238 |
| 110s |   205 |    95 |   556ms  |  4326ms  |   333ms  |     262 |

#### Channel 대비 비교 (300봇)
| 지표 | Channel | AOI | 변화 |
|------|---------|-----|------|
| Move avg | 322ms | 641ms | **+99% 악화** |
| Move p95 | 1175ms | 4566ms | **+288% 악화** |
| Chat avg | 517ms | 368ms | -29% 개선 |
| Chat max | 31916ms | 28997ms | -9% 개선 |

---

## AOI 전체 비교 요약

| 봇 수 | 지표 | 베이스라인 | Channel | AOI | AOI 효과 |
|-------|------|-----------|---------|-----|---------|
| 50  | Move avg | 163ms | 177ms | **136ms** | ✓ |
| 50  | Move p95 | 675ms | 770ms | **284ms** | ✓ |
| 100 | Move avg | 336ms | 350ms | **264ms** | ✓ |
| 100 | Move p95 | 912ms | 1260ms | **891ms** | ✓ |
| 150 | Move avg | 667ms | 298ms | **190ms** | ✓ |
| 150 | Move p95 | 4125ms | 813ms | **694ms** | ✓ |
| 200 | Move avg | — | **275ms** | 704ms | ✗ 악화 (r=300) |
| 200 | Move p95 | — | **831ms** | 4517ms | ✗ 악화 (r=300) |
| 200 (r=150) | Move avg | — | 275ms | **351ms** | △ (Channel 특수케이스†) |
| 200 (r=150) | Move p95 | — | 831ms | **1063ms** | △ (Channel 특수케이스†) |
| 300 | Move avg | — | **322ms** | 641ms | ✗ 악화 (r=300) |
| 300 | Move p95 | — | **1175ms** | 4566ms | ✗ 악화 (r=300) |
| 300 (r=150) | Move avg | — | 322ms | **575ms** | △ 소폭 개선 |
| 300 (r=150) | Chat avg | — | 517ms | **150ms** | ✓ -71% |

† Channel 200봇은 Broken pipe 14개로 실제 부하가 낮았던 특수 케이스

**패턴 분석**
- 150봇 이하: AOI(range=300)이 Move 개선 효과
- 200봇 이상: Move 악화 — 로비(840×840)에 150명+ 적체 시 range=300 내에 수십 명이 포함되어 AOI 효과 감소, 오히려 거리 계산 오버헤드 + 로비 밀집으로 실질적 필터링 없음
- Chat은 200봇+에서도 일관되게 개선 (Chat은 전체 브로드캐스트라 AOI 직접 효과 없지만 Move 부하 감소로 간접 개선)
- 200봇 Channel 테스트(275ms)는 Broken pipe 14개로 실제 부하 봇 수가 적었던 점 감안 필요

---

## AOI range=150 (축소 후)

range=300 → 150으로 축소. 로비 840×840 기준 수신 면적 비율: ~40% → ~10%.
이 시점부터 TCP auth 재시도 시 TCP 연결 재생성 적용 → auth_fail 0 해결.

### 50봇 — 2026-04-01
```
bots=50  duration=120s  auth_ok=50  auth_fail=0

Move RTT   avg=210ms  p95=674ms  min=29ms  max=6092ms  (Bot39: 3145ms avg 이상치)
Chat RTT   avg=45ms   min=30ms   max=441ms
Matches    total=91   wins=0  deaths=80
Connected  50/50
```

#### range=300 대비 비교 (50봇)
| 지표 | AOI range=300 | AOI range=150 | 변화 |
|------|--------------|--------------|------|
| Move avg | 136ms | 210ms | **+54% 악화** |
| Move p95 | 284ms | 674ms | **+137% 악화** |
| Chat avg | 41ms | 45ms | +10% |

**비고**: 소규모(50봇)에서는 로비가 충분히 비어 있어 range=150도 여전히 수신자가 많음. range 축소의 이점이 없고, 오히려 플레이어가 멀리 있을 때 Move 응답이 늦게 오는 케이스 증가.

---

### 100봇 — 2026-04-01
```
bots=100  duration=120s  auth_ok=100  auth_fail=0

Move RTT   avg=216ms  p95=788ms  min=26ms  max=4761ms
Chat RTT   avg=405ms  min=28ms   max=29290ms  (Bot4: 23303ms, Bot30: 11612ms 이상치)
Matches    total=159  wins=0  deaths=318
Connected  100/100
```

**라이브 추이**
| 시간 | lobby | arena | move avg | move p95 | chat avg | matches |
|------|-------|-------|----------|----------|----------|---------|
|  10s |    55 |    22 |   245ms  |  3503ms  |   139ms  |       0 |
|  20s |    92 |     8 |   193ms  |  3576ms  |    97ms  |       7 |
|  30s |    65 |    35 |   184ms  |  1000ms  |   273ms  |      35 |
|  40s |    63 |    37 |   179ms  |   888ms  |   362ms  |      43 |
|  50s |    62 |    38 |   181ms  |   801ms  |   374ms  |      51 |
|  60s |    62 |    38 |   185ms  |   787ms  |   374ms  |      79 |
|  70s |    75 |    25 |   189ms  |   787ms  |   378ms  |      95 |
|  80s |    63 |    37 |   194ms  |   793ms  |   383ms  |     115 |
|  90s |    79 |    21 |   198ms  |   789ms  |   389ms  |     123 |
| 100s |    85 |    15 |   203ms  |   791ms  |   393ms  |     139 |
| 110s |    81 |    19 |   210ms  |   790ms  |   401ms  |     151 |

#### range=300 대비 비교 (100봇)
| 지표 | AOI range=300 | AOI range=150 | 변화 |
|------|--------------|--------------|------|
| Move avg | 264ms | **216ms** | -18% |
| Move p95 | 891ms | **788ms** | -12% |
| Chat avg | 588ms* | 405ms* | -31% |
| Chat max | 44729ms | 29290ms | -35% |

\* 두 결과 모두 이상치 포함. 이상치 제외 시 대부분 봇의 Chat avg는 40~80ms 수준.

---

### 150봇 — 2026-04-01
```
bots=150  duration=120s  auth_ok=150  auth_fail=0

Move RTT   avg=195ms  p95=589ms  min=26ms  max=4091ms
Chat RTT   avg=677ms  min=28ms   max=33625ms  (Bot18: 30160ms, Bot8: 15641ms, Bot22: 11848ms 등 이상치 6개)
Matches    total=233  wins=0  deaths=652
Connected  150/150
```

**라이브 추이**
| 시간 | lobby | arena | move avg | move p95 | chat avg | matches |
|------|-------|-------|----------|----------|----------|---------|
|  10s |    55 |    13 |    42ms  |   147ms  |    36ms  |       0 |
|  20s |    83 |    37 |    56ms  |   160ms  |   353ms  |       9 |
|  30s |    92 |    58 |    71ms  |   187ms  |   353ms  |      41 |
|  40s |   114 |    36 |    96ms  |   315ms  |   370ms  |      41 |
|  50s |   109 |    41 |   109ms  |   297ms  |   496ms  |      73 |
|  60s |   109 |    41 |   122ms  |   363ms  |   571ms  |     105 |
|  70s |   109 |    41 |   134ms  |   381ms  |   597ms  |     105 |
|  80s |   109 |    41 |   146ms  |   461ms  |   624ms  |     161 |
|  90s |   125 |    25 |   159ms  |   486ms  |   640ms  |     173 |
| 100s |   105 |    45 |   171ms  |   499ms  |   659ms  |     193 |
| 110s |   132 |    18 |   182ms  |   579ms  |   666ms  |     213 |

#### range=300 대비 비교 (150봇)
| 지표 | AOI range=300 | AOI range=150 | 변화 |
|------|--------------|--------------|------|
| Move avg | 190ms | 195ms | +3% |
| Move p95 | 694ms | **589ms** | -15% |
| Chat avg | 62ms | 677ms* | 이상치 왜곡 |
| Chat max | 640ms | 33625ms* | 이상치 왜곡 |

\* Bot3/8/13/18/20/22 이상치(5000~33000ms) 6개가 평균 왜곡. 이상치 제외 시 대부분 봇의 Chat avg는 50~90ms 수준.

---

### 200봇 — 2026-04-01
```
bots=200  duration=120s  auth_ok=200  auth_fail=0

Move RTT   avg=351ms  p95=1063ms  min=26ms  max=15040ms  (Bot15: max=15040ms 이상치)
Chat RTT   avg=257ms  min=29ms    max=35838ms  (Bot67: 35706ms 이상치)
Matches    total=238  wins=0  deaths=975
Connected  200/200
```

**라이브 추이**
| 시간 | lobby | arena | move avg | move p95 | chat avg | matches |
|------|-------|-------|----------|----------|----------|---------|
|  10s |    60 |    18 |   284ms  |   674ms  |   297ms  |       0 |
|  20s |    93 |    37 |   309ms  |  3729ms  |   207ms  |      18 |
|  30s |   114 |    86 |   250ms  |  1279ms  |   138ms  |      34 |
|  40s |   153 |    47 |   237ms  |  1139ms  |   110ms  |      34 |
|  50s |   160 |    40 |   243ms  |  1146ms  |    95ms  |      70 |
|  60s |   160 |    40 |   263ms  |  1056ms  |   270ms  |      86 |
|  70s |   160 |    40 |   279ms  |  1065ms  |   268ms  |      86 |
|  80s |   152 |    48 |   294ms  |  1080ms  |   263ms  |     154 |
|  90s |   152 |    48 |   307ms  |  1068ms  |   261ms  |     170 |
| 100s |   152 |    48 |   323ms  |  1063ms  |   259ms  |     170 |
| 110s |   159 |    41 |   336ms  |  1030ms  |   258ms  |     230 |

#### range=300 대비 비교 (200봇)
| 지표 | AOI range=300 | AOI range=150 | 변화 |
|------|--------------|--------------|------|
| Move avg | 704ms | **351ms** | **-50%** |
| Move p95 | 4517ms | **1063ms** | **-76%** |
| Chat avg | 203ms | 257ms | +27% |
| Chat max | 13752ms | 35838ms* | 이상치 |

\* Bot67(35706ms) 이상치. 이상치 제외 시 대부분 봇의 Chat avg는 60~130ms 수준.

---

### 300봇 — 2026-04-01
```
bots=300  duration=120s  auth_ok=300  auth_fail=0

Move RTT   avg=575ms  p95=4239ms  min=26ms  max=6255ms
Chat RTT   avg=150ms  min=29ms    max=1229ms
Matches    total=136  wins=0  deaths=1149
Connected  300/300
```

**라이브 추이**
| 시간 | lobby | arena | move avg | move p95 | chat avg | matches |
|------|-------|-------|----------|----------|----------|---------|
|  10s |    33 |    24 |   225ms  |   949ms  |    51ms  |       0 |
|  20s |    74 |    48 |   267ms  |  3412ms  |   190ms  |       4 |
|  30s |   101 |   124 |   317ms  |  3931ms  |   247ms  |      28 |
|  40s |   135 |   139 |   408ms  |  3893ms  |   294ms  |      36 |
|  50s |   158 |   141 |   420ms  |  3921ms  |   229ms  |      40 |
|  60s |   179 |   121 |   416ms  |  4021ms  |   185ms  |      64 |
|  70s |   198 |   102 |   414ms  |  3974ms  |   162ms  |      72 |
|  80s |   211 |    89 |   438ms  |  4014ms  |   159ms  |      76 |
|  90s |   220 |    80 |   472ms  |  4009ms  |   158ms  |     100 |
| 100s |   228 |    72 |   507ms  |  3987ms  |   162ms  |     108 |
| 110s |   233 |    67 |   540ms  |  4244ms  |   153ms  |     112 |

#### range=300 대비 비교 (300봇)
| 지표 | AOI range=300 | AOI range=150 | 변화 |
|------|--------------|--------------|------|
| Move avg | 641ms | 575ms | -10% |
| Move p95 | 4566ms | 4239ms | -7% |
| Chat avg | 368ms | **150ms** | **-59%** |
| Chat max | 28997ms | **1229ms** | **-96%** |

**비고**
- Chat이 대폭 개선: Move 패킷 수신 대상이 줄면서 send queue 적체 해소 → Chat 전송 지연 크게 감소
- Move p95는 여전히 4000ms대 — 로비에 200명+ 적체 시 range=150 내에도 수십 명 포함
- Broken pipe 14개 증가 (range=300: 4개) — 높은 부하에서 일부 연결 불안정

---

## 최적화 완료 요약

| 최적화 | 내용 | 효과 |
|--------|------|------|
| Channel 전환 | ConcurrentQueue+플래그 → Channel+SendLoop | Move avg -55%, p95 -80% (150봇 기준) |
| Move AOI range=150 | 전체 존 브로드캐스트 → 150 유닛 이내만 전송 | 200봇 Move -50%, p95 -76% |
| Auth TCP 재연결 | 재시도 시 TCP 연결 재생성 | auth_fail=0 |
| Chat 전체 브로드캐스트 | 유지 (글로벌 채팅 특성상 의도적 결정) | — |
