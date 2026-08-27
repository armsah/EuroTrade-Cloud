# EuroTrade Cloud Service Level Objectives

## API availability

SLI: Successful HTTP requests / total HTTP requests.

SLO: >= 99% successful requests during the test window.

## API latency

SLI: HTTP request duration.

SLO:
- p95 < 500 ms
- p99 < 1000 ms

## Health endpoint

SLI: GET /health response duration.

SLO: p95 < 250 ms.

## Kubernetes recovery

SLI: Elapsed time between intentional EuroTrade pod deletion and restoration of two Ready replicas.

SLO: Recovery < 120 seconds.

## Messaging reliability

SLI: Successfully committed order events that are eventually published and processed.

SLO: No loss of successfully committed order events during the controlled failure test.
