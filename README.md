# Aygaz E-Commerce AI Agent

Kontrollü C# servisleriyle genişletilen yerel bir e-ticaret Agentic AI geliştirme projesidir. Local Ollama bağlantısı, tamamen sentetik Customer veri katmanı, native tool calling, merkezi Aygaz domain guardrail'i ve read-only Order sorgulama yetenekleri Aşama 1-5 kapsamında tamamlanmıştır.

## Teknolojiler ve gereksinimler

- .NET 9 SDK
- C# console application
- Ollama ve `qwen3:4b-instruct`
- Entity Framework Core 9
- SQLite
- xUnit

Qwen3 4B Instruct, görece hafif olması ve native tool calling desteği nedeniyle seçildi. Bu makinedeki düşük kapasiteli GPU'da Ollama backend hatasını önlemek için `GpuLayers` değeri `0` olarak ayarlanmış, inference CPU üzerinde doğrulanmıştır.

## Kurulum ve çalıştırma

Ollama Windows uygulamasını başlatın. Arka planda çalışmıyorsa ayrı bir terminal kullanın:

```powershell
ollama serve
```

Model yoksa indirin:

```powershell
ollama pull qwen3:4b-instruct
```

Repository kökünde uygulamayı çalıştırın:

```powershell
dotnet run --project .\src\Aygaz.ECommerce.Agent\Aygaz.ECommerce.Agent.csproj
```

Ana menü seçenekleri:

```text
1 - Local LLM Test
2 - Customer Database Test
3 - E-Commerce AI Agent
0 - Exit
```

Customer test menüsünde tüm müşteriler listelenebilir; ID, e-posta veya ad/soyad ile sorgulama yapılabilir. Bu menü yalnızca geliştirme ve test amaçlıdır.

E-Commerce AI Agent menüsünde doğal dilde müşteri ve sipariş sorguları yazılabilir. Her mesaj agent'tan önce domain guardrail tarafından sınıflandırılır. `back` ana menüye döner, `exit` uygulamayı kapatır.

## Aygaz domain guardrail ve scope control

Aşama 4'te eklenen sınır korunur: kullanıcı girdisi doğrudan E-Commerce AI Agent'a ulaşmaz. Önce ayrı ve tool kullanmayan bir classifier tarafından merkezi policy'ye göre değerlendirilir:

```text
User input
   ↓
DomainGuardrailService
   ├── Allowed    → OllamaAgentService → mevcut tool allow-list
   ├── OutOfScope → sabit C# kapsam cevabı
   └── Ambiguous  → sabit C# netleştirme cevabı
```

Ana güvenlik sınırı yalnızca system prompt değildir. `DomainGuardedAgentService`, `OutOfScope`, `Ambiguous`, bozuk classifier cevabı veya classifier hatası durumunda raw agent'ı çağırmaz. Agent prompt'undaki scope hatırlatması yalnız defense-in-depth katmanıdır.

Kararlar:

- `Allowed`: Aygaz e-ticaret isteği, sentetik müşteri/sipariş sorgusu veya kısa selamlama
- `OutOfScope`: başka organizasyonlara ait talepler ya da e-ticaret domain'iyle ilgisiz genel konular
- `Ambiguous`: Aygaz e-ticaret bağlantısı güvenilir biçimde belirlenemeyen talepler

Classifier, kullanıcı girdisini ayrı ve güvenilmeyen `user` mesajı olarak alır. Ollama'ya hiçbir tool gönderilmez; `temperature: 0`, kısa output limiti ve yalnız `Allowed | OutOfScope | Ambiguous` kabul eden JSON schema kullanılır. Parse edilemeyen veya bilinmeyen her cevap `Ambiguous` ile fail-closed sonuçlanır. Guardrail logu yalnız karar ve kapalı reason code içerir; raw kullanıcı mesajını yazmaz.

Scope policy'si `appsettings.json` içindeki `DomainGuardrail` bölümünde merkezidir:

- `Domain`
- `AllowedOrganizations`
- `AllowedCapabilities`
- classifier input/output limitleri

