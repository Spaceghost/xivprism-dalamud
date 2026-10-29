# Portable HTTP transport

`src/XivPrism.Core/Http/` is adapted from the MIT-licensed
Spaceghost/xivmcp-dalamud commit `27a9dd54033278b4808a7d178745dd9ca7af963a`.
The unused SSE writer is excluded; the strict HTTP/1.1 parser and bounded socket
connection handling retain their parser regression tests. The publisher configures
eight connections, 8 KiB headers, 512-byte bodies and short read/write timeouts.
Every response closes its connection. Application authentication and origin/host
policy remain in ViewerServer; the transport never touches game services.
