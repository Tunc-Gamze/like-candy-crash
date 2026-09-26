# V1 QA raporu

26 Eylül 2026 — Unity **2022.3.62f1**, Windows Editor.

## Sonuçlar

- **EditMode: 26/26 geçti.** 1.000 başlangıç board'u; 3–12 hücre boyutlarında fallback; skor/objective/save kuralları; 1.200 seeded level simülasyonu.
- **PlayMode: 10/10 geçti.** Gerçek MainScene, prefab ve coroutine'ler; ortak mouse/touch pointer kodu; gerçek PlayerPrefs; UI render testi.
- **Ses güncellemesi: ek hedefli PlayMode testi 1/1 geçti.** `SoundEffectsAreAssignedNonSilentAndRespectToggle`: sekiz scene clip referansı, gerçek PCM örneklerinde ses seviyesi, AudioSource playback, OFF ile anında stop/mute, OFF sırasında yeni ses engeli, ON ile yeniden playback, kayıt round-trip ve null clip güvenliği. Sonuç: `TestResults/Audio.xml`.
- **Windows x64 Development build başarılı:** yaklaşık 162 MiB. Runtime ile Editor/test assembly'leri ayrıdır.
- **Görsel kontrol:** 540×960, 540×1200, 768×1024; home, level select, gameplay, pause, win ve lose. Kuş boyutu ve filtreleme iyileştirildi; kullanılan sprite sheet'ler 2048 texture sınırı, mipmap ve trilinear filtrelemeyle tekrar import edildi.
- **Scene bağlantıları:** MainScene build başlangıcı; Board/Input/Camera/Campaign/UI/Audio referansları Unity'de kaydedildi. Eksik LightingSettings referansı temizlendi. Özgün script GUID'leri korundu.

Ham sonuçlar yerelde `TestResults/EditMode.xml`, `TestResults/PlayMode.xml`, `TestResults/build.log` ve `TestResults/Previews/` içindedir. TestResults Git dışında tutulur. Seçilmiş portföy görüntüleri `docs/screenshots/` altındadır.

## İstenen 20 senaryo

| # | Senaryo | Doğrulama |
| --- | --- | --- |
| 1 | Başlangıçta otomatik match yok | 1.000 seeded board ve gerçek altı level başlangıcı |
| 2 | Başlangıçta geçerli hamle var | Aynı testler ve constructive fallback boyut taraması |
| 3 | Geçersiz swap geri döner | PlayMode: aynı Bird nesneleri, grid ve transform konumları |
| 4 | Geçersiz swap hamle tüketmez | Invalid swap ve ortak pointer testleri |
| 5 | Geçerli swap bir hamle tüketir | Gerçek animasyon/resolution sonrası hamle sayısı |
| 6 | Eşleşen kuşlar kaldırılır | Beklenen nesnelerin resolve sonrası destroy olduğu doğrulanır |
| 7 | Collapse doğru çalışır | Saf kuralda sıra koruma; gerçek board koordinat/transform kontrolü |
| 8 | Refill tüm hücreleri doldurur | Resolve sonrası 36 hücrede Bird ve doğru indeksler |
| 9 | Cascade tamamen çözülür | Stabil board'da sıfır match ve en az bir hamle |
| 10 | Resolve sırasında input kilitli | İkinci swap reddi, CanInteract ve ortak pointer testi |
| 11 | Skor doğru güncellenir | 3/4/5/6 eşleşme ve cascade çarpanı parametrik testleri |
| 12 | Collection doğru güncellenir | Renk sayaçları ve birleşik objective testi |
| 13 | Win doğru çalışır | Son hamlede kazanma, gerçek Won state |
| 14 | Lose doğru çalışır | Son hamlede tamamlanamayan hedefle Lost state |
| 15 | Kazanma sonraki level'ı açar | GameController ve save round-trip |
| 16 | Kaybetme sonraki level'ı açmaz | Gerçek win→next→lose akışı |
| 17 | Save yeniden yüklenir | Memory store ve gerçek PlayerPrefs ile yeni ProgressStore: best, sound, unlock |
| 18 | Level 6, Level 7 açmaz | Gerçek finale, Next düğmesi yokluğu, save sınır testi |
| 19 | Hamlesiz board karışır | Dead-board, tek renkli degenerate board ve bounded fallback |
| 20 | Restart temiz state oluşturur | Swap sırasında pause/restart: 20 hamle, 0 puan, 36 kuş |

