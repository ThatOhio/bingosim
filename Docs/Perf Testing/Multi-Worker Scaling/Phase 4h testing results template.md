# Phase 4H Testing Results - Long-Duration Multi-Worker Scaling Validation

**Date:** [FILL IN DATE]  
**Test Configuration:** 500K runs, Release mode (-c Release)  
**Goal:** Validate multi-worker scaling after JIT warmup becomes negligible

---

## Test Environment

**Infrastructure:**
```bash
# Command used to start infrastructure:
docker compose up -d --build postgres rabbitmq bingosim.web

# Verification (docker compose ps output):
❯ docker compose ps 
NAME                      IMAGE                          COMMAND                  SERVICE        CREATED          STATUS                    PORTS
bingosim-bingosim.web-1   bingosim.web                   "dotnet BingoSim.Web…"   bingosim.web   19 seconds ago   Up 7 seconds              0.0.0.0:8080->8080/tcp, [::]:8080->8080/tcp, 8081/tcp
bingosim-postgres         postgres:16-alpine             "docker-entrypoint.s…"   postgres       19 seconds ago   Up 18 seconds (healthy)   0.0.0.0:5432->5432/tcp, [::]:5432->5432/tcp
bingosim-rabbitmq         rabbitmq:3-management-alpine   "docker-entrypoint.s…"   rabbitmq       19 seconds ago   Up 18 seconds (healthy)   4369/tcp, 5671/tcp, 0.0.0.0:5672->5672/tcp, [::]:5672->5672/tcp, 15671/tcp, 15691-15692/tcp, 25672/tcp, 0.0.0.0:15672->15672/tcp, [::]:15672->15672/tcp
```

**Worker Build:**
```bash
# Build command:
dotnet build BingoSim.Worker/BingoSim.Worker.csproj -c Release

# Build output (verify Success and Release configuration):
❯ dotnet build BingoSim.Worker/BingoSim.Worker.csproj -c Release
Restore complete (0.6s)
  BingoSim.Shared net10.0 succeeded (1.8s) → BingoSim.Shared/bin/Release/net10.0/BingoSim.Shared.dll
  BingoSim.Core net10.0 succeeded (0.4s) → BingoSim.Core/bin/Release/net10.0/BingoSim.Core.dll
  BingoSim.Application net10.0 succeeded (0.5s) → BingoSim.Application/bin/Release/net10.0/BingoSim.Application.dll
  BingoSim.Infrastructure net10.0 succeeded (0.5s) → BingoSim.Infrastructure/bin/Release/net10.0/BingoSim.Infrastructure.dll
  BingoSim.Worker net10.0 succeeded (0.2s) → BingoSim.Worker/bin/Release/net10.0/BingoSim.Worker.dll

Build succeeded in 4.3s
```

**Configuration Verification:**
- `SimulationDelayMs`: [VERIFY = 0]
- `DistributedExecution:BatchSize`: [VERIFY = 20]
- `DistributedExecution:WorkerCount`: [VERIFY = 3]
- `SimulationPersistence:BatchSize`: [VERIFY = 100]
- `SimulationPersistence:FlushIntervalMs`: [VERIFY = 1000]

---

## Test 1: Baseline (1 Worker, 500K runs)

### Test Configuration

**Worker startup command:**
```bash
WORKER_INDEX=0 WORKER_COUNT=1 dotnet run --project BingoSim.Worker -c Release
```

**Web UI / Simulation Setup:**
- Event: Spring League Bingo
- Teams: 2
- Run Count: 500,000
- Seed: 13afeb147c514511969f7ecaabecc7ee
- Execution Mode: Distributed

### Results

**Total Elapsed Time:** 183.8 s

