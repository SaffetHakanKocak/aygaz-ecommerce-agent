# Aygaz E-Commerce AI Agent

Kontrollü C# servisleriyle genişletilen yerel bir e-ticaret Agentic AI geliştirme projesidir. Local Ollama bağlantısı, tamamen sentetik Customer/Order/Product/Inventory/Sales veri katmanı, native tool calling, merkezi Aygaz domain guardrail'i, read-only analitik yetenekleri ve local RAG doküman sorgulama Aşama 1-8 kapsamında tamamlanmıştır.

## Teknolojiler ve gereksinimler

- .NET 9 SDK
- C# console application
- Ollama ve `qwen3:4b-instruct`
- Ollama embedding modeli `nomic-embed-text` (local RAG için)
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
ollama pull nomic-embed-text
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

E-Commerce AI Agent menüsünde doğal dilde müşteri, sipariş, ürün ve stok sorguları yazılabilir. Her mesaj agent'tan önce domain guardrail tarafından sınıflandırılır. `back` ana menüye döner, `exit` uygulamayı kapatır.

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

- `Allowed`: Aygaz e-ticaret isteği, sentetik müşteri/sipariş/ürün/stok sorgusu veya kısa selamlama
- `OutOfScope`: başka organizasyonlara ait talepler ya da e-ticaret domain'iyle ilgisiz genel konular
- `Ambiguous`: Aygaz e-ticaret bağlantısı güvenilir biçimde belirlenemeyen talepler

Classifier, kullanıcı girdisini ayrı ve güvenilmeyen `user` mesajı olarak alır. Ollama'ya hiçbir tool gönderilmez; `temperature: 0`, kısa output limiti ve yalnız `Allowed | OutOfScope | Ambiguous` kabul eden JSON schema kullanılır. Parse edilemeyen veya bilinmeyen her cevap `Ambiguous` ile fail-closed sonuçlanır. Guardrail logu yalnız karar ve kapalı reason code içerir; raw kullanıcı mesajını yazmaz.

Scope policy'si `appsettings.json` içindeki `DomainGuardrail` bölümünde merkezidir:

- `Domain`
- `AllowedOrganizations`
- `AllowedCapabilities`
- classifier input/output limitleri

Rakip şirket adlarından oluşan bir blacklist veya keyword router kullanılmaz. Policy ileride yeni organizasyon ve capability değerleriyle genişletilebilir.

Domain ve capability farklı kavramlardır. Customer, Order, Product ve Inventory sorguları desteklenen read-only capability'lerdir. `Aygaz ürününün stok miktarını artır` domain içinde `Allowed` olabilir; ancak write tool bulunmadığından hiçbir veri değişmez. Başka bir organizasyonun müşteri, sipariş, ürün veya stok talebi `OutOfScope` kalır. Domain guardrail konu uygunluğunu, tool allow-list'i ise hangi işlemin yetkili olduğunu bağımsız olarak denetler.

Örnekler:

```text
Allowed:    ahmet.yilmaz@example.com müşterisi kim?
Allowed:    AYG-DEMO-1001 siparişi ne durumda?
Allowed:    AYG-DEMO-PRD-001 stokta mı?
Allowed:    Demo Product Alpha ürününü bul.
Allowed:    İade süresi kaç gün?
OutOfScope: Arçelik'in iade politikası nedir?
OutOfScope: Ford stoklarını getir.
OutOfScope: Bugünkü futbol maçlarını anlat.
Ambiguous:  Bunun durumunu kontrol et.
```

MVP'de her kullanıcı mesajı için ayrı guardrail inference çalışır. `Allowed` isteklerde bunu agent inference'ı izlediğinden toplam iki local LLM çağrısı olabilir. Bu bilinçli güvenlik/performans tradeoff'udur; keyword routing ile bypass edilmez.

## E-Commerce AI Agent ve native tool calling

