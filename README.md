# Aygaz E-Commerce AI Agent

Aygaz e-ticaret operasyonlarında ileride kontrollü C# araçlarıyla genişletilebilecek yerel bir Agentic AI sistemi için başlangıç projesidir. Şu anda yalnızca **Aşama 1** tamamlanmıştır: C# console uygulamasının local Ollama modeliyle iletişimi.

## Gereksinimler

- .NET 9 SDK
- Windows üzerinde çalışan Ollama
- `qwen3:4b-instruct` modeli

Qwen3 4B Instruct; görece hafif, Türkçe destekli ve ilerideki aşamalarda tool calling için uygun olduğu için seçildi. Bu aşamada tool calling uygulanmamıştır. Bu makinedeki düşük kapasiteli GPU'da Ollama backend hatasını önlemek için `GpuLayers` değeri `0` olarak ayarlanmış ve inference CPU üzerinde doğrulanmıştır.

## Kurulum ve çalıştırma

Ollama Windows uygulamasını başlatın. Arka planda çalışmıyorsa ayrı bir terminalde şu komutu kullanabilirsiniz:

```powershell
ollama serve
```

Model bilgisayarda yoksa indirin:

```powershell
ollama pull qwen3:4b-instruct
```

Repository kökünde uygulamayı çalıştırın:

```powershell
dotnet run --project .\src\Aygaz.ECommerce.Agent\Aygaz.ECommerce.Agent.csproj
```

Örnek kullanım:

```text
Sorunuz:
> Merhaba. Tek cümleyle kendini tanıt.

Yanıt:
Ben, Aygaz E-Commerce AI Agent projesinin yerel geliştirme asistanıyım.
```

Yeni sorular yazmaya devam edebilir, `exit` ile uygulamayı kapatabilirsiniz. Ollama adresi ve model adı `appsettings.json` içindeki `Ollama` bölümünden yönetilir.

## Sonraki aşamalar

Sentetik e-ticaret veritabanı ve Customer yapısı; ardından kontrollü servis/tool katmanı, yetkilendirme, domain guardrail'leri ve RAG desteği aşamalı olarak eklenecektir. Bu bileşenler henüz uygulanmamıştır.
