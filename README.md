# Aygaz E-Commerce AI Agent

Kontrollü C# servisleriyle genişletilen yerel bir e-ticaret Agentic AI geliştirme projesidir. Aşama 1'de local Ollama bağlantısı, Aşama 2'de tamamen sentetik Customer veri katmanı, Aşama 3'te ise native Ollama tool calling kullanan Customer AI Agent tamamlanmıştır.

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
3 - Customer AI Agent
0 - Exit
```

Customer test menüsünde tüm müşteriler listelenebilir; ID, e-posta veya ad/soyad ile sorgulama yapılabilir. Bu menü yalnızca geliştirme ve test amaçlıdır.

Customer AI Agent menüsünde doğal dilde müşteri sorguları yazılabilir. `back` ana menüye döner, `exit` uygulamayı kapatır.

## Customer AI Agent ve native tool calling

Aşama 3, herhangi bir agent framework kullanmadan Ollama'nın native `POST /api/chat` tool calling sözleşmesini doğrudan uygular. Semantic Kernel, Microsoft Agent Framework, LangChain veya AutoGen kullanılmaz.

Agent'a yalnızca şu üç tool açılır:

- `get_customer_by_email(email)` — e-posta ile tek müşteri
- `get_customer_by_id(id)` — pozitif ID ile tek müşteri
- `search_customers_by_name(query)` — ad/soyad ile en fazla yapılandırılmış sayıda sonuç

`get_all_customers` agent tool allow-list'inde bulunmaz. Database test menüsündeki toplu listeleme özelliği Aşama 2 regresyonu olarak çalışmaya devam eder, fakat LLM bu metoda erişemez. Tool executor yalnız exact allow-list adlarını kabul eder, JSON argument'larını C# tarafında doğrular ve yalnız `ICustomerService` çağırır; LLM metni SQL, shell, filesystem veya reflection olarak çalıştırılmaz.

Akış:

```text
User → Ollama → assistant + tool_calls
                  ↓
          C# allow-list executor
                  ↓
     ICustomerService → EF Core → SQLite
                  ↓
        role=tool JSON sonucu → Ollama → Türkçe final yanıt
```

Assistant'ın `tool_calls` mesajı ve ardından `role=tool` sonucu doğru sırayla conversation history'ye eklenir. Tool çağrısı kalmayana kadar loop devam eder; iteration, bir yanıttaki tool sayısı ve saklanan tamamlanmış conversation turn sayısı `Agent` ayarlarıyla sınırlandırılır.

LLM'e entity veya tam `CustomerDto` verilmez. Agent'a özel sonuç yalnızca `Id`, `FirstName`, `LastName`, `Email` ve `City` alanlarını içerir. `Phone` ve `CreatedAt` hiçbir tool sonucuna dahil edilmez. Tool logları yalnız tool adı, güvenli argument özeti ve `Success`, `NotFound` veya `Rejected` durumunu gösterir; sonuç payload'ını yazmaz.

Örnek sorgular:

```text
ahmet.yilmaz@example.com müşterisi kim?
Ahmet Yılmaz isimli müşteriyi bul.
1 numaralı müşteriyi getir.
nobody@example.com müşterisi kim?
```

## Customer database

- Database: SQLite
- Dosya: repository kökündeki `aygaz-ecommerce.db` (yukarıdaki komut kökten çalıştırıldığında)
- Şema: `Customer` entity ve unique e-posta kısıtı
- Veri: 12 deterministik ve tamamen sentetik demo müşteri
- E-postalar yalnızca `example.com`, telefonlar açıkça test formatındadır
- Dışarıya dönüş modeli: `CustomerDto`

Database dosyası ile SQLite WAL/journal yan dosyaları Git tarafından ignore edilir. Başlangıçta `EnsureCreatedAsync` kullanılır; tablo boşsa seed uygulanır ve tekrar çalıştırmalarda duplicate kayıt eklenmez. Bu MVP yaklaşımı migration çalıştırmaz; ileride şema değişiklikleri migration altyapısıyla yönetilecektir.

Bağlantı dizesi, Ollama ayarları ve `Agent` limitleri `src/Aygaz.ECommerce.Agent/appsettings.json` içinden yönetilir.

## Build ve test

```powershell
dotnet restore .\Aygaz.ECommerce.Agent.sln
dotnet build .\Aygaz.ECommerce.Agent.sln --no-restore
dotnet test .\Aygaz.ECommerce.Agent.sln --no-build --no-restore
```

Testler gerçek SQLite in-memory bağlantısıyla seed idempotency, unique e-posta ve CustomerService sorgularını; elle yazılmış fake'lerle de tool allow-list'ini, argument validation'ını, veri minimizasyonunu ve agent loop mesaj sırasını doğrular. Automated testler local Ollama'ya bağımlı değildir.

## Roadmap

- Aşama 1 — C# + Ollama — **TAMAMLANDI**
- Aşama 2 — Customer Database — **TAMAMLANDI**
- Aşama 3 — Local LLM Tool Calling — **TAMAMLANDI**
- Aşama 4 — Aygaz Domain Guardrails
- Aşama 5 — Orders
- Aşama 6 — Products / Inventory
- Aşama 7 — Sales Analysis
- Aşama 8 — RAG
- Aşama 9 — Authorization / Audit
- Aşama 10 — Web UI