Aşama 3, herhangi bir agent framework kullanmadan Ollama'nın native `POST /api/chat` tool calling sözleşmesini doğrudan uygular. Semantic Kernel, Microsoft Agent Framework, LangChain veya AutoGen kullanılmaz.

Agent'a yalnızca şu on dört read-only tool açılır:

Customer tools:

- `get_customer_by_email(email)` — e-posta ile tek müşteri
- `get_customer_by_id(id)` — pozitif ID ile tek müşteri
- `search_customers_by_name(query)` — ad/soyad ile en fazla yapılandırılmış sayıda sonuç

Order tools:

- `get_order_by_number(orderNumber)` — numarayla tek sipariş
- `get_customer_orders(customerId)` — müşteri için en yeni en fazla yapılandırılmış sayıda sipariş
- `get_latest_customer_order(customerId)` — müşterinin en son siparişi

Product tools:

- `get_product_by_sku(sku)` — sentetik SKU ile tek ürün
- `search_products(query)` — ürün adı, SKU veya kategori ile en fazla yapılandırılmış sayıda sonuç

Inventory tools:

- `get_product_inventory(productId)` — ürünün sınırlı lokasyon bazlı stok kayıtları
- `get_total_product_stock(productId)` — ürünün C# servisinde hesaplanan toplam kullanılabilir stoku

Sales Analytics tools:

- `get_sales_summary(fromDate, toDate)` — iptal siparişleri hariç hesaplanmış satış özeti
- `get_top_selling_products(fromDate, toDate, limit)` — en çok satan en fazla 10 ürünün aggregate sonucu
- `get_customer_purchase_summary(customerId, fromDate, toDate)` — müşterinin hesaplanmış alışveriş özeti

Document tool:

- `search_documents(query)` — sentetik demo politika/prosedür dokümanlarında en alakalı en fazla 3 chunk

`get_all_customers`, `get_all_orders`, `get_all_products`, `get_all_inventory`, `get_all_sales`, `get_all_order_items` ve `get_all_documents` allow-list'te bulunmaz. Service-only ID metotları LLM'e tool olarak açılmaz. Create, update, delete, export, price/stock mutation, refund ve raw SQL tool'ları yoktur. Altı domain executor'ı exact tool adlarını kullanan `CompositeAgentToolExecutor` altında modüler olarak birleşir; reflection veya kullanıcı metnine dayalı routing yapılmaz.

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

Product → Inventory zinciri de aynı generic loop'u kullanır:

```text
User → Domain Guardrail → Ollama Agent
                              ↓
                    get_product_by_sku
                              ↓
                       Product ID sonucu
                              ↓
               get_product_inventory veya
                get_total_product_stock
                              ↓
          IInventoryService → EF Core → SQLite
                              ↓
                  role=tool minimum JSON
                              ↓
                       Türkçe final yanıt
```

Customer → Sales zinciri de C# keyword routing olmadan aynı loop'ta yürür:

```text
User → search_customers_by_name → Customer ID
                                  ↓
                   get_customer_purchase_summary
                                  ↓
             SalesAnalyticsService aggregate sonucu
                                  ↓
                         Türkçe final yanıt
```

Customer → Order → Document multi-tool zinciri de C# keyword routing olmadan aynı loop'ta yürür:

```text
User → search_customers_by_name → Customer ID
                                  ↓
                   get_latest_customer_order
                                  ↓
                         search_documents
                                  ↓
              return-policy / delivery-policy chunk
                                  ↓
                         Türkçe final yanıt
```

Örnek: `Ahmet Yılmaz'ın son siparişini kontrol et ve iade politikasını söyle.`

