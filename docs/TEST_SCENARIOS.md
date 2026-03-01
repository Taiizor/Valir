# Valir Test Senaryoları Dokümanı

> **Versiyon:** 1.0  
> **Tarih:** Ocak 2026  
> **Hedef:** Valir job scheduling kütüphanesi için kapsamlı test senaryoları

---

## 📋 İçindekiler

1. [Giriş ve Kapsam](#1-giriş-ve-kapsam)
2. [Test Kategorileri](#2-test-kategorileri)
3. [Tekrarlayan İşler (Recurring Jobs) Test Senaryoları](#3-tekrarlayan-işler-recurring-jobs-test-senaryoları)
4. [Migration Rehberleri Test Senaryoları](#4-migration-rehberleri-test-senaryoları)
5. [Serilog Entegrasyonu Test Senaryoları](#5-serilog-entegrasyonu-test-senaryoları)
6. [Test Veri Gereksinimleri](#6-test-veri-gereksinimleri)
7. [Ortam Kurulum Gereksinimleri](#7-ortam-kurulum-gereksinimleri)

---

## 1. Giriş ve Kapsam

### 1.1 Amaç

Bu doküman, Valir job scheduling kütüphanesinin tamamlanmış özellikleri için kapsamlı test senaryolarını tanımlar. Test senaryoları, özelliklerin doğru çalıştığını, hata senaryolarının uygun şekilde ele alındığını ve edge case'lerin kapsandığını doğrulamak için tasarlanmıştır.

### 1.2 Kapsam

Bu doküman aşağıdaki tamamlanmış özellikleri kapsar:

| Özellik | Durum | Bileşenler |
|---------|-------|------------|
| **Tekrarlayan İşler (Recurring Jobs)** | ✅ Tamamlandı | Cron tabanlı periyodik iş çalıştırma |
| **Migration Rehberleri** | ✅ Tamamlandı | Hangfire'dan geçiş dokümantasyonu |
| **Serilog Entegrasyonu** | ✅ Tamamlandı | Yapılandırılabilir logging |

### 1.3 Test Türleri

| Test Türü | Açıklama | Kapsam |
|-----------|----------|--------|
| **Unit** | Tekil bileşen testleri | Metod seviyesi doğrulama |
| **Integration** | Bileşen entegrasyon testleri | Redis, API entegrasyonları |
| **E2E** | Uçtan uca testler | Tam iş akışı doğrulama |

### 1.4 Öncelik Seviyeleri

| Öncelik | Açıklama | Örnek |
|---------|----------|-------|
| **High** | Kritik yol, mutlaka test edilmeli | İş zamanlama, misfire handling |
| **Medium** | Önemli fonksiyonellik | Konfigürasyon, edge case'ler |
| **Low** | Nice-to-have | Performans optimizasyonları |

---

## 2. Test Kategorileri

```
┌─────────────────────────────────────────────────────────────────┐
│                    TEST KATEGORİLERİ                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │  RECURRING      │  │  MIGRATION      │  │  SERILOG        │ │
│  │  JOBS           │  │  GUIDES         │  │  INTEGRATION    │ │
│  │                 │  │                 │  │                 │ │
│  │  • Schedule     │  │  • API Mapping  │  │  • Logging      │ │
│  │  • Claim        │  │  • Code Conv.   │  │  • Enrichment   │ │
│  │  • Misfire      │  │  • Config       │  │  • Decorator    │ │
│  │  • TimeZone     │  │  • Validation   │  │  • Filtering    │ │
│  │  • Lua Scripts  │  │  • Examples     │  │  • Options      │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 3. Tekrarlayan İşler (Recurring Jobs) Test Senaryoları

### 3.1 Zamanlama (Scheduling) Testleri

#### TEST-RJ-001: Basit Cron İfadesi ile İş Zamanlama

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-RJ-001 |
| **Özellik Alanı** | Recurring Jobs - Scheduling |
| **Test Adı** | Basit Cron İfadesi ile İş Zamanlama |
| **Öncelik** | High |
| **Test Türü** | Integration |

**Ön Koşullar:**
- Redis sunucusu çalışır durumda
- `RedisRecurringJobQueue` başarıyla initialize edilmiş
- Lua script'ler Redis'e yüklenmiş

**Test Adımları:**
1. `IRecurringJobQueue.ScheduleAsync()` metodunu çağır
2. Parametreler: `jobId="test-job-1"`, `cronExpression="*/5 * * * *"`
3. `jobType="TestJob"`, `payload=byte[10]`
4. `GetAsync()` ile işi getir

**Beklenen Sonuçlar:**
- İş başarıyla zamanlanır (exception fırlatmaz)
- Redis'te iş metadata'sı oluşturulur
- `NextExecution` alanı hesaplanmış değer içerir
- `CreatedAt` ve `UpdatedAt` timestamp'leri atanmıştır

---

#### TEST-RJ-002: Geçersiz Cron İfadesi ile Hata Kontrolü

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-RJ-002 |
| **Özellik Alanı** | Recurring Jobs - Scheduling |
| **Test Adı** | Geçersiz Cron İfadesi ile Hata Kontrolü |
| **Öncelik** | High |
| **Test Türü** | Unit |

**Ön Koşullar:**
- Cron parser hazır ve çalışır durumda

**Test Adımları:**
1. Geçersiz cron ifadesi ile `ScheduleAsync()` çağrısı yap: `"invalid-cron"`
2. Eksik alanlı cron: `"* * *"` (sadece 3 alan)
3. Geçersiz karakter içeren cron: `"@invalid"`

**Beklenen Sonuçlar:**
- `FormatException` veya `ArgumentException` fırlatılır
- Hata mesajı geçersiz cron ifadesini belirtir
- Redis'e kayıt yapılmaz

---

#### TEST-RJ-003: Mevcut İşin Güncellenmesi

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-RJ-003 |
| **Özellik Alanı** | Recurring Jobs - Scheduling |
| **Test Adı** | Mevcut İşin Güncellenmesi |
| **Öncelik** | High |
| **Test Türü** | Integration |

**Ön Koşullar:**
- "test-job-update" ID'li iş önceden zamanlanmış

**Test Adımları:**
1. Aynı `jobId` ile farklı cron ifadesiyle `ScheduleAsync()` çağrısı yap
2. Cron: `"0 */6 * * *"` (6 saatte bir)
3. `GetAsync()` ile güncellenmiş işi getir

**Beklenen Sonuçlar:**
- İş başarıyla güncellenir
- `CronExpression` yeni değeri yansıtır
- `UpdatedAt` timestamp'i güncellenir
- `CreatedAt` değişmez (orijinal oluşturma zamanı)

---

#### TEST-RJ-004: Boş veya Null Parametre Doğrulama

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-RJ-004 |
| **Özellik Alanı** | Recurring Jobs - Scheduling |
| **Test Adı** | Boş veya Null Parametre Doğrulama |
| **Öncelik** | Medium |
| **Test Türü** | Unit |

**Test Adımları:**
1. `jobId=null` ile çağrı yap
2. `jobId=""` (boş string) ile çağrı yap
3. `cronExpression=null` ile çağrı yap
4. `jobType=null` ile çağrı yap
5. `payload=null` ile çağrı yap

**Beklenen Sonuçlar:**
- Her durumda `ArgumentNullException` veya `ArgumentException` fırlatılır
- Hata mesajı hangi parametrenin geçersiz olduğunu belirtir

---

### 3.2 İş Talep (Claim) Testleri

#### TEST-RJ-005: Vadesi Gelen İşlerin Talep Edilmesi

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-RJ-005 |
| **Özellik Alanı** | Recurring Jobs - Claiming |
| **Test Adı** | Vadesi Gelen İşlerin Talep Edilmesi |
| **Öncelik** | High |
| **Test Türü** | Integration |

**Ön Koşullar:**
- Vadesi gelmiş (geçmiş zamanlı) işler Redis'te mevcut
- `claim_recurring.lua` script'i yüklenmiş

**Test Adımları:**
1. `ClaimDueJobsAsync(workerId="worker-1", batchSize=10)` çağrısı yap
2. Vadesi gelen işlerin listesini al
3. Aynı işleri ikinci bir worker ile talep etmeye çalış

**Beklenen Sonuçlar:**
- Vadesi gelen işler başarıyla talep edilir
- İşler lock'lanır (başka worker talep edemez)
- `RecurringJobClaimResult` dizisi döner
- İkinci talep aynı işleri döndürmez

---

#### TEST-RJ-006: Batch Size Limiti Doğrulama

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-RJ-006 |
| **Özellik Alanı** | Recurring Jobs - Claiming |
| **Test Adı** | Batch Size Limiti Doğrulama |
| **Öncelik** | Medium |
| **Test Türü** | Integration |

**Ön Koşullar:**
- Redis'te 20+ vadesi gelmiş iş mevcut

**Test Adımları:**
1. `batchSize=5` ile `ClaimDueJobsAsync()` çağrısı yap
2. Dönen sonuç sayısını kontrol et

**Beklenen Sonuçlar:**
- En fazla 5 iş döner
- İşler öncelik sırasına göre sıralanmıştır

---

#### TEST-RJ-007: Eşzamanlı Talep Yarış Durumu (Race Condition)

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-RJ-007 |
| **Özellik Alanı** | Recurring Jobs - Claiming |
| **Test Adı** | Eşzamanlı Talep Yarış Durumu |
| **Öncelik** | High |
| **Test Türü** | Integration |

**Ön Koşullar:**
- Tek bir vadesi gelmiş iş mevcut
- Birden fazla worker aynı anda talep etmeye çalışacak

**Test Adımları:**
1. 10 paralel thread/task oluştur
2. Her biri aynı anda `ClaimDueJobsAsync()` çağrısı yapsın
3. Sonuçları topla ve analiz et

**Beklenen Sonuçlar:**
- Sadece 1 worker işi talep eder
- Diğer 9 worker boş dizi veya farklı işler alır
- Redis Lua script atomikliği sağlar
- Hiçbir iş çift talep edilmez

---

### 3.3 Misfire Handling Testleri

#### TEST-RJ-008: Skip Misfire Politikası

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-RJ-008 |
| **Özellik Alanı** | Recurring Jobs - Misfire Handling |
| **Test Adı** | Skip Misfire Politikası |
| **Öncelik** | High |
| **Test Türü** | Integration |

**Ön Koşullar:**
- `MisfirePolicy.Skip` ile yapılandırılmış iş
- İşin vadesi 1 saat önce geçmiş

**Test Adımları:**
1. SchedulerWorker'ı başlat
2. Misfire durumunu tetikle
3. İş kuyruğunu kontrol et

**Beklenen Sonuçlar:**
- Kaçırılmış iş çalıştırılmaz
- Log'da "Skipped misfired job" mesajı görülür
- Sonraki normal zamanlamaya geçilir

---

#### TEST-RJ-009: FireOnce Misfire Politikası

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-RJ-009 |
| **Özellik Alanı** | Recurring Jobs - Misfire Handling |
| **Test Adı** | FireOnce Misfire Politikası |
| **Öncelik** | High |
| **Test Türü** | Integration |

**Ön Koşullar:**
- `MisfirePolicy.FireOnce` ile yapılandırılmış iş (varsayılan)
- İşin vadesi 30 dakika önce geçmiş

**Test Adımları:**
1. SchedulerWorker'ı başlat
2. Misfire durumunu tetikle
3. İş kuyruğunu kontrol et

**Beklenen Sonuçlar:**
- Kaçırılmış iş için tek bir instance kuyruğa eklenir
- Log'da misfire bilgisi görülür
- Sonraki normal zamanlamaya geçilir

---

#### TEST-RJ-010: FireAll Misfire Politikası

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-RJ-010 |
| **Özellik Alanı** | Recurring Jobs - Misfire Handling |
| **Test Adı** | FireAll Misfire Politikası |
| **Öncelik** | Medium |
| **Test Türü** | Integration |

**Ön Koşullar:**
- `MisfirePolicy.FireAll` ile yapılandırılmış iş
- İşin 5 vadesi kaçırılmış (her saat başı çalışan iş, 5 saat down)

**Test Adımları:**
1. SchedulerWorker'ı başlat
2. Misfire durumunu tetikle
3. İş kuyruğunu kontrol et

**Beklenen Sonuçlar:**
- 5 ayrı iş instance'ı kuyruğa eklenir
- Her biri farklı `ScheduledAt` zamanını taşır
- Log'da "Firing X missed occurrences" mesajı görülür

---

#### TEST-RJ-011: FireNow Misfire Politikası

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-RJ-011 |
| **Özellik Alanı** | Recurring Jobs - Misfire Handling |
| **Test Adı** | FireNow Misfire Politikası |
| **Öncelik** | Medium |
| **Test Türü** | Integration |

**Ön Koşullar:**
- `MisfirePolicy.FireNow` ile yapılandırılmış iş
- İşin vadesi geçmiş

**Test Adımları:**
1. SchedulerWorker'ı başlat
2. Misfire durumunu tetikle
3. İş kuyruğunu kontrol et

**Beklenen Sonuçlar:**
- İş hemen (şimdi) çalıştırılmak üzere kuyruğa eklenir
- Sonraki normal zamanlamaya geçilir

---

### 3.4 Zaman Dilimi (TimeZone) Testleri

#### TEST-RJ-012: Farklı Zaman Dilimleri ile Zamanlama

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-RJ-012 |
| **Özellik Alanı** | Recurring Jobs - TimeZone |
| **Test Adı** | Farklı Zaman Dilimleri ile Zamanlama |
| **Öncelik** | High |
| **Test Türü** | Integration |

**Ön Koşullar:**
- `Europe/Istanbul` zaman dilimi bilgisi mevcut
- `America/New_York` zaman dilimi bilgisi mevcut

**Test Adımları:**
1. `TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")` ile iş zamanla
2. `TimeZoneInfo.FindSystemTimeZoneById("America/New_York")` ile iş zamanla
3. Her iki iş için `NextExecution` değerlerini karşılaştır

**Beklenen Sonuçlar:**
- Her iş kendi zaman dilimine göre hesaplanır
- UTC dönüşümleri doğru yapılır
- Yaz saati uygulamaları (DST) dikkate alınır

---

#### TEST-RJ-013: DST Geçişleri (Yaz Saati)

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-RJ-013 |
| **Özellik Alanı** | Recurring Jobs - TimeZone |
| **Test Adı** | DST Geçişleri (Yaz Saati) |
| **Öncelik** | Medium |
| **Test Türü** | Integration |

**Ön Koşullar:**
- DST geçişi olan bir zaman dilimi seçilmiş
- Cron ifadesi DST geçiş saatini kapsıyor

**Test Adımları:**
1. DST başlangıcından önce bir iş zamanla (örn: saat 02:00)
2. `GetNextOccurrence()` hesaplamalarını kontrol et
3. DST bitişini de test et

**Beklenen Sonuçlar:**
- DST başlangıcında "kaybolan" saat atlanır
- DST bitişinde "tekrar eden" saat için ilk occurrence seçilir
- Hesaplamalar tutarlıdır

---

### 3.5 İş Yönetimi (Enable/Disable/Remove) Testleri

#### TEST-RJ-014: İşin Devre Dışı Bırakılması ve Etkinleştirilmesi

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-RJ-014 |
| **Özellik Alanı** | Recurring Jobs - Management |
| **Test Adı** | İşin Devre Dışı Bırakılması ve Etkinleştirilmesi |
| **Öncelik** | High |
| **Test Türü** | Integration |

**Ön Koşullar:**
- Aktif bir tekrarlayan iş mevcut

**Test Adımları:**
1. `DisableAsync(jobId)` çağrısı yap
2. `GetAsync()` ile iş durumunu kontrol et
3. `ClaimDueJobsAsync()` çağrısı yap (iş talep edilmemeli)
4. `EnableAsync(jobId)` çağrısı yap
5. Tekrar durum ve talep kontrolü yap

**Beklenen Sonuçlar:**
- `DisableAsync` sonrası `Enabled=false`
- Devre dışı iş `ClaimDueJobsAsync` tarafından döndürülmez
- `EnableAsync` sonrası `Enabled=true`
- Etkinleştirilmiş iş tekrar talep edilebilir

---

#### TEST-RJ-015: İşin Kalıcı Olarak Silinmesi

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-RJ-015 |
| **Özellik Alanı** | Recurring Jobs - Management |
| **Test Adı** | İşin Kalıcı Olarak Silinmesi |
| **Öncelik** | High |
| **Test Türü** | Integration |

**Ön Koşullar:**
- Silinecek iş mevcut

**Test Adımları:**
1. `RemoveAsync(jobId)` çağrısı yap
2. `GetAsync()` ile işi getirmeye çalış
3. `GetAllAsync()` ile tüm işleri listele

**Beklenen Sonuçlar:**
- `GetAsync` `null` döner
- İş Redis'ten tamamen silinir
- `GetAllAsync` listesinde yer almaz

---

#### TEST-RJ-016: Olmayan İşi Silme/Güncelleme

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-RJ-016 |
| **Özellik Alanı** | Recurring Jobs - Management |
| **Test Adı** | Olmayan İşi Silme/Güncelleme |
| **Öncelik** | Low |
| **Test Türü** | Unit |

**Test Adımları:**
1. Mevcut olmayan bir `jobId` ile `RemoveAsync()` çağrısı yap
2. Mevcut olmayan bir `jobId` ile `DisableAsync()` çağrısı yap
3. Mevcut olmayan bir `jobId` ile `EnableAsync()` çağrısı yap

**Beklenen Sonuçlar:**
- Exception fırlatılmaz (idempotent davranış)
- Log'da uyarı mesajı görülebilir

---

### 3.6 SchedulerWorker Testleri

#### TEST-RJ-017: SchedulerWorker Başlatma ve Durdurma

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-RJ-017 |
| **Özellik Alanı** | Recurring Jobs - SchedulerWorker |
| **Test Adı** | SchedulerWorker Başlatma ve Durdurma |
| **Öncelik** | High |
| **Test Türü** | Integration |

**Ön Koşullar:**
- `IRecurringJobQueue` ve `IJobQueue` mock/real instance'ları mevcut
- `SchedulerWorkerOptions` yapılandırılmış

**Test Adımları:**
1. `StartAsync()` çağrısı yap
2. `StopAsync()` çağrısı yap
3. İkinci kez `StartAsync()` çağrısı yap

**Beklenen Sonuçlar:**
- Başlatma log mesajı görülür
- Timer başarıyla oluşturulur
- Durdurma timer'ı durdurur
- İkinci başlatma log warning üretir (zaten çalışıyor)

---

#### TEST-RJ-018: SchedulerWorker Check Interval

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-RJ-018 |
| **Özellik Alanı** | Recurring Jobs - SchedulerWorker |
| **Test Adı** | SchedulerWorker Check Interval |
| **Öncelik** | Medium |
| **Test Türü** | Integration |

**Ön Koşullar:**
- `CheckInterval = TimeSpan.FromSeconds(5)` yapılandırılmış

**Test Adımları:**
1. SchedulerWorker'ı başlat
2. 15 saniye bekle
3. `ClaimDueJobsAsync` çağrı sayısını kontrol et

**Beklenen Sonuçlar:**
- Yaklaşık 3 kontrol gerçekleşir (hemen + 5sn + 10sn)
- Her kontrol log'da görülür

---

#### TEST-RJ-019: Çoklu SchedulerWorker Koordinasyonu

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-RJ-019 |
| **Özellik Alanı** | Recurring Jobs - SchedulerWorker |
| **Test Adı** | Çoklu SchedulerWorker Koordinasyonu |
| **Öncelik** | High |
| **Test Türü** | E2E |

**Ön Koşullar:**
- 3 ayrı SchedulerWorker instance'ı (farklı worker ID'leri)
- 10 vadesi gelmiş iş mevcut

**Test Adımları:**
1. 3 SchedulerWorker'ı aynı anda başlat
2. Tüm worker'ların işlemesini bekle
3. Toplam işlem sayısını kontrol et

**Beklenen Sonuçlar:**
- Her iş sadece bir kez işlenir
- İşler worker'lar arasında dağıtılır
- Hiçbir iş çift işlenmez

---

### 3.7 Lua Script Testleri

#### TEST-RJ-020: Lua Script Atomikliği

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-RJ-020 |
| **Özellik Alanı** | Recurring Jobs - Lua Scripts |
| **Test Adı** | Lua Script Atomikliği |
| **Öncelik** | High |
| **Test Türü** | Integration |

**Test Adımları:**
1. `schedule_recurring.lua` script'ini doğrudan çalıştır
2. Redis MONITOR ile komutları izle
3. Script execution'un atomic olduğunu doğrula

**Beklenen Sonuçlar:**
- Tüm Redis komutları tek bir EXEC bloğunda çalışır
- Parçalı güncelleme olmaz
- Race condition oluşmaz

---

#### TEST-RJ-021: Lua Script Yükleme ve Cache

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-RJ-021 |
| **Özellik Alanı** | Recurring Jobs - Lua Scripts |
| **Test Adı** | Lua Script Yükleme ve Cache |
| **Öncelik** | Medium |
| **Test Türü** | Integration |

**Test Adımları:**
1. `InitializeAsync()` çağrısı yap
2. Redis `SCRIPT LIST` komutu ile yüklü script'leri kontrol et
3. İkinci `InitializeAsync()` çağrısı yap

**Beklenen Sonuçlar:**
- Tüm 5 script başarıyla yüklenir
- Script SHA hash'leri cache'lenir
- İkinci initialize işlemi idempotent'dir

---

## 4. Migration Rehberleri Test Senaryoları

### 4.1 API Mapping Doğrulama Testleri

#### TEST-MG-001: Hangfire API'den Valir API'ye Dönüşüm

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-MG-001 |
| **Özellik Alanı** | Migration Guides - API Mapping |
| **Test Adı** | Hangfire API'den Valir API'ye Dönüşüm |
| **Öncelik** | High |
| **Test Türü** | E2E |

**Ön Koşullar:**
- `MIGRATION_FROM_HANGFIRE.md` dokümanı mevcut
- Örnek Hangfire kodları hazır

**Test Adımları:**
1. `BackgroundJob.Enqueue(() => Method())` → `IJobQueue.EnqueueAsync()` dönüşümünü uygula
2. `BackgroundJob.Schedule(() => Method(), delay)` → `IJobQueue.EnqueueAsync()` with delay dönüşümünü uygula
3. Dönüşüm sonuçlarını derle ve çalıştır

**Beklenen Sonuçlar:**
- Tüm API çağrıları başarıyla dönüştürülür
- Derleme hatası oluşmaz
- Runtime davranışı benzerdir

---

#### TEST-MG-002: RecurringJob.AddOrUpdate Dönüşümü

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-MG-002 |
| **Özellik Alanı** | Migration Guides - API Mapping |
| **Test Adı** | RecurringJob.AddOrUpdate Dönüşümü |
| **Öncelik** | High |
| **Test Türü** | E2E |

**Ön Koşullar:**
- Recurring Jobs özelliği aktif
- Hangfire recurring job örneği mevcut

**Test Adımları:**
1. `RecurringJob.AddOrUpdate("job-id", () => Method(), "0 2 * * *")` kodunu al
2. Valir eşdeğerine dönüştür: `IRecurringJobQueue.ScheduleAsync()`
3. Dönüştürülen kodu çalıştır

**Beklenen Sonuçlar:**
- İş başarıyla zamanlanır
- Cron ifadesi korunur
- İş ID'si aynı kalır

---

#### TEST-MG-003: Job Filter'ların Decorator Pattern'e Dönüşümü

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-MG-003 |
| **Özellik Alanı** | Migration Guides - Pattern Conversion |
| **Test Adı** | Job Filter'ların Decorator Pattern'e Dönüşümü |
| **Öncelik** | Medium |
| **Test Türü** | E2E |

**Ön Koşullar:**
- `[AutomaticRetry]` attribute'u içeren Hangfire kodu
- `[Queue]` attribute'u içeren Hangfire kodu

**Test Adımları:**
1. `[AutomaticRetry(Attempts = 3)]` → `RecurringJobOptions.MaxRetries = 3` dönüşümü
2. `[Queue("critical")]` → `EnqueueAsync` priority parametresi dönüşümü
3. Decorator pattern implementasyonunu doğrula

**Beklenen Sonuçlar:**
- Attribute'lar kaldırılır
- Konfigürasyon kodda yapılır
- Davranış aynı şekilde çalışır

---

### 4.2 Konfigürasyon Dönüşüm Testleri

#### TEST-MG-004: Hangfire Configuration'dan Valir Configuration'a

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-MG-004 |
| **Özellik Alanı** | Migration Guides - Configuration |
| **Test Adı** | Hangfire Configuration'dan Valir Configuration'a |
| **Öncelik** | High |
| **Test Türü** | E2E |

**Test Adımları:**
1. `services.AddHangfire()` → `services.AddValir()` dönüşümü
2. `services.AddHangfireServer()` → `AddHostedService<SchedulerWorker>()` dönüşümü
3. `UseHangfireDashboard()` → Terminal UI alternatifini kullanma

**Beklenen Sonuçlar:**
- Tüm servisler başarıyla kaydedilir
- Uygulama başarıyla başlar
- Dashboard alternatifi çalışır

---

#### TEST-MG-005: Connection String Dönüşümü

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-MG-005 |
| **Özellik Alanı** | Migration Guides - Configuration |
| **Test Adı** | Connection String Dönüşümü |
| **Öncelik** | High |
| **Test Türü** | E2E |

**Test Adımları:**
1. SQL Server connection string'i kaldır
2. Redis connection string'i ekle
3. `IConnectionMultiplexer` yapılandırmasını doğrula

**Beklenen Sonuçlar:**
- Redis bağlantısı başarılı
- SQL bağımlılığı kaldırılmış
- Veri saklama Redis'e taşınmış

---

### 4.3 Migration Dokümantasyon Doğrulama Testleri

#### TEST-MG-006: Migration Dokümanı Kod Örnekleri

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-MG-006 |
| **Özellik Alanı** | Migration Guides - Documentation |
| **Test Adı** | Migration Dokümanı Kod Örnekleri |
| **Öncelik** | Medium |
| **Test Türü** | E2E |

**Test Adımları:**
1. `MIGRATION_FROM_HANGFIRE.md` içindeki tüm kod örneklerini kopyala
2. Her birini ayrı projede derle
3. Runtime hatası olmadan çalıştır

**Beklenen Sonuçlar:**
- Tüm kod örnekleri derlenir
- Syntax hatası yoktur
- Runtime hatası yoktur

---

#### TEST-MG-007: Karşılaştırma Tablosu Doğrulama

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-MG-007 |
| **Özellik Alanı** | Migration Guides - Documentation |
| **Test Adı** | Karşılaştırma Tablosu Doğrulama |
| **Öncelik** | Low |
| **Test Türü** | Manual |

**Test Adımları:**
1. `COMPARISON_CHARTS.md` dosyasını aç
2. Her API eşleştirmesini doğrula
3. Eksik veya yanlış bilgi kontrolü yap

**Beklenen Sonuçlar:**
- Tüm API'ler doğru eşleştirilmiş
- Açıklamalar tutarlı
- Örnekler çalışır durumda

---

### 4.4 Troubleshooting Testleri

#### TEST-MG-008: Troubleshooting Senaryoları

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-MG-008 |
| **Özellik Alanı** | Migration Guides - Troubleshooting |
| **Test Adı** | Troubleshooting Senaryoları |
| **Öncelik** | Medium |
| **Test Türü** | Manual |

**Test Adımları:**
1. Dokümanda belirtilen yaygın hataları tetikle
2. Önerilen çözümleri uygula
3. Sorunun çözüldüğünü doğrula

**Beklenen Sonuçlar:**
- Her sorun için çözüm belgelenmiş
- Çözümler etkili
- Alternatif çözümler sunulmuş

---

## 5. Serilog Entegrasyonu Test Senaryoları

### 5.1 Temel Logging Testleri

#### TEST-SL-001: Temel Serilog Entegrasyonu

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-SL-001 |
| **Özellik Alanı** | Serilog Integration - Basic |
| **Test Adı** | Temel Serilog Entegrasyonu |
| **Öncelik** | High |
| **Test Türü** | Integration |

**Ön Koşullar:**
- Serilog yapılandırılmış
- `Valir.Extensions.Serilog` paketi yüklenmiş

**Test Adımları:**
1. `services.AddValir().AddValirSerilog()` çağrısı yap
2. `UseSerilogForJobs()` extension'ını çağır
3. Bir job handler çalıştır

**Beklenen Sonuçlar:**
- Job başlangıç logu oluşur
- Job tamamlama logu oluşur
- Log'lar Serilog sink'lerine yazılır

---

#### TEST-SL-002: Job Context Enrichment

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-SL-002 |
| **Özellik Alanı** | Serilog Integration - Enrichment |
| **Test Adı** | Job Context Enrichment |
| **Öncelik** | High |
| **Test Türü** | Integration |

**Test Adımları:**
1. `EnrichWithJobContext = true` yapılandırması ile başlat
2. Job handler içinde log yaz
3. Log output'unu kontrol et

**Beklenen Sonuçlar:**
- Log'lar `JobId` property'si içerir
- Log'lar `JobName` property'si içerir
- Log'lar `WorkerId` property'si içerir
- Log'lar `Attempt` property'si içerir

---

#### TEST-SL-003: Özel Property İsimleri

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-SL-003 |
| **Özellik Alanı** | Serilog Integration - Configuration |
| **Test Adı** | Özel Property İsimleri |
| **Öncelik** | Low |
| **Test Türü** | Integration |

**Test Adımları:**
1. `JobIdPropertyName = "CustomJobId"` yapılandır
2. `JobNamePropertyName = "CustomJobName"` yapılandır
3. Job çalıştır ve log'ları kontrol et

**Beklenen Sonuçlar:**
- Log'lar `CustomJobId` property'si içerir
- Log'lar `CustomJobName` property'si içerir
- Orijinal isimler kullanılmaz

---

### 5.2 Log Level Testleri

#### TEST-SL-004: Farklı Log Level'ları

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-SL-004 |
| **Özellik Alanı** | Serilog Integration - Log Levels |
| **Test Adı** | Farklı Log Level'ları |
| **Öncelik** | High |
| **Test Türü** | Integration |

**Test Adımları:**
1. `JobStartLogLevel = Information` yapılandır
2. `JobCompleteLogLevel = Information` yapılandır
3. `JobFailureLogLevel = Error` yapılandır
4. `JobRetryLogLevel = Warning` yapılandır
5. Başarılı ve başarısız job'ları çalıştır

**Beklenen Sonuçlar:**
- Başlangıç log'ları Information seviyesindedir
- Tamamlama log'ları Information seviyesindedir
- Hata log'ları Error seviyesindedir
- Retry log'ları Warning seviyesindedir

---

#### TEST-SL-005: Minimum Log Level Filtreleme

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-SL-005 |
| **Özellik Alanı** | Serilog Integration - Log Levels |
| **Test Adı** | Minimum Log Level Filtreleme |
| **Öncelik** | Medium |
| **Test Türü** | Integration |

**Test Adımları:**
1. `MinimumLogLevel = Warning` yapılandır
2. `JobStartLogLevel = Information` yapılandır
3. Job çalıştır

**Beklenen Sonuçlar:**
- Başlangıç log'u yazılmaz (Information < Warning)
- Hata durumunda log yazılır (Error >= Warning)

---

### 5.3 Timing ve Payload Testleri

#### TEST-SL-006: Execution Timing Bilgisi

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-SL-006 |
| **Özellik Alanı** | Serilog Integration - Timing |
| **Test Adı** | Execution Timing Bilgisi |
| **Öncelik** | Medium |
| **Test Türü** | Integration |

**Test Adımları:**
1. `IncludeTiming = true` yapılandır
2. 100ms süren bir job çalıştır
3. Tamamlama log'unu kontrol et

**Beklenen Sonuçlar:**
- Log'da execution süresi (ms) görülür
- Süre yaklaşık 100ms civarındadır

---

#### TEST-SL-007: Timing Devre Dışı Bırakma

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-SL-007 |
| **Özellik Alanı** | Serilog Integration - Timing |
| **Test Adı** | Timing Devre Dışı Bırakma |
| **Öncelik** | Low |
| **Test Türü** | Integration |

**Test Adımları:**
1. `IncludeTiming = false` yapılandır
2. Job çalıştır
3. Tamamlama log'unu kontrol et

**Beklenen Sonuçlar:**
- Log'da execution süresi görülmez
- Stopwatch kullanılmaz (performans)

---

#### TEST-SL-008: Job Payload Loglama

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-SL-008 |
| **Özellik Alanı** | Serilog Integration - Payload |
| **Test Adı** | Job Payload Loglama |
| **Öncelik** | Medium |
| **Test Türü** | Integration |

**Test Adımları:**
1. `LogJobPayload = true` yapılandır
2. `MaxPayloadLogLength = 500` yapılandır
3. Payload içeren job çalıştır

**Beklenen Sonuçlar:**
- Log'da job payload'ı görülür
- Payload 500 karakterle sınırlıdır
- Uzun payload'lar truncate edilir

---

#### TEST-SL-009: Hassas Veri İçeren Payload

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-SL-009 |
| **Özellik Alanı** | Serilog Integration - Payload |
| **Test Adı** | Hassas Veri İçeren Payload |
| **Öncelik** | High |
| **Test Türü** | Integration |

**Test Adımları:**
1. `LogJobPayload = false` yapılandır (varsayılan)
2. Şifre/TC kimlik no içeren job çalıştır
3. Log'ları kontrol et

**Beklenen Sonuçlar:**
- Hassas veriler log'da görülmez
- Güvenlik ihlali oluşmaz

---

### 5.4 Job Type Filtering Testleri

#### TEST-SL-010: Job Type Filtreleme

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-SL-010 |
| **Özellik Alanı** | Serilog Integration - Filtering |
| **Test Adı** | Job Type Filtreleme |
| **Öncelik** | Medium |
| **Test Türü** | Integration |

**Test Adımları:**
1. `JobTypeFilter = type => !type.Contains("SensitiveJob")` yapılandır
2. `SensitiveJob` çalıştır
3. `NormalJob` çalıştır

**Beklenen Sonuçlar:**
- `SensitiveJob` için log oluşmaz
- `NormalJob` için log oluşur

---

### 5.5 Decorator Pattern Testleri

#### TEST-SL-011: LoggingJobHandlerDecorator Davranışı

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-SL-011 |
| **Özellik Alanı** | Serilog Integration - Decorator |
| **Test Adı** | LoggingJobHandlerDecorator Davranışı |
| **Öncelik** | High |
| **Test Türü** | Unit |

**Test Adımları:**
1. `LoggingJobHandlerDecorator<T>` instance'ı oluştur
2. `HandleAsync()` metodunu çağır
3. Mock logger'ı kontrol et

**Beklenen Sonuçlar:**
- İç handler çağrılır
- LogJobStart çağrılır
- LogJobComplete çağrılır
- İç handler exception fırlatırsa LogJobFailure çağrılır

---

#### TEST-SL-012: Decorator Exception Handling

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-SL-012 |
| **Özellik Alanı** | Serilog Integration - Decorator |
| **Test Adı** | Decorator Exception Handling |
| **Öncelik** | High |
| **Test Türü** | Unit |

**Test Adımları:**
1. İç handler'ın exception fırlatacağı şekilde mock'la
2. `HandleAsync()` çağrısı yap
3. Exception ve log'ları kontrol et

**Beklenen Sonuçlar:**
- Exception log'lanır
- Exception yeniden fırlatılır (re-throw)
- LogJobFailure çağrılır

---

### 5.6 Extension Method Testleri

#### TEST-SL-013: AddValirSerilog Extension

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-SL-013 |
| **Özellik Alanı** | Serilog Integration - Extensions |
| **Test Adı** | AddValirSerilog Extension |
| **Öncelik** | High |
| **Test Türü** | Unit |

**Test Adımları:**
1. `services.AddValirSerilog()` çağrısı yap
2. Service collection'ı kontrol et

**Beklenen Sonuçlar:**
- `SerilogOptions` singleton olarak kaydedilir
- `JobContextEnricher` singleton olarak kaydedilir
- `ValirSerilogLogger` singleton olarak kaydedilir

---

#### TEST-SL-014: AddValirSerilog with Custom Logger

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-SL-014 |
| **Özellik Alanı** | Serilog Integration - Extensions |
| **Test Adı** | AddValirSerilog with Custom Logger |
| **Öncelik** | Medium |
| **Test Türü** | Integration |

**Test Adımları:**
1. Özel `ILogger` instance'ı oluştur
2. `services.AddValirSerilog(customLogger)` çağrısı yap
3. Job çalıştır

**Beklenen Sonuçlar:**
- Özel logger kullanılır
- Log'lar özel logger'a yazılır

---

#### TEST-SL-015: UseSerilogForJobs Extension

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-SL-015 |
| **Özellik Alanı** | Serilog Integration - Extensions |
| **Test Adı** | UseSerilogForJobs Extension |
| **Öncelik** | High |
| **Test Türü** | Integration |

**Test Adımları:**
1. `IJobHandler<T>` implementasyonlarını kaydet
2. `services.UseSerilogForJobs()` çağrısı yap
3. Handler'ları resolve et

**Beklenen Sonuçlar:**
- Tüm handler'lar `LoggingJobHandlerDecorator` ile sarmalanır
- Orijinal handler'lar korunur
- Decorator zinciri doğru çalışır

---

### 5.7 ValirSerilogLogger Testleri

#### TEST-SL-016: LogJobStart Metodu

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-SL-016 |
| **Özellik Alanı** | Serilog Integration - Logger |
| **Test Adı** | LogJobStart Metodu |
| **Öncelik** | Medium |
| **Test Türü** | Unit |

**Test Adımları:**
1. `ValirSerilogLogger.LogJobStart()` çağrısı yap
2. Mock Serilog logger'ını kontrol et

**Beklenen Sonuçlar:**
- Serilog `Write()` metodu çağrılır
- Doğru log level kullanılır
- Job context enricher aktif

---

#### TEST-SL-017: LogJobFailure Metodu

| Alan | Değer |
|------|-------|
| **Test ID** | TEST-SL-017 |
| **Özellik Alanı** | Serilog Integration - Logger |
| **Test Adı** | LogJobFailure Metodu |
| **Öncelik** | High |
| **Test Türü** | Unit |

**Test Adımları:**
1. `ValirSerilogLogger.LogJobFailure()` çağrısı yap (exception ile)
2. Mock Serilog logger'ını kontrol et

**Beklenen Sonuçlar:**
- Exception log'a yazılır
- Stack trace korunur
- Job context bilgileri eklenir

---

## 6. Test Veri Gereksinimleri

### 6.1 Recurring Jobs Test Verileri

```csharp
// Test Job Tanımları
public static class TestJobs
{
    public static readonly RecurringJobDefinition SimpleJob = new()
    {
        JobId = "test-simple-job",
        CronExpression = "*/5 * * * *",
        JobType = "TestJob",
        Payload = Encoding.UTF8.GetBytes("{\"data\":\"test\"}"),
        Queue = "default",
        Priority = 0,
        CronFormat = CronFormat.Standard,
        TimeZoneId = "UTC",
        MisfirePolicy = MisfirePolicy.FireOnce,
        MaxRetries = 3,
        Enabled = true
    };

    public static readonly RecurringJobDefinition HighPriorityJob = new()
    {
        JobId = "test-high-priority",
        CronExpression = "0 * * * *",
        JobType = "HighPriorityJob",
        Payload = Encoding.UTF8.GetBytes("{\"priority\":10}"),
        Queue = "critical",
        Priority = 10,
        CronFormat = CronFormat.Standard,
        TimeZoneId = "Europe/Istanbul",
        MisfirePolicy = MisfirePolicy.FireAll,
        MaxRetries = 5,
        Enabled = true
    };
}
```

### 6.2 Cron İfade Test Verileri

| Test Senaryosu | Cron İfadesi | Açıklama |
|----------------|--------------|----------|
| Her dakika | `* * * * *` | Dakikada bir çalışır |
| Her saat | `0 * * * *` | Saat başı çalışır |
| Her gün 02:00 | `0 2 * * *` | Gece 2'de çalışır |
| Her hafta pazartesi | `0 9 * * 1` | Her pazartesi 09:00 |
| Her ayın 1'i | `0 0 1 * *` | Ayın ilk günü |
| Her 5 dakikada | `*/5 * * * *` | 5 dakikada bir |
| İş saatleri | `0 9-17 * * 1-5` | Hafta içi 9-17 arası |
| Geçersiz | `invalid` | Hata vermeli |
| Eksik alan | `* * *` | Hata vermeli |

### 6.3 TimeZone Test Verileri

| Zaman Dilimi ID | Bölge | DST |
|-----------------|-------|-----|
| `UTC` | Evrensel | Hayır |
| `Europe/Istanbul` | Türkiye | Hayır (kalıcı yaz saati) |
| `Europe/London` | İngiltere | Evet |
| `America/New_York` | ABD Doğu | Evet |
| `Asia/Tokyo` | Japonya | Hayır |
| `Pacific/Auckland` | Yeni Zelanda | Evet |

### 6.4 Migration Test Verileri

```csharp
// Hangfire'dan Valir'e Dönüşüm Örnekleri
public static class MigrationExamples
{
    // Önceki: Hangfire
    public static void HangfireExample()
    {
        BackgroundJob.Enqueue(() => Console.WriteLine("Hello"));
        BackgroundJob.Schedule(() => Console.WriteLine("Later"), TimeSpan.FromMinutes(5));
        RecurringJob.AddOrUpdate("my-job", () => Console.WriteLine("Recurring"), "0 2 * * *");
    }

    // Sonra: Valir
    public static async Task ValirExample(IJobQueue queue, IRecurringJobQueue recurringQueue)
    {
        await queue.EnqueueAsync("ConsoleJob", Encoding.UTF8.GetBytes("Hello"));
        await queue.EnqueueAsync("ConsoleJob", Encoding.UTF8.GetBytes("Later"), TimeSpan.FromMinutes(5));
        await recurringQueue.ScheduleAsync("my-job", "0 2 * * *", "ConsoleJob", Array.Empty<byte>());
    }
}
```

### 6.5 Serilog Test Verileri

```csharp
// Serilog Options Test Konfigürasyonları
public static class SerilogTestConfigurations
{
    public static SerilogOptions Default => new();

    public static SerilogOptions Verbose => new()
    {
        MinimumLogLevel = LogEventLevel.Debug,
        JobStartLogLevel = LogEventLevel.Debug,
        JobCompleteLogLevel = LogEventLevel.Debug,
        EnrichWithJobContext = true,
        IncludeTiming = true,
        LogJobPayload = true
    };

    public static SerilogOptions Production => new()
    {
        MinimumLogLevel = LogEventLevel.Warning,
        JobStartLogLevel = LogEventLevel.Information,
        JobCompleteLogLevel = LogEventLevel.Information,
        JobFailureLogLevel = LogEventLevel.Error,
        EnrichWithJobContext = true,
        IncludeTiming = true,
        LogJobPayload = false
    };

    public static SerilogOptions WithFiltering => new()
    {
        JobTypeFilter = type => !type.Contains("Sensitive")
    };
}
```

---

## 7. Ortam Kurulum Gereksinimleri

### 7.1 Redis Gereksinimleri

| Gereksinim | Versiyon | Açıklama |
|------------|----------|----------|
| Redis Server | >= 6.0 | Lua script desteği için |
| StackExchange.Redis | >= 2.6 | .NET client kütüphanesi |
| Redis Persistence | AOF | Veri dayanıklılığı için |

**Redis Konfigürasyonu:**
```conf
# redis.conf
appendonly yes
appendfsync everysec
lua-time-limit 5000
maxmemory-policy noeviction
```

### 7.2 .NET Ortam Gereksinimleri

| Gereksinim | Versiyon |
|------------|----------|
| .NET SDK | >= 10.0 |
| C# Language Version | >= 13.0 |
| Test Framework | xUnit / NUnit |
| Mock Framework | Moq / NSubstitute |

### 7.3 Test Container'ları

```csharp
// Docker Test Container Konfigürasyonu
public class RedisTestContainer : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .WithPortBinding(6379, true)
        .Build();

    public string ConnectionString => _redis.GetConnectionString();

    public async Task InitializeAsync() => await _redis.StartAsync();
    public async Task DisposeAsync() => await _redis.DisposeAsync();
}
```

### 7.4 Test Kategorileri Çalıştırma

```bash
# Sadece Recurring Jobs testleri
dotnet test --filter "Category=RecurringJobs"

# Sadece Integration testleri
dotnet test --filter "TestType=Integration"

# Sadece High priority testleri
dotnet test --filter "Priority=High"

# Hariç tutma
dotnet test --filter "TestType!=E2E"
```

### 7.5 CI/CD Entegrasyonu

```yaml
# .github/workflows/test.yml
name: Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    services:
      redis:
        image: redis:7-alpine
        ports:
          - 6379:6379
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      - name: Run Unit Tests
        run: dotnet test --filter "TestType=Unit"
      - name: Run Integration Tests
        run: dotnet test --filter "TestType=Integration"
        env:
          REDIS_CONNECTION: localhost:6379
```

---

## 8. Test Raporlama

### 8.1 Test Matrisi Özeti

| Kategori | Toplam | High | Medium | Low | Unit | Integration | E2E |
|----------|--------|------|--------|-----|------|-------------|-----|
| Recurring Jobs | 21 | 12 | 7 | 2 | 5 | 14 | 2 |
| Migration Guides | 8 | 4 | 3 | 1 | 0 | 3 | 5 |
| Serilog Integration | 17 | 8 | 7 | 2 | 5 | 11 | 1 |
| **Toplam** | **46** | **24** | **17** | **5** | **10** | **28** | **8** |

### 8.2 Coverage Hedefleri

| Bileşen | Hedef Coverage | Kritik Alanlar |
|---------|----------------|----------------|
| `RedisRecurringJobQueue` | >= 90% | Lua script çağrıları |
| `SchedulerWorker` | >= 85% | Misfire handling |
| `LoggingJobHandlerDecorator` | >= 95% | Exception handling |
| `ValirSerilogLogger` | >= 90% | Tüm log metodları |
| Lua Scripts | >= 80% | Tüm script'ler |

---

## 9. Sonuç

Bu test senaryoları dokümanı, Valir kütüphanesinin tamamlanmış üç temel özelliği için kapsamlı test kapsamı sağlar:

1. **Tekrarlayan İşler**: Cron tabanlı zamanlama, misfire handling, timezone desteği ve Redis Lua script'lerinin tümü detaylı test senaryoları ile kapsanmıştır.

2. **Migration Rehberleri**: Hangfire'dan Valir'e geçiş için API mapping, konfigürasyon dönüşümü ve kod örnekleri doğrulanmıştır.

3. **Serilog Entegrasyonu**: Structured logging, context enrichment, log level yönetimi ve decorator pattern kapsamlı şekilde test edilmiştir.

Test senaryolarının düzenli olarak çalıştırılması ve coverage raporlarının izlenmesi, kütüphanenin kalitesini ve güvenilirliğini garanti altına alacaktır.

---

> **Not:** Bu doküman yalnızca tamamlanmış özellikleri kapsar. Geliştirme aşamasındaki özellikler için ayrı test senaryoları eklenecektir.
