# Aygaz E-Commerce AI Agent

Yerel veri katmani, guardrail'ler, RAG dokuman arama ve Semantic Kernel tabanli cok ajanli sohbet akisi iceren bir C#/.NET e-ticaret AI agent demosu.

Proje tamamen demo/sentetik veriyle calisir. Gercek Aygaz musteri, siparis, urun, stok, fiyat veya politika verisi kullanilmaz.

## Ozellikler

- .NET 9 ile console app, ASP.NET Core Web API ve vanilla Chat UI
- Semantic Kernel uzerinden customer, order, product, inventory, sales ve support-policy ajanlari
- Merkezi Aygaz domain guardrail'i ve capability tabanli routing
- MongoDB varsayilan veri katmani; SQLite ve SQL Server icin Dapper tabanli alternatif
- Siparis durumu guncelleme ve iptal akislarinda kontrollu operasyon servisleri
- MongoDB destekli kalici RAG index'i ve opsiyonel local in-memory RAG
- Ollama veya Groq/OpenAI uyumlu Semantic Kernel provider secimi
- Lexical embedding varsayilani; istenirse Ollama `nomic-embed-text`
- AI provider gecici hata siniflandirma, retry ve deterministik fast-path fallback'leri
- xUnit testleriyle veri erisimi, routing, guardrail, RAG ve web API sozlesmeleri

## Teknoloji Gereksinimleri

- .NET 9 SDK
- MongoDB (`localhost:27017` varsayilan)
- Opsiyonel: Ollama
- Opsiyonel: SQLite veya SQL Server
- xUnit test runner

Ollama ile calismak icin onerilen modeller:

```powershell
ollama pull qwen3:4b-instruct
ollama pull nomic-embed-text
```

Groq veya OpenAI kullanilacaksa ilgili API anahtari ortam degiskeni olarak verilmelidir:

```powershell
$env:GROQ_API_KEY = "..."
$env:OPENAI_API_KEY = "..."
```

## Hizli Baslangic

Repository kok dizininde restore, build ve test:

```powershell
dotnet restore .\Aygaz.ECommerce.Agent.sln
dotnet build .\Aygaz.ECommerce.Agent.sln --no-restore
dotnet test .\Aygaz.ECommerce.Agent.sln --no-build --no-restore
```

Ana Semantic Kernel Web Chat demosu:

```powershell
dotnet run --project .\src\Aygaz.ECommerce.SemanticKernel.Web\Aygaz.ECommerce.SemanticKernel.Web.csproj
```

Tarayici:

```text
http://localhost:5190
```

MongoDB demo verisini ilk kurulumda veya yenilemek istediginizde:

```powershell
dotnet run --project .\src\Aygaz.ECommerce.SemanticKernel.Web\Aygaz.ECommerce.SemanticKernel.Web.csproj -- --seed-mongodb
```

RAG dokumanlarini manuel yeniden indexlemek icin:

```powershell
dotnet run --project .\src\Aygaz.ECommerce.SemanticKernel.Web\Aygaz.ECommerce.SemanticKernel.Web.csproj -- --ingest-rag
```

Console demo:

```powershell
dotnet run --project .\src\Aygaz.ECommerce.Agent\Aygaz.ECommerce.Agent.csproj
```

## Proje Yapisi

```text
src/
  Aygaz.AgentFramework/                  Agent kayit, calistirma, telemetry ve retry altyapisi
  Aygaz.ECommerce.Agent/                 Veri erisimi, servisler, tools, guardrail ve RAG
  Aygaz.ECommerce.SemanticKernel/        Semantic Kernel ajanlari, plugin'ler ve chat orchestration
  Aygaz.ECommerce.SemanticKernel.Web/    Ana Web API + Chat UI demosu
  Aygaz.ECommerce.Web/                   Ollama/native agent Web API alternatifi
  Aygaz.ECommerce.SemanticKernel.Demo/   Minimal Semantic Kernel console demosu
tests/
  *.Tests/                               Unit ve API sozlesme testleri
data/
  demo-documents/                        Sentetik destek politikasi dokumanlari
```

