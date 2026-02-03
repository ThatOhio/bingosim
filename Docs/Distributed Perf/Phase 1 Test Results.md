Following the steps in PERF_NOTES.md I ran two dimulations, one with one worker, and one with three workers, per the steps in the document. 


When running the single worker perf test I got the following results, I removed the start and end log lines as they were a bit skewed.
Single worker throughput logs:
      Worker throughput: 992 runs in last 10s. Phase totals (ms, count): sim=564ms(991); snapshot_load=44ms(991); claim=13284ms(991); persist=1439ms(1011)
      Worker throughput: 988 runs in last 10s. Phase totals (ms, count): sim=8ms(988); snapshot_load=12ms(988); claim=13508ms(988); persist=1430ms(1007)
      Worker throughput: 975 runs in last 10s. Phase totals (ms, count): sim=10ms(975); snapshot_load=18ms(975); claim=14183ms(975); persist=1353ms(943)
      Worker throughput: 958 runs in last 10s. Phase totals (ms, count): sim=7ms(958); snapshot_load=8ms(958); claim=14176ms(958); persist=1480ms(980)
      Worker throughput: 849 runs in last 10s. Phase totals (ms, count): sim=6ms(849); snapshot_load=13ms(849); claim=14159ms(849); persist=1614ms(832)
      Worker throughput: 922 runs in last 10s. Phase totals (ms, count): sim=11ms(922); snapshot_load=24ms(922); claim=14133ms(922); persist=1563ms(942)
      Worker throughput: 987 runs in last 10s. Phase totals (ms, count): sim=8ms(987); snapshot_load=1ms(987); claim=13767ms(987); persist=1350ms(952)
      Worker throughput: 986 runs in last 10s. Phase totals (ms, count): sim=7ms(986); snapshot_load=11ms(986); claim=13524ms(986); persist=1413ms(1005)
      Worker throughput: 975 runs in last 10s. Phase totals (ms, count): sim=7ms(975); snapshot_load=16ms(975); claim=13635ms(975); persist=1404ms(990)
      Worker throughput: 983 runs in last 10s. Phase totals (ms, count): sim=3ms(983); snapshot_load=16ms(983); claim=14350ms(983); persist=1366ms(950)


Next I ran the same simulation (via the UI with the same seed), just this time with three workers. Here are their logs:
Worker 1:
      Worker throughput: 286 runs in last 10s. Phase totals (ms, count): sim=2ms(286); snapshot_load=6ms(286); claim=4603ms(286); persist=883ms(281)
      Worker throughput: 335 runs in last 10s. Phase totals (ms, count): sim=4ms(335); snapshot_load=5ms(335); claim=4498ms(335); persist=739ms(341)
      Worker throughput: 337 runs in last 10s. Phase totals (ms, count): sim=1ms(337); snapshot_load=2ms(337); claim=4532ms(337); persist=714ms(325)
      Worker throughput: 337 runs in last 10s. Phase totals (ms, count): sim=0ms(337); snapshot_load=1ms(337); claim=4628ms(337); persist=856ms(345)
      Worker throughput: 330 runs in last 10s. Phase totals (ms, count): sim=1ms(330); snapshot_load=3ms(330); claim=4618ms(330); persist=751ms(337)
      Worker throughput: 328 runs in last 10s. Phase totals (ms, count): sim=4ms(328); snapshot_load=6ms(328); claim=4741ms(328); persist=716ms(318)
      Worker throughput: 335 runs in last 10s. Phase totals (ms, count): sim=2ms(335); snapshot_load=4ms(335); claim=4493ms(335); persist=982ms(332)
      Worker throughput: 335 runs in last 10s. Phase totals (ms, count): sim=1ms(335); snapshot_load=8ms(335); claim=4557ms(335); persist=805ms(340)
      Worker throughput: 330 runs in last 10s. Phase totals (ms, count): sim=2ms(330); snapshot_load=3ms(330); claim=4615ms(330); persist=800ms(323)

Worker 2:
      Worker throughput: 285 runs in last 10s. Phase totals (ms, count): sim=5ms(285); snapshot_load=10ms(285); claim=4611ms(285); persist=929ms(283)
      Worker throughput: 335 runs in last 10s. Phase totals (ms, count): sim=4ms(335); snapshot_load=2ms(335); claim=4547ms(335); persist=833ms(342)
      Worker throughput: 337 runs in last 10s. Phase totals (ms, count): sim=2ms(337); snapshot_load=1ms(337); claim=4500ms(337); persist=811ms(328)
      Worker throughput: 337 runs in last 10s. Phase totals (ms, count): sim=2ms(337); snapshot_load=6ms(337); claim=4598ms(337); persist=904ms(346)
      Worker throughput: 330 runs in last 10s. Phase totals (ms, count): sim=2ms(330); snapshot_load=8ms(330); claim=4625ms(330); persist=819ms(322)
      Worker throughput: 326 runs in last 10s. Phase totals (ms, count): sim=4ms(327); snapshot_load=11ms(327); claim=4618ms(327); persist=816ms(321)
      Worker throughput: 336 runs in last 10s. Phase totals (ms, count): sim=2ms(335); snapshot_load=3ms(335); claim=4527ms(335); persist=854ms(343)
      Worker throughput: 335 runs in last 10s. Phase totals (ms, count): sim=2ms(335); snapshot_load=8ms(335); claim=4595ms(335); persist=768ms(340)
      Worker throughput: 331 runs in last 10s. Phase totals (ms, count): sim=2ms(331); snapshot_load=3ms(331); claim=4638ms(331); persist=755ms(322)

Worker 3:
      Worker throughput: 286 runs in last 10s. Phase totals (ms, count): sim=1ms(286); snapshot_load=6ms(286); claim=4658ms(286); persist=1006ms(284)
      Worker throughput: 334 runs in last 10s. Phase totals (ms, count): sim=1ms(334); snapshot_load=1ms(334); claim=4527ms(334); persist=757ms(323)
      Worker throughput: 336 runs in last 10s. Phase totals (ms, count): sim=2ms(336); snapshot_load=1ms(336); claim=4529ms(336); persist=786ms(342)
      Worker throughput: 337 runs in last 10s. Phase totals (ms, count): sim=1ms(337); snapshot_load=3ms(337); claim=4641ms(337); persist=747ms(341)
      Worker throughput: 331 runs in last 10s. Phase totals (ms, count): sim=0ms(331); snapshot_load=12ms(331); claim=4621ms(331); persist=751ms(321)
      Worker throughput: 327 runs in last 10s. Phase totals (ms, count): sim=0ms(327); snapshot_load=9ms(327); claim=4736ms(327); persist=742ms(335)
      Worker throughput: 335 runs in last 10s. Phase totals (ms, count): sim=2ms(335); snapshot_load=4ms(335); claim=4520ms(335); persist=738ms(341)
      Worker throughput: 335 runs in last 10s. Phase totals (ms, count): sim=3ms(335); snapshot_load=12ms(335); claim=4523ms(335); persist=726ms(323)
      Worker throughput: 331 runs in last 10s. Phase totals (ms, count): sim=1ms(331); snapshot_load=5ms(331); claim=4585ms(331); persist=819ms(339)