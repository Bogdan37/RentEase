import axios from 'axios';

const API_BASE_URL = 'http://localhost:8080/api';

const api = axios.create({
    baseURL: API_BASE_URL,
    headers: {
        'Content-Type': 'application/json',
    },
});

api.interceptors.request.use((config) => {
    const token = localStorage.getItem('rentease_jwt');
    if (token) {
        config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
});

export const propertyApi = {
    getAll: (page = 1, pageSize = 6) =>
        api.get(`/Properties?page=${page}&pageSize=${pageSize}`),

    search: (query) =>
        api.get(`/Properties/search?q=${encodeURIComponent(query)}`),

    getById: (id) =>
        api.get(`/Properties/${id}`),

    create: (data) =>
        api.post('/Properties', data),
};

export const authApi = {
    loginDemo: (email) =>
        api.post('/Auth/demo-login', { email }),
};

export default api;