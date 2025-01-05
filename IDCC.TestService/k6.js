import http from 'k6/http';
import { check } from 'k6';
import { Rate } from 'k6/metrics';

const cacheHitRate = new Rate('cache_hit_rate');

export const options = {
    scenarios: {
        contacts: {
            executor: 'constant-vus',
            vus: 10,
            duration: '30m',
        },
    },
};

export default function () {
    const id = Math.floor(Math.random() * 1000) + 1;
    const response = http.get(`http://localhost:30000/${id}`);

    const cacheStatus = response.headers['X-Cache'];

    cacheHitRate.add(cacheStatus === 'HIT');

    check(response, {
        'HTTP 200': (r) => r.status === 200,
    });
}
