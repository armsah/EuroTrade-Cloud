import http from "k6/http";
import { check, sleep } from "k6";

export const options = {
  vus: 1,
  duration: "30s",
  thresholds: {
    http_req_failed: ["rate<0.01"],
    http_req_duration: ["p(95)<500"],
    checks: ["rate>0.99"],
  },
};

const baseUrl = __ENV.BASE_URL || "http://localhost:8080";

export default function () {
  const response = http.get(`${baseUrl}/health/ready`, {
    tags: { name: "Health" },
  });

  check(response, {
    "health returns 200": (r) => r.status === 200,
  });

  sleep(1);
}