## Konfigurasyon

Varsayilan ayarlar `appsettings.json` dosyalarindadir. En onemli bolumler:

```json
{
  "DataAccess": {
    "Provider": "MongoDb",
    "MongoDb": {
      "ConnectionString": "mongodb://localhost:27017",
      "DatabaseName": "aygaz_ecommerce_demo"
    }
  },
  "SemanticKernel": {
    "Provider": "Groq",
    "ModelId": "openai/gpt-oss-20b",
    "Endpoint": "https://api.groq.com/openai/v1"
  },
  "Rag": {
    "StoreProvider": "MongoDb",
    "EmbeddingProvider": "Lexical",
    "DocumentsPath": "data/demo-documents",
    "MaxRetrievalResults": 3,
    "MaxQueryLength": 500,
    "MinimumSimilarityScore": 0.15,
    "AutoIngestOnStartup": true
  }
}
```

`DataAccess:Provider` degeri `MongoDb`, `Sqlite` veya `SqlServer` olabilir. MongoDB varsayilandir.

`Rag:StoreProvider`:

- `MongoDb`: dokuman chunk ve embedding'lerini MongoDB'de kalici tutar.
- `Local`: dokumanlari uygulama icinde in-memory indexler.

`Rag:EmbeddingProvider`:

- `Lexical`: ek model gerektirmeyen deterministik lexical embedding.
- `Ollama`: `Ollama:EmbeddingModel` ile local Ollama embedding servisi.

## Mimari Akis

Kullanici mesaji once domain guardrail'den gecer. Guardrail sonucu izin verirse capability belirlenir ve ilgili Semantic Kernel ajani calisir.

```text
Browser / Console
  -> Chat API / Chat Service
  -> Aygaz Domain Guardrail
  -> Capability Resolver
  -> Semantic Kernel Agent
  -> Plugin / C# Service
  -> MongoDB, SQL veya RAG
  -> Turkce final cevap
```

Out-of-scope veya belirsiz taleplerde is ajani cagrilmaz. Bu sinir yalniz prompt'a birakilmaz; C# servis akisi icinde enforce edilir.

## Domain Guardrail

Kullanici girdisi dogrudan is ajanina gitmez. Once ayri bir guardrail katmani tarafindan degerlendirilir:

```text
User input
  -> DomainGuardrail
     -> Allowed    -> ilgili ajan ve tool akisi
     -> OutOfScope -> sabit kapsam disi cevabi
     -> Ambiguous  -> sabit netlestirme cevabi
```

Kararlar:

- `Allowed`: Aygaz e-ticaret demo kapsamina giren musteri, siparis, urun, stok, satis veya destek politikasi talebi
- `OutOfScope`: baska organizasyonlara ait talepler veya e-ticaret domain'i disindaki konular
- `Ambiguous`: Aygaz e-ticaret baglantisi guvenilir bicimde anlasilmayan mesajlar

Guardrail kararindan sonra capability resolver ilgili yetenegi belirler. Policy, order, product, inventory, customer ve sales sinirlari birbirinden bagimsiz tutulur. Bu sayede ornegin baska bir sirketin iade politikasi `OutOfScope` kalirken, Aygaz demo iade politikasi support-policy RAG akisina yonlenir.

Ornekler:

```text
Allowed:    ahmet.yilmaz@example.com musterisi kim?
Allowed:    AYG-DEMO-1001 siparisi ne durumda?
Allowed:    AYG-DEMO-PRD-001 stokta mi?
Allowed:    Iade suresi kac gun?
OutOfScope: Arcelik'in iade politikasi nedir?
OutOfScope: Bugunku futbol maclarini anlat.
Ambiguous:  Bunun durumunu kontrol et.
```

## Ajanlar ve Yetenekler

