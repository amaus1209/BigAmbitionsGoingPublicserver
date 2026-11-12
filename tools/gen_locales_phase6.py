"""Save notice (SAVE-NOTICE-1), full server (AMP-MAXPLAYERS-1) and AMP's save / world dropdowns (AMP-PICK-1): merges into
Locales/<lang>.json. Machine translations - review welcome.
    python3 tools/gen_locales_phase6.py Locales"""
import json, os, re, sys
K = ["gps_save_running", "gps_save_ok", "gps_save_failed", "gps_save_noanswer", "gps_reason_full",
     "gps_saves_newest", "gps_saves_lastworld", "gps_saves_day"]
T = {
"en": ["Saving the game on the server...", "Game saved on the server.", "Saving on the server failed ({0}).", "no answer from the server",
       "Join refused — the server is full. Try again later.", "(newest save)", "(world played last)", "day {0}"],
"de": ["Spielstand wird auf dem Server gespeichert...", "Spielstand auf dem Server gespeichert.", "Speichern auf dem Server fehlgeschlagen ({0}).", "keine Antwort vom Server",
       "Beitritt abgelehnt – der Server ist voll. Versuch es später erneut.", "(neuester Spielstand)", "(zuletzt gespielte Welt)", "Tag {0}"],
"fr": ["Sauvegarde de la partie sur le serveur...", "Partie sauvegardée sur le serveur.", "Échec de la sauvegarde sur le serveur ({0}).", "pas de réponse du serveur",
       "Connexion refusée – le serveur est plein. Réessayez plus tard.", "(sauvegarde la plus récente)", "(dernier monde joué)", "jour {0}"],
"es": ["Guardando la partida en el servidor...", "Partida guardada en el servidor.", "Error al guardar en el servidor ({0}).", "sin respuesta del servidor",
       "Unión rechazada: el servidor está lleno. Inténtalo más tarde.", "(partida más reciente)", "(último mundo jugado)", "día {0}"],
"it": ["Salvataggio della partita sul server...", "Partita salvata sul server.", "Salvataggio sul server non riuscito ({0}).", "nessuna risposta dal server",
       "Accesso rifiutato: il server è pieno. Riprova più tardi.", "(salvataggio più recente)", "(ultimo mondo giocato)", "giorno {0}"],
"pt": ["A guardar o jogo no servidor...", "Jogo guardado no servidor.", "Falha ao guardar no servidor ({0}).", "sem resposta do servidor",
       "Entrada recusada – o servidor está cheio. Tenta mais tarde.", "(gravação mais recente)", "(último mundo jogado)", "dia {0}"],
"nl": ["Spel wordt op de server opgeslagen...", "Spel opgeslagen op de server.", "Opslaan op de server mislukt ({0}).", "geen antwoord van de server",
       "Deelname geweigerd – de server is vol. Probeer het later opnieuw.", "(nieuwste opslag)", "(laatst gespeelde wereld)", "dag {0}"],
"da": ["Gemmer spillet på serveren...", "Spillet er gemt på serveren.", "Gemning på serveren mislykkedes ({0}).", "intet svar fra serveren",
       "Deltagelse afvist – serveren er fuld. Prøv igen senere.", "(nyeste gemte spil)", "(senest spillede verden)", "dag {0}"],
"fi": ["Tallennetaan peliä palvelimelle...", "Peli tallennettu palvelimelle.", "Tallennus palvelimelle epäonnistui ({0}).", "ei vastausta palvelimelta",
       "Liittyminen estetty – palvelin on täynnä. Yritä myöhemmin uudelleen.", "(uusin tallennus)", "(viimeksi pelattu maailma)", "päivä {0}"],
"pl": ["Zapisywanie gry na serwerze...", "Gra zapisana na serwerze.", "Zapis na serwerze nie powiódł się ({0}).", "brak odpowiedzi serwera",
       "Dołączenie odrzucone – serwer jest pełny. Spróbuj później.", "(najnowszy zapis)", "(ostatnio grany świat)", "dzień {0}"],
"cs": ["Ukládám hru na server...", "Hra uložena na serveru.", "Uložení na server selhalo ({0}).", "žádná odpověď ze serveru",
       "Připojení odmítnuto – server je plný. Zkus to později.", "(nejnovější uložení)", "(naposledy hraný svět)", "den {0}"],
"hu": ["Játék mentése a szerverre...", "Játék elmentve a szerverre.", "A mentés a szerverre nem sikerült ({0}).", "nincs válasz a szervertől",
       "Csatlakozás elutasítva – a szerver tele van. Próbáld később.", "(legújabb mentés)", "(utoljára játszott világ)", "{0}. nap"],
"ro": ["Se salvează jocul pe server...", "Joc salvat pe server.", "Salvarea pe server a eșuat ({0}).", "niciun răspuns de la server",
       "Intrare refuzată – serverul este plin. Încearcă mai târziu.", "(cea mai nouă salvare)", "(ultima lume jucată)", "ziua {0}"],
"lt": ["Žaidimas įrašomas serveryje...", "Žaidimas įrašytas serveryje.", "Nepavyko įrašyti serveryje ({0}).", "serveris neatsako",
       "Prisijungimas atmestas – serveris pilnas. Bandyk vėliau.", "(naujausias įrašas)", "(paskutinis žaistas pasaulis)", "{0} diena"],
"el": ["Αποθήκευση του παιχνιδιού στον διακομιστή...", "Το παιχνίδι αποθηκεύτηκε στον διακομιστή.", "Η αποθήκευση στον διακομιστή απέτυχε ({0}).", "καμία απάντηση από τον διακομιστή",
       "Η σύνδεση απορρίφθηκε – ο διακομιστής είναι γεμάτος. Δοκίμασε αργότερα.", "(νεότερη αποθήκευση)", "(τελευταίος κόσμος που παίχτηκε)", "ημέρα {0}"],
"tr": ["Oyun sunucuya kaydediliyor...", "Oyun sunucuya kaydedildi.", "Sunucuya kaydetme başarısız ({0}).", "sunucudan yanıt yok",
       "Katılım reddedildi – sunucu dolu. Daha sonra tekrar dene.", "(en yeni kayıt)", "(en son oynanan dünya)", "{0}. gün"],
"ru": ["Сохранение игры на сервере...", "Игра сохранена на сервере.", "Не удалось сохранить на сервере ({0}).", "сервер не отвечает",
       "Подключение отклонено — сервер заполнен. Попробуйте позже.", "(новейшее сохранение)", "(последний сыгранный мир)", "день {0}"],
"uk": ["Збереження гри на сервері...", "Гру збережено на сервері.", "Не вдалося зберегти на сервері ({0}).", "сервер не відповідає",
       "Приєднання відхилено — сервер заповнений. Спробуйте пізніше.", "(найновіше збереження)", "(останній зіграний світ)", "день {0}"],
"ar": ["جارٍ حفظ اللعبة على الخادم...", "تم حفظ اللعبة على الخادم.", "فشل الحفظ على الخادم ({0}).", "لا رد من الخادم",
       "رُفض الانضمام – الخادم ممتلئ. حاول لاحقًا.", "(أحدث حفظ)", "(آخر عالم تم لعبه)", "اليوم {0}"],
"ja": ["サーバーにゲームを保存しています...", "サーバーにゲームを保存しました。", "サーバーへの保存に失敗しました（{0}）。", "サーバーから応答がありません",
       "参加拒否 — サーバーは満員です。後でもう一度お試しください。", "（最新のセーブ）", "（最後にプレイしたワールド）", "{0}日目"],
"ko": ["서버에 게임을 저장하는 중...", "서버에 게임이 저장되었습니다.", "서버 저장에 실패했습니다({0}).", "서버 응답 없음",
       "참가 거부 — 서버가 가득 찼습니다. 나중에 다시 시도하세요.", "(최신 저장)", "(마지막으로 플레이한 월드)", "{0}일차"],
"zh-cn": ["正在将游戏保存到服务器...", "游戏已保存到服务器。", "保存到服务器失败（{0}）。", "服务器无响应",
          "加入被拒绝——服务器已满。请稍后再试。", "（最新存档）", "（上次游玩的世界）", "第 {0} 天"],
"zh-tw": ["正在將遊戲儲存到伺服器...", "遊戲已儲存到伺服器。", "儲存到伺服器失敗（{0}）。", "伺服器無回應",
          "加入被拒絕——伺服器已滿。請稍後再試。", "（最新存檔）", "（上次遊玩的世界）", "第 {0} 天"],
}
ph = lambda s: sorted(set(re.findall(r"\{\d\}", s)))
for lang, vals in T.items():
    assert len(vals) == len(K), lang
    for k, en, v in zip(K, T["en"], vals): assert ph(en) == ph(v), (lang, k)
    p = os.path.join(sys.argv[1], lang + ".json"); d = json.load(open(p, encoding="utf-8")); d.update(dict(zip(K, vals)))
    with open(p, "w", encoding="utf-8") as f: json.dump(d, f, ensure_ascii=False, indent=4); f.write("\n")
print(len(T), "languages,", len(K), "keys")
