# Aygaz E-Commerce AI Agent

Kontrollü C# servisleriyle genişletilen yerel bir e-ticaret Agentic AI geliştirme projesidir. Local Ollama bağlantısı, tamamen sentetik Customer/Order/Product/Inventory veri katmanı, native tool calling, merkezi Aygaz domain guardrail'i ve read-only sorgulama yetenekleri Aşama 1-6 kapsamında tamamlanmıştır.

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
OutOfScope: Arçelik hakkında bilgi ver.
OutOfScope: Ford stoklarını getir.
OutOfScope: Bugünkü futbol maçlarını anlat.
Ambiguous:  Bunun durumunu kontrol et.
```

MVP'de her kullanıcı mesajı için ayrı guardrail inference çalışır. `Allowed` isteklerde bunu agent inference'ı izlediğinden toplam iki local LLM çağrısı olabilir. Bu bilinçli güvenlik/performans tradeoff'udur; keyword routing ile bypass edilmez.

## E-Commerce AI Agent ve native tool calling

Aşama 3, herhangi bir agent framework kullanmadan Ollama'nın native `POST /api/chat` tool calling sözleşmesini doğrudan uygular. Semantic Kernel, Microsoft Agent Framework, LangChain veya AutoGen kullanılmaz.

Agent'a yalnızca şu on read-only tool açılır:

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

`get_all_customers`, `get_all_orders`, `get_all_products` ve `get_all_inventory` allow-list'te bulunmaz. Service-only ID metotları LLM'e tool olarak açılmaz. Create, update, delete, price/stock mutation, refund ve raw SQL tool'ları yoktur. Dört domain executor'ı exact tool adlarını kullanan `CompositeAgentToolExecutor` altında modüler olarak birleşir; reflection veya kullanıcı metnine dayalı routing yapılmaz.

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

Assistant'ın `tool_calls` mesajı ve ardından `role=tool` sonucu doğru sırayla conversation history'ye eklenir. Tool çağrısı kalmayana kadar loop devam eder. Customer → Order ve Product → Inventory zincirlerini C# seçmez; Ollama ilk lookup sonucundaki ID'yi gördükten sonra ikinci tool'u native olarak seçer. Iteration, bir yanıttaki tool sayısı, arama/lokasyon limitleri ve saklanan tamamlanmış conversation turn sayısı `Agent` ayarlarıyla sınırlandırılır.

LLM'e EF entity veya tam DTO verilmez. Customer ve Order sonuçları önceki minimum alanlarını korur. `ProductAgentResult` yalnız `Id`, `Sku`, `Name`, `Category`, `UnitPrice`, `IsActive`; lokasyon sonucu yalnız `LocationCode`, `LocationName`, `QuantityAvailable`; toplam stok sonucu yalnız `TotalQuantityAvailable` içerir. Inventory internal ID, ProductId, ReorderLevel, UpdatedAt ve navigation graph dışarı verilmez. Para birimi modelde bulunmadığından agent para birimi uydurmaması için sınırlandırılır. Tool logları yalnız ad, güvenli argument özeti ve `Success | NotFound | Rejected` durumunu gösterir; serbest arama metni ve demo dışı SKU redakte edilir, sonuç payload'ı yazılmaz.

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
```

## Customer, Order, Product ve Inventory database

- Database: SQLite
- Dosya: repository kökündeki `aygaz-ecommerce-v3.db` (yukarıdaki komut kökten çalıştırıldığında)
- Şema: `Customer`, `CustomerOrder`, `Product`, `InventoryRecord`
- İlişkiler: Customer 1 → * Orders; Product 1 → * InventoryRecords
- Kısıtlar: zorunlu FK'ler, unique case-insensitive OrderNumber/SKU, unique Product+Location, decimal precision ve negatif olmayan tutar/miktar eşikleri
- Veri: 12 müşteri, 24 sipariş, 12 ürün ve 16 inventory kaydı; tamamı deterministik ve sentetik
- Çeşitlilik: tüm beş Order status değeri, birden fazla siparişli müşteriler ve siparişsiz Burak Yıldız
- E-postalar yalnızca `example.com`, telefonlar açıkça test formatındadır
- Sipariş numaraları yalnız `AYG-DEMO-*` test formatındadır
- Ürün SKU'ları yalnız `AYG-DEMO-PRD-*`; lokasyonlar yalnız `DEMO-LOC-*` formatındadır
- Ürün adları, kategoriler, fiyatlar, lokasyonlar ve stok miktarları demo verisidir; gerçek Aygaz katalog, fiyat, depo veya stok bilgisi değildir
- Seed; yüksek, düşük ve sıfır stok, çoklu/tek lokasyon, inventory kayıtsız ürün ve inactive ürün senaryolarını içerir
- Service dönüşleri: `CustomerDto`, `OrderDto`, `ProductDto` ve `InventoryDto`

Database dosyası ile SQLite WAL/journal yan dosyaları Git tarafından ignore edilir. Dört seed grubu ayrı missing-only kontrollerle idempotent çalışır; tekrar başlangıç duplicate üretmez.

Proje hâlâ `EnsureCreatedAsync` kullandığından mevcut şemaları otomatik yükseltmez. Aşama 6, güvenli sentetik MVP stratejisi olarak yeni versioned `aygaz-ecommerce-v3.db` dosyasını kullanır; v1/v2 development DB dosyaları sessizce silinmez veya değiştirilmez. Eski dosyalar artık gerekmiyorsa kullanıcı manuel silebilir. Production şema değişiklikleri ileride EF Core migrations ile yönetilmelidir.

Bağlantı dizesi, Ollama ayarları, `Agent` limitleri ve `DomainGuardrail` policy'si `src/Aygaz.ECommerce.Agent/appsettings.json` içinden yönetilir.

## Build ve test

```powershell
dotnet restore .\Aygaz.ECommerce.Agent.sln
dotnet build .\Aygaz.ECommerce.Agent.sln --no-restore
dotnet test .\Aygaz.ECommerce.Agent.sln --no-build --no-restore
```

273 automated test; gerçek SQLite in-memory bağlantısıyla dört seed grubunun idempotency'sini, Product → Inventory ilişkisini, FK/unique/check kısıtlarını, sıfır stok ile inventory kaydı olmaması ayrımını ve tüm service sorgularını doğrular. Elle yazılmış fake'ler exact on-tool allow-list'ini, strict argument validation'ı, output minimization'ı, Customer → Order ve Product → Inventory tool history sıralarını, strict classifier parsing'i ve fail-closed davranışı kapsar. Automated testler local Ollama'ya bağımlı değildir; native model seçimi ayrıca gerçek Ollama A-L senaryolarıyla doğrulanmıştır.

## Roadmap

- Aşama 1 — C# + Ollama — **TAMAMLANDI**
- Aşama 2 — Customer Database — **TAMAMLANDI**
- Aşama 3 — Local LLM Tool Calling — **TAMAMLANDI**
- Aşama 4 — Aygaz Domain Guardrails — **TAMAMLANDI**
- Aşama 5 — Orders — **TAMAMLANDI**
- Aşama 6 — Products / Inventory — **TAMAMLANDI**
- Aşama 7 — Sales Analysis
- Aşama 8 — RAG
- Aşama 9 — Authorization / Audit
- Aşama 10 — Web UI
