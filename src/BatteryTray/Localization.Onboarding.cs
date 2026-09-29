namespace BatteryTray;

internal static partial class I18n
{
    private static readonly IReadOnlyDictionary<string, string[]> OnboardingTranslations =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] =
            [
                "Welcome",
                "Review the detected devices and choose how monitoring should start.",
                "No compatible devices were found. Pair a Bluetooth device and make sure Windows can report its battery level. You can still start monitoring.",
                "Open Bluetooth settings",
                "Start monitoring",
                "Start at sign-in",
                "Windows has disabled startup for this app. Enable it from Settings > Apps > Startup.",
                "Startup is controlled by your organization.",
                "Startup could not be changed.",
                "Settings were reset because they could not be read. The invalid file was backed up to {0}.",
                "Settings could not be read. Defaults are being used. {0}"
            ],
            ["ja"] =
            [
                "ようこそ",
                "検出されたデバイスを確認し、監視の開始方法を選択してください。",
                "対応デバイスが見つかりません。Bluetooth機器をペアリングし、Windowsが電池残量を取得できることを確認してください。このまま監視を開始することもできます。",
                "Bluetooth設定を開く",
                "監視を開始",
                "サインイン時に自動起動する",
                "Windowsでこのアプリの自動起動が無効にされています。「設定 > アプリ > スタートアップ」から有効にしてください。",
                "自動起動は組織によって管理されています。",
                "自動起動の設定を変更できませんでした。",
                "設定を読み取れなかったため初期化しました。無効なファイルは {0} に退避しました。",
                "設定を読み取れないため初期値を使用します。{0}"
            ],
            ["zh-Hans"] =
            [
                "欢迎",
                "请检查检测到的设备，并选择监视的启动方式。",
                "未找到兼容设备。请配对蓝牙设备，并确认Windows能够报告其电池电量。您仍可开始监视。",
                "打开蓝牙设置",
                "开始监视",
                "登录时启动",
                "Windows已禁用此应用的启动。请在“设置 > 应用 > 启动”中启用。",
                "启动设置由您的组织管理。",
                "无法更改启动设置。",
                "由于无法读取设置，已将其重置。无效文件已备份到 {0}。",
                "无法读取设置，正在使用默认值。{0}"
            ],
            ["zh-Hant"] =
            [
                "歡迎",
                "請檢查偵測到的裝置，並選擇監視的啟動方式。",
                "找不到相容裝置。請配對藍牙裝置，並確認Windows能夠回報電池電量。您仍可開始監視。",
                "開啟藍牙設定",
                "開始監視",
                "登入時啟動",
                "Windows已停用此應用程式的啟動。請在「設定 > 應用程式 > 啟動」中啟用。",
                "啟動設定由您的組織管理。",
                "無法變更啟動設定。",
                "因為無法讀取設定，已將其重設。無效檔案已備份至 {0}。",
                "無法讀取設定，正在使用預設值。{0}"
            ],
            ["ko"] =
            [
                "환영합니다",
                "검색된 장치를 확인하고 모니터링 시작 방법을 선택하세요.",
                "호환 장치를 찾지 못했습니다. Bluetooth 장치를 페어링하고 Windows에서 배터리 잔량을 보고할 수 있는지 확인하세요. 모니터링은 계속 시작할 수 있습니다.",
                "Bluetooth 설정 열기",
                "모니터링 시작",
                "로그인할 때 시작",
                "Windows에서 이 앱의 시작을 사용 중지했습니다. 설정 > 앱 > 시작 프로그램에서 사용하도록 설정하세요.",
                "시작 설정은 조직에서 관리합니다.",
                "시작 설정을 변경하지 못했습니다.",
                "설정을 읽을 수 없어 초기화했습니다. 잘못된 파일은 {0}에 백업했습니다.",
                "설정을 읽을 수 없어 기본값을 사용합니다. {0}"
            ],
            ["it"] =
            [
                "Benvenuto",
                "Controlla i dispositivi rilevati e scegli come avviare il monitoraggio.",
                "Nessun dispositivo compatibile trovato. Associa un dispositivo Bluetooth e verifica che Windows possa segnalarne il livello della batteria. Puoi comunque avviare il monitoraggio.",
                "Apri impostazioni Bluetooth",
                "Avvia monitoraggio",
                "Avvia all'accesso",
                "Windows ha disabilitato l'avvio per questa app. Abilitalo da Impostazioni > App > Avvio.",
                "L'avvio è gestito dalla tua organizzazione.",
                "Impossibile modificare l'avvio.",
                "Le impostazioni sono state reimpostate perché illeggibili. Il file non valido è stato salvato in {0}.",
                "Impossibile leggere le impostazioni. Vengono usati i valori predefiniti. {0}"
            ],
            ["es"] =
            [
                "Bienvenido",
                "Revisa los dispositivos detectados y elige cómo iniciar la supervisión.",
                "No se encontraron dispositivos compatibles. Empareja un dispositivo Bluetooth y comprueba que Windows pueda informar de su batería. Aun así, puedes iniciar la supervisión.",
                "Abrir configuración de Bluetooth",
                "Iniciar supervisión",
                "Iniciar al iniciar sesión",
                "Windows ha deshabilitado el inicio de esta aplicación. Actívalo en Configuración > Aplicaciones > Inicio.",
                "El inicio está administrado por tu organización.",
                "No se pudo cambiar el inicio.",
                "La configuración se restableció porque no se pudo leer. El archivo no válido se guardó en {0}.",
                "No se pudo leer la configuración. Se usan los valores predeterminados. {0}"
            ],
            ["hi"] =
            [
                "स्वागत है",
                "मिले हुए डिवाइस देखें और निगरानी शुरू करने का तरीका चुनें।",
                "कोई संगत डिवाइस नहीं मिला। Bluetooth डिवाइस को पेयर करें और सुनिश्चित करें कि Windows उसका बैटरी स्तर बता सकता है। आप फिर भी निगरानी शुरू कर सकते हैं।",
                "Bluetooth सेटिंग खोलें",
                "निगरानी शुरू करें",
                "साइन इन पर शुरू करें",
                "Windows ने इस ऐप का स्टार्टअप बंद किया है। इसे सेटिंग्स > ऐप्स > स्टार्टअप में चालू करें।",
                "स्टार्टअप आपके संगठन द्वारा प्रबंधित है।",
                "स्टार्टअप बदला नहीं जा सका।",
                "सेटिंग पढ़ी नहीं जा सकी, इसलिए रीसेट की गई। अमान्य फ़ाइल का बैकअप {0} में है।",
                "सेटिंग पढ़ी नहीं जा सकी। डिफ़ॉल्ट मान उपयोग किए जा रहे हैं। {0}"
            ],
            ["fr"] =
            [
                "Bienvenue",
                "Vérifiez les appareils détectés et choisissez comment démarrer la surveillance.",
                "Aucun appareil compatible trouvé. Associez un appareil Bluetooth et vérifiez que Windows peut indiquer son niveau de batterie. Vous pouvez quand même démarrer la surveillance.",
                "Ouvrir les paramètres Bluetooth",
                "Démarrer la surveillance",
                "Démarrer à la connexion",
                "Windows a désactivé le démarrage de cette application. Activez-le dans Paramètres > Applications > Démarrage.",
                "Le démarrage est géré par votre organisation.",
                "Impossible de modifier le démarrage.",
                "Les paramètres ont été réinitialisés car ils étaient illisibles. Le fichier non valide a été sauvegardé dans {0}.",
                "Impossible de lire les paramètres. Les valeurs par défaut sont utilisées. {0}"
            ],
            ["ru"] =
            [
                "Добро пожаловать",
                "Проверьте обнаруженные устройства и выберите способ запуска мониторинга.",
                "Совместимые устройства не найдены. Сопрягите устройство Bluetooth и убедитесь, что Windows сообщает уровень его заряда. Мониторинг всё равно можно запустить.",
                "Открыть параметры Bluetooth",
                "Запустить мониторинг",
                "Запускать при входе",
                "Windows отключила автозапуск этого приложения. Включите его в Параметры > Приложения > Автозагрузка.",
                "Автозапуск контролируется вашей организацией.",
                "Не удалось изменить автозапуск.",
                "Параметры сброшены, так как их не удалось прочитать. Недопустимый файл сохранён в {0}.",
                "Не удалось прочитать параметры. Используются значения по умолчанию. {0}"
            ],
            ["pt"] =
            [
                "Bem-vindo",
                "Confira os dispositivos detectados e escolha como iniciar o monitoramento.",
                "Nenhum dispositivo compatível foi encontrado. Emparelhe um dispositivo Bluetooth e confirme que o Windows informa o nível da bateria. Ainda é possível iniciar o monitoramento.",
                "Abrir configurações de Bluetooth",
                "Iniciar monitoramento",
                "Iniciar ao entrar",
                "O Windows desativou a inicialização deste aplicativo. Ative-a em Configurações > Aplicativos > Inicialização.",
                "A inicialização é gerenciada pela sua organização.",
                "Não foi possível alterar a inicialização.",
                "As configurações foram redefinidas porque não puderam ser lidas. O arquivo inválido foi salvo em {0}.",
                "Não foi possível ler as configurações. Os padrões estão sendo usados. {0}"
            ],
            ["ar"] =
            [
                "مرحبًا",
                "راجع الأجهزة المكتشفة واختر كيفية بدء المراقبة.",
                "لم يتم العثور على أجهزة متوافقة. قم بإقران جهاز Bluetooth وتأكد من أن Windows يمكنه عرض مستوى بطاريته. لا يزال بإمكانك بدء المراقبة.",
                "فتح إعدادات Bluetooth",
                "بدء المراقبة",
                "البدء عند تسجيل الدخول",
                "عطّل Windows بدء تشغيل هذا التطبيق. قم بتمكينه من الإعدادات > التطبيقات > بدء التشغيل.",
                "تدير مؤسستك إعداد بدء التشغيل.",
                "تعذر تغيير إعداد بدء التشغيل.",
                "تمت إعادة تعيين الإعدادات لتعذر قراءتها. تم نسخ الملف غير الصالح احتياطيًا إلى {0}.",
                "تعذرت قراءة الإعدادات. يتم استخدام القيم الافتراضية. {0}"
            ],
            ["tr"] =
            [
                "Hoş geldiniz",
                "Algılanan cihazları inceleyin ve izlemenin nasıl başlayacağını seçin.",
                "Uyumlu cihaz bulunamadı. Bir Bluetooth cihazını eşleştirin ve Windows'un pil düzeyini bildirebildiğini doğrulayın. Yine de izlemeyi başlatabilirsiniz.",
                "Bluetooth ayarlarını aç",
                "İzlemeyi başlat",
                "Oturum açıldığında başlat",
                "Windows bu uygulamanın başlangıcını devre dışı bıraktı. Ayarlar > Uygulamalar > Başlangıç bölümünden etkinleştirin.",
                "Başlangıç ayarı kuruluşunuz tarafından yönetiliyor.",
                "Başlangıç ayarı değiştirilemedi.",
                "Ayarlar okunamadığı için sıfırlandı. Geçersiz dosya {0} konumuna yedeklendi.",
                "Ayarlar okunamadı. Varsayılanlar kullanılıyor. {0}"
            ],
            ["de"] =
            [
                "Willkommen",
                "Prüfen Sie die erkannten Geräte und wählen Sie, wie die Überwachung gestartet wird.",
                "Keine kompatiblen Geräte gefunden. Koppeln Sie ein Bluetooth-Gerät und prüfen Sie, ob Windows dessen Akkustand melden kann. Sie können die Überwachung trotzdem starten.",
                "Bluetooth-Einstellungen öffnen",
                "Überwachung starten",
                "Bei Anmeldung starten",
                "Windows hat den Start dieser App deaktiviert. Aktivieren Sie ihn unter Einstellungen > Apps > Autostart.",
                "Der Autostart wird von Ihrer Organisation verwaltet.",
                "Der Autostart konnte nicht geändert werden.",
                "Die Einstellungen wurden zurückgesetzt, da sie nicht gelesen werden konnten. Die ungültige Datei wurde unter {0} gesichert.",
                "Die Einstellungen konnten nicht gelesen werden. Standardwerte werden verwendet. {0}"
            ],
            ["id"] =
            [
                "Selamat datang",
                "Tinjau perangkat yang terdeteksi dan pilih cara memulai pemantauan.",
                "Tidak ada perangkat kompatibel yang ditemukan. Pasangkan perangkat Bluetooth dan pastikan Windows dapat melaporkan tingkat baterainya. Pemantauan tetap dapat dimulai.",
                "Buka pengaturan Bluetooth",
                "Mulai pemantauan",
                "Mulai saat masuk",
                "Windows telah menonaktifkan startup aplikasi ini. Aktifkan di Pengaturan > Aplikasi > Startup.",
                "Startup dikelola oleh organisasi Anda.",
                "Startup tidak dapat diubah.",
                "Pengaturan direset karena tidak dapat dibaca. File yang tidak valid dicadangkan ke {0}.",
                "Pengaturan tidak dapat dibaca. Nilai default digunakan. {0}"
            ],
            ["vi"] =
            [
                "Chào mừng",
                "Xem lại các thiết bị đã phát hiện và chọn cách bắt đầu theo dõi.",
                "Không tìm thấy thiết bị tương thích. Hãy ghép đôi thiết bị Bluetooth và đảm bảo Windows có thể báo mức pin. Bạn vẫn có thể bắt đầu theo dõi.",
                "Mở cài đặt Bluetooth",
                "Bắt đầu theo dõi",
                "Khởi động khi đăng nhập",
                "Windows đã tắt khởi động cho ứng dụng này. Hãy bật trong Cài đặt > Ứng dụng > Khởi động.",
                "Khởi động do tổ chức của bạn quản lý.",
                "Không thể thay đổi cài đặt khởi động.",
                "Cài đặt đã được đặt lại vì không thể đọc. Tệp không hợp lệ được sao lưu tại {0}.",
                "Không thể đọc cài đặt. Đang dùng giá trị mặc định. {0}"
            ],
            ["pl"] =
            [
                "Witamy",
                "Sprawdź wykryte urządzenia i wybierz sposób uruchamiania monitorowania.",
                "Nie znaleziono zgodnych urządzeń. Sparuj urządzenie Bluetooth i upewnij się, że Windows może podać poziom baterii. Nadal możesz rozpocząć monitorowanie.",
                "Otwórz ustawienia Bluetooth",
                "Rozpocznij monitorowanie",
                "Uruchamiaj przy logowaniu",
                "Windows wyłączył uruchamianie tej aplikacji. Włącz je w Ustawienia > Aplikacje > Uruchamianie.",
                "Uruchamianie jest zarządzane przez Twoją organizację.",
                "Nie można zmienić ustawienia uruchamiania.",
                "Ustawienia zresetowano, ponieważ nie można ich odczytać. Nieprawidłowy plik zapisano w {0}.",
                "Nie można odczytać ustawień. Używane są wartości domyślne. {0}"
            ],
            ["th"] =
            [
                "ยินดีต้อนรับ",
                "ตรวจสอบอุปกรณ์ที่พบและเลือกวิธีเริ่มการตรวจสอบ",
                "ไม่พบอุปกรณ์ที่เข้ากันได้ โปรดจับคู่อุปกรณ์ Bluetooth และตรวจสอบว่า Windows รายงานระดับแบตเตอรี่ได้ คุณยังสามารถเริ่มการตรวจสอบได้",
                "เปิดการตั้งค่า Bluetooth",
                "เริ่มการตรวจสอบ",
                "เริ่มเมื่อเข้าสู่ระบบ",
                "Windows ปิดการเริ่มต้นของแอปนี้ไว้ โปรดเปิดใน การตั้งค่า > แอป > การเริ่มต้น",
                "การเริ่มต้นถูกจัดการโดยองค์กรของคุณ",
                "ไม่สามารถเปลี่ยนการตั้งค่าการเริ่มต้นได้",
                "รีเซ็ตการตั้งค่าแล้วเนื่องจากอ่านไม่ได้ ไฟล์ที่ไม่ถูกต้องสำรองไว้ที่ {0}",
                "ไม่สามารถอ่านการตั้งค่าได้ กำลังใช้ค่าเริ่มต้น {0}"
            ]
        };
}