- Customer: musteri ID, e-posta ve ad/soyad sorgulari
- Order: siparis detaylari, musteri siparisleri, son siparis, durum guncelleme ve iptal
- Product: SKU, ad ve kategori bazli urun arama
- Inventory: urun stok ve lokasyon bilgisi
- Sales: satis ozeti, en cok satan urunler, musteri alisveris ozeti
- Support Policy: iade, teslimat, kampanya ve destek dokumanlari

Veri disari ham entity graph olarak tasinmaz. Tool cevaplari sinirli DTO'lar ve hesaplanmis ozetlerle tutulur.

## Tool Allow-List

Ajanlara yalniz kontrollu servis metotlari acilir. Reflection, raw SQL, export, genel listeleme veya sinirsiz veri dokme tool'u yoktur.

Customer:

- `get_customer_by_email(email)`
- `get_customer_by_id(id)`
- `search_customers_by_name(query)`

Order:

- `get_order_by_number(orderNumber)`
- `get_customer_orders(customerId)`
- `get_latest_customer_order(customerId)`
- `update_order_status(orderNumber, status, reason)`
- `cancel_order(orderNumber, reason)`

Product:

- `get_product_by_sku(sku)`
- `search_products(query)`

Inventory:

- `get_product_inventory(productId)`
- `get_total_product_stock(productId)`

Sales:

- `get_sales_summary(fromDate, toDate)`
- `get_top_selling_products(fromDate, toDate, limit)`
- `get_customer_purchase_summary(customerId, fromDate, toDate)`

Support Policy:

- `search_documents(query)`

`get_all_customers`, `get_all_orders`, `get_all_products`, `get_all_inventory`, raw entity graph, raw SQL, price mutation, stock mutation, refund ve export tool'lari bilincli olarak acik degildir.

## Coklu Tool Akislari

Semantic Kernel ajanlari tek bir cevap icinde birden fazla tool kullanabilir. C# tarafinda keyword routing ile sonraki tool zorlanmaz; ajan onceki tool sonucundaki ID veya baglami kullanarak devam eder.

Musteri -> Siparis:

```text
User -> search_customers_by_name -> Customer ID
     -> get_latest_customer_order
     -> Turkce final cevap
```

Urun -> Stok:

```text
User -> get_product_by_sku -> Product ID
     -> get_total_product_stock veya get_product_inventory
     -> Turkce final cevap
```

Musteri -> Siparis -> Politika:

```text
User -> search_customers_by_name -> Customer ID
     -> get_latest_customer_order
     -> search_documents
     -> Turkce final cevap
```

Provider gecici olarak kullanilamazsa desteklenen deterministik fast-path'ler devreye girebilir. Ornegin exact siparis numarasi, SKU veya acik policy sorgularinda servis/RAG sonucu ile cevap uretilir.

## RAG / Dokuman Arama

Demo dokumanlari:

- `data/demo-documents/return-policy.txt`
- `data/demo-documents/delivery-policy.txt`
- `data/demo-documents/campaign-policy.txt`
- `data/demo-documents/customer-support.txt`

MongoDB modunda uygulama baslangicinda `AutoIngestOnStartup=true` ise dokumanlar chunk'lanir, embedding uretilir ve `documentChunks` collection'ina upsert edilir. Arama sirasinda yalniz en alakali sinirli chunk'lar ajan cevabina kaynak olur.

RAG akisi:

```text
Documents
  -> paragraph chunks
  -> Lexical veya Ollama embedding
  -> MongoDB documentChunks veya in-memory index
  -> cosine similarity
  -> en alakali en fazla 3 chunk
  -> support-policy agent cevabi
```

`Lexical` embedding varsayilani ek model gerektirmez ve testlerde deterministik davranir. `Ollama` embedding secilirse `nomic-embed-text` gibi local bir embedding modeli kullanilir.

Ornek:

```text
Iade suresi kac gun?
Ahmet Yilmaz'in son siparisini kontrol et ve iade politikasini soyle.
```

## Web API ve Chat UI

Ana demo `Aygaz.ECommerce.SemanticKernel.Web` projesidir. Mevcut servis, guardrail, ajan ve RAG katmanlarini yeniden yazmadan HTTP API ve static chat UI sunar.