Assistant'ın `tool_calls` mesajı ve ardından `role=tool` sonucu doğru sırayla conversation history'ye eklenir. Tool çağrısı kalmayana kadar loop devam eder. Customer → Order, Product → Inventory, Customer → Sales ve Customer → Order → Document zincirlerini C# seçmez; Ollama ilk lookup sonucundaki ID'yi veya bağlamı gördükten sonra sonraki tool'u native olarak seçer. Iteration, bir yanıttaki tool sayısı, sonuç limitleri ve saklanan tamamlanmış conversation turn sayısı yapılandırmayla sınırlandırılır.

LLM'e EF entity, OrderItem veya tam DTO verilmez. Sales tool'ları ham sipariş satırları yerine C#/EF Core tarafından hesaplanmış `SUM`, `COUNT`, `AVG` ve ürün aggregate sonuçlarını döndürür; `Cancelled` siparişler sorgu tabanında dışlanır. Analitik para alanları `Commerce:CurrencyCode` üzerinden açıkça `TRY` taşır. Sentetik analiz referans tarihi `2026-03-06` olduğundan son 30 gün dahil `2026-02-05..2026-03-06` aralığıdır. Tool logları yalnız ad, güvenli argument özeti ve `Success | NotFound | Rejected` durumunu gösterir; isim sorguları ve müşteri ID'leri redakte edilir, sonuç payload'ı yazılmaz.

Örnek sorgular:

```text
ahmet.yilmaz@example.com müşterisi kim?
Ahmet Yılmaz isimli müşteriyi bul.
1 numaralı müşteriyi getir.
nobody@example.com müşterisi kim?
AYG-DEMO-1001 numaralı siparişin durumu nedir?
ahmet.yilmaz@example.com müşterisinin siparişlerini getir.
Ahmet Yılmaz'ın son siparişinin durumu nedir?
AYG-DEMO-PRD-001 ürününü bul.
Demo Product Alpha stokta mı?
AYG-DEMO-PRD-003 stokta mı?
Son 30 günlük e-ticaret satış özetini getir.
Son 90 günde en çok satılan 5 ürünü göster.
Ahmet Yılmaz son 90 günde ne kadar alışveriş yaptı?
İade süresi kaç gün?
Ahmet Yılmaz'ın son siparişini kontrol et ve iade politikasını söyle.
```

## Local RAG / Doküman Sorgulama

Aşama 8, vector database veya cloud servis kullanmadan yerel doküman sorgulama ekler. Semantic Kernel veya Azure kullanılmaz.

Sentetik demo dokümanlar repository kökündeki `data/demo-documents/` altındadır:

- `return-policy.txt`
- `delivery-policy.txt`
- `campaign-policy.txt`
- `customer-support.txt`

Gerçek Aygaz iç dokümanı kullanılmaz; metinler açıkça sentetik/demo olduğunu belirtir.

Akış:

```text
Documents
   ↓
Text chunks (paragraf bazlı)
   ↓
Ollama POST /api/embed (nomic-embed-text)
   ↓
Memory içindeki vector index
   ↓
Cosine similarity
   ↓
En alakalı en fazla 3 chunk
   ↓
search_documents tool sonucu → LLM → Türkçe cevap
```

`search_documents(query)` yalnızca en alakalı maksimum 3 chunk döndürür; tüm dokümanları modele göndermez. Politika/prosedür sorularında agent kendi hafızasından bilgi üretmemeli; tool sonucu yoksa uydurmamalıdır.

Örnek: `İade süresi kaç gün?` → `search_documents` → demo return policy chunk → `Demo iade politikasına göre süre 14 gündür.`

RAG ayarları `appsettings.json` içindeki `Rag` ve `Ollama:EmbeddingModel` bölümlerinden yönetilir.

## Customer, Order, Product, Inventory ve Sales database

