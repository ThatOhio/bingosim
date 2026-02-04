Here is my raw testing data

First, I ran 500000 runs on a single worker. 
From the web container I have this log:
bingosim.web-1  |       Published 25000 batches (500000 runs) in 174170ms (6.97ms per batch) [CHUNKED PARALLEL]

And here is the logs from the worker itself:
      Worker throughput: 9497 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9501); sim=20892ms(9504); snapshot_load=80ms(8); claim=758ms(477), claim_avg=1ms; runs_claimed=0ms(9540); persist=3054ms(9443); snapshot_cache_miss=0ms(8)
      Worker throughput: 30311 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(30298); sim=14394ms(30301); claim=434ms(1514), claim_avg=0ms; runs_claimed=0ms(30280); persist=6679ms(30224)
      Worker throughput: 31361 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(31360); sim=1598ms(31361); claim=148ms(1568), claim_avg=0ms; runs_claimed=0ms(31360); persist=5194ms(31301)
      Worker throughput: 28939 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(28940); sim=1513ms(28939); claim=160ms(1449), claim_avg=0ms; runs_claimed=0ms(28980); persist=4807ms(28921)
      Worker throughput: 30669 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(30670); sim=1522ms(30669); claim=141ms(1533), claim_avg=0ms; runs_claimed=0ms(30660); persist=5166ms(30680)
      Worker throughput: 30683 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(30680); sim=1804ms(30682); claim=157ms(1532), claim_avg=0ms; runs_claimed=0ms(30640); persist=5305ms(30791)
      Worker throughput: 27786 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(27788); sim=1281ms(27787); claim=112ms(1391), claim_avg=0ms; runs_claimed=0ms(27820); persist=4670ms(27680)
      Worker throughput: 22135 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(22134); sim=900ms(22133); claim=101ms(1106), claim_avg=0ms; runs_claimed=0ms(22120); persist=3866ms(22151)
      Worker throughput: 29843 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(29844); sim=1608ms(29844); claim=166ms(1492), claim_avg=0ms; runs_claimed=0ms(29840); persist=5175ms(29798)
      Worker throughput: 27274 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(27274); sim=1341ms(27274); claim=239ms(1364), claim_avg=0ms; runs_claimed=0ms(27280); persist=4767ms(27310)
      Worker throughput: 32583 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(32583); sim=1859ms(32582); claim=155ms(1629), claim_avg=0ms; runs_claimed=0ms(32580); persist=5497ms(32602)
      Worker throughput: 27906 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(27906); sim=1491ms(27906); claim=236ms(1396), claim_avg=0ms; runs_claimed=0ms(27920); persist=4798ms(27886)
      Worker throughput: 32426 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(32427); sim=1857ms(32426); claim=162ms(1621), claim_avg=0ms; runs_claimed=0ms(32420); persist=5547ms(32395)
      Worker throughput: 27601 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(27599); sim=1412ms(27598); claim=219ms(1380), claim_avg=0ms; runs_claimed=0ms(27600); persist=4826ms(27669)
      Worker throughput: 30952 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(30952); sim=1523ms(30953); claim=106ms(1548), claim_avg=0ms; runs_claimed=0ms(30960); persist=5393ms(30887)
      Worker throughput: 24681 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(24680); sim=1164ms(24680); claim=109ms(1233), claim_avg=0ms; runs_claimed=0ms(24660); persist=4215ms(24739)
      Worker throughput: 30536 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(30538); sim=1788ms(30536); claim=210ms(1527), claim_avg=0ms; runs_claimed=0ms(30540); persist=5133ms(30472)
      Worker throughput: 24817 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(24814); sim=1358ms(24816); claim=144ms(1239), claim_avg=0ms; runs_claimed=0ms(24780); persist=4281ms(24951)
      Batch 63b1ec5b-53a2-487a-b3f0-fba6b68d1f7a finalized: Completed (0 failed, duration 194.672422s)

Next I brought up 2 additional workers and ran the same batch. 

Here is the log from the web container:
bingosim.web-1  |       Published 25000 batches (500000 runs) in 179242ms (7.17ms per batch) [CHUNKED PARALLEL]