Ek testler: pause/resume, init sırasında level select, bozuk primary'den backup, primary kaybı, ileri save sürümünü koruma, eksik şema ve reset'in başka kayıt anahtarlarını koruması. İlk V1 turunda boş ses alanlarıyla güvenli oyun akışı sınandı; ses güncellemesinde gerçek atanmış clip'ler ayrıca doğrulandı.

## Denge simülasyonu

Her politika için level başına 100 oyun. Rastgele politika yalnızca geçerli hamle seçer; diğer politika bir sonraki eşleşmenin skor/collection değerine bakar. Bunlar insan playtest'i veya gerçek oyuncu başarı tahmini değildir.

| Level | Rastgele politika kazanma | Bir hamle ileri bakan politika kazanma | Rastgele politika ortalama kullanılan hamle |
| --- | --- | --- | --- |
| 1 | 100/100 | 100/100 | 4,23 |
| 2 | 100/100 | 100/100 | 7,91 |
| 3 | 98/100 | 100/100 | 14,17 |
| 4 | 100/100 | 100/100 | 13,06 |
| 5 | 81/100 | 91/100 | 13,09 |
| 6 | 75/100 | 94/100 | 18,43 |

İlk önerilen değerlerle son iki bölüm rastgele politikada %99/%97 oranında tamamlanıyordu. Bu nedenle son iki bölümün hamle bütçesi düşürüldü; çoklu toplama bölümü 12+12 yapıldı. İlk iki bölüm öğreticidir. İnsanlarla son dengeleme henüz yapılmadı.

## Test sınırları

- Windows executable derlendi; oyun akışı testleri Editor PlayMode'da çalıştı. Fiziksel Android cihazında APK/AAB kurulum veya performans testi yapılmadı.
- Mouse/touch ortak pointer yolu ekran koordinatlarıyla sınandı; fiziksel dokunmatik donanım testi değildir.
- PlayerPrefs yazılıp yeni store ile okundu; zorla süreç öldürme/elektrik kesintisi sınanmadı.
- Önizlemeler off-screen RenderTexture görüntüleridir; gerçek çentikli cihaz testi değildir. Win/lose görselleri panel örneğidir; gerçek kazanma/kaybetme ayrıca PlayMode testlerinde sınandı.
- Sekiz özgün ses efekti atanmıştır; üçüncü taraf kayıt/sample kullanılmaz. Hoparlör/kulaklık ve fiziksel Android cihazında işitsel kalite değerlendirmesi, signing, mağaza yayını ve kuş görsellerinin asset yeniden dağıtım lisansı doğrulaması yapılmış sayılmaz.

## MANUAL UNITY STEPS REQUIRED

**Editor'da çalıştırmak için zorunlu scene veya reference ataması yoktur.** Aşağıdakiler yayın öncesi veya isteğe bağlı adımlardır.

| İşlem | Scene | GameObject | Component / panel | Field / reference | Değer / işlem | Neden |
| --- | --- | --- | --- | --- | --- | --- |
| Android cihaz testi | `Assets/2DBirds_Pack3/Scenes/MainScene.unity` | `InputManager`, `Birdsong` | `InputManager`, `GameController`, `GameUI`; Build Settings | Platform / Run Device | Android, bağlı test cihazı, Build And Run; swipe, çoklu dokunma, pause, çentik ve restart kontrolü | Fiziksel cihaz doğrulaması eksik; kod reference ataması gerekmez |
| Yayın kimliği | Proje geneli | Uygulanmaz | Project Settings → Player → Android | Other Settings → Package Name | `com.example.birdsong` yerine size ait benzersiz kimlik | Mevcut değer yayın placeholder'ıdır |
| Yayın build'i | Proje geneli | Uygulanmaz | Player → Android / Build Settings | Publishing Settings, Target API Level, Build App Bundle | Kendi signing anahtarınız, yayın tarihindeki mağaza koşullarına uygun API, release AAB | Development V1 imzalı mağaza teslimi değildir |
| İsteğe bağlı ses değiştirme | `MainScene` | `Birdsong` | `GameAudio` | `select`, `swap`, `match`, `invalid`, `cascade`, `win`, `lose`, `button` | Hazır efektleri tercih ettiğiniz kullanım hakkına sahip clip'lerle değiştirebilirsiniz | Sekiz alan zaten bağlıdır; manuel atama gerekmez |

Referansları yeniden kurmak gerekirse `Birdsong → Configure V1 Scene` mevcut MainScene'i açıp kaydeder. Kaydedilmemiş scene değişikliklerini önce saklayın.