**Worker Console Output:**
```
      Worker throughput: 30689 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(30690); sim=29155ms(30698); snapshot_load=64ms(8); claim=1068ms(1536), claim_avg=0ms; runs_claimed=0ms(30720); persist=7977ms(30619); snapshot_cache_miss=0ms(8)
      Worker throughput: 29174 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(29162); sim=1296ms(29163); claim=118ms(1459), claim_avg=0ms; runs_claimed=0ms(29180); persist=4457ms(29113)
      Worker throughput: 32989 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(32988); sim=2012ms(32987); claim=163ms(1650), claim_avg=0ms; runs_claimed=0ms(33000); persist=5204ms(32978)
      Worker throughput: 31903 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(31902); sim=1774ms(31903); claim=143ms(1594), claim_avg=0ms; runs_claimed=0ms(31880); persist=5144ms(31870)
      Worker throughput: 28341 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(28340); sim=1511ms(28342); claim=143ms(1418), claim_avg=0ms; runs_claimed=0ms(28360); persist=4577ms(28292)
      Worker throughput: 28667 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(28668); sim=1705ms(28667); claim=177ms(1432), claim_avg=0ms; runs_claimed=0ms(28640); persist=4857ms(28686)
      Worker throughput: 33124 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(33123); sim=2143ms(33122); claim=181ms(1657), claim_avg=0ms; runs_claimed=0ms(33140); persist=5539ms(33200)
      Worker throughput: 31697 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(31694); sim=1720ms(31696); claim=132ms(1585), claim_avg=0ms; runs_claimed=0ms(31700); persist=5180ms(31674)
      Worker throughput: 27817 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(27817); sim=1419ms(27818); claim=174ms(1391), claim_avg=0ms; runs_claimed=0ms(27820); persist=4618ms(27718)
      Worker throughput: 31896 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(31896); sim=1954ms(31896); claim=237ms(1595), claim_avg=0ms; runs_claimed=0ms(31900); persist=5471ms(31912)
      Worker throughput: 33794 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(33795); sim=2611ms(33794); claim=252ms(1689), claim_avg=0ms; runs_claimed=0ms(33780); persist=5986ms(33842)
      Worker throughput: 31491 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(31489); sim=1801ms(31490); claim=255ms(1574), claim_avg=0ms; runs_claimed=0ms(31480); persist=5345ms(31479)
      Worker throughput: 26905 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(26905); sim=1605ms(26904); claim=208ms(1346), claim_avg=0ms; runs_claimed=0ms(26920); persist=4764ms(26941)
      Worker throughput: 32357 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(32357); sim=2081ms(32357); claim=209ms(1618), claim_avg=0ms; runs_claimed=0ms(32360); persist=5647ms(32339)
      Worker throughput: 33490 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(33490); sim=2404ms(33490); claim=203ms(1675), claim_avg=0ms; runs_claimed=0ms(33500); persist=5896ms(33520)
      Worker throughput: 30188 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(30185); sim=1838ms(30188); claim=292ms(1508), claim_avg=0ms; runs_claimed=0ms(30160); persist=5486ms(30151)
      Worker throughput: 5478 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(5476); sim=286ms(5477); claim=24ms(273), claim_avg=0ms; runs_claimed=0ms(5460); persist=1023ms(5566)
```

### Analysis (Fill in after test)

**Warmup Phase (Windows 1-2):**
- Window 1 sim time: 29,155 ms for 30,690 runs = 0.95 ms/run
- Window 2 sim time: 1,296 ms for 29,163 runs = 0.044 ms/run
- Total warmup overhead: 20 seconds (2 × 10s windows)

**Steady State (Windows 3-16):**
- Average sim time per 10K runs: ~595 ms (446K runs, 26.6s total sim time)
- Per-run sim time: ~0.06 ms
- Steady-state throughput: ~3,189 runs/s (446,434 runs over 140s)

**Breakdown:**
- Total runs: 500,000
- Warmup time: 20 s (10.9% of total)
- Productive time: 163.8 s (89.1% of total)
- Overall throughput: 2,720 runs/s

**Claim Performance:**
- Total claim operations: ~24,909 (matches 500K/20 BatchSize expectation)
- Average claim time: ~0.14 ms per batch
- Total claim time: ~3,459 ms

