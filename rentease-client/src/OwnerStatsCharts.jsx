import { useMemo } from 'react';
import {
    Chart as ChartJS,
    CategoryScale,
    LinearScale,
    BarElement,
    ArcElement,
    Title,
    Tooltip,
    Legend,
} from 'chart.js';
import { Bar, Doughnut } from 'react-chartjs-2';

ChartJS.register(
    CategoryScale,
    LinearScale,
    BarElement,
    ArcElement,
    Title,
    Tooltip,
    Legend
);

export default function OwnerStatsCharts({
                                             ownerProperties = [],
                                             ownerBookings = [],
                                             allProperties = [],
                                         }) {
    const revenueByPropertyData = useMemo(() => {
        const labels = ownerProperties.map((p) =>
            p.title.length > 22 ? p.title.slice(0, 22) + '…' : p.title
        );
        const values = ownerProperties.map((p) =>
            ownerBookings
                .filter((b) => b.propertyId?.toLowerCase() === p.id.toLowerCase())
                .reduce((sum, b) => sum + Number(b.totalPrice || 0), 0)
        );

        return {
            labels: labels.length ? labels : ['Немає об’єктів'],
            datasets: [
                {
                    label: 'Дохід від бронювань (₴)',
                    data: values.length ? values : [0],
                    backgroundColor: 'rgba(37, 99, 235, 0.75)',
                    borderColor: '#1d4ed8',
                    borderWidth: 1,
                    borderRadius: 6,
                },
            ],
        };
    }, [ownerProperties, ownerBookings]);

    const bookingsStatusData = useMemo(() => {
        const counts = { Confirmed: 0, Pending: 0, Cancelled: 0 };
        ownerBookings.forEach((b) => {
            const key = b.status in counts ? b.status : 'Pending';
            counts[key] += 1;
        });

        return {
            labels: ['Підтверджено (Confirmed)', 'Очікує (Pending)', 'Скасовано (Cancelled)'],
            datasets: [
                {
                    data: [counts.Confirmed, counts.Pending, counts.Cancelled],
                    backgroundColor: ['#16a34a', '#f59e0b', '#ef4444'],
                    borderWidth: 1,
                },
            ],
        };
    }, [ownerBookings]);

    const avgPriceByCityData = useMemo(() => {
        const cityMap = new Map();
        allProperties.forEach((p) => {
            const city = p.city || 'Інше';
            const daily = Math.round(Number(p.pricePerMonth || 0) / 30);
            if (!cityMap.has(city)) cityMap.set(city, []);
            cityMap.get(city).push(daily);
        });

        const labels = Array.from(cityMap.keys());
        const averages = labels.map((city) => {
            const arr = cityMap.get(city);
            return Math.round(arr.reduce((a, b) => a + b, 0) / arr.length);
        });

        return {
            labels: labels.length ? labels : ['Немає даних'],
            datasets: [
                {
                    label: 'Середня ціна за добу (₴)',
                    data: averages.length ? averages : [0],
                    backgroundColor: 'rgba(16, 185, 129, 0.75)',
                    borderColor: '#059669',
                    borderWidth: 1,
                    borderRadius: 6,
                },
            ],
        };
    }, [allProperties]);

    return (
        <section className="card">
            <div className="row-between" style={{ marginBottom: '18px' }}>
                <div>
                    <h2 style={{ margin: 0 }}>Статистика та візуалізація (Chart.js)</h2>
                    <p className="muted" style={{ margin: '4px 0 0' }}>
                        Аналітика дохідності ваших об'єктів, статусів бронювань та ринкових цін по містах
                    </p>
                </div>
            </div>

            <div className="charts-grid">
                <div className="chart-box">
                    <h4>Дохід по об'єктах нерухомості (₴)</h4>
                    <Bar
                        data={revenueByPropertyData}
                        options={{
                            responsive: true,
                            plugins: { legend: { display: false } },
                        }}
                    />
                </div>

                <div className="chart-box">
                    <h4>Статуси бронювань</h4>
                    <div style={{ maxWidth: '260px', margin: '0 auto' }}>
                        <Doughnut
                            data={bookingsStatusData}
                            options={{
                                responsive: true,
                                plugins: { legend: { position: 'bottom' } },
                            }}
                        />
                    </div>
                </div>

                <div className="chart-box chart-wide">
                    <h4>Середня вартість оренди за добу по містах (₴)</h4>
                    <Bar
                        data={avgPriceByCityData}
                        options={{
                            responsive: true,
                            plugins: { legend: { display: false } },
                        }}
                    />
                </div>
            </div>
        </section>
    );
}