And here is the logs from the workers:
Worker 1:
      Worker throughput: 781 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(788); sim=3985ms(791); snapshot_load=72ms(8); claim=522ms(44), claim_avg=11ms; runs_claimed=0ms(880); persist=592ms(701); snapshot_cache_miss=0ms(8)
      Worker throughput: 9601 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9585); sim=6623ms(9588); claim=218ms(476), claim_avg=0ms; runs_claimed=0ms(9520); persist=2344ms(9604)
      Worker throughput: 9158 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9158); sim=193ms(9158); claim=96ms(457), claim_avg=0ms; runs_claimed=0ms(9140); persist=1499ms(9235)
      Worker throughput: 9140 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9140); sim=165ms(9140); claim=33ms(457), claim_avg=0ms; runs_claimed=0ms(9140); persist=1453ms(9080)
      Worker throughput: 7400 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(7400); sim=130ms(7400); claim=24ms(370), claim_avg=0ms; runs_claimed=0ms(7400); persist=1195ms(7420)
      Worker throughput: 9080 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9080); sim=232ms(9080); claim=52ms(455), claim_avg=0ms; runs_claimed=0ms(9100); persist=1635ms(9090)
      Worker throughput: 9962 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9963); sim=208ms(9963); claim=69ms(498), claim_avg=0ms; runs_claimed=0ms(9960); persist=1689ms(9984)
      Worker throughput: 9268 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9268); sim=202ms(9268); claim=36ms(464), claim_avg=0ms; runs_claimed=0ms(9280); persist=1460ms(9167)
      Worker throughput: 10337 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10337); sim=200ms(10337); claim=45ms(516), claim_avg=0ms; runs_claimed=0ms(10320); persist=1673ms(10330)
      Worker throughput: 9373 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9371); sim=193ms(9372); claim=41ms(468), claim_avg=0ms; runs_claimed=0ms(9360); persist=1551ms(9409)
      Worker throughput: 10720 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10720); sim=245ms(10720); claim=54ms(536), claim_avg=0ms; runs_claimed=0ms(10720); persist=1742ms(10709)
      Worker throughput: 8980 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(8980); sim=200ms(8980); claim=45ms(449), claim_avg=0ms; runs_claimed=0ms(8980); persist=1459ms(8992)
      Worker throughput: 9880 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9880); sim=189ms(9880); claim=43ms(494), claim_avg=0ms; runs_claimed=0ms(9880); persist=1617ms(9903)
      Worker throughput: 8440 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(8440); sim=162ms(8440); claim=90ms(422), claim_avg=0ms; runs_claimed=0ms(8440); persist=1347ms(8465)
      Worker throughput: 9220 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9221); sim=209ms(9221); claim=34ms(462), claim_avg=0ms; runs_claimed=0ms(9240); persist=1516ms(9152)
      Worker throughput: 9760 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9759); sim=243ms(9759); claim=115ms(487), claim_avg=0ms; runs_claimed=0ms(9740); persist=1613ms(9839)
      Worker throughput: 8260 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(8260); sim=137ms(8260); claim=38ms(413), claim_avg=0ms; runs_claimed=0ms(8260); persist=1442ms(8251)
      Worker throughput: 10166 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10167); sim=189ms(10167); claim=49ms(509), claim_avg=0ms; runs_claimed=0ms(10180); persist=1716ms(10181)
      Worker throughput: 7214 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(7213); sim=135ms(7213); claim=27ms(360), claim_avg=0ms; runs_claimed=0ms(7200); persist=1209ms(7228)

Worker 2:
      Worker throughput: 8160 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(8152); sim=12242ms(8160); snapshot_load=80ms(8); claim=795ms(408), claim_avg=1ms; runs_claimed=0ms(8160); persist=2574ms(8140); snapshot_cache_miss=0ms(8)
      Worker throughput: 10815 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10816); sim=265ms(10815); claim=39ms(541), claim_avg=0ms; runs_claimed=0ms(10820); persist=1773ms(10780)
      Worker throughput: 7821 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(7821); sim=149ms(7820); claim=49ms(391), claim_avg=0ms; runs_claimed=0ms(7820); persist=1249ms(7840)
      Worker throughput: 7390 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(7390); sim=148ms(7390); claim=36ms(370), claim_avg=0ms; runs_claimed=0ms(7400); persist=1149ms(7420)
      Worker throughput: 9154 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9153); sim=218ms(9154); claim=52ms(457), claim_avg=0ms; runs_claimed=0ms(9140); persist=1634ms(9160)
      Worker throughput: 9578 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9578); sim=209ms(9578); claim=54ms(479), claim_avg=0ms; runs_claimed=0ms(9580); persist=1626ms(9500)
      Worker throughput: 9363 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9362); sim=190ms(9363); claim=56ms(469), claim_avg=0ms; runs_claimed=0ms(9380); persist=1628ms(9364)
      Worker throughput: 10139 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10138); sim=193ms(10139); claim=47ms(506), claim_avg=0ms; runs_claimed=0ms(10120); persist=1608ms(10164)
      Worker throughput: 9700 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9700); sim=228ms(9700); claim=41ms(485), claim_avg=0ms; runs_claimed=0ms(9700); persist=1743ms(9731)
      Worker throughput: 10344 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10345); sim=263ms(10344); claim=48ms(518), claim_avg=0ms; runs_claimed=0ms(10360); persist=1714ms(10285)
      Worker throughput: 9379 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9378); sim=200ms(9378); claim=43ms(469), claim_avg=0ms; runs_claimed=0ms(9380); persist=1513ms(9376)
      Worker throughput: 9777 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9777); sim=199ms(9777); claim=41ms(488), claim_avg=0ms; runs_claimed=0ms(9760); persist=1575ms(9840)
      Worker throughput: 8368 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(8369); sim=210ms(8368); claim=34ms(419), claim_avg=0ms; runs_claimed=0ms(8380); persist=1323ms(8360)
      Worker throughput: 10552 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10551); sim=285ms(10552); claim=37ms(527), claim_avg=0ms; runs_claimed=0ms(10540); persist=1746ms(10520)
      Worker throughput: 8788 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(8790); sim=178ms(8789); claim=120ms(440), claim_avg=0ms; runs_claimed=0ms(8800); persist=1487ms(8740)
      Worker throughput: 7994 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(7993); sim=194ms(7992); claim=29ms(400), claim_avg=0ms; runs_claimed=0ms(8000); persist=1581ms(8080)
      Worker throughput: 10078 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10077); sim=214ms(10078); claim=47ms(504), claim_avg=0ms; runs_claimed=0ms(10080); persist=1718ms(10076)
      Worker throughput: 8963 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(8963); sim=183ms(8963); claim=44ms(448), claim_avg=0ms; runs_claimed=0ms(8960); persist=1542ms(8947)
      Batch 81d5996d-9536-46f9-aa73-95e2a4571f9b finalized: Completed (0 failed, duration 198.908301s)
      Worker throughput: 177 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(177); sim=4ms(177); claim=0ms(8), claim_avg=0ms; runs_claimed=0ms(160); persist=41ms(217)

