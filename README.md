# bingosim
Runs simulations to aid in planning OSRS bingo pathing

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
