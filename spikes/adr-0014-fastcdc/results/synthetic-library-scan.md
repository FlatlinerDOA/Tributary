Scanned `synthetic-library`: 76 files, 286.0 MiB, of which 286.0 MiB WAV sample data (chunked on frame boundaries).

| Strategy | unique MiB | dedup ratio | saved | chunks | unique chunks | mean chunk KiB | index MiB (48 B/entry) |
|---|---:|---:|---:|---:|---:|---:|---:|
| whole file (raw bytes) | 286.0 | 1.000 | 0.0 % | 76 | 76 | 3853.5 | 0.00 |
| whole content (PCM data or file) | 245.5 | 1.165 | 14.2 % | 152 | 128 | 1926.8 | 0.01 |
| fixed 16 KiB | 169.7 | 1.686 | 40.7 % | 18418 | 10953 | 15.9 | 0.50 |
| FastCDC avg 16 KiB | 84.6 | 3.382 | 70.4 % | 15555 | 4613 | 18.8 | 0.21 |
| fixed 64 KiB | 170.4 | 1.678 | 40.4 % | 4683 | 2817 | 62.5 | 0.13 |
| FastCDC avg 64 KiB | 88.0 | 3.250 | 69.2 % | 3814 | 1221 | 76.8 | 0.06 |
| fixed 256 KiB | 172.2 | 1.661 | 39.8 % | 1263 | 788 | 231.9 | 0.04 |
| FastCDC avg 256 KiB | 97.4 | 2.936 | 65.9 % | 1118 | 412 | 262.0 | 0.02 |
