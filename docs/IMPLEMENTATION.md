# Mevcut proje incelemesi ve uygulama kararları

## Kaynak

İlk repository `1361ac7` commit'inde yalnızca dört C# dosyası ve `.meta` kimlikleri vardı. Tam proje `C:\Users\gabar\Documents\GitHub\Crash` içinde bulundu. Unity sürümü `2022.3.62f1 (4af31df58517)`. Dört script'in SHA256 değerleri repository ile aynıydı.

Yalnızca `Assets`, `Packages`, `ProjectSettings` mevcut çalışma deposuna alındı. Kaynak Crash klasörüne yazılmadı. Library, Temp, Logs, obj/bin, IDE cache'leri ve iç içe Git metadata'sı aktarımın sonuç dosyalarına dahil edilmedi. Özgün dört script `.meta` GUID'leri korunarak Assets altındaki gerçek yerlerine taşındı.

## Başlangıç durumu

- `SampleScene` sadece kamera içeriyordu; yanlışlıkla build başlangıcındaydı.
- `2DBirds_Pack3/Scenes/MainScene` 6×6 board, InputManager, kamera ve GridVisualizer içeriyordu.
- BoardManager üzerinde gereksiz bir Bird component'i vardı.
- MainScene mevcut olmayan bir LightingSettings asset'ine referans veriyordu; 2D sprite oyununda gerekmeyen bu kırık referans temizlendi.
- Board oluşturma, tip+renk eşleştirme, sütun collapse ve kuş hareket/uçuş animasyonları mevcuttu; çalışma durumu başlangıçta runtime ile doğrulanmış değildi.
- InputManager ve BoardManager birlikte swap koordinatları yönetiyordu. Eşleşme kontrolü 0,1 saniyede, swap animasyonu 0,3 saniyede bitiyordu.
- Geçersiz swap dönüşü input tarafından daha önce temizlenmiş seçili kuş referanslarına dayanıyordu; geri dönüş koordinatları da gerçek başlangıç konumunu korumuyordu.
- Dolum coroutine'leri beklenmeden board tekrar değerlendiriliyordu; swap sonrası cascade zinciri tamamlanmıyordu.
- Başlangıç güvenlik kontrolü gerçek prefab Instantiate/DestroyImmediate yapıyordu; yalnızca veri üzerinden kontrol yeterlidir.
- Çalışırken anlamsız `go != null && go == null` temizliği ve periyodik debug taraması vardı.
- Kuşların bazı animator controller'larında Fly trigger yoktu.
- Prefab sprite'ları hücre aralığına göre büyüktü; board ve mobil ekran oranı için ölçekleme yoktu.
- Hamle, skor, hedef, level, kayıt, menü ve audio sistemleri mevcut değildi.
- Unity Test Framework zaten kuruluydu. Yeni framework eklenmedi.

## Korunan ve değiştirilen parçalar

Kuş görselleri, prefab kimlikleri, animasyon clip/controller'ları, Bird hareket/fall eğrileri, aynı tür+renk match kuralı, 6×6 boyut ve sütunların alttan doldurulması korundu. GridVisualizer korunarak gameplay scene'inde debug çizgileri kapatıldı.

BoardManager'ın swap/resolution koordinasyonu teknik yarış durumlarını gidermek için değiştirildi. Tek coroutine sahibi grid'i değiştirir; input yalnızca komşu kuş isteği gönderir. FindMatches kuralı saf veri üzerinde aynı yatay/dikey koşulu uygular; kesişen eşleşmeler deduplicate edilir. Bird'ün animasyon yöntemleri yeniden kullanılır. Refill'in başlangıçtaki no-match kısıtını kullanması kaldırıldı; refill doğal cascade üretebilir.

Yeni V1 sistemleri ayrı sınıflarda eklendi. MainScene üzerinde serialized referanslar `V1ProjectSetup.Configure` aracılığıyla Unity Editor API'leriyle bağlandı ve kaydedildi. UI Canvas/anchor hiyerarşisi runtime'da deterministik oluşturulur; Inspector'da elle düğme bağlamak gerekmez.

## Uygulama sırası

1. Mevcut kaynakların ve scene/prefab ilişkilerinin incelemesi.
2. İşlem kilidi, güvenilir swap/düşme/refill/cascade ve başlangıç/reshuffle garantileri.
3. Merkezi campaign/scoring, objective oturumu ve yedekli local progress.
4. Aynı scene içinde ana menü, level seçimi, HUD, pause ve sonuçlar.
5. Mouse/touch, güvenli eksik audio desteği, portrait/safe-area görünümü.
6. Unity derleme, otomatik test, görsel kontrol ve development build doğrulaması.

Doğrulanmış sonuçlar ve henüz manuel yapılması gereken cihaz kontrolleri `QA.md` içinde ayrı belirtilir.
