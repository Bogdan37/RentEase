import axios from 'axios';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:8080/api';

export const api = axios.create({
    baseURL: API_BASE_URL,
    headers: {
        'Content-Type': 'application/json',
    },
});

api.interceptors.request.use((config) => {
    const token = localStorage.getItem('token');
    if (token) {
        config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
});

export const authApi = {
    demoLogin: (email) => api.post('/Auth/demo-login', { email }),
    register: (fullName, email, phoneNumber) =>
        api.post('/Auth/register', { fullName, email, phoneNumber }),
};

export const propertiesApi = {
    getAll: (page = 1, pageSize = 6) =>
        api.get(`/Properties?page=${page}&pageSize=${pageSize}`),
    search: (q) => api.get(`/Properties/search?q=${encodeURIComponent(q)}`),
    create: (data) => api.post('/Properties', data),
    delete: (id) => api.delete(`/Properties/${id}`),
};

export const bookingsApi = {
    getAll: () => api.get('/Bookings'),
    create: (data) => api.post('/Bookings', data),
};

export const analyticsApi = {
    getPricingPlan: () => api.get('/Analytics/pricing-plan'),
    getPremiumReport: () => api.get('/Analytics/premium-report'),
};