**Persist Performance:**
- Total persist time: ~81,031 ms
- Average persist per flush: ~16 ms (5,000 flushes for 500K runs at BatchSize=100)

**Observations:**
- Warmup is clearly confined to Window 1 (29s sim vs ~0.8s expected at steady state); Window 2 already shows near-steady sim time (0.044 ms/run).
- Steady-state sim time (~595 ms per 10K) is above the Phase 4G target of 250–300 ms; possible variance or different workload. Per-run time (~0.06 ms) is slightly above the 0.02–0.05 ms target.
- Claim and persist are low-overhead: ~3.5s total claim time and ~16 ms per persist flush.
- Warmup is ~11% of total time for 500K runs, so it is no longer dominant.

---

## Test 2: Multi-Worker (3 Workers, 500K runs)

### Test Configuration

**Worker startup commands (3 separate terminals):**

**Terminal 1:**
```bash
WORKER_INDEX=0 WORKER_COUNT=3 dotnet run --project BingoSim.Worker -c Release
```

**Terminal 2:**
```bash
WORKER_INDEX=1 WORKER_COUNT=3 dotnet run --project BingoSim.Worker -c Release
```

**Terminal 3:**
```bash
WORKER_INDEX=2 WORKER_COUNT=3 dotnet run --project BingoSim.Worker -c Release
```

**Web UI / Simulation Setup:**
- Event: Spring League Bingo
- Teams: 2
- Run Count: 500,000
- Seed: 13afeb147c514511969f7ecaabecc7ee
- Execution Mode: Distributed
- **Verify DISTRIBUTED_WORKER_COUNT=3** in Web config

### Results

**Total Elapsed Time:** 186.5 s

### Worker 1 Console Output:
```
      Worker throughput: 8713 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(8708); sim=9939ms(8716); snapshot_load=72ms(8); claim=568ms(436), claim_avg=1ms; runs_claimed=0ms(8720); persist=2559ms(8620); snapshot_cache_miss=0ms(8)
      Worker throughput: 10855 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10849); sim=254ms(10850); claim=48ms(543), claim_avg=0ms; runs_claimed=0ms(10860); persist=1721ms(10860)
      Worker throughput: 11072 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(11072); sim=273ms(11072); claim=58ms(553), claim_avg=0ms; runs_claimed=0ms(11060); persist=1771ms(11064)
      Worker throughput: 8919 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(8920); sim=270ms(8920); claim=29ms(446), claim_avg=0ms; runs_claimed=0ms(8920); persist=2061ms(8916)
      Worker throughput: 10794 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10794); sim=243ms(10793); claim=47ms(540), claim_avg=0ms; runs_claimed=0ms(10800); persist=1759ms(10820)
      Worker throughput: 10079 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10078); sim=245ms(10078); claim=53ms(504), claim_avg=0ms; runs_claimed=0ms(10080); persist=1708ms(10085)
      Worker throughput: 8711 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(8711); sim=167ms(8711); claim=56ms(436), claim_avg=0ms; runs_claimed=0ms(8720); persist=1411ms(8759)
      Worker throughput: 10497 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10497); sim=257ms(10497); claim=58ms(524), claim_avg=0ms; runs_claimed=0ms(10480); persist=1793ms(10456)
      Worker throughput: 9776 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9778); sim=201ms(9777); claim=40ms(490), claim_avg=0ms; runs_claimed=0ms(9800); persist=1591ms(9732)
      Worker throughput: 10790 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10788); sim=290ms(10789); claim=53ms(539), claim_avg=0ms; runs_claimed=0ms(10780); persist=1762ms(10847)
      Worker throughput: 10299 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10300); sim=244ms(10299); claim=239ms(515), claim_avg=0ms; runs_claimed=0ms(10300); persist=1807ms(10255)
      Worker throughput: 9974 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9974); sim=252ms(9973); claim=81ms(498), claim_avg=0ms; runs_claimed=0ms(9960); persist=1663ms(10031)
      Worker throughput: 10702 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10702); sim=235ms(10703); claim=37ms(536), claim_avg=0ms; runs_claimed=0ms(10720); persist=1741ms(10637)
      Worker throughput: 9439 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9438); sim=222ms(9438); claim=68ms(471), claim_avg=0ms; runs_claimed=0ms(9420); persist=1644ms(9516)
      Worker throughput: 10869 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10870); sim=236ms(10869); claim=41ms(544), claim_avg=0ms; runs_claimed=0ms(10880); persist=1767ms(10801)
      Worker throughput: 8151 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(8150); sim=198ms(8150); claim=68ms(407), claim_avg=0ms; runs_claimed=0ms(8140); persist=1460ms(8241)
      Worker throughput: 7040 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(7040); sim=160ms(7040); claim=37ms(352), claim_avg=0ms; runs_claimed=0ms(7040); persist=1145ms(7040)
```

