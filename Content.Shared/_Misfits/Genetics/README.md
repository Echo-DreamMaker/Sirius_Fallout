# Trauma Station genetics port

This directory and the matching `_Misfits/Genetics` code/resource directories contain the genetics system ported from [Trauma Station](https://github.com/Trauma-Station/Trauma-Station), source commit `92f1800637` (2026-07-21).

The source targets newer action, entity-effect, body, status-effect, and trigger APIs than Nuclear-14. Compatibility implementations live under `_Misfits`; the small changes outside `_Misfits` are event/access hooks needed by genetics for mobs, polymorphing, DNA scrambling, melee strength, speech, footsteps, tethering, and death triggers.

Imported content remains AGPL-3.0-or-later as marked in the source files.