Rakip şirket adlarından oluşan bir blacklist veya keyword router kullanılmaz. Policy ileride yeni organizasyon ve capability değerleriyle genişletilebilir.

Domain ve capability farklı kavramlardır. Customer ve Order sorguları artık desteklenen capability'lerdir. `Aygaz ürün stoklarını göster` ise domain içinde `Allowed` olabilir; Inventory tool'u henüz bulunmadığından agent bu veriyi sağlamadığını söyler ve stok uydurmaz. Başka bir organizasyonun müşteri veya sipariş talebi `OutOfScope` kalır. Domain guardrail konu uygunluğunu, tool allow-list'i ise hangi işlemin yetkili olduğunu bağımsız olarak denetler.

Örnekler:

```text
Allowed:    ahmet.yilmaz@example.com müşterisi kim?
Allowed:    AYG-DEMO-1001 siparişi ne durumda?
Allowed:    Aygaz ürün stoklarını göster.  (capability henüz yok)
OutOfScope: Arçelik hakkında bilgi ver.
OutOfScope: Bugünkü futbol maçlarını anlat.
Ambiguous:  Bunun durumunu kontrol et.
```

MVP'de her kullanıcı mesajı için ayrı guardrail inference çalışır. `Allowed` isteklerde bunu agent inference'ı izlediğinden toplam iki local LLM çağrısı olabilir. Bu bilinçli güvenlik/performans tradeoff'udur; keyword routing ile bypass edilmez.

## E-Commerce AI Agent ve native tool calling

Aşama 3, herhangi bir agent framework kullanmadan Ollama'nın native `POST /api/chat` tool calling sözleşmesini doğrudan uygular. Semantic Kernel, Microsoft Agent Framework, LangChain veya AutoGen kullanılmaz.

Agent'a yalnızca şu altı read-only tool açılır:

Customer tools:

- `get_customer_by_email(email)` — e-posta ile tek müşteri
- `get_customer_by_id(id)` — pozitif ID ile tek müşteri
- `search_customers_by_name(query)` — ad/soyad ile en fazla yapılandırılmış sayıda sonuç

Order tools:

- `get_order_by_number(orderNumber)` — numarayla tek sipariş
- `get_customer_orders(customerId)` — müşteri için en yeni en fazla yapılandırılmış sayıda sipariş
- `get_latest_customer_order(customerId)` — müşterinin en son siparişi

`get_all_customers` ve `get_all_orders` allow-list'te bulunmaz. `GetOrderByIdAsync` service katmanında vardır ancak LLM'e tool olarak açılmaz. Create, update, cancel, delete, refund ve raw SQL tool'ları yoktur. `CustomerToolExecutor` ile `OrderToolExecutor`, exact tool adlarını kullanan `CompositeAgentToolExecutor` altında modüler olarak birleşir; reflection veya kullanıcı metnine dayalı routing yapılmaz.

Akış:

```text
User → Domain Guardrail → Ollama Agent
                              ↓
                 get_customer_by_email
                              ↓
                     Customer ID sonucu
                              ↓
               get_latest_customer_order
                              ↓
              IOrderService → EF Core → SQLite
                              ↓
                  role=tool minimum JSON
                              ↓
                       Türkçe final yanıt
```

Assistant'ın `tool_calls` mesajı ve ardından `role=tool` sonucu doğru sırayla conversation history'ye eklenir. Tool çağrısı kalmayana kadar loop devam eder. Customer → Order zincirini C# seçmez; Ollama ilk tool sonucundaki Customer ID'yi gördükten sonra ikinci tool'u native olarak seçer. Iteration, bir yanıttaki tool sayısı, Order liste limiti ve saklanan tamamlanmış conversation turn sayısı `Agent` ayarlarıyla sınırlandırılır.

