import { useEffect, useMemo, useState } from 'react';
import {
  Building2,
  Search,
  MapPin,
  BedDouble,
  Maximize2,
  Calendar,
  LogIn,
  LogOut,
  Plus,
  Trash2,
  Sparkles,
  ChevronLeft,
  ChevronRight,
  CheckCircle2,
  AlertCircle,
  UserCheck,
  Briefcase,
  Home,
  User,
  UserPlus,
  ArrowLeft,
  Eye,
  Clock,
} from 'lucide-react';
import { analyticsApi, authApi, bookingsApi, propertiesApi } from './api';
import './App.css';

const STORAGE_KEY = 'rentease_session';

function loadSavedSession() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

function buildMonthDays(year, month, occupiedRanges) {
  const firstDay = new Date(year, month, 1);
  const lastDay = new Date(year, month + 1, 0);
  const startWeekday = (firstDay.getDay() + 6) % 7;
  const days = [];

  for (let i = 0; i < startWeekday; i++) {
    days.push(null);
  }

  for (let d = 1; d <= lastDay.getDate(); d++) {
    const iso = `${year}-${String(month + 1).padStart(2, '0')}-${String(d).padStart(2, '0')}`;
    const isBusy = occupiedRanges.some(
        (r) => iso >= r.startDate && iso < r.endDate
    );
    days.push({ day: d, iso, isBusy });
  }

  return days;
}

