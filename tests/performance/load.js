import http from "k6/http";
import { check, sleep } from "k6";

export const options = {
  stages: [
    { duration: "30s", target: 10 },
    { duration: "1m", target: 25 },
    { duration: "2m", target: 25 },
    { duration: "30s", target: 0 },
  ],
  thresholds: {
    http_req_failed: ["rate<0.01"],
    http_req_duration: ["p(95)<500", "p(99)<1000"],
    "http_req_duration{name:Health}": ["p(95)<250"],
    checks: ["rate>0.99"],
  },
};

const baseUrl = __ENV.BASE_URL || "http://localhost:8080";

export default function () {
  const response = http.get(`${baseUrl}/health/ready`, {
    tags: { name: "Health" },
  });

  check(response, {
    "HTTP 200": (r) => r.status === 200,
  });

  sleep(0.5);
}