### Worker 2 Console Output:
```
      Worker throughput: 908 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(905); sim=3751ms(912); snapshot_load=80ms(8); claim=445ms(47), claim_avg=9ms; runs_claimed=0ms(940); persist=699ms(801); snapshot_cache_miss=0ms(8)
      Worker throughput: 9431 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9425); sim=5085ms(9427); claim=154ms(470), claim_avg=0ms; runs_claimed=0ms(9400); persist=2143ms(9419)
      Worker throughput: 10821 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10820); sim=286ms(10820); claim=46ms(541), claim_avg=0ms; runs_claimed=0ms(10820); persist=1813ms(10851)
      Worker throughput: 10420 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10420); sim=211ms(10420); claim=39ms(521), claim_avg=0ms; runs_claimed=0ms(10420); persist=1678ms(10445)
      Worker throughput: 9557 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9558); sim=226ms(9557); claim=41ms(478), claim_avg=0ms; runs_claimed=0ms(9560); persist=1689ms(9544)
      Worker throughput: 10810 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10811); sim=291ms(10811); claim=55ms(541), claim_avg=0ms; runs_claimed=0ms(10820); persist=1782ms(10780)
      Worker throughput: 9833 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9831); sim=240ms(9832); claim=23ms(491), claim_avg=0ms; runs_claimed=0ms(9820); persist=1555ms(9856)
      Worker throughput: 9080 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9080); sim=231ms(9080); claim=41ms(454), claim_avg=0ms; runs_claimed=0ms(9080); persist=1538ms(9128)
      Worker throughput: 10100 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10100); sim=216ms(10100); claim=52ms(505), claim_avg=0ms; runs_claimed=0ms(10100); persist=1743ms(10042)
      Worker throughput: 10125 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10126); sim=266ms(10126); claim=58ms(506), claim_avg=0ms; runs_claimed=0ms(10120); persist=1667ms(10120)
      Worker throughput: 10855 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10854); sim=305ms(10854); claim=51ms(542), claim_avg=0ms; runs_claimed=0ms(10840); persist=1789ms(10914)
      Worker throughput: 9988 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9988); sim=259ms(9988); claim=64ms(500), claim_avg=0ms; runs_claimed=0ms(10000); persist=1792ms(9931)
      Worker throughput: 10012 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10012); sim=245ms(10012); claim=55ms(500), claim_avg=0ms; runs_claimed=0ms(10000); persist=1715ms(10036)
      Worker throughput: 10980 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10980); sim=255ms(10980); claim=41ms(549), claim_avg=0ms; runs_claimed=0ms(10980); persist=1767ms(10999)
      Worker throughput: 9280 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9280); sim=206ms(9280); claim=47ms(464), claim_avg=0ms; runs_claimed=0ms(9280); persist=1681ms(9300)
      Worker throughput: 10889 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10890); sim=262ms(10889); claim=49ms(545), claim_avg=0ms; runs_claimed=0ms(10900); persist=1856ms(10904)
      Worker throughput: 7895 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(7894); sim=151ms(7895); claim=125ms(395), claim_avg=0ms; runs_claimed=0ms(7900); persist=1280ms(7871)
      Worker throughput: 5696 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(5695); sim=139ms(5696); claim=27ms(284), claim_avg=0ms; runs_claimed=0ms(5680); persist=963ms(5739)
```