LLM'e EF entity veya tam DTO verilmez. `CustomerAgentResult` yalnız `Id`, `FirstName`, `LastName`, `Email`, `City`; `OrderAgentResult` yalnız `Id`, `OrderNumber`, `OrderDate`, Türkçe `Status`, `TotalAmount` içerir. Order sonucunda `CustomerId` tekrarlanmaz. Telefon, CreatedAt, adres, ödeme ve navigation graph hiçbir tool sonucuna dahil edilmez. Tutarın para birimi modelde bulunmadığından agent para birimi uydurmaması için açıkça sınırlandırılır. Tool logları yalnız ad, güvenli argument özeti ve `Success | NotFound | Rejected` durumunu gösterir; ad/soyad arama metni redakte edilir ve sonuç payload'ını yazmaz.

Örnek sorgular:

```text
ahmet.yilmaz@example.com müşterisi kim?
Ahmet Yılmaz isimli müşteriyi bul.
1 numaralı müşteriyi getir.
nobody@example.com müşterisi kim?
AYG-DEMO-1001 numaralı siparişin durumu nedir?
ahmet.yilmaz@example.com müşterisinin siparişlerini getir.
Ahmet Yılmaz'ın son siparişinin durumu nedir?
```

## Customer ve Order database

- Database: SQLite
- Dosya: repository kökündeki `aygaz-ecommerce-v2.db` (yukarıdaki komut kökten çalıştırıldığında)
- Şema: `Customer` ve `CustomerOrder`; Customer 1 → * Orders
- Kısıtlar: zorunlu FK, unique case-insensitive OrderNumber, decimal precision ve negatif olmayan tutar
- Veri: 12 deterministik müşteri ve 24 deterministik sipariş; tamamı sentetik
- Çeşitlilik: tüm beş Order status değeri, birden fazla siparişli müşteriler ve siparişsiz Burak Yıldız
- E-postalar yalnızca `example.com`, telefonlar açıkça test formatındadır
- Sipariş numaraları yalnız `AYG-DEMO-*` test formatındadır
- Service dönüşleri: `CustomerDto` ve `OrderDto`

Database dosyası ile SQLite WAL/journal yan dosyaları Git tarafından ignore edilir. Customer ve Order seedleri ayrı missing-only kontrollerle idempotent çalışır; tekrar başlangıç duplicate üretmez.

Proje hâlâ `EnsureCreatedAsync` kullandığından eski `aygaz-ecommerce.db` şemasını otomatik yükseltmez. Aşama 5, güvenli MVP reset stratejisi olarak yeni versioned `aygaz-ecommerce-v2.db` dosyasını kullanır; eski development DB sessizce silinmez veya değiştirilmez. Eski dosya artık gerekmiyorsa kullanıcı manuel silebilir. Production şema değişiklikleri ileride EF Core migrations ile yönetilmelidir.

Bağlantı dizesi, Ollama ayarları, `Agent` limitleri ve `DomainGuardrail` policy'si `src/Aygaz.ECommerce.Agent/appsettings.json` içinden yönetilir.

## Build ve test

```powershell
dotnet restore .\Aygaz.ECommerce.Agent.sln
dotnet build .\Aygaz.ECommerce.Agent.sln --no-restore
dotnet test .\Aygaz.ECommerce.Agent.sln --no-build --no-restore
```

168 automated test; gerçek SQLite in-memory bağlantısıyla Customer/Order seed idempotency, FK, unique numara, negatif olmayan tutar, status kapsamı ve service sorgularını doğrular. Elle yazılmış fake'ler exact altı-tool allow-list'ini, strict argument validation'ı, output minimization'ı, Customer → Order tool history sırasını, strict classifier parsing'i ve fail-closed davranışı kapsar. Automated testler local Ollama'ya bağımlı değildir; native model seçimi ayrıca gerçek Ollama A-J senaryolarıyla doğrulanmıştır.

## Roadmap

- Aşama 1 — C# + Ollama — **TAMAMLANDI**
- Aşama 2 — Customer Database — **TAMAMLANDI**
- Aşama 3 — Local LLM Tool Calling — **TAMAMLANDI**
- Aşama 4 — Aygaz Domain Guardrails — **TAMAMLANDI**
- Aşama 5 — Orders — **TAMAMLANDI**
- Aşama 6 — Products / Inventory
- Aşama 7 — Sales Analysis
- Aşama 8 — RAG
- Aşama 9 — Authorization / Audit
- Aşama 10 — Web UI
