# P10 — Performance, SLO and Recovery Evidence

P10 validates EuroTrade Cloud under controlled load and Kubernetes pod failure.

## Test environment

- Platform: Azure Kubernetes Service
- Application replicas: 2
- Database: Azure Database for PostgreSQL
- Messaging: Azure Service Bus
- Load generator: Grafana k6
- Maximum virtual users: 25
- Load-test duration: 4 minutes
- Readiness endpoint: `/health/ready`

## Smoke test

- Requests: 28
- HTTP failure rate: 0.00%
- Checks passed: 100%
- p95 latency: 84.09 ms
- Result: PASS

## Load test

- Requests: 7,930
- Maximum virtual users: 25
- Throughput: 32.97 req/s
- HTTP failure rate: 0.00%
- Checks passed: 100%
- Average latency: 74.32 ms
- p95 latency: 113.84 ms
- p99 latency: 163.55 ms
- Maximum latency: 376.67 ms
- Readiness p95: 113.84 ms
- Interrupted iterations: 0
- Result: PASS

## Recovery test

A running EuroTrade API pod was intentionally deleted.

- Recovery target: < 120 seconds
- Measured recovery: 12.12 seconds
- Result: PASS
- Post-recovery replicas: 2/2 Ready
- Post-recovery readiness endpoint: HTTP 200 Healthy

## SLO evaluation

| SLO | Target | Measured | Result |
|---|---:|---:|---|
| Availability | >= 99% | 100% | PASS |
| API p95 latency | < 500 ms | 113.84 ms | PASS |
| API p99 latency | < 1000 ms | 163.55 ms | PASS |
| Readiness p95 latency | < 250 ms | 113.84 ms | PASS |
| Kubernetes recovery | < 120 s | 12.12 s | PASS |

## Evidence

- `smoke-summary.json`
- `smoke-output.txt`
- `load-summary.json`
- `load-output.txt`
- `recovery-output.txt`
- `pods-after-recovery.txt`
- `monitoring/azure-monitor-p10.png`

## Conclusion

EuroTrade Cloud satisfied all P10 performance and recovery SLOs during the measured test window.

The service sustained approximately 33 requests per second with 25 concurrent virtual users and zero HTTP failures. Tail latency remained comfortably below the defined thresholds.

During the controlled Kubernetes failure test, deletion of one API replica required no manual intervention. AKS restored the Deployment to two Ready replicas in 12.12 seconds, well below the 120-second recovery objective.