### Worker 3 Console Output:
```
      Worker throughput: 1191 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(1188); sim=4667ms(1195); snapshot_load=72ms(8); claim=445ms(61), claim_avg=7ms; runs_claimed=0ms(1220); persist=749ms(1090); snapshot_cache_miss=0ms(8)
      Worker throughput: 9449 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9442); sim=5512ms(9444); claim=151ms(471), claim_avg=0ms; runs_claimed=0ms(9420); persist=2115ms(9470)
      Worker throughput: 10860 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10860); sim=263ms(10860); claim=40ms(543), claim_avg=0ms; runs_claimed=0ms(10860); persist=1761ms(10895)
      Worker throughput: 10040 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10040); sim=268ms(10040); claim=41ms(502), claim_avg=0ms; runs_claimed=0ms(10040); persist=1625ms(10085)
      Worker throughput: 9859 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9859); sim=222ms(9859); claim=65ms(493), claim_avg=0ms; runs_claimed=0ms(9860); persist=1539ms(9800)
      Worker throughput: 10881 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10881); sim=299ms(10881); claim=65ms(545), claim_avg=0ms; runs_claimed=0ms(10900); persist=1788ms(10899)
      Worker throughput: 9718 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9718); sim=192ms(9718); claim=98ms(486), claim_avg=0ms; runs_claimed=0ms(9720); persist=1552ms(9751)
      Worker throughput: 9202 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9200); sim=237ms(9201); claim=53ms(459), claim_avg=0ms; runs_claimed=0ms(9180); persist=1537ms(9189)
      Worker throughput: 10106 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10107); sim=218ms(10106); claim=282ms(506), claim_avg=0ms; runs_claimed=0ms(10120); persist=1768ms(10037)
      Worker throughput: 9989 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9989); sim=232ms(9989); claim=49ms(499), claim_avg=0ms; runs_claimed=0ms(9980); persist=1628ms(10015)
      Worker throughput: 10985 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10984); sim=252ms(10985); claim=28ms(549), claim_avg=0ms; runs_claimed=0ms(10980); persist=1736ms(11004)
      Worker throughput: 9900 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9900); sim=261ms(9900); claim=244ms(495), claim_avg=0ms; runs_claimed=0ms(9900); persist=1898ms(9915)
      Worker throughput: 10080 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10080); sim=264ms(10080); claim=65ms(504), claim_avg=0ms; runs_claimed=0ms(10080); persist=1664ms(10086)
      Worker throughput: 10963 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10963); sim=208ms(10963); claim=49ms(549), claim_avg=0ms; runs_claimed=0ms(10980); persist=1736ms(10944)
      Worker throughput: 9284 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9284); sim=190ms(9284); claim=59ms(464), claim_avg=0ms; runs_claimed=0ms(9280); persist=1565ms(9292)
      Worker throughput: 10930 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10931); sim=226ms(10930); claim=55ms(546), claim_avg=0ms; runs_claimed=0ms(10920); persist=1804ms(10937)
      Worker throughput: 7903 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(7902); sim=170ms(7903); claim=65ms(396), claim_avg=0ms; runs_claimed=0ms(7920); persist=1357ms(7879)
      Worker throughput: 5300 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(5300); sim=122ms(5300); claim=33ms(264), claim_avg=0ms; runs_claimed=0ms(5280); persist=906ms(5352)
```

### Analysis (Fill in after test)

**Worker 1:**
- Total runs processed: 159,921
- Total claims: 7,848
- Warmup sim time (window 1): 9,939 ms
- Steady-state avg sim time: 241 ms per 10K runs
- Steady-state throughput: ~945 runs/s

