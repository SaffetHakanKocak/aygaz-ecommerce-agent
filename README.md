# Aygaz E-Commerce AI Agent

Kontrollü C# servisleriyle genişletilecek yerel bir e-ticaret Agentic AI sistemi için geliştirme projesidir. Aşama 1'de local Ollama bağlantısı, Aşama 2'de ise tamamen sentetik Customer veri katmanı tamamlanmıştır. Local LLM henüz database veya `ICustomerService` ile bağlantılı değildir.

## Teknolojiler ve gereksinimler

- .NET 9 SDK
- C# console application
- Ollama ve `qwen3:4b-instruct`
- Entity Framework Core 9
- SQLite

Qwen3 4B Instruct, görece hafif ve ilerideki tool calling aşamasına uygun olduğu için seçildi. Bu makinedeki düşük kapasiteli GPU'da Ollama backend hatasını önlemek için `GpuLayers` değeri `0` olarak ayarlanmış, inference CPU üzerinde doğrulanmıştır.

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
0 - Exit
```

Customer test menüsünde tüm müşteriler listelenebilir; ID, e-posta veya ad/soyad ile sorgulama yapılabilir. Bu menü yalnızca geliştirme ve test amaçlıdır.

## Customer database

- Database: SQLite
- Dosya: repository kökündeki `aygaz-ecommerce.db` (yukarıdaki komut kökten çalıştırıldığında)
- Şema: `Customer` entity ve unique e-posta kısıtı
- Veri: 12 deterministik ve tamamen sentetik demo müşteri
- E-postalar yalnızca `example.com`, telefonlar açıkça test formatındadır
- Dışarıya dönüş modeli: `CustomerDto`

Database dosyası ile SQLite WAL/journal yan dosyaları Git tarafından ignore edilir. Başlangıçta `EnsureCreatedAsync` kullanılır; tablo boşsa seed uygulanır ve tekrar çalıştırmalarda duplicate kayıt eklenmez. Bu MVP yaklaşımı migration çalıştırmaz; ileride şema değişiklikleri migration altyapısıyla yönetilecektir.

Bağlantı dizesi ve Ollama ayarları `src/Aygaz.ECommerce.Agent/appsettings.json` içinden yönetilir.

## Build ve test

```powershell
dotnet restore .\Aygaz.ECommerce.Agent.sln
dotnet build .\Aygaz.ECommerce.Agent.sln --no-restore
dotnet test .\Aygaz.ECommerce.Agent.sln --no-build --no-restore
```

Integration testleri gerçek SQLite in-memory bağlantısıyla seed idempotency, unique e-posta ve CustomerService sorgularını doğrular.

## Roadmap

- Aşama 1 — C# + Ollama — **TAMAMLANDI**
- Aşama 2 — Customer Database — **TAMAMLANDI**
- Aşama 3 — LLM Tool Calling
- Aşama 4 — Domain Guardrails
- Aşama 5 — Orders
- Aşama 6 — Products / Inventory
- Aşama 7 — Sales Analysis
- Aşama 8 — RAG
- Aşama 9 — Authorization / Audit
- Aşama 10 — Web UI
