using TestClient;

const string Host = "4.230.20.98";
const int Port = 7000;
const int Duration = 120; // 초
int BotCount = args.Length > 0 && int.TryParse(args[0], out var n) ? n : 100;

Console.WriteLine($"[TestClient] {BotCount} bots → {Host}:{Port}  (duration: {Duration}s)");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
cts.CancelAfter(TimeSpan.FromSeconds(Duration));

var sw = System.Diagnostics.Stopwatch.StartNew();

var bots = Enumerable.Range(1, BotCount)
    .Select(i => new BotClient(i, Host, Port))
    .ToList();

var botTasks = bots.Select(async (bot, i) =>
{
    await Task.Delay(i * 100, cts.Token).ContinueWith(_ => { });
    await bot.RunAsync(cts.Token);
});

// 라이브 진행 상황 (10초마다)
var liveTask = Task.Run(async () =>
{
    while (!cts.Token.IsCancellationRequested)
    {
        try { await Task.Delay(10_000, cts.Token); } catch { break; }
        int elapsed   = (int)sw.Elapsed.TotalSeconds;
        int connected = bots.Count(b => b.Connected);
        int inMatch   = bots.Count(b => b.Connected && b.ZoneId != 10);
        int inLobby   = bots.Count(b => b.Connected && b.ZoneId == 10);
        int failed    = bots.Count(b => b.AuthFailed);

        long mvAvg  = Avg(bots.Select(b => b.MvAvg));
        long mvP95  = P95(bots);
        long chatAvg = Avg(bots.Select(b => b.ChatAvg));
        int  matches = bots.Sum(b => b.MatchesPlayed);

        Console.WriteLine(
            $"[{elapsed,3}s] conn={connected}/{BotCount}  lobby={inLobby}  arena={inMatch}  failed={failed}" +
            $"  move={mvAvg}ms(p95={mvP95}ms)  chat={chatAvg}ms  matches={matches}");
    }
});

try { await Task.WhenAll(botTasks.Append(liveTask).ToArray()); }
catch (OperationCanceledException) { }

sw.Stop();

// ── 최종 리포트 ──────────────────────────────────────────────
var active  = bots.Where(b => !b.AuthFailed).ToList();
var failed  = bots.Where(b => b.AuthFailed).ToList();

long gMvAvg  = Avg(active.Select(b => b.MvAvg));
long gMvMin  = active.Where(b => b.MvMin > 0).Select(b => b.MvMin).DefaultIfEmpty(0).Min();
long gMvMax  = active.Select(b => b.MvMax).DefaultIfEmpty(0).Max();
long gMvP95  = P95(active);
long gChatAvg = Avg(active.Select(b => b.ChatAvg));
long gChatMin = active.Where(b => b.ChatMin > 0).Select(b => b.ChatMin).DefaultIfEmpty(0).Min();
long gChatMax = active.Select(b => b.ChatMax).DefaultIfEmpty(0).Max();
int  gMatches = active.Sum(b => b.MatchesPlayed);
int  gWins    = active.Sum(b => b.Wins);
int  gDeaths  = active.Sum(b => b.Deaths);

Console.WriteLine();
Console.WriteLine($"╔══════════════════════════════════════════════════════════════════════════╗");
Console.WriteLine($"║  TEST RESULTS   bots={BotCount}   elapsed={sw.Elapsed.TotalSeconds:F1}s   auth_ok={active.Count}  auth_fail={failed.Count}");
Console.WriteLine($"╠══════════════════════════════════════════════════════════════════════════╣");
Console.WriteLine();

// 지연 테이블
Console.WriteLine($"{"Bot",-8} {"Conn",-6} {"Mv avg/p95/max",18} {"Ch avg/max",14} {"Matches",8} {"W/D",8}");
Console.WriteLine(new string('─', 68));

foreach (var bot in bots.OrderBy(b => b.BotId))
{
    string conn = bot.AuthFailed ? "FAIL" : "OK";
    string mv   = bot.AuthFailed ? "-" : $"{bot.MvAvg}/{bot.MvP95}/{bot.MvMax}ms";
    string ch   = bot.AuthFailed ? "-" : $"{bot.ChatAvg}/{bot.ChatMax}ms";
    string wd   = bot.AuthFailed ? "-" : $"{bot.Wins}/{bot.Deaths}";
    Console.WriteLine($"Bot{bot.BotId,-5} {conn,-6} {mv,18} {ch,14} {bot.MatchesPlayed,8} {wd,8}");
}

Console.WriteLine(new string('─', 68));
Console.WriteLine($"{"TOTAL",-8} {active.Count + "/" + BotCount,-6} {$"{gMvAvg}/{gMvP95}/{gMvMax}ms",18} {$"{gChatAvg}/{gChatMax}ms",14} {gMatches,8} {$"{gWins}/{gDeaths}",8}");

Console.WriteLine();
Console.WriteLine($"╠══════════════════════════════════════════════════════════════════════════╣");
Console.WriteLine($"║  SUMMARY");
Console.WriteLine($"║  Move RTT   avg={gMvAvg}ms  p95={gMvP95}ms  min={gMvMin}ms  max={gMvMax}ms");
Console.WriteLine($"║  Chat RTT   avg={gChatAvg}ms  min={gChatMin}ms  max={gChatMax}ms");
Console.WriteLine($"║  Matches    total={gMatches}  wins={gWins}  deaths={gDeaths}");
Console.WriteLine($"║  Auth       ok={active.Count}  fail={failed.Count}");
Console.WriteLine($"╚══════════════════════════════════════════════════════════════════════════╝");

static long Avg(IEnumerable<long> values)
{
    var nonZero = values.Where(v => v > 0).ToList();
    return nonZero.Count > 0 ? nonZero.Sum() / nonZero.Count : 0;
}

static long P95(IEnumerable<BotClient> bots)
{
    var samples = bots.SelectMany(b => b.MvP95 > 0 ? [b.MvP95] : Array.Empty<long>()).Order().ToList();
    return samples.Count > 0 ? samples[(int)(samples.Count * 0.95)] : 0;
}
