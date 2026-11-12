"""Server key texts (DEDICATED-KEY-1): merges into Locales/<lang>.json. Machine translations - review welcome.
    python3 tools/gen_locales_phase3.py Locales"""
import json, os, sys
T = {
 "en": ["Server key", "Join refused — this server needs its key. Enter it in the Server key field of Join a Game."],
 "de": ["Server-Schlüssel", "Beitritt abgelehnt — dieser Server braucht seinen Schlüssel. Trage ihn unter „Spiel beitreten“ im Feld „Server-Schlüssel“ ein."],
 "fr": ["Clé du serveur", "Connexion refusée — ce serveur exige sa clé. Saisissez-la dans le champ Clé du serveur de Rejoindre une partie."],
 "es": ["Clave del servidor", "Unión rechazada — este servidor necesita su clave. Escríbela en el campo Clave del servidor de Unirse a una partida."],
 "it": ["Chiave del server", "Accesso rifiutato — questo server richiede la sua chiave. Inseriscila nel campo Chiave del server di Partecipa a una partita."],
 "pt": ["Chave do servidor", "Entrada recusada — este servidor precisa da chave dele. Digite-a no campo Chave do servidor em Entrar em um jogo."],
 "nl": ["Serversleutel", "Deelname geweigerd — deze server vraagt om zijn sleutel. Vul die in bij Serversleutel in Deelnemen aan een spel."],
 "da": ["Servernøgle", "Deltagelse afvist — denne server kræver sin nøgle. Indtast den i feltet Servernøgle under Deltag i et spil."],
 "fi": ["Palvelimen avain", "Liittyminen hylätty — tämä palvelin vaatii avaimensa. Kirjoita se Liity peliin -näkymän Palvelimen avain -kenttään."],
 "pl": ["Klucz serwera", "Dołączenie odrzucone — ten serwer wymaga swojego klucza. Wpisz go w polu Klucz serwera w oknie Dołącz do gry."],
 "cs": ["Klíč serveru", "Připojení odmítnuto — tento server vyžaduje svůj klíč. Zadejte ho do pole Klíč serveru v okně Připojit se ke hře."],
 "hu": ["Szerverkulcs", "Csatlakozás elutasítva — ehhez a szerverhez kulcs kell. Írd be a Csatlakozás játékhoz ablak Szerverkulcs mezőjébe."],
 "ro": ["Cheia serverului", "Intrare refuzată — acest server cere cheia lui. Introdu-o în câmpul Cheia serverului din Alătură-te unui joc."],
 "lt": ["Serverio raktas", "Prisijungimas atmestas — šiam serveriui reikia jo rakto. Įrašykite jį lauke Serverio raktas lange Prisijungti prie žaidimo."],
 "el": ["Κλειδί διακομιστή", "Η σύνδεση απορρίφθηκε — αυτός ο διακομιστής χρειάζεται το κλειδί του. Πληκτρολόγησέ το στο πεδίο Κλειδί διακομιστή της Συμμετοχής σε παιχνίδι."],
 "tr": ["Sunucu anahtarı", "Katılım reddedildi — bu sunucu anahtarını istiyor. Bir oyuna katıl penceresindeki Sunucu anahtarı alanına yaz."],
 "ru": ["Ключ сервера", "Подключение отклонено — этому серверу нужен его ключ. Введите его в поле «Ключ сервера» окна «Присоединиться к игре»."],
 "uk": ["Ключ сервера", "Підключення відхилено — цьому серверу потрібен його ключ. Введіть його в полі «Ключ сервера» вікна «Приєднатися до гри»."],
 "ar": ["مفتاح الخادم", "تم رفض الانضمام — يحتاج هذا الخادم إلى مفتاحه. أدخله في حقل مفتاح الخادم في نافذة الانضمام إلى لعبة."],
 "ja": ["サーバーキー", "参加拒否 — このサーバーにはキーが必要です。「ゲームに参加」の「サーバーキー」欄に入力してください。"],
 "ko": ["서버 키", "참가 거부 — 이 서버는 키가 필요합니다. 게임 참가 창의 서버 키 칸에 입력하세요."],
 "zh-cn": ["服务器密钥", "加入被拒绝 — 此服务器需要它的密钥。请在“加入游戏”的“服务器密钥”栏中输入。"],
 "zh-tw": ["伺服器金鑰", "加入遭拒 — 此伺服器需要它的金鑰。請在「加入遊戲」的「伺服器金鑰」欄中輸入。"],
}
for lang, (a, b) in T.items():
    p = os.path.join(sys.argv[1], lang + ".json"); d = json.load(open(p, encoding="utf-8"))
    d["gps_ui_server_key"] = a; d["gps_reason_joinkey"] = b
    with open(p, "w", encoding="utf-8") as f: json.dump(d, f, ensure_ascii=False, indent=4); f.write("\n")
print(len(T), "languages")