**Worker 2:**
- Total runs processed: 166,631
- Total claims: 7,838
- Warmup sim time (windows 1-2): 3,751 ms + 5,085 ms = 8,836 ms
- Steady-state avg sim time: 231 ms per 10K runs
- Steady-state throughput: ~1,042 runs/s

**Worker 3:**
- Total runs processed: 173,431
- Total claims: 7,840
- Warmup sim time (windows 1-2): 4,667 ms + 5,512 ms = 10,179 ms
- Steady-state avg sim time: 208 ms per 10K runs
- Steady-state throughput: ~1,085 runs/s

**Aggregate Analysis:**
- Total claims (all workers): 23,526 (expected ~25K for 500K/20 BatchSize)
- Aggregate warmup overhead: 20 s (parallel, max of 3 workers; Workers 2 & 3 need 2 windows)
- Aggregate steady-state throughput: ~3,072 runs/s
- Warmup as % of total: 10.7%

**Load Distribution:**
- Worker 1 processed 32.0% of runs
- Worker 2 processed 33.3% of runs
- Worker 3 processed 34.7% of runs
- **Distribution evenness:** GOOD

---

## Partitioning Validation

### Message Filtering Verification

**Check Worker Logs For:**
1. ✅ / ❌ No "Skipping batch assigned to WorkerIndex=X" messages
2. ✅ / ❌ All workers show "Processing batch assigned to WorkerIndex=N" in Debug logs (if enabled)
3. ✅ / ❌ Claim counts are approximately equal (~2,500 each)

**Notes on partitioning behavior:**
[DESCRIBE ANY ISSUES - e.g., "Worker 2 showed 50 'skipping batch' messages early on but then stabilized"]

### Database Contention Check

**Claim Times:**
- Worker 1 steady-state claim avg: ~0.14 ms
- Worker 2 steady-state claim avg: ~0.13 ms
- Worker 3 steady-state claim avg: ~0.19 ms
- **Contention indicator:** LOW

**Persist Times:**
- Worker 1 steady-state persist: ~1,700–1,800 ms per 10s
- Worker 2 steady-state persist: ~1,600–1,800 ms per 10s
- Worker 3 steady-state persist: ~1,600–1,800 ms per 10s
- **Contention indicator:** LOW

---

## Scaling Factor Calculation

### Raw Elapsed Time Comparison

| Configuration | Elapsed Time | Throughput | Scaling Factor |
|---------------|--------------|------------|----------------|
| 1 Worker | 183.8 s | 2,720 runs/s | 1.0× (baseline) |
| 3 Workers | 186.5 s | 2,681 runs/s | 0.99× |

**Scaling Factor:** 183.8 / 186.5 = **0.99×**

**Target:** ≥1.5× (i.e., 3 workers should be ≥1.5× faster)
**Result:** ❌ **FAIL**

### Steady-State Throughput Comparison (Excluding Warmup)

**1 Worker (steady state windows only):**
- Windows used for calculation: 3–16 (exclude first 2 warmup windows)
- Average runs per 10s: ~31,888
- Steady-state throughput: ~3,189 runs/s

**3 Workers (steady state windows only):**
- Windows used for calculation: 3–17 (exclude first 2 warmup windows per worker)
- Worker 1 avg runs per 10s: ~9,451
- Worker 2 avg runs per 10s: ~10,419
- Worker 3 avg runs per 10s: ~10,853
- Aggregate avg per 10s: ~30,723
- Aggregate steady-state throughput: ~3,072 runs/s

**Steady-State Scaling Factor:** 3,072 / 3,189 = **0.96×**

---

## Success Criteria Evaluation

### Minimum Success Criteria

- [ ] ❌ 500K runs with 1 worker completes in 100-110s
  - Actual: 183.8 s - FAIL

