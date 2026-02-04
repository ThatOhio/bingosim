As I did with phase 1, I ran the tests outlined in PERF_NOTES.md after the second phase of our Plan

When running the single worker perf test I got the following results, I removed the start and end log lines as they were a bit skewed.
Single worker throughput logs:
      Worker throughput: 1015 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(1015); sim=251ms(1016); claim=13417ms(1015), claim_avg=13ms; persist=1472ms(1035)
      Worker throughput: 995 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(994); sim=7ms(994); claim=13696ms(994), claim_avg=13ms; persist=1357ms(958)
      Worker throughput: 899 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(899); sim=13ms(899); claim=13710ms(899), claim_avg=15ms; persist=1625ms(931)
      Worker throughput: 977 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(977); sim=7ms(977); claim=14092ms(977), claim_avg=14ms; persist=1359ms(942)
      Worker throughput: 999 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(999); sim=11ms(999); claim=14068ms(999), claim_avg=14ms; persist=1440ms(1020)
      Worker throughput: 1000 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(1000); sim=7ms(1000); claim=14156ms(1000), claim_avg=14ms; persist=1379ms(1017)
      Worker throughput: 996 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(996); sim=7ms(996); claim=13537ms(996), claim_avg=13ms; persist=1362ms(963)
      Worker throughput: 999 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(999); sim=7ms(999); claim=13686ms(999), claim_avg=13ms; persist=1375ms(1016)
      Worker throughput: 1008 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(1008); sim=9ms(1008); claim=13548ms(1008), claim_avg=13ms; persist=1356ms(1024)

Next I ran the same simulation (via the UI with the same seed), just this time with three workers. Here are their logs:
Worker 1:
      Worker throughput: 317 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(316); sim=47ms(317); claim=4553ms(316), claim_avg=14ms; persist=850ms(324)
      Worker throughput: 324 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(324); sim=3ms(324); claim=4610ms(324), claim_avg=14ms; persist=780ms(314)
      Worker throughput: 317 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(317); sim=2ms(317); claim=4579ms(317), claim_avg=14ms; persist=798ms(324)
      Worker throughput: 317 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(317); sim=1ms(317); claim=4639ms(317), claim_avg=14ms; persist=767ms(307)
      Worker throughput: 314 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(314); sim=0ms(314); claim=4727ms(314), claim_avg=15ms; persist=934ms(323)
      Worker throughput: 322 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(322); sim=2ms(322); claim=4626ms(322), claim_avg=14ms; persist=776ms(313)
      Worker throughput: 334 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(334); sim=3ms(334); claim=4636ms(334), claim_avg=13ms; persist=882ms(344)
      Worker throughput: 335 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(335); sim=3ms(335); claim=4502ms(335), claim_avg=13ms; persist=791ms(326)
      Worker throughput: 333 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(333); sim=3ms(333); claim=4490ms(333), claim_avg=13ms; persist=755ms(339)

Worker 2:
      Worker throughput: 315 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(314); sim=69ms(315); claim=4496ms(314), claim_avg=14ms; persist=959ms(324)
      Worker throughput: 325 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(325); sim=3ms(325); claim=4597ms(325), claim_avg=14ms; persist=817ms(317)
      Worker throughput: 317 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(317); sim=3ms(317); claim=4625ms(317), claim_avg=14ms; persist=947ms(327)
      Worker throughput: 317 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(317); sim=1ms(317); claim=4624ms(317), claim_avg=14ms; persist=758ms(304)
      Worker throughput: 314 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(314); sim=2ms(314); claim=4712ms(314), claim_avg=15ms; persist=849ms(321)
      Worker throughput: 322 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(322); sim=1ms(322); claim=4698ms(322), claim_avg=14ms; persist=823ms(315)
      Worker throughput: 333 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(333); sim=4ms(333); claim=4639ms(333), claim_avg=13ms; persist=765ms(339)
      Worker throughput: 335 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(335); sim=0ms(335); claim=4505ms(335), claim_avg=13ms; persist=768ms(341)
      Worker throughput: 333 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(333); sim=3ms(333); claim=4486ms(333), claim_avg=13ms; persist=728ms(323)

Worker 3:
      Worker throughput: 316 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(316); sim=59ms(316); claim=4554ms(316), claim_avg=14ms; persist=912ms(308)
      Worker throughput: 324 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(324); sim=2ms(324); claim=4630ms(324), claim_avg=14ms; persist=791ms(330)
      Worker throughput: 318 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(318); sim=1ms(318); claim=4634ms(318), claim_avg=14ms; persist=961ms(314)
      Worker throughput: 316 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(316); sim=1ms(316); claim=4622ms(316), claim_avg=14ms; persist=919ms(324)
      Worker throughput: 314 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(314); sim=3ms(314); claim=4712ms(314), claim_avg=15ms; persist=832ms(307)
      Worker throughput: 322 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(322); sim=1ms(322); claim=4634ms(322), claim_avg=14ms; persist=934ms(332)
      Worker throughput: 334 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(334); sim=2ms(334); claim=4632ms(334), claim_avg=13ms; persist=770ms(323)
      Worker throughput: 334 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(334); sim=5ms(334); claim=4501ms(334), claim_avg=13ms; persist=852ms(344)
      Worker throughput: 334 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(334); sim=1ms(334); claim=4531ms(334), claim_avg=13ms; persist=725ms(323)