export default function App() {
  const [session, setSession] = useState(() => loadSavedSession());

  const [activePage, setActivePage] = useState(() => {
    const saved = loadSavedSession();
    if (!saved) return 'catalog';
    return saved.role === 'Owner' ? 'my-properties' : 'catalog';
  });

  const [detailProperty, setDetailProperty] = useState(null);

  const [authMode, setAuthMode] = useState('login');
  const [selectedRole, setSelectedRole] = useState('Client');
  const [authForm, setAuthForm] = useState({
    fullName: '',
    email: '',
    phoneNumber: '+380671234567',
  });
  const [authSubmitting, setAuthSubmitting] = useState(false);

  const [properties, setProperties] = useState([]);
  const [allPropertiesForOwner, setAllPropertiesForOwner] = useState([]);
  const [pageInfo, setPageInfo] = useState({
    page: 1,
    pageSize: 6,
    totalPages: 1,
    totalCount: 0,
    hasPreviousPage: false,
    hasNextPage: false,
  });
  const [searchQuery, setSearchQuery] = useState('');
  const [isSearching, setIsSearching] = useState(false);
  const [loading, setLoading] = useState(false);

  const [bookingForm, setBookingForm] = useState(() => {
    const tomorrow = new Date(Date.now() + 86400000).toISOString().split('T')[0];
    const nextWeek = new Date(Date.now() + 8 * 86400000).toISOString().split('T')[0];
    return { startDate: tomorrow, endDate: nextWeek };
  });
  const [bookings, setBookings] = useState([]);

  const [newProp, setNewProp] = useState({
    title: '',
    description: '',
    type: 0,
    pricePerMonth: 15000,
    areaSqM: 55,
    roomsCount: 2,
    latitude: 50.4501,
    longitude: 30.5234,
    address: '',
    city: 'Київ',
    imageUrl: 'https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?w=800',
  });

  const [pricingPlan, setPricingPlan] = useState(null);
  const [premiumReport, setPremiumReport] = useState(null);

  const [toast, setToast] = useState(null);
  const notify = (type, text) => {
    setToast({ type, text });
    setTimeout(() => setToast(null), 4500);
  };

  const isAuthenticated = Boolean(session?.token);
  const role = session?.role || 'Guest';

  const saveSession = (authDto, chosenRole) => {
    const nextSession = {
      token: authDto.token,
      userId: authDto.userId,
      email: authDto.email,
      fullName: authDto.fullName,
      role: chosenRole,
    };
    localStorage.setItem('token', authDto.token);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(nextSession));
    setSession(nextSession);
    setPremiumReport(null);
    setActivePage(chosenRole === 'Owner' ? 'my-properties' : 'catalog');
  };

  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem(STORAGE_KEY);
    setSession(null);
    setPremiumReport(null);
    setActivePage('catalog');
    notify('success', 'Ви вийшли з акаунту.');
  };

  const loadCatalog = async (page = 1) => {
    setLoading(true);
    setIsSearching(false);
    try {
      const { data } = await propertiesApi.getAll(page, pageInfo.pageSize);
      setProperties(data.items || []);
      setPageInfo({
        page: data.page,
        pageSize: data.pageSize,
        totalPages: data.totalPages || 1,
        totalCount: data.totalCount || 0,
        hasPreviousPage: data.hasPreviousPage,
        hasNextPage: data.hasNextPage,
      });
    } catch {
      notify('error', 'Не вдалося завантажити каталог оголошень.');
    } finally {
      setLoading(false);
    }
  };

  const loadOwnerProperties = async () => {
    setLoading(true);
    try {
      const { data } = await propertiesApi.getAll(1, 100);
      setAllPropertiesForOwner(data.items || []);
    } catch {
      notify('error', 'Не вдалося завантажити ваші оголошення.');
    } finally {
      setLoading(false);
    }
  };

  const loadBookings = async () => {
    try {
      const { data } = await bookingsApi.getAll();
      setBookings(data || []);
    } catch {
      notify('error', 'Не вдалося завантажити список бронювань.');
    }
  };

  const loadPricingBanner = async () => {
    try {
      const { data } = await analyticsApi.getPricingPlan();
      setPricingPlan(data);
    } catch {
      // ignore
    }
  };

  useEffect(() => {
    loadCatalog(1);
    loadPricingBanner();
  }, []);

  useEffect(() => {
    if (activePage === 'catalog') loadCatalog(1);
    if (activePage === 'property-details') loadBookings();
    if (activePage === 'my-bookings') loadBookings();
    if (activePage === 'my-properties') loadOwnerProperties();
    if (activePage === 'owner-bookings') {
      loadOwnerProperties();
      loadBookings();
    }
  }, [activePage, session?.userId]);

  const openPropertyDetails = (prop) => {
    setDetailProperty(prop);
    setActivePage('property-details');
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const propertyOccupiedRanges = useMemo(() => {
    if (!detailProperty) return [];
    return bookings
        .filter(
            (b) =>
                b.propertyId?.toLowerCase() === detailProperty.id.toLowerCase() &&
                (b.status === 'Confirmed' || b.status === 'Pending')
        )
        .sort((a, b) => a.startDate.localeCompare(b.startDate));
  }, [bookings, detailProperty]);

  const calendarMonths = useMemo(() => {
    const now = new Date();
    const months = [];
    for (let offset = 0; offset < 2; offset++) {
      const d = new Date(now.getFullYear(), now.getMonth() + offset, 1);
      const label = d.toLocaleDateString('uk-UA', { month: 'long', year: 'numeric' });
      months.push({
        label: label.charAt(0).toUpperCase() + label.slice(1),
        days: buildMonthDays(d.getFullYear(), d.getMonth(), propertyOccupiedRanges),
      });
    }
    return months;
  }, [propertyOccupiedRanges]);

  const estimatedBookingPrice = useMemo(() => {
    if (!detailProperty || !bookingForm.startDate || !bookingForm.endDate) return 0;
    const start = new Date(bookingForm.startDate);
    const end = new Date(bookingForm.endDate);
    const diffDays = Math.round((end - start) / 86400000);
    if (diffDays <= 0) return 0;
    return Math.round((detailProperty.pricePerMonth / 30) * diffDays);
  }, [detailProperty, bookingForm]);

  const ownerProperties = useMemo(() => {
    if (!session?.userId) return [];
    return allPropertiesForOwner.filter(
        (p) => p.ownerId?.toLowerCase() === session.userId.toLowerCase()
    );
  }, [allPropertiesForOwner, session?.userId]);

  const tenantBookings = useMemo(() => {
    if (!session?.userId) return [];
    return bookings.filter(
        (b) => b.clientId?.toLowerCase() === session.userId.toLowerCase()
    );
  }, [bookings, session?.userId]);

  const ownerBookings = useMemo(() => {
    const myPropIds = new Set(ownerProperties.map((p) => p.id.toLowerCase()));
    return bookings.filter((b) => myPropIds.has(b.propertyId?.toLowerCase()));
  }, [bookings, ownerProperties]);

  const ownerTotalRevenue = useMemo(
      () => ownerBookings.reduce((sum, b) => sum + Number(b.totalPrice || 0), 0),
      [ownerBookings]
  );

  const handleAuthSubmit = async (e) => {
    e.preventDefault();
    setAuthSubmitting(true);
    try {
      let response;
      if (authMode === 'login') {
        response = await authApi.demoLogin(authForm.email.trim());
      } else {
        response = await authApi.register(
            authForm.fullName.trim(),
            authForm.email.trim(),
            authForm.phoneNumber.trim()
        );
      }
      saveSession(response.data, selectedRole);
      notify(
          'success',
          `Ви увійшли як ${response.data.fullName} (${
              selectedRole === 'Owner' ? 'Власник' : 'Орендар'
          })`
      );
    } catch (err) {
      const msg =
          typeof err.response?.data === 'string'
              ? err.response.data
              : err.response?.data?.message || 'Помилка автентифікації. Перевірте дані.';
      notify('error', msg);
    } finally {
      setAuthSubmitting(false);
    }
  };

  const handleSearch = async (e) => {
    e.preventDefault();
    if (!searchQuery.trim()) {
      loadCatalog(1);
      return;
    }
    setLoading(true);
    setIsSearching(true);
    try {
      const { data } = await propertiesApi.search(searchQuery.trim());
      setProperties(data || []);
    } catch {
      notify('error', 'Помилка пошуку.');
    } finally {
      setLoading(false);
    }
  };

  const handleCreateBooking = async (e) => {
    e.preventDefault();
    if (!isAuthenticated) {
      notify('error', 'Щоб забронювати житло, увійдіть або зареєструйтесь як Орендар.');
      setSelectedRole('Client');
      setActivePage('auth');
      return;
    }
    try {
      await bookingsApi.create({
        propertyId: detailProperty.id,
        clientId: session.userId,
        startDate: bookingForm.startDate,
        endDate: bookingForm.endDate,
      });
      notify('success', 'Бронювання створено! Зайняті дати оновлено.');
      loadBookings();
    } catch (err) {
      const msg =
          typeof err.response?.data === 'string'
              ? err.response.data
              : 'На обрані дати житло вже зайняте або дати некоректні.';
      notify('error', msg);
    }
  };

  const handleCreateProperty = async (e) => {
    e.preventDefault();
    try {
      await propertiesApi.create({
        ownerId: session.userId,
        title: newProp.title,
        description: newProp.description,
        type: Number(newProp.type),
        pricePerMonth: Number(newProp.pricePerMonth),
        areaSqM: Number(newProp.areaSqM),
        roomsCount: Number(newProp.roomsCount),
        latitude: Number(newProp.latitude),
        longitude: Number(newProp.longitude),
        address: newProp.address,
        city: newProp.city,
        images: newProp.imageUrl
            ? [
              {
                fileUrl: newProp.imageUrl,
                fileName: 'cover.jpg',
                contentType: 'image/jpeg',
                fileSizeBytes: 120000,
                isPrimary: true,
              },
            ]
            : [],
      });
      notify('success', 'Нове оголошення опубліковано!');
      setActivePage('my-properties');
      loadOwnerProperties();
    } catch (err) {
      const msg =
          typeof err.response?.data === 'string'
              ? err.response.data
              : 'Не вдалося створити оголошення.';
      notify('error', msg);
    }
  };

  const handleDeleteProperty = async (id) => {
    try {
      await propertiesApi.delete(id);
      notify('success', 'Оголошення видалено.');
      loadOwnerProperties();
    } catch {
      notify('error', 'Не вдалося видалити оголошення.');
    }
  };

  const handleCheckPremium = async () => {
    try {
      const { data } = await analyticsApi.getPremiumReport();
      setPremiumReport(data);
      notify('success', 'Преміум-звіт власника завантажено!');
    } catch {
      setPremiumReport(null);
      notify('error', 'Ендпоінт недоступний (404): прапорець PremiumAnalytics вимкнено.');
    }
  };

  return (
      <div className="app-shell">
        {/* HEADER */}
        <header className="topbar">
          <div
              className="brand"
              onClick={() => setActivePage(role === 'Owner' ? 'my-properties' : 'catalog')}
          >
            <Building2 size={26} />
            <span>RentEase</span>
          </div>

          <nav className="nav-tabs">
            {!isAuthenticated && (
                <button
                    className={activePage === 'catalog' || activePage === 'property-details' ? 'active' : ''}
                    onClick={() => setActivePage('catalog')}
                >
                  <Home size={16} /> Каталог житла
                </button>
            )}

            {isAuthenticated && role === 'Client' && (
                <>
                  <button
                      className={activePage === 'catalog' || activePage === 'property-details' ? 'active' : ''}
                      onClick={() => setActivePage('catalog')}
                  >
                    <Home size={16} /> Усе житло
                  </button>
                  <button
                      className={activePage === 'my-bookings' ? 'active' : ''}
                      onClick={() => setActivePage('my-bookings')}
                  >
                    <Calendar size={16} /> Мої бронювання
                  </button>
                </>
            )}

            {isAuthenticated && role === 'Owner' && (
                <>
                  <button
                      className={activePage === 'my-properties' ? 'active' : ''}
                      onClick={() => setActivePage('my-properties')}
                  >
                    <Building2 size={16} /> Мої оголошення
                  </button>
                  <button
                      className={activePage === 'owner-bookings' ? 'active' : ''}
                      onClick={() => setActivePage('owner-bookings')}
                  >
                    <UserCheck size={16} /> Хто забронював
                  </button>
                  <button
                      className={activePage === 'create' ? 'active' : ''}
                      onClick={() => setActivePage('create')}
                  >
                    <Plus size={16} /> Додати житло
                  </button>
                </>
            )}
          </nav>

          <div className="auth-box">
            {isAuthenticated && role === 'Owner' && (
                <button
                    className="btn-outline"
                    onClick={handleCheckPremium}
                    title="Преміум-аналітика власника (Feature Flag B6)"
                >
                  <Sparkles size={16} /> Premium звіт
                </button>
            )}

            {isAuthenticated ? (
                <div className="user-badge-wrap">
                  <div className="user-info">
                    <span className="user-name">{session.fullName}</span>
                    <span className="user-sub">
                  {session.email} •{' '}
                      <b className={role === 'Owner' ? 'text-owner' : 'text-client'}>
                    {role === 'Owner' ? 'Власник' : 'Орендар'}
                  </b>
                </span>
                  </div>
                  <button className="btn-secondary" onClick={handleLogout}>
                    <LogOut size={16} /> Вийти
                  </button>
                </div>
            ) : (
                <button className="btn-primary" onClick={() => setActivePage('auth')}>
                  <LogIn size={16} /> Увійти / Реєстрація
                </button>
            )}
          </div>
        </header>

        {role !== 'Owner' && pricingPlan?.discountApplied && (
            <div className="promo-banner">
              <Sparkles size={16} />
              <span>
            {pricingPlan.notice} Комісія сервісу зараз:{' '}
                <b>{pricingPlan.effectiveFeePercent}%</b>
          </span>
            </div>
        )}

        {toast && (
            <div className={`toast ${toast.type}`}>
              {toast.type === 'success' ? <CheckCircle2 size={18} /> : <AlertCircle size={18} />}
              <span>{toast.text}</span>
            </div>
        )}

        {isAuthenticated && role === 'Owner' && premiumReport && (
            <section className="container card premium-box">
              <div className="row-between">
                <h3>Преміум-аналітика дохідності (Feature Flag: PremiumAnalytics)</h3>
                <button className="btn-text" onClick={() => setPremiumReport(null)}>
                  Закрити
                </button>
              </div>
              <pre>{JSON.stringify(premiumReport, null, 2)}</pre>
            </section>
        )}

        <main className="container">
          {activePage === 'auth' && !isAuthenticated && (
              <div className="auth-page-wrap">
                <button
                    type="button"
                    className="btn-text"
                    style={{ marginBottom: '12px' }}
                    onClick={() => setActivePage('catalog')}
                >
                  <ArrowLeft size={16} /> Повернутися до каталогу житла
                </button>

                <div className="card auth-card">
                  <div className="auth-tabs">
                    <button
                        type="button"
                        className={authMode === 'login' ? 'active' : ''}
                        onClick={() => setAuthMode('login')}
                    >
                      <LogIn size={16} /> Вхід в акаунт
                    </button>
                    <button
                        type="button"
                        className={authMode === 'register' ? 'active' : ''}
                        onClick={() => setAuthMode('register')}
                    >
                      <UserPlus size={16} /> Реєстрація
                    </button>
                  </div>

                  <form onSubmit={handleAuthSubmit}>
                    <p className="muted" style={{ marginTop: 0 }}>
                      Оберіть, у якій ролі ви хочете працювати в системі:
                    </p>

                    <div className="role-cards">
                      <div
                          className={`role-option ${selectedRole === 'Client' ? 'selected' : ''}`}
                          onClick={() => setSelectedRole('Client')}
                      >
                        <User size={22} />
                        <div>
                          <b>Я Орендар</b>
                          <span>Шукаю та бронюю житло</span>
                        </div>
                      </div>

                      <div
                          className={`role-option ${selectedRole === 'Owner' ? 'selected' : ''}`}
                          onClick={() => setSelectedRole('Owner')}
                      >
                        <Briefcase size={22} />
                        <div>
                          <b>Я Власник</b>
                          <span>Здаю квартири та бачу бронювання</span>
                        </div>
                      </div>
                    </div>

                    {authMode === 'register' && (
                        <>
                          <label>
                            Прізвище та ім'я
                            <input
                                required
                                placeholder="Олександр Коваленко"
                                value={authForm.fullName}
                                onChange={(e) => setAuthForm({ ...authForm, fullName: e.target.value })}
                            />
                          </label>
                          <label>
                            Номер телефону
                            <input
                                placeholder="+380671234567"
                                value={authForm.phoneNumber}
                                onChange={(e) =>
                                    setAuthForm({ ...authForm, phoneNumber: e.target.value })
                                }
                            />
                          </label>
                        </>
                    )}

                    <label>
                      Email
                      <input
                          type="email"
                          required
                          placeholder="user@example.com"
                          value={authForm.email}
                          onChange={(e) => setAuthForm({ ...authForm, email: e.target.value })}
                      />
                    </label>

                    <button
                        type="submit"
                        className="btn-primary"
                        style={{ width: '100%', marginTop: '8px' }}
                        disabled={authSubmitting}
                    >
                      {authSubmitting
                          ? 'Зачекайте...'
                          : authMode === 'login'
                              ? `Увійти як ${selectedRole === 'Owner' ? 'Власник' : 'Орендар'}`
                              : `Зареєструватися як ${selectedRole === 'Owner' ? 'Власник' : 'Орендар'}`}
                    </button>
                  </form>
                </div>
              </div>
          )}

          {/* ==================== КАТАЛОГ ЖИТЛА ==================== */}
          {activePage === 'catalog' && role !== 'Owner' && (
              <>
                {!isAuthenticated && (
                    <div className="guest-notice">
                <span>
                  Ви переглядаєте каталог у режимі <b>Гостя</b>. Натисніть на будь-яку квартиру,
                  щоб переглянути вільні та зайняті дати.
                </span>
                      <button className="btn-primary" onClick={() => setActivePage('auth')}>
                        Увійти / Реєстрація
                      </button>
                    </div>
                )}

                <form className="search-bar" onSubmit={handleSearch}>
                  <Search size={18} />
                  <input
                      type="text"
                      placeholder="Пошук житла за назвою, містом або адресою..."
                      value={searchQuery}
                      onChange={(e) => setSearchQuery(e.target.value)}
                  />
                  <button type="submit" className="btn-primary">
                    Знайти
                  </button>
                  {isSearching && (
                      <button
                          type="button"
                          className="btn-secondary"
                          onClick={() => {
                            setSearchQuery('');
                            loadCatalog(1);
                          }}
                      >
                        Скинути
                      </button>
                  )}
                </form>

                {loading ? (
                    <p className="muted">Завантаження доступного житла...</p>
                ) : properties.length === 0 ? (
                    <div className="card empty">Наразі доступних оголошень не знайдено.</div>
                ) : (
                    <div className="grid">
                      {properties.map((p) => {
                        const cover =
                            p.images?.[0]?.fileUrl ||
                            'https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?w=800';
                        return (
                            <article key={p.id} className="card prop-card">
                              <img
                                  src={cover}
                                  alt={p.title}
                                  className="prop-img clickable"
                                  onClick={() => openPropertyDetails(p)}
                              />
                              <div className="prop-body">
                                <div className="row-between">
                                  <span className="badge">{p.city}</span>
                                  <span className="price">{Math.round(p.pricePerMonth / 30)} ₴/доба</span>
                                </div>
                                <h3 className="clickable-title" onClick={() => openPropertyDetails(p)}>
                                  {p.title}
                                </h3>
                                <p className="muted desc">{p.description}</p>
                                <div className="meta">
                          <span>
                            <MapPin size={14} /> {p.address}
                          </span>
                                  <span>
                            <BedDouble size={14} /> {p.roomsCount} кімн.
                          </span>
                                  <span>
                            <Maximize2 size={14} /> {p.areaSqM} м²
                          </span>
                                </div>
                                <div className="card-actions">
                                  <button
                                      className="btn-primary full"
                                      onClick={() => openPropertyDetails(p)}
                                  >
                                    <Eye size={16} /> Переглянути дати та забронювати
                                  </button>
                                </div>
                              </div>
                            </article>
                        );
                      })}
                    </div>
                )}

                {!isSearching && pageInfo.totalPages > 1 && (
                    <div className="pagination">
                      <button
                          className="btn-secondary"
                          disabled={!pageInfo.hasPreviousPage}
                          onClick={() => loadCatalog(pageInfo.page - 1)}
                      >
                        <ChevronLeft size={16} /> Назад
                      </button>
                      <span>
                  Сторінка <b>{pageInfo.page}</b> з <b>{pageInfo.totalPages}</b> (всього:{' '}
                        {pageInfo.totalCount})
                </span>
                      <button
                          className="btn-secondary"
                          disabled={!pageInfo.hasNextPage}
                          onClick={() => loadCatalog(pageInfo.page + 1)}
                      >
                        Вперед <ChevronRight size={16} />
                      </button>
                    </div>
                )}
              </>
          )}

          {activePage === 'property-details' && detailProperty && (
              <div>
                <button
                    type="button"
                    className="btn-text"
                    style={{ marginBottom: '14px' }}
                    onClick={() => setActivePage('catalog')}
                >
                  <ArrowLeft size={16} /> Назад до каталогу житла
                </button>

                <div className="details-layout">
                  <div className="details-main">
                    <div className="card" style={{ marginBottom: '20px' }}>
                      <img
                          src={
                              detailProperty.images?.[0]?.fileUrl ||
                              'https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?w=1000'
                          }
                          alt={detailProperty.title}
                          className="details-hero-img"
                      />
                      <div style={{ padding: '24px' }}>
                        <div className="row-between">
                          <span className="badge">{detailProperty.city}</span>
                          <span className="price" style={{ fontSize: '1.25rem' }}>
                        {Math.round(detailProperty.pricePerMonth / 30)} ₴ / доба
                      </span>
                        </div>
                        <h1 style={{ margin: '12px 0 8px', fontSize: '1.6rem' }}>
                          {detailProperty.title}
                        </h1>
                        <div className="meta" style={{ fontSize: '0.95rem' }}>
                      <span>
                        <MapPin size={16} /> {detailProperty.address}
                      </span>
                          <span>
                        <BedDouble size={16} /> {detailProperty.roomsCount} кімн.
                      </span>
                          <span>
                        <Maximize2 size={16} /> {detailProperty.areaSqM} м²
                      </span>
                        </div>
                        <p style={{ lineHeight: 1.6, color: '#334155', marginTop: '16px' }}>
                          {detailProperty.description}
                        </p>
                      </div>
                    </div>

                    <section className="card">
                      <div className="row-between" style={{ marginBottom: '12px' }}>
                        <h2 style={{ margin: 0, fontSize: '1.2rem' }}>
                          <Calendar size={18} style={{ verticalAlign: 'middle' }} /> Календар
                          зайнятості житла
                        </h2>
                        <div className="legend">
                      <span className="legend-item">
                        <i className="dot free" /> Вільно
                      </span>
                          <span className="legend-item">
                        <i className="dot busy" /> Зайнято
                      </span>
                        </div>
                      </div>

                      <div className="calendars-grid">
                        {calendarMonths.map((m) => (
                            <div key={m.label} className="month-box">
                              <h4>{m.label}</h4>
                              <div className="weekdays">
                                <span>Пн</span>
                                <span>Вт</span>
                                <span>Ср</span>
                                <span>Чт</span>
                                <span>Пт</span>
                                <span>Сб</span>
                                <span>Нд</span>
                              </div>
                              <div className="days-grid">
                                {m.days.map((item, idx) =>
                                        item === null ? (
                                            <span key={`empty-${idx}`} />
                                        ) : (
                                            <span
                                                key={item.iso}
                                                className={`day-cell ${item.isBusy ? 'busy' : 'free'}`}
                                                title={
                                                  item.isBusy
                                                      ? `${item.iso} — Зайнято`
                                                      : `${item.iso} — Вільно для бронювання`
                                                }
                                            >
                                {item.day}
                              </span>
                                        )
                                )}
                              </div>
                            </div>
                        ))}
                      </div>

                      <div style={{ marginTop: '20px' }}>
                        <h4 style={{ margin: '0 0 10px' }}>Заброньовані періоди:</h4>
                        {propertyOccupiedRanges.length === 0 ? (
                            <p className="muted" style={{ margin: 0 }}>
                              На найближчий час бронювань немає — усі дати вільні!
                            </p>
                        ) : (
                            <div className="occupied-list">
                              {propertyOccupiedRanges.map((b) => (
                                  <div key={b.id} className="occupied-item">
                                    <Clock size={15} />
                                    <span>
                              <b>{b.startDate}</b> — <b>{b.endDate}</b>
                            </span>
                                    <span className="busy-tag">Зайнято ({b.status})</span>
                                  </div>
                              ))}
                            </div>
                        )}
                      </div>
                    </section>
                  </div>

                  <aside className="details-sidebar">
                    <form className="card modal" style={{ maxWidth: '100%' }} onSubmit={handleCreateBooking}>
                      <h3 style={{ marginTop: 0 }}>Забронювати житло</h3>
                      <p className="muted">
                        Ціна:{' '}
                        <b>{Math.round(detailProperty.pricePerMonth / 30)} ₴ / доба</b>
                      </p>

                      <label>
                        Дата заїзду
                        <input
                            type="date"
                            required
                            value={bookingForm.startDate}
                            onChange={(e) =>
                                setBookingForm({ ...bookingForm, startDate: e.target.value })
                            }
                        />
                      </label>

                      <label>
                        Дата виїзду
                        <input
                            type="date"
                            required
                            value={bookingForm.endDate}
                            onChange={(e) =>
                                setBookingForm({ ...bookingForm, endDate: e.target.value })
                            }
                        />
                      </label>

                      {estimatedBookingPrice > 0 && (
                          <div className="price-summary">
                            <span>Разом до сплати:</span>
                            <b>{estimatedBookingPrice} ₴</b>
                          </div>
                      )}

                      {!isAuthenticated ? (
                          <button
                              type="button"
                              className="btn-primary"
                              style={{ width: '100%', marginTop: '12px' }}
                              onClick={() => {
                                setSelectedRole('Client');
                                setActivePage('auth');
                              }}
                          >
                            Увійдіть, щоб забронювати
                          </button>
                      ) : session?.userId &&
                      detailProperty.ownerId?.toLowerCase() === session.userId.toLowerCase() ? (
                          <button
                              type="button"
                              className="btn-secondary"
                              style={{ width: '100%', marginTop: '12px' }}
                              disabled
                          >
                            Це ваше власне оголошення
                          </button>
                      ) : (
                          <button
                              type="submit"
                              className="btn-primary"
                              style={{ width: '100%', marginTop: '12px' }}
                          >
                            Підтвердити бронювання
                          </button>
                      )}
                    </form>
                  </aside>
                </div>
              </div>
          )}

          {isAuthenticated && role === 'Client' && activePage === 'my-bookings' && (
              <section className="card">
                <h2>Мої бронювання</h2>
                <p className="muted">
                  Заявки на оренду, оформлені з акаунту <b>{session.fullName}</b> ({session.email})
                </p>
                {tenantBookings.length === 0 ? (
                    <p className="muted">Ви ще не створювали бронювань.</p>
                ) : (
                    <div className="table-wrap">
                      <table>
                        <thead>
                        <tr>
                          <th>Об'єкт нерухомості</th>
                          <th>Дати оренди</th>
                          <th>Сума до сплати</th>
                          <th>Статус</th>
                        </tr>
                        </thead>
                        <tbody>
                        {tenantBookings.map((b) => (
                            <tr key={b.id}>
                              <td>
                                <b>{b.propertyTitle}</b>
                              </td>
                              <td>
                                {b.startDate} — {b.endDate}
                              </td>
                              <td>{b.totalPrice} ₴</td>
                              <td>
                                <span className="badge">{b.status}</span>
                              </td>
                            </tr>
                        ))}
                        </tbody>
                      </table>
                    </div>
                )}
              </section>
          )}

          {isAuthenticated && role === 'Owner' && activePage === 'my-properties' && (
              <>
                <div className="row-between" style={{ marginBottom: '18px' }}>
                  <div>
                    <h2 style={{ margin: 0 }}>Мої об'єкти нерухомості</h2>
                    <p className="muted" style={{ margin: '4px 0 0' }}>
                      Власник: <b>{session.fullName}</b> ({session.email})
                    </p>
                  </div>
                  <button className="btn-primary" onClick={() => setActivePage('create')}>
                    <Plus size={16} /> Нове оголошення
                  </button>
                </div>

                {loading ? (
                    <p className="muted">Завантаження ваших об'єктів...</p>
                ) : ownerProperties.length === 0 ? (
                    <div className="card empty">
                      У вашому профілі ще немає доданих оголошень. Натисніть «Нове оголошення», щоб
                      опублікувати першу квартиру.
                    </div>
                ) : (
                    <div className="grid">
                      {ownerProperties.map((p) => {
                        const cover =
                            p.images?.[0]?.fileUrl ||
                            'https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?w=800';
                        return (
                            <article key={p.id} className="card prop-card">
                              <img src={cover} alt={p.title} className="prop-img" />
                              <div className="prop-body">
                                <div className="row-between">
                                  <span className="badge">{p.city}</span>
                                  <span className="price">{Math.round(p.pricePerMonth / 30)} ₴/доба</span>
                                </div>
                                <h3>{p.title}</h3>
                                <p className="muted desc">{p.description}</p>
                                <div className="meta">
                          <span>
                            <MapPin size={14} /> {p.address}
                          </span>
                                  <span>
                            <BedDouble size={14} /> {p.roomsCount} кімн.
                          </span>
                                  <span>
                            <Maximize2 size={14} /> {p.areaSqM} м²
                          </span>
                                </div>
                                <div className="card-actions">
                                  <button
                                      className="btn-danger full"
                                      onClick={() => handleDeleteProperty(p.id)}
                                  >
                                    <Trash2 size={16} /> Зняти з публікації
                                  </button>
                                </div>
                              </div>
                            </article>
                        );
                      })}
                    </div>
                )}
              </>
          )}

          {isAuthenticated && role === 'Owner' && activePage === 'owner-bookings' && (
              <section className="card">
                <div className="row-between" style={{ marginBottom: '16px' }}>
                  <div>
                    <h2 style={{ margin: 0 }}>Бронювання мого житла</h2>
                    <p className="muted" style={{ margin: '4px 0 0' }}>
                      Список орендарів, які забронювали ваші квартири
                    </p>
                  </div>
                  <div className="revenue-box">
                    Очікуваний дохід: <b>{ownerTotalRevenue.toFixed(2)} ₴</b>
                  </div>
                </div>

                {ownerBookings.length === 0 ? (
                    <p className="muted">На ваші об'єкти ще немає заявок на бронювання.</p>
                ) : (
                    <div className="table-wrap">
                      <table>
                        <thead>
                        <tr>
                          <th>Квартира</th>
                          <th>Орендар</th>
                          <th>Період оренди</th>
                          <th>Сума</th>
                          <th>Статус</th>
                        </tr>
                        </thead>
                        <tbody>
                        {ownerBookings.map((b) => (
                            <tr key={b.id}>
                              <td>
                                <b>{b.propertyTitle}</b>
                              </td>
                              <td>{b.clientName || b.clientId}</td>
                              <td>
                                {b.startDate} — {b.endDate}
                              </td>
                              <td>
                                <b>{b.totalPrice} ₴</b>
                              </td>
                              <td>
                                <span className="badge">{b.status}</span>
                              </td>
                            </tr>
                        ))}
                        </tbody>
                      </table>
                    </div>
                )}
              </section>
          )}

          {isAuthenticated && role === 'Owner' && activePage === 'create' && (
              <form className="card form-card" onSubmit={handleCreateProperty}>
                <h2>Опублікувати нове житло</h2>
                <p className="muted">Власник: {session.fullName}</p>
                <label>
                  Назва оголошення
                  <input
                      required
                      value={newProp.title}
                      onChange={(e) => setNewProp({ ...newProp, title: e.target.value })}
                      placeholder="Сучасна 2-кімнатна квартира в центрі"
                  />
                </label>
                <label>
                  Опис
                  <textarea
                      required
                      value={newProp.description}
                      onChange={(e) => setNewProp({ ...newProp, description: e.target.value })}
                  />
                </label>
                <div className="grid-2">
                  <label>
                    Місто
                    <input
                        required
                        value={newProp.city}
                        onChange={(e) => setNewProp({ ...newProp, city: e.target.value })}
                    />
                  </label>
                  <label>
                    Адреса
                    <input
                        required
                        value={newProp.address}
                        onChange={(e) => setNewProp({ ...newProp, address: e.target.value })}
                    />
                  </label>
                  <label>
                    Ціна за місяць (₴)
                    <input
                        type="number"
                        required
                        value={newProp.pricePerMonth}
                        onChange={(e) => setNewProp({ ...newProp, pricePerMonth: e.target.value })}
                    />
                  </label>
                  <label>
                    Площа (м²)
                    <input
                        type="number"
                        required
                        value={newProp.areaSqM}
                        onChange={(e) => setNewProp({ ...newProp, areaSqM: e.target.value })}
                    />
                  </label>
                  <label>
                    Кількість кімнат
                    <input
                        type="number"
                        required
                        value={newProp.roomsCount}
                        onChange={(e) => setNewProp({ ...newProp, roomsCount: e.target.value })}
                    />
                  </label>
                  <label>
                    URL фото
                    <input
                        value={newProp.imageUrl}
                        onChange={(e) => setNewProp({ ...newProp, imageUrl: e.target.value })}
                    />
                  </label>
                </div>
                <button type="submit" className="btn-primary">
                  Опублікувати оголошення
                </button>
              </form>
          )}
        </main>
      </div>
  );
}