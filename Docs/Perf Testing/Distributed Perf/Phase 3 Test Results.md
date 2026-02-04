There have been significant gains previously for 10000 runs it would take around 100 to 105 seconds to finish the full batch. Now it takes around 11 seconds for 10000

Here is the logs for a single worker with 10000 run in 11.6 s

Single worker logs:
      Worker throughput: 6884 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(6881); sim=3599ms(6889); snapshot_load=80ms(8); claim=10576ms(690), claim_avg=15ms; runs_claimed=0ms(6900); persist=4067ms(6772); snapshot_cache_miss=0ms(8)
      Worker throughput: 3116 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(3109); sim=44ms(3109); claim=4230ms(310), claim_avg=13ms; runs_claimed=0ms(3100); persist=1635ms(3228)


And here is the logs for 3 workers with 10000 run in 11 s

Worker 1:
      Worker throughput: 3340 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(3332); sim=2121ms(3340); snapshot_load=64ms(8); claim=5339ms(334), claim_avg=15ms; runs_claimed=0ms(3340); persist=2073ms(3330); snapshot_cache_miss=0ms(8)

Worker 2:
      Worker throughput: 3270 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(3262); sim=2307ms(3270); snapshot_load=64ms(8); claim=5176ms(327), claim_avg=15ms; runs_claimed=0ms(3270); persist=2025ms(3190); snapshot_cache_miss=0ms(8)
      Worker throughput: 90 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(90); sim=0ms(90); claim=127ms(9), claim_avg=14ms; runs_claimed=0ms(90); persist=66ms(170)

Worker 3:
      Worker throughput: 3270 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(3262); sim=2330ms(3270); snapshot_load=64ms(8); claim=5348ms(327), claim_avg=16ms; runs_claimed=0ms(3270); persist=2012ms(3210); snapshot_cache_miss=0ms(8)
      Worker throughput: 30 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(30); sim=0ms(30); claim=43ms(3), claim_avg=14ms; runs_claimed=0ms(30); persist=22ms(90)


Now because of how fast this executed, I wanted to get a bit more data, so I switched to 50000

First, here is a single worker with 50000 run in 52.5 s

Single worker logs:
      Worker throughput: 2059 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(2054); sim=4182ms(2061); snapshot_load=64ms(8); claim=4042ms(207), claim_avg=19ms; runs_claimed=0ms(2070); persist=1439ms(1983); snapshot_cache_miss=0ms(8)
      Worker throughput: 9961 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9957); sim=7805ms(9958); claim=14308ms(995), claim_avg=14ms; runs_claimed=0ms(9950); persist=5525ms(9969)
      Worker throughput: 9948 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9949); sim=337ms(9949); claim=14378ms(995), claim_avg=14ms; runs_claimed=0ms(9950); persist=4925ms(9917)
      Worker throughput: 10006 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10005); sim=255ms(10005); claim=14057ms(1001), claim_avg=14ms; runs_claimed=0ms(10010); persist=4832ms(10011)
      Worker throughput: 9692 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9693); sim=275ms(9692); claim=14532ms(969), claim_avg=14ms; runs_claimed=0ms(9690); persist=5013ms(9733)
      Worker throughput: 8334 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(8333); sim=229ms(8333); claim=12041ms(833), claim_avg=14ms; runs_claimed=0ms(8330); persist=4208ms(8387)


And here is 3 workers with 50000 run in 55.3 s

Worker 1:
      Worker throughput: 2940 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(2940); sim=60ms(2940); claim=4884ms(294), claim_avg=16ms; runs_claimed=0ms(2940); persist=1710ms(2940)
      Worker throughput: 3330 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(3331); sim=88ms(3331); claim=4701ms(334), claim_avg=14ms; runs_claimed=0ms(3340); persist=1519ms(3251)
      Worker throughput: 3340 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(3339); sim=72ms(3339); claim=4818ms(333), claim_avg=14ms; runs_claimed=0ms(3330); persist=1673ms(3396)
      Worker throughput: 3320 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(3320); sim=69ms(3320); claim=4789ms(332), claim_avg=14ms; runs_claimed=0ms(3320); persist=1667ms(3323)
      Worker throughput: 2180 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(2180); sim=35ms(2180); claim=3254ms(218), claim_avg=14ms; runs_claimed=0ms(2180); persist=1145ms(2230)

Worker 2:
      Worker throughput: 2920 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(2920); sim=39ms(2920); claim=4860ms(292), claim_avg=16ms; runs_claimed=0ms(2920); persist=1681ms(2930)
      Worker throughput: 3338 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(3339); sim=49ms(3338); claim=4695ms(334), claim_avg=14ms; runs_claimed=0ms(3340); persist=1594ms(3342)
      Worker throughput: 3341 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(3341); sim=45ms(3341); claim=4840ms(334), claim_avg=14ms; runs_claimed=0ms(3340); persist=1538ms(3288)
      Worker throughput: 3311 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(3310); sim=69ms(3310); claim=4826ms(331), claim_avg=14ms; runs_claimed=0ms(3310); persist=1679ms(3350)
      Worker throughput: 2310 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(2310); sim=35ms(2310); claim=3547ms(231), claim_avg=15ms; runs_claimed=0ms(2310); persist=1221ms(2370)

Worker 3:
      Worker throughput: 2929 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(2930); sim=50ms(2930); claim=4878ms(293), claim_avg=16ms; runs_claimed=0ms(2930); persist=1674ms(2870)
      Worker throughput: 3341 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(3340); sim=69ms(3340); claim=4700ms(334), claim_avg=14ms; runs_claimed=0ms(3340); persist=1710ms(3370)
      Worker throughput: 3330 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(3330); sim=47ms(3330); claim=4841ms(333), claim_avg=14ms; runs_claimed=0ms(3330); persist=1572ms(3350)
      Worker throughput: 3319 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(3320); sim=67ms(3320); claim=4800ms(332), claim_avg=14ms; runs_claimed=0ms(3320); persist=1569ms(3280)
      Worker throughput: 2241 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(2240); sim=50ms(2240); claim=3383ms(224), claim_avg=15ms; runs_claimed=0ms(2240); persist=1192ms(2340)