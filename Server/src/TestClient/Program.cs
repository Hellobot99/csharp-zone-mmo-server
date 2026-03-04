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

// 라이브 진행 상황 (10초마다) — RTT 위주
var liveTask = Task.Run(async () =>
{
    while (!cts.Token.IsCancellationRequested)
    {
        try { await Task.Delay(10_000, cts.Token); } catch { break; }
        int elapsed = (int)sw.Elapsed.TotalSeconds;
        int connected = bots.Count(b => b.Connected);

        long mvAvg = Avg(bots.Select(b => b.MvAvg));
        long ztAvg = Avg(bots.Select(b => b.ZtAvg));
        long chatAvg = Avg(bots.Select(b => b.ChatAvg));

        long ztReq = bots.Sum(b => b.ZoneTransfersSent);
        long ztRes = bots.Sum(b => b.ZoneTransferResponsesReceived);
        double ztRate = ztReq > 0 ? ztRes * 100.0 / ztReq : 100;

        Console.WriteLine($"[{elapsed,3}s] bots={connected}/{BotCount}  move={mvAvg}ms  zt={ztAvg}ms  chat={chatAvg}ms  ztOK={ztRes}/{ztReq}({ztRate:F1}%)");
    }
});

try { await Task.WhenAll(botTasks.Append(liveTask).ToArray()); }
catch (OperationCanceledException) { }

sw.Stop();

// ── 최종 리포트 ──────────────────────────────────────────────
long sumMoveReq = bots.Sum(b => b.MoveRequestsSent);
long sumZTReq = bots.Sum(b => b.ZoneTransfersSent);
long sumZTRes = bots.Sum(b => b.ZoneTransferResponsesReceived);
long sumChatReq = bots.Sum(b => b.ChatRequestsSent);
double totalZTRate = sumZTReq > 0 ? sumZTRes * 100.0 / sumZTReq : 100;
int connectedCount = bots.Count(b => b.Connected);

Console.WriteLine();
Console.WriteLine($"=== TEST RESULTS  bots={BotCount}  elapsed={sw.Elapsed.TotalSeconds:F1}s ===");
Console.WriteLine();

// 지연률 테이블
Console.WriteLine($"{"Bot",-8} {"Conn",-5} {"Mv avg/min/max",16} {"ZT avg/min/max",16} {"Ch avg/min/max",16} {"ZTRate",8}");
Console.WriteLine(new string('-', 75));

foreach (var bot in bots.OrderBy(b => b.BotId))
{
    double ztRate = bot.ZoneTransfersSent > 0 ? bot.ZoneTransferResponsesReceived * 100.0 / bot.ZoneTransfersSent : 100;
    string conn = bot.Connected ? "OK" : "FAIL";
    string mv = $"{bot.MvAvg}/{bot.MvMin}/{bot.MvMax}ms";
    string zt = $"{bot.ZtAvg}/{bot.ZtMin}/{bot.ZtMax}ms";
    string chat = $"{bot.ChatAvg}/{bot.ChatMin}/{bot.ChatMax}ms";
    Console.WriteLine($"Bot{bot.BotId,-5} {conn,-5} {mv,16} {zt,16} {chat,16} {ztRate,7:F1}%");
}

Console.WriteLine(new string('-', 75));

long gMvAvg = Avg(bots.Select(b => b.MvAvg));
long gMvMin = bots.Where(b => b.MvMin > 0).Select(b => b.MvMin).DefaultIfEmpty(0).Min();
long gMvMax = bots.Select(b => b.MvMax).DefaultIfEmpty(0).Max();
long gZtAvg = Avg(bots.Select(b => b.ZtAvg));
long gZtMin = bots.Where(b => b.ZtMin > 0).Select(b => b.ZtMin).DefaultIfEmpty(0).Min();
long gZtMax = bots.Select(b => b.ZtMax).DefaultIfEmpty(0).Max();
long gChatAvg = Avg(bots.Select(b => b.ChatAvg));
long gChatMin = bots.Where(b => b.ChatMin > 0).Select(b => b.ChatMin).DefaultIfEmpty(0).Min();
long gChatMax = bots.Select(b => b.ChatMax).DefaultIfEmpty(0).Max();

string gmv = $"{gMvAvg}/{gMvMin}/{gMvMax}ms";
string gzt = $"{gZtAvg}/{gZtMin}/{gZtMax}ms";
string gchat = $"{gChatAvg}/{gChatMin}/{gChatMax}ms";
Console.WriteLine($"{"TOTAL",-8} {connectedCount + "/" + BotCount,-5} {gmv,16} {gzt,16} {gchat,16} {totalZTRate,7:F1}%");

// 패킷 전송률 요약
Console.WriteLine();
Console.WriteLine("=== PACKET RATES ===");
Console.WriteLine($"ZoneTransfer : {totalZTRate:F2}%  (loss {100 - totalZTRate:F2}%)");
Console.WriteLine($"Connected    : {connectedCount}/{BotCount}");
Console.WriteLine($"Move sent    : {sumMoveReq}  Chat sent: {sumChatReq}");

static long Avg(IEnumerable<long> values)
{
    var nonZero = values.Where(v => v > 0).ToList();
    return nonZero.Count > 0 ? nonZero.Sum() / nonZero.Count : 0;
}
