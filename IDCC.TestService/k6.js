import http from 'k6/http';
import { check } from 'k6';
import { Rate } from 'k6/metrics';

const cacheHitRate = new Rate('cache_hit_rate');

const BASE_URL = 'http://localhost:30000';
const TOTAL_EMPLOYEES = 1000;
const UPDATE_PROBABILITY = 0.1;

export const options = {
    scenarios: {
        contacts: {
            executor: 'constant-vus',
            vus: 10,
            duration: '5m',
        },
    },
};

export default function () {
    const id = Math.floor(Math.random() * TOTAL_EMPLOYEES) + 1;
    const shouldUpdate = Math.random() < UPDATE_PROBABILITY;

    if (shouldUpdate) {
        // ---- PUT: Update employee FullName ----
        const payload = JSON.stringify({
            FullName: `Updated FullName ${id} - VU${__VU}-ITER${__ITER}`
        });

        const headers = {
            'Content-Type': 'application/json',
        };

        const res = http.put(`${BASE_URL}/${id}`, payload, { headers });

        check(res, {
            'PUT status is 200': (r) => r.status === 200,
        });

    } else {
        // ---- GET: Retrieve employee and check cache ----
        const res = http.get(`${BASE_URL}/${id}`);
        const cacheHeader = res.headers['X-Cache'];

        // Add to custom rate metric if HIT
        cacheHitRate.add(cacheHeader === 'HIT');

        check(res, {
            'GET status is 200': (r) => r.status === 200,
            'X-Cache header present': (r) => 'X-Cache' in r.headers,
        });
    }
}
