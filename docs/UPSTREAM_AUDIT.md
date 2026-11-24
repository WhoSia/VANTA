# Shutter Encoder 20.5 initial audit

Upstream tag: https://github.com/paulpacifico/shutter-encoder/tree/20.5
Tree cbb7d4df3990b4a0cef2b1314de2dd1b25443bf2.

Inspected source landmarks (not exhaustive review):
- src/shutterencoder/functions/settings/BitratesAdjustement.java (c3cc514dd7e36fee15029b5957ee505812c29837): pass selection and passlog naming.
- src/shutterencoder/functions/VideoEncoders.java (d9ce91dd1404492f60bd88ec70ea67dbd1a02d23): assembles codec, bitrate, frame rate, resolution, color and mapping options; second pass dispatch.
- src/shutterencoder/library/FFmpeg.java (73800a3b455bba3a9f676e2fab5a8c14c5541a21): process lifecycle and pass-specific logic.
- LICENSE.txt: GPLv3 text.

VANTA independently implements a narrow argument-list based pipeline. No Java source copied in this revision. Audit specific behavior before porting; preserve provenance and comply with applicable license before redistribution.