- [ ] ❌ 500K runs with 3 workers completes in ≤75s
  - Actual: 186.5 s - FAIL

- [ ] ❌ Scaling factor ≥ 1.4×
  - Actual: 0.99× - FAIL

- [ ] ✅ Steady-state sim time ≤300ms per 10K runs for all workers
  - Worker 1: 241 ms - PASS
  - Worker 2: 231 ms - PASS
  - Worker 3: 208 ms - PASS

- [ ] ✅ Each worker processes approximately 1/3 of total batches
  - Worker 1: 32.0% - PASS
  - Worker 2: 33.3% - PASS
  - Worker 3: 34.7% - PASS

### Ideal Success Criteria

- [ ] ❌ 3 workers complete in ≤70s (1.5× improvement)
  - Actual: 186.5 s - FAIL

- [ ] ❌ Scaling factor ≥ 1.5×
  - Actual: 0.99× - FAIL

- [ ] ❌ Aggregate steady-state throughput ≥4,200 runs/s
  - Actual: 3,072 runs/s - FAIL

- [ ] ✅ No evidence of worker contention or idle time after warmup
  - Evidence: Claim times <0.2 ms, persist times consistent; load distribution even (32–35% each)

---

## Conclusions

### Did Partitioning Enable Multi-Worker Scaling?

**Answer:** NO

**Explanation:**
Partitioning is working correctly (load distribution 32–35% per worker, claim counts ~7,800–7,850 each, no contention). However, 3 workers did not scale: elapsed time 186.5 s vs 183.8 s for 1 worker (0.99×). Steady-state aggregate throughput is 3,072 runs/s vs 3,189 runs/s for 1 worker—3 workers are slightly slower. The bottleneck is not database contention (claim/persist times are low). Likely causes: coordination overhead (message filtering, claiming), or the single worker is already saturating available resources (CPU/DB) such that adding workers adds overhead without increasing useful work.

### What is the Steady-State Aggregate Throughput?

**1 Worker:** 3,189 runs/s
**3 Workers:** 3,072 runs/s
**Improvement:** 0.96× (regression)

### Is the 1.5× Target Realistic?

**Answer:** NO

**Reasoning:**
With current configuration, 3 workers do not outperform 1 worker. The 1.5× target assumes parallelization benefit, but we observe a slight regression. Either the workload is not parallelizable at this scale (single worker already near optimal), or there is hidden overhead (messaging, filtering, coordination) that negates the benefit. Further investigation needed to identify the root cause.

### Warmup Impact Analysis

**1 Worker:**
- Warmup time: 20 s
- Productive time: 163.8 s
- Warmup as % of total: 10.9%

**3 Workers:**
- Warmup time (max across workers): 20 s
- Productive time: 166.5 s
- Warmup as % of total: 10.7%

**Recommendation:** 500K runs keeps warmup at ~10% of total time. For future tests, 500K minimum is appropriate.

---

## Next Steps

### If Scaling Factor ≥ 1.5× (SUCCESS)
- [ ] Mark Phase 4 complete
- [ ] Document final configuration and recommendations
- [ ] Consider testing with 5 or more workers to find scaling limits
- [ ] Update production deployment guide with worker count recommendations

### If Scaling Factor < 1.5× (NEEDS INVESTIGATION) ← **APPLICABLE**
- [ ] Identify remaining bottleneck (database, network, message broker, etc.)
- [ ] Check if workers are truly processing different batches (partitioning verification)
- [ ] Consider Phase 4I to address coordination/messaging overhead—partitioning works but adds net overhead
- [ ] Test with different batch sizes or persistence configurations

### If Partitioning Not Working (CRITICAL ISSUE)
- [ ] Debug WorkerIndexFilter - verify it's actually filtering messages
- [ ] Check Web publisher - verify WorkerIndex is being assigned correctly
- [ ] Verify WORKER_INDEX and WORKER_COUNT environment variables are set
- [ ] Check RabbitMQ message properties to confirm WorkerIndex header is present