- Database: SQLite
- Dosya: repository kökündeki `aygaz-ecommerce-v4.db` (yukarıdaki komut kökten çalıştırıldığında)
- Şema: `Customer`, `CustomerOrder`, `OrderItem`, `Product`, `InventoryRecord`
- İlişkiler: Customer 1 → * Orders; CustomerOrder 1 → * OrderItems; Product 1 → * OrderItems ve InventoryRecords
- Kısıtlar: zorunlu FK'ler, unique case-insensitive OrderNumber/SKU, unique Order+Product ve Product+Location, decimal precision ve pozitif/negatif olmayan eşikler
- Veri: 12 müşteri, 24 sipariş, 48 order item, 12 ürün ve 16 inventory kaydı; tamamı deterministik ve sentetik
- Çeşitlilik: tüm beş Order status değeri, birden fazla siparişli müşteriler ve siparişsiz Burak Yıldız
- E-postalar yalnızca `example.com`, telefonlar açıkça test formatındadır
- Sipariş numaraları yalnız `AYG-DEMO-*` test formatındadır
- Ürün SKU'ları yalnız `AYG-DEMO-PRD-*`; lokasyonlar yalnız `DEMO-LOC-*` formatındadır
- Ürün adları, kategoriler, fiyatlar, lokasyonlar, stok ve satış tutarları demo verisidir; gerçek Aygaz katalog, fiyat, depo, stok veya satış bilgisi değildir
- Seed; yüksek, düşük ve sıfır stok, çoklu/tek lokasyon, inventory kayıtsız ürün ve inactive ürün senaryolarını içerir
- Satış analitiği yalnız hesaplanmış özet DTO'ları döndürür; ham OrderItem/entity graph dışarı çıkmaz

Database dosyası ile SQLite WAL/journal yan dosyaları Git tarafından ignore edilir. Beş seed grubu ayrı missing-only kontrollerle idempotent çalışır; tekrar başlangıç duplicate üretmez.

Proje hâlâ `EnsureCreatedAsync` kullandığından mevcut şemaları otomatik yükseltmez. Aşama 7, güvenli sentetik MVP stratejisi olarak yeni versioned `aygaz-ecommerce-v4.db` dosyasını kullanır; v1-v3 development DB dosyaları sessizce silinmez veya değiştirilmez. Production şema değişiklikleri ileride EF Core migrations ile yönetilmelidir.

Bağlantı dizesi, Ollama ayarları, `Commerce` currency/tarih/limitleri ve `DomainGuardrail` policy'si `src/Aygaz.ECommerce.Agent/appsettings.json` içinden yönetilir.

## Build ve test

```powershell
dotnet restore .\Aygaz.ECommerce.Agent.sln
dotnet build .\Aygaz.ECommerce.Agent.sln --no-restore
dotnet test .\Aygaz.ECommerce.Agent.sln --no-build --no-restore
```

394 automated test; gerçek SQLite in-memory bağlantısıyla 48 OrderItem seed'ini, ilişkileri, cancelled exclusion'ı ve exact revenue/order/items/average/top-product hesaplarını doğrular. Elle yazılmış fake'ler exact 14-tool allow-list'ini, document chunking/retrieval, strict ISO tarih/limit doğrulamasını, minimum çıktıları, Customer → Sales/Document history sırasını ve fail-closed guardrail'i kapsar. Automated testler local Ollama'ya bağımlı değildir; yalnız üç kritik RAG/agent senaryosu gerçek Ollama ile ayrıca doğrulanmıştır.

## Roadmap

- Aşama 1 — C# + Ollama — **TAMAMLANDI**
- Aşama 2 — Customer Database — **TAMAMLANDI**
- Aşama 3 — Local LLM Tool Calling — **TAMAMLANDI**
- Aşama 4 — Aygaz Domain Guardrails — **TAMAMLANDI**
- Aşama 5 — Orders — **TAMAMLANDI**
- Aşama 6 — Products / Inventory — **TAMAMLANDI**
- Aşama 7 — Sales Analysis — **TAMAMLANDI**
- Aşama 8 — RAG — **TAMAMLANDI**
- Aşama 9 — Authorization / Audit
- Aşama 10 — Web UI
