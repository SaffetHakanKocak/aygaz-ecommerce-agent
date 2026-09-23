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

## Ajanlar ve Yetenekler

- Customer: musteri ID, e-posta ve ad/soyad sorgulari
- Order: siparis detaylari, musteri siparisleri, son siparis, durum guncelleme ve iptal
- Product: SKU, ad ve kategori bazli urun arama
- Inventory: urun stok ve lokasyon bilgisi
- Sales: satis ozeti, en cok satan urunler, musteri alisveris ozeti
- Support Policy: iade, teslimat, kampanya ve destek dokumanlari

Veri disari ham entity graph olarak tasinmaz. Tool cevaplari sinirli DTO'lar ve hesaplanmis ozetlerle tutulur.

## RAG / Dokuman Arama

Demo dokumanlari:

- `data/demo-documents/return-policy.txt`
- `data/demo-documents/delivery-policy.txt`
- `data/demo-documents/campaign-policy.txt`
- `data/demo-documents/customer-support.txt`

MongoDB modunda uygulama baslangicinda `AutoIngestOnStartup=true` ise dokumanlar chunk'lanir, embedding uretilir ve `documentChunks` collection'ina upsert edilir. Arama sirasinda yalniz en alakali sinirli chunk'lar ajan cevabina kaynak olur.

Ornek:

```text
Iade suresi kac gun?
Ahmet Yilmaz'in son siparisini kontrol et ve iade politikasini soyle.
```

## Ornek Sorular

```text
ahmet.yilmaz@example.com musterisi kim?
Ahmet Yilmaz isimli musteriyi bul.
AYG-DEMO-1001 numarali siparisin durumu nedir?
AYG-DEMO-1001 siparisini teslim edildi yap.
AYG-DEMO-1002 siparisini iptal et.
AYG-DEMO-PRD-001 urununu bul.
Demo Product Alpha stokta mi?
Son 30 gunluk e-ticaret satis ozetini getir.
Son 90 gunde en cok satilan 5 urunu goster.
Iade suresi kac gun?
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
