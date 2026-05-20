# S2M Region Slices Summary

Generated: 2026-05-19 15:14:56 UTC
Root: c:\Steam\steamapps\common\Stronghold 2
Maps analyzed: 48

## Coverage

- Maps with region slices: 30/48
- Average slices per covered map: 20.4

## Label distribution

| Label | Count |
|---|---:|
| mixed-unknown | 225 |
| binary-opaque | 166 |
| grid-or-terrain | 103 |
| entities-and-systems | 90 |
| environment-fx | 25 |
| core-simulation | 3 |

## Top region anchors for reconstruction

| File | MapType | Regions | Label Mix | Dominant Start |
|---|---|---:|---|---:|
| war_chapter9.s2m | warcampaign | 28 | mixed-unknown:15|binary-opaque:5|grid-or-terrain:4|entities-and-systems:3|environment-fx:1 | 14725 |
| war_chapter1.s2m | warcampaign | 27 | mixed-unknown:14|binary-opaque:5|grid-or-terrain:5|entities-and-systems:2|environment-fx:1 | 14127 |
| war_chapter8.s2m | warcampaign | 27 | mixed-unknown:10|binary-opaque:6|entities-and-systems:5|grid-or-terrain:5|environment-fx:1 | 17963 |
| Kill Barclay.s2m | warcampaign | 24 | binary-opaque:7|mixed-unknown:7|grid-or-terrain:6|entities-and-systems:3|core-simulation:1 | 13742 |
| peace_chapter1.s2m | peacecampaign | 24 | mixed-unknown:11|grid-or-terrain:6|binary-opaque:5|entities-and-systems:1|environment-fx:1 | 23730 |
| war_chapter11.s2m | warcampaign | 24 | binary-opaque:8|grid-or-terrain:7|mixed-unknown:7|entities-and-systems:1|environment-fx:1 | 26665 |
| war_chapter6.s2m | warcampaign | 24 | binary-opaque:8|mixed-unknown:7|entities-and-systems:5|grid-or-terrain:3|environment-fx:1 | 17796 |
| Strong and Stable.s2m | kingmaker | 23 | mixed-unknown:9|binary-opaque:8|grid-or-terrain:3|entities-and-systems:2|environment-fx:1 | 22326 |
| war_chapter4.s2m | warcampaign | 23 | mixed-unknown:10|binary-opaque:6|grid-or-terrain:5|entities-and-systems:1|environment-fx:1 | 19595 |
| Germany.s2m | kingmaker | 22 | mixed-unknown:8|binary-opaque:7|entities-and-systems:4|grid-or-terrain:2|environment-fx:1 | 17767 |
| Great Britain.s2m | kingmaker | 20 | mixed-unknown:7|binary-opaque:5|entities-and-systems:3|grid-or-terrain:3|core-simulation:1|environment-fx:1 | 13947 |
| To The Sea.s2m | kingmaker | 20 | binary-opaque:7|mixed-unknown:5|entities-and-systems:4|grid-or-terrain:3|environment-fx:1 | 13261 |
| Whitebear.s2m | kingmaker | 20 | entities-and-systems:6|binary-opaque:5|mixed-unknown:5|grid-or-terrain:3|environment-fx:1 | 12795 |
| World_Europe.s2m | kingmaker | 20 | binary-opaque:7|mixed-unknown:7|grid-or-terrain:4|entities-and-systems:2 | 16245 |
| Arena of Kings.s2m | kingmaker | 19 | binary-opaque:5|mixed-unknown:5|entities-and-systems:4|grid-or-terrain:4|environment-fx:1 | 16668 |
| Baltic.s2m | kingmaker | 19 | binary-opaque:6|grid-or-terrain:4|mixed-unknown:4|entities-and-systems:3|core-simulation:1|environment-fx:1 | 12633 |
| India.s2m | kingmaker | 19 | mixed-unknown:7|binary-opaque:6|grid-or-terrain:3|entities-and-systems:2|environment-fx:1 | 12739 |
| World_Korea.s2m | kingmaker | 19 | mixed-unknown:8|binary-opaque:6|entities-and-systems:2|grid-or-terrain:2|environment-fx:1 | 12634 |
| Coastal County.s2m | freebuild | 18 | mixed-unknown:7|binary-opaque:5|grid-or-terrain:3|entities-and-systems:2|environment-fx:1 | 14689 |
| River Don.s2m | kingmaker | 18 | mixed-unknown:8|binary-opaque:5|entities-and-systems:3|grid-or-terrain:2 | 12093 |

## Suggested import order (generic)

1. Parse `core-simulation` region(s) first to initialize global systems.
2. Parse `grid-or-terrain` regions to construct terrain/height/material grids.
3. Parse `environment-fx` regions for ambient/environmental emitters and weather settings.
4. Parse `entities-and-systems` regions for placed actors/buildings/path layers.
5. Keep `mixed-unknown`/`binary-opaque` bytes archived for forward-compatible decoding.