Endpoint'ler:

| Endpoint | Aciklama |
|----------|----------|
| `GET /health` | Basit durum kontrolu |
| `POST /api/chat` | Guardrail ve Semantic Kernel ajanlariyla sohbet |
| `POST /api/chat/clear` | Oturum gecmisini temizler |

`POST /api/chat` request:

```json
{
  "message": "Ahmet Yilmaz'in son siparisi nedir?",
  "sessionId": "opsiyonel-browser-session-id"
}
```

Response:

```json
{
  "success": true,
  "message": "...",
  "scope": "Allowed",
  "sessionId": "..."
}
```

API ham tool payload, raw LLM cevabi veya entity graph dondurmez. Browser session ID ile in-memory sohbet baglami korunur.

## Veri Katmani ve Demo Seed

Varsayilan veri katmani MongoDB'dir. `DataAccess:Provider` ile `MongoDb`, `Sqlite` veya `SqlServer` secilebilir.

Demo veri ozellikleri:

- Musteri, siparis, siparis kalemi, urun, stok ve siparis audit log kayitlari sentetiktir.
- E-postalar `example.com`, siparisler `AYG-DEMO-*`, SKU'lar `AYG-DEMO-PRD-*` formatindadir.
- Mongo seed yalniz `--seed-mongodb` ile calisir ve sabit anahtarlarla upsert yaptigi icin tekrar calistirildiginda duplicate uretmez.
- Mongo startup normalde collection/index hazirligi yapar; seed yazimi acik komuta baglidir.
- `documentChunks` collection'i RAG chunk ve embedding kayitlarini tutar.
- Iliskisel provider secildiginde Dapper initializer tablo semasini hazirlar; production migration sureci yerine gecmez.

Sales analytics servisleri ham order item listesi yerine hesaplanmis DTO'lar dondurur. Iptal siparisleri aggregate hesaplarindan dislanir.

## Ornek Sorular

```text
ahmet.yilmaz@example.com musterisi kim?
Ahmet Yilmaz isimli musteriyi bul.
1 numarali musteriyi getir.
AYG-DEMO-1001 numarali siparisin durumu nedir?
AYG-DEMO-1001 siparisini teslim edildi yap.
AYG-DEMO-1002 siparisini iptal et.
AYG-DEMO-PRD-001 urununu bul.
AYG-DEMO-PRD-003 stokta mi?
Demo Product Alpha stokta mi?
Son 30 gunluk e-ticaret satis ozetini getir.
Son 90 gunde en cok satilan 5 urunu goster.
Ahmet Yilmaz son 90 gunde ne kadar alisveris yapti?
Iade suresi kac gun?
Ahmet Yilmaz'in son siparisini kontrol et ve iade politikasini soyle.
Arcelik'in iade politikasi nedir?
```

## Test Kapsami

Testler local LLM'e bagimli olmayacak sekilde tasarlanmistir. Kapsanan basliklar:

- Dapper/SQLite veri erisimi sozlesmeleri
- Mongo provider secimi, index ve seed sozlesmeleri
- Lexical embedding ve dokuman retrieval davranisi
- Semantic Kernel routing, fast-path ve follow-up davranislari
- Guardrail fail-closed kararlari
- Web API validation, session ve hata cevaplari
- AI provider retry/fallback davranislari

Calistirma:

```powershell
dotnet test .\Aygaz.ECommerce.Agent.sln
```

## Guvenlik ve Sinirlar

- Veri seti deterministik ve sentetiktir.
- Authentication/authorization bu demo kapsaminda production seviyesinde tamamlanmis degildir.
- Guardrail out-of-scope isteklerde ajan/tool katmanini cagirmadan cevap verir.
- Mongo seed yalniz `--seed-mongodb` ile manuel calisir.
- RAG dokumanlari gercek kurum dokumani degildir.
- LLM provider hatalarinda desteklenen deterministik fast-path'ler devreye girebilir.
