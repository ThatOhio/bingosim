# bingosim
Runs simulations to aid in planning OSRS bingo pathing

This was written hastily just to satisfy my curiosity, this is some of the worse code I've written in years, so sorry for that.

# Dummy data
At time of writing, there are several tiles that have not been revealed so I have used data from previous bingos to fill these in. These tiles are the following:
- Row 3 Easy
- Row 5 Easy and Hard
- Row 10 Easy
- Row 11 Hard

# Known Misses
There are several things I am not currently accounting for that would be good to improve in the future
- Multiple activites per tile
  - Right now each tile only ties to one activity, this should be changes to an array of activities to allow for overlap with say, jars for certain bosses, finer tuned overlaps of gwd bosses or dt2, etc.
  - Champion scrolls another good example, usually has its own tile but could be overlap with many bosses.
- Pets
  - Pets auto complete tiles, and right now none of the pets are accounted for on individual tiles. With the changes I made to account for raids uniques, this could be added for all of the tiles that have a pet.

# Strategies
Below is a short description of each available strategy and the flag to use it with the --strategy option.

- greedy
  - Flag: --strategy greedy
  - Chooses the single best unlocked tile by expected points per unit time (fastest value).

- unlocker
  - Flag: --strategy unlocker
  - Focuses on unlocking the board quickly by targeting the furthest currently unlocked row, with a penalty for activities that have many locked tiles elsewhere to reduce wasted drops.

- row-threshold
  - Flag: --strategy row-threshold
  - Pushes the frontier row to 5 points as efficiently as possible; once the threshold is met, switches to the best efficiency across all unlocked rows.

- risk-averse
  - Flag: --strategy risk-averse
  - Strongly penalizes activities that have many high-value locked tiles (especially deeper rows) to minimize wasted progress on locked content.

- risk-seeking
  - Flag: --strategy risk-seeking
  - Favors activities with "lumpy" progress (big-chunk successes or uniques). Slightly de-emphasizes rarity to chase high-impact outcomes, normalized by time.

- ppm-row-bonus
  - Flag: --strategy ppm-row-bonus
  - Base is points-per-expected-time like greedy, but adds a bonus that scales with proximity to unlocking the current frontier row (approaching 5 points).

- row-sweep (alias: completionist)
  - Flags: --strategy row-sweep or --strategy completionist
  - Completionist-first: within the lowest unlocked row that still has incomplete tiles, finish the easiest tiles (smallest expected remaining time) first.

- monte-carlo
  - Flag: --strategy monte-carlo
  - Monte Carlo lookahead to evaluate near-term sequences. Powerful but slow/experimental. Note: it may be disabled in the default "all" set; you can still invoke it explicitly.

- all
  - Flag: --strategy all
  - Runs all built-in strategies and prints a comparison summary at the end. This is the default if no strategy is specified.

- combo-unlocker
  - Flag: --strategy combo-unlocker
  - Two-phase approach. Phase 1: until the last row is unlocked, only work on the furthest unlocked row and pick among the three 2-tile combinations that reach 5+ points (Easy+Elite, Medium+Hard, Medium+Elite), penalizing combos whose activities have many locked tiles elsewhere to avoid wasted overlap. Phase 2: once all rows are unlocked, target the activity with the most incomplete tiles to maximize overlap potential.

Example usage:
- dotnet run --project BingoSim -- --strategy greedy --runs 1000 --config BingoSim/bingo-board.json
- dotnet run --project BingoSim -- --strategy combo-unlocker --runs 500 --threads 8
- dotnet run --project BingoSim -- --strategy all --runs 2000 --threads 8