Worker 3:
      Worker throughput: 4676 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(4672); sim=10004ms(4678); snapshot_load=80ms(8); claim=835ms(236), claim_avg=3ms; runs_claimed=0ms(4720); persist=1776ms(4558); snapshot_cache_miss=0ms(8)
      Worker throughput: 10104 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10098); sim=1597ms(10100); claim=78ms(503), claim_avg=0ms; runs_claimed=0ms(10060); persist=1814ms(10057)
      Worker throughput: 8940 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(8940); sim=161ms(8940); claim=44ms(447), claim_avg=0ms; runs_claimed=0ms(8940); persist=1444ms(8985)
      Worker throughput: 8040 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(8040); sim=124ms(8040); claim=21ms(402), claim_avg=0ms; runs_claimed=0ms(8040); persist=1198ms(7980)
      Worker throughput: 8686 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(8688); sim=138ms(8687); claim=32ms(435), claim_avg=0ms; runs_claimed=0ms(8700); persist=1414ms(8660)
      Worker throughput: 8625 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(8623); sim=189ms(8624); claim=137ms(431), claim_avg=0ms; runs_claimed=0ms(8620); persist=1461ms(8649)
      Worker throughput: 10565 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10565); sim=254ms(10565); claim=60ms(528), claim_avg=0ms; runs_claimed=0ms(10560); persist=1752ms(10592)
      Worker throughput: 8876 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(8876); sim=179ms(8876); claim=30ms(444), claim_avg=0ms; runs_claimed=0ms(8880); persist=1532ms(8863)
      Worker throughput: 9668 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9667); sim=223ms(9668); claim=114ms(483), claim_avg=0ms; runs_claimed=0ms(9660); persist=1703ms(9676)
      Worker throughput: 10194 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10195); sim=275ms(10195); claim=26ms(510), claim_avg=0ms; runs_claimed=0ms(10200); persist=1641ms(10140)
      Worker throughput: 9706 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9705); sim=218ms(9705); claim=35ms(485), claim_avg=0ms; runs_claimed=0ms(9700); persist=1655ms(9756)
      Worker throughput: 10003 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10003); sim=240ms(10003); claim=52ms(501), claim_avg=0ms; runs_claimed=0ms(10020); persist=1658ms(10023)
      Worker throughput: 8345 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(8346); sim=182ms(8345); claim=28ms(417), claim_avg=0ms; runs_claimed=0ms(8340); persist=1326ms(8321)
      Worker throughput: 10226 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10226); sim=244ms(10226); claim=30ms(511), claim_avg=0ms; runs_claimed=0ms(10220); persist=1652ms(10205)
      Worker throughput: 9006 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9005); sim=175ms(9006); claim=173ms(450), claim_avg=0ms; runs_claimed=0ms(9000); persist=1632ms(9014)
      Worker throughput: 9134 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(9135); sim=206ms(9134); claim=27ms(457), claim_avg=0ms; runs_claimed=0ms(9140); persist=1463ms(9132)
      Worker throughput: 8790 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(8789); sim=202ms(8789); claim=73ms(440), claim_avg=0ms; runs_claimed=0ms(8800); persist=1511ms(8829)
      Worker throughput: 10216 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(10216); sim=216ms(10216); claim=51ms(510), claim_avg=0ms; runs_claimed=0ms(10200); persist=1689ms(10229)
      Worker throughput: 2920 runs in last 10s. Phase totals (ms, count): snapshot_cache_hit=0ms(2920); sim=63ms(2920); claim=23ms(146), claim_avg=0ms; runs_claimed=0ms(2920); persist=472ms(2951)