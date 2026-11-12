# BIZ-LOC-1: the Business page's texts per language, in the order of EN (one line per text). Machine translation except
# German - review welcome. Applied with: python3 tools/biz_locales.py --apply tools/biz_tr.py
EN = """Unknown player
Offers to you
Your offers
Loans
Access
Company
Friends
From
Offer
Amount
Details
To
Status
Player
Loan
Left to repay
Per day
Days left
Vehicles
Homes
Businesses
Member
Steam name
{0} days
Online
Offline
Your company
Offer loan
Offer gift
Leave merger
Cancel merger offer
Propose merger
{0}% for {1} days: you pay ${2} + ${3} a day
Accept
Decline
No offers right now.
{0}% for {1} days: they pay ${2} + ${3} a day
Waiting
Cancel
You have no offers waiting.
You owe
Owes you
Pay all
Confirm ${0}
Pay ${0}
No active loans.
Your company: shares everything
Offline · Your company: shares everything
Proposes a company merger
You
Your company: {0}
You're not in a company. Pick a player on the left and choose Propose merger.
Friend (also on Steam)
Friend
Remove
Steam friend
Wants to be your friend
Request sent
Add friend
No other players online. Friends see each other on the city map.
Gift
Business sale
Players
Banned players ({0})
Ban
Interest
Term
Part payment
Ticked: that player can use yours as if they were their own.
Co-op feature
Merge your company with {0}'s and run them as one: you both get full use of everything the other owns, including businesses, homes and vehicles. Either of you can leave the merger at any time.
Company merger
Confirm
<b>Experimental feature.</b> Company mergers are new and may still have bugs. If something looks wrong, tell the server admin what you were doing when it happened.
A merger runs your companies as <b>one</b>: every member gets full access to everything the others own — businesses, registers and storage, homes, and vehicles — including selling and spending on the company's behalf. Any member can leave the merger at any time.
Propose merging companies with {0}?
They will be asked to accept.
Merge companies with {0}?
Their businesses, homes, warehouses, vehicles and items are removed, and their buildings go back on the market. This can't be undone.
BAN PLAYER
Also remove everything they own
BANNED PLAYERS
No one is banned.
Remove property
Unban
Close
REMOVE PROPERTY
{0} must accept before any money moves. A loan of ${1} costs them ${2} a day in interest plus ${3} a day back, for {4} days: ${5} interest in total ({6}%). The bank charges 20% for 244 days.
{0}: the business, its furniture and staff
Ban {0}? They're removed now and can't join your games until you unban them.
Banned on day {0} · {1}
Steam account
Joined by IP
Remove everything {0} owns?"""

L = {}
L["de"] = """Unbekannter Spieler
Angebote an dich
Deine Angebote
Kredite
Zugriff
Firma
Freunde
Von
Angebot
Betrag
Details
An
Status
Spieler
Kredit
Noch zu zahlen
Pro Tag
Tage übrig
Fahrzeuge
Wohnungen
Geschäfte
Mitglied
Steam-Name
{0} Tage
Online
Offline
Deine Firma
Kredit anbieten
Geschenk anbieten
Fusion verlassen
Fusionsangebot zurückziehen
Fusion vorschlagen
{0}% für {1} Tage: du zahlst ${2} + ${3} pro Tag
Annehmen
Ablehnen
Gerade keine Angebote.
{0}% für {1} Tage: er/sie zahlt ${2} + ${3} pro Tag
Wartet
Abbrechen
Du hast keine offenen Angebote.
Du schuldest
Schuldet dir
Alles zahlen
${0} bestätigen
${0} zahlen
Keine laufenden Kredite.
Deine Firma: teilt alles
Offline · Deine Firma: teilt alles
Schlägt eine Firmenfusion vor
Du
Deine Firma: {0}
Du bist in keiner Firma. Wähle links einen Spieler und dann Fusion vorschlagen.
Freund (auch auf Steam)
Freund
Entfernen
Steam-Freund
Möchte dein Freund sein
Anfrage gesendet
Als Freund hinzufügen
Keine anderen Spieler online. Freunde sehen sich auf der Stadtkarte.
Geschenk
Geschäftsverkauf
Spieler
Gesperrte Spieler ({0})
Sperren
Zinsen
Laufzeit
Teilzahlung
Angehakt: Dieser Spieler kann deines nutzen, als wäre es seins.
Koop-Funktion
Verbinde deine Firma mit der von {0} und führt sie gemeinsam: Ihr beide dürft alles nutzen, was dem anderen gehört, auch Geschäfte, Wohnungen und Fahrzeuge. Jeder von euch kann die Fusion jederzeit verlassen.
Firmenfusion
Bestätigen
<b>Experimentelle Funktion.</b> Firmenfusionen sind neu und können noch Fehler haben. Wenn etwas falsch aussieht, sag dem Server-Admin, was du gerade gemacht hast.
Eine Fusion führt eure Firmen als <b>eine</b>: Jedes Mitglied bekommt vollen Zugriff auf alles, was den anderen gehört — Geschäfte, Kassen und Lager, Wohnungen und Fahrzeuge —, auch Verkaufen und Ausgeben im Namen der Firma. Jedes Mitglied kann die Fusion jederzeit verlassen.
Fusion der Firmen mit {0} vorschlagen?
Er/sie wird gefragt, ob er/sie annimmt.
Firmen mit {0} fusionieren?
Seine/ihre Geschäfte, Wohnungen, Lager, Fahrzeuge und Gegenstände werden entfernt, und die Gebäude kommen wieder auf den Markt. Das lässt sich nicht rückgängig machen.
SPIELER SPERREN
Auch alles entfernen, was ihm/ihr gehört
GESPERRTE SPIELER
Niemand ist gesperrt.
Besitz entfernen
Entsperren
Schließen
BESITZ ENTFERNEN
{0} muss zustimmen, bevor Geld fließt. Ein Kredit über ${1} kostet ihn/sie ${2} Zinsen pro Tag plus ${3} Tilgung pro Tag, {4} Tage lang: ${5} Zinsen insgesamt ({6}%). Die Bank verlangt 20% für 244 Tage.
{0}: das Geschäft mit Einrichtung und Personal
{0} sperren? Er/sie wird sofort entfernt und kann deinen Spielen nicht mehr beitreten, bis du die Sperre aufhebst.
Gesperrt an Tag {0} · {1}
Steam-Konto
Über IP beigetreten
Alles entfernen, was {0} gehört?"""

L["fr"] = """Joueur inconnu
Offres pour toi
Tes offres
Prêts
Accès
Entreprise
Amis
De
Offre
Montant
Détails
À
Statut
Joueur
Prêt
Reste à payer
Par jour
Jours restants
Véhicules
Logements
Commerces
Membre
Nom Steam
{0} jours
En ligne
Hors ligne
Ton entreprise
Proposer un prêt
Offrir un cadeau
Quitter la fusion
Annuler l'offre de fusion
Proposer une fusion
{0}% sur {1} jours : tu paies {2} $ + {3} $ par jour
Accepter
Refuser
Aucune offre pour le moment.
{0}% sur {1} jours : il/elle paie {2} $ + {3} $ par jour
En attente
Annuler
Tu n'as aucune offre en attente.
Tu dois
Te doit
Tout payer
Confirmer {0} $
Payer {0} $
Aucun prêt en cours.
Ton entreprise : partage tout
Hors ligne · Ton entreprise : partage tout
Propose une fusion d'entreprises
Toi
Ton entreprise : {0}
Tu n'es dans aucune entreprise. Choisis un joueur à gauche, puis Proposer une fusion.
Ami (aussi sur Steam)
Ami
Retirer
Ami Steam
Veut être ton ami
Demande envoyée
Ajouter en ami
Aucun autre joueur en ligne. Les amis se voient sur la carte de la ville.
Cadeau
Vente de commerce
Joueurs
Joueurs bannis ({0})
Bannir
Intérêts
Durée
Paiement partiel
Coché : ce joueur peut utiliser le tien comme s'il était à lui.
Fonction coop
Fusionne ton entreprise avec celle de {0} et gérez-les comme une seule : vous pouvez tous deux utiliser tout ce que l'autre possède, commerces, logements et véhicules compris. Chacun peut quitter la fusion à tout moment.
Fusion d'entreprises
Confirmer
<b>Fonction expérimentale.</b> Les fusions d'entreprises sont nouvelles et peuvent encore contenir des bugs. Si quelque chose semble anormal, dis à l'admin du serveur ce que tu faisais à ce moment-là.
Une fusion gère vos entreprises comme <b>une seule</b> : chaque membre a un accès complet à tout ce que possèdent les autres — commerces, caisses et stocks, logements et véhicules —, y compris vendre et dépenser au nom de l'entreprise. Chaque membre peut quitter la fusion à tout moment.
Proposer de fusionner les entreprises avec {0} ?
Il/elle devra accepter.
Fusionner les entreprises avec {0} ?
Ses commerces, logements, entrepôts, véhicules et objets sont supprimés, et ses bâtiments retournent sur le marché. C'est irréversible.
BANNIR LE JOUEUR
Supprimer aussi tout ce qu'il/elle possède
JOUEURS BANNIS
Personne n'est banni.
Supprimer les biens
Débannir
Fermer
SUPPRIMER LES BIENS
{0} doit accepter avant tout mouvement d'argent. Un prêt de {1} $ lui coûte {2} $ d'intérêts par jour plus {3} $ de remboursement par jour, pendant {4} jours : {5} $ d'intérêts au total ({6}%). La banque prend 20% pour 244 jours.
{0} : le commerce, son mobilier et son personnel
Bannir {0} ? Il/elle est retiré(e) maintenant et ne peut plus rejoindre tes parties tant que tu ne le/la débannis pas.
Banni le jour {0} · {1}
Compte Steam
Rejoint par IP
Supprimer tout ce que possède {0} ?"""

L["es"] = """Jugador desconocido
Ofertas para ti
Tus ofertas
Préstamos
Acceso
Empresa
Amigos
De
Oferta
Importe
Detalles
Para
Estado
Jugador
Préstamo
Pendiente de pagar
Por día
Días restantes
Vehículos
Viviendas
Negocios
Miembro
Nombre de Steam
{0} días
En línea
Desconectado
Tu empresa
Ofrecer préstamo
Ofrecer regalo
Salir de la fusión
Retirar oferta de fusión
Proponer fusión
{0}% durante {1} días: pagas ${2} + ${3} al día
Aceptar
Rechazar
No hay ofertas ahora mismo.
{0}% durante {1} días: paga ${2} + ${3} al día
Esperando
Cancelar
No tienes ofertas pendientes.
Debes
Te debe
Pagar todo
Confirmar ${0}
Pagar ${0}
No hay préstamos activos.
Tu empresa: lo comparte todo
Desconectado · Tu empresa: lo comparte todo
Propone una fusión de empresas
Tú
Tu empresa: {0}
No estás en ninguna empresa. Elige un jugador a la izquierda y luego Proponer fusión.
Amigo (también en Steam)
Amigo
Quitar
Amigo de Steam
Quiere ser tu amigo
Solicitud enviada
Añadir amigo
No hay otros jugadores en línea. Los amigos se ven en el mapa de la ciudad.
Regalo
Venta de negocio
Jugadores
Jugadores bloqueados ({0})
Bloquear
Interés
Plazo
Pago parcial
Marcado: ese jugador puede usar lo tuyo como si fuera suyo.
Función cooperativa
Fusiona tu empresa con la de {0} y llevadlas como una sola: ambos podéis usar todo lo que tiene el otro, incluidos negocios, viviendas y vehículos. Cualquiera de los dos puede salir de la fusión en cualquier momento.
Fusión de empresas
Confirmar
<b>Función experimental.</b> Las fusiones de empresas son nuevas y aún pueden tener errores. Si algo parece incorrecto, cuéntale al administrador del servidor qué estabas haciendo.
Una fusión lleva vuestras empresas como <b>una sola</b>: cada miembro tiene acceso completo a todo lo que poseen los demás — negocios, cajas y almacenes, viviendas y vehículos —, incluido vender y gastar en nombre de la empresa. Cualquier miembro puede salir de la fusión en cualquier momento.
¿Proponer fusionar las empresas con {0}?
Se le pedirá que acepte.
¿Fusionar las empresas con {0}?
Sus negocios, viviendas, almacenes, vehículos y objetos se eliminan, y sus edificios vuelven al mercado. No se puede deshacer.
BLOQUEAR JUGADOR
Eliminar también todo lo que posee
JUGADORES BLOQUEADOS
No hay nadie bloqueado.
Eliminar propiedades
Desbloquear
Cerrar
ELIMINAR PROPIEDADES
{0} debe aceptar antes de mover dinero. Un préstamo de ${1} le cuesta ${2} al día en intereses más ${3} al día de devolución, durante {4} días: ${5} de intereses en total ({6}%). El banco cobra un 20% por 244 días.
{0}: el negocio, su mobiliario y su personal
¿Bloquear a {0}? Se le expulsa ahora y no podrá unirse a tus partidas hasta que lo desbloquees.
Bloqueado el día {0} · {1}
Cuenta de Steam
Se unió por IP
¿Eliminar todo lo que posee {0}?"""

L["it"] = """Giocatore sconosciuto
Offerte per te
Le tue offerte
Prestiti
Accesso
Azienda
Amici
Da
Offerta
Importo
Dettagli
A
Stato
Giocatore
Prestito
Da rimborsare
Al giorno
Giorni rimasti
Veicoli
Abitazioni
Attività
Membro
Nome Steam
{0} giorni
Online
Offline
La tua azienda
Offri prestito
Offri regalo
Lascia la fusione
Ritira l'offerta di fusione
Proponi fusione
{0}% per {1} giorni: paghi ${2} + ${3} al giorno
Accetta
Rifiuta
Nessuna offerta al momento.
{0}% per {1} giorni: paga ${2} + ${3} al giorno
In attesa
Annulla
Non hai offerte in attesa.
Devi
Ti deve
Paga tutto
Conferma ${0}
Paga ${0}
Nessun prestito attivo.
La tua azienda: condivide tutto
Offline · La tua azienda: condivide tutto
Propone una fusione aziendale
Tu
La tua azienda: {0}
Non sei in nessuna azienda. Scegli un giocatore a sinistra e poi Proponi fusione.
Amico (anche su Steam)
Amico
Rimuovi
Amico di Steam
Vuole essere tuo amico
Richiesta inviata
Aggiungi amico
Nessun altro giocatore online. Gli amici si vedono sulla mappa della città.
Regalo
Vendita attività
Giocatori
Giocatori banditi ({0})
Banna
Interessi
Durata
Pagamento parziale
Spuntato: quel giocatore può usare il tuo come se fosse suo.
Funzione cooperativa
Unisci la tua azienda a quella di {0} e gestitele come una sola: entrambi potete usare tutto ciò che possiede l'altro, comprese attività, abitazioni e veicoli. Ognuno di voi può lasciare la fusione in qualsiasi momento.
Fusione aziendale
Conferma
<b>Funzione sperimentale.</b> Le fusioni aziendali sono nuove e potrebbero avere ancora dei bug. Se qualcosa sembra sbagliato, di' all'amministratore del server cosa stavi facendo.
Una fusione gestisce le vostre aziende come <b>una sola</b>: ogni membro ha pieno accesso a tutto ciò che possiedono gli altri — attività, casse e magazzini, abitazioni e veicoli —, compreso vendere e spendere per conto dell'azienda. Ogni membro può lasciare la fusione in qualsiasi momento.
Proporre di fondere le aziende con {0}?
Gli verrà chiesto di accettare.
Fondere le aziende con {0}?
Le sue attività, abitazioni, magazzini, veicoli e oggetti vengono rimossi e i suoi edifici tornano sul mercato. Non si può annullare.
BANNA GIOCATORE
Rimuovi anche tutto ciò che possiede
GIOCATORI BANDITI
Nessuno è bandito.
Rimuovi proprietà
Sbanna
Chiudi
RIMUOVI PROPRIETÀ
{0} deve accettare prima che il denaro si muova. Un prestito di ${1} gli costa ${2} al giorno di interessi più ${3} al giorno di rimborso, per {4} giorni: ${5} di interessi in totale ({6}%). La banca chiede il 20% per 244 giorni.
{0}: l'attività, i suoi arredi e il personale
Bannare {0}? Viene rimosso subito e non potrà unirsi alle tue partite finché non lo sbanni.
Bandito il giorno {0} · {1}
Account Steam
Entrato tramite IP
Rimuovere tutto ciò che possiede {0}?"""

L["pt"] = """Jogador desconhecido
Ofertas para você
Suas ofertas
Empréstimos
Acesso
Empresa
Amigos
De
Oferta
Valor
Detalhes
Para
Status
Jogador
Empréstimo
Falta pagar
Por dia
Dias restantes
Veículos
Casas
Negócios
Membro
Nome na Steam
{0} dias
Online
Offline
Sua empresa
Oferecer empréstimo
Oferecer presente
Sair da fusão
Cancelar oferta de fusão
Propor fusão
{0}% por {1} dias: você paga ${2} + ${3} por dia
Aceitar
Recusar
Nenhuma oferta no momento.
{0}% por {1} dias: ele/ela paga ${2} + ${3} por dia
Aguardando
Cancelar
Você não tem ofertas pendentes.
Você deve
Deve a você
Pagar tudo
Confirmar ${0}
Pagar ${0}
Nenhum empréstimo ativo.
Sua empresa: compartilha tudo
Offline · Sua empresa: compartilha tudo
Propõe uma fusão de empresas
Você
Sua empresa: {0}
Você não está em nenhuma empresa. Escolha um jogador à esquerda e depois Propor fusão.
Amigo (também na Steam)
Amigo
Remover
Amigo da Steam
Quer ser seu amigo
Pedido enviado
Adicionar amigo
Nenhum outro jogador online. Amigos se veem no mapa da cidade.
Presente
Venda de negócio
Jogadores
Jogadores banidos ({0})
Banir
Juros
Prazo
Pagamento parcial
Marcado: esse jogador pode usar o seu como se fosse dele.
Recurso cooperativo
Una sua empresa à de {0} e administrem-nas como uma só: vocês dois podem usar tudo o que o outro possui, incluindo negócios, casas e veículos. Qualquer um pode sair da fusão a qualquer momento.
Fusão de empresas
Confirmar
<b>Recurso experimental.</b> Fusões de empresas são novas e ainda podem ter bugs. Se algo parecer errado, conte ao administrador do servidor o que você estava fazendo.
Uma fusão administra suas empresas como <b>uma só</b>: cada membro tem acesso total a tudo o que os outros possuem — negócios, caixas e estoques, casas e veículos —, incluindo vender e gastar em nome da empresa. Qualquer membro pode sair da fusão a qualquer momento.
Propor a fusão das empresas com {0}?
Ele/ela precisará aceitar.
Fundir as empresas com {0}?
Os negócios, casas, armazéns, veículos e itens dele/dela são removidos, e os prédios voltam ao mercado. Isso não pode ser desfeito.
BANIR JOGADOR
Remover também tudo o que ele/ela possui
JOGADORES BANIDOS
Ninguém está banido.
Remover propriedades
Desbanir
Fechar
REMOVER PROPRIEDADES
{0} precisa aceitar antes de qualquer dinheiro ser movido. Um empréstimo de ${1} custa a ele/ela ${2} por dia em juros mais ${3} por dia de devolução, por {4} dias: ${5} de juros no total ({6}%). O banco cobra 20% por 244 dias.
{0}: o negócio, seus móveis e funcionários
Banir {0}? Ele/ela é removido(a) agora e não pode entrar nos seus jogos até você desbanir.
Banido no dia {0} · {1}
Conta Steam
Entrou por IP
Remover tudo o que {0} possui?"""

L["nl"] = """Onbekende speler
Aanbiedingen aan jou
Jouw aanbiedingen
Leningen
Toegang
Bedrijf
Vrienden
Van
Aanbod
Bedrag
Details
Aan
Status
Speler
Lening
Nog te betalen
Per dag
Dagen over
Voertuigen
Woningen
Zaken
Lid
Steam-naam
{0} dagen
Online
Offline
Jouw bedrijf
Lening aanbieden
Cadeau aanbieden
Fusie verlaten
Fusieaanbod intrekken
Fusie voorstellen
{0}% voor {1} dagen: jij betaalt ${2} + ${3} per dag
Accepteren
Weigeren
Op dit moment geen aanbiedingen.
{0}% voor {1} dagen: hij/zij betaalt ${2} + ${3} per dag
Wacht
Annuleren
Je hebt geen openstaande aanbiedingen.
Jij bent verschuldigd
Is jou verschuldigd
Alles betalen
${0} bevestigen
${0} betalen
Geen lopende leningen.
Jouw bedrijf: deelt alles
Offline · Jouw bedrijf: deelt alles
Stelt een bedrijfsfusie voor
Jij
Jouw bedrijf: {0}
Je zit in geen bedrijf. Kies links een speler en dan Fusie voorstellen.
Vriend (ook op Steam)
Vriend
Verwijderen
Steam-vriend
Wil je vriend zijn
Verzoek verstuurd
Vriend toevoegen
Geen andere spelers online. Vrienden zien elkaar op de stadskaart.
Cadeau
Verkoop van zaak
Spelers
Verbannen spelers ({0})
Verbannen
Rente
Looptijd
Deelbetaling
Aangevinkt: die speler kan het jouwe gebruiken alsof het van hem/haar is.
Coöpfunctie
Voeg je bedrijf samen met dat van {0} en leid ze als één: jullie mogen allebei alles gebruiken wat de ander heeft, ook zaken, woningen en voertuigen. Ieder van jullie kan de fusie op elk moment verlaten.
Bedrijfsfusie
Bevestigen
<b>Experimentele functie.</b> Bedrijfsfusies zijn nieuw en kunnen nog bugs hebben. Als iets niet klopt, vertel de serverbeheerder wat je op dat moment deed.
Een fusie leidt jullie bedrijven als <b>één</b>: elk lid krijgt volledige toegang tot alles wat de anderen bezitten — zaken, kassa's en opslag, woningen en voertuigen —, inclusief verkopen en uitgeven namens het bedrijf. Elk lid kan de fusie op elk moment verlaten.
Fusie van de bedrijven met {0} voorstellen?
Hij/zij moet het accepteren.
Bedrijven samenvoegen met {0}?
Zijn/haar zaken, woningen, magazijnen, voertuigen en spullen worden verwijderd, en de gebouwen komen weer op de markt. Dit kan niet ongedaan worden gemaakt.
SPELER VERBANNEN
Ook alles verwijderen wat hij/zij bezit
VERBANNEN SPELERS
Niemand is verbannen.
Bezit verwijderen
Verbanning opheffen
Sluiten
BEZIT VERWIJDEREN
{0} moet akkoord gaan voordat er geld wordt overgemaakt. Een lening van ${1} kost hem/haar ${2} rente per dag plus ${3} aflossing per dag, {4} dagen lang: ${5} rente in totaal ({6}%). De bank rekent 20% voor 244 dagen.
{0}: de zaak met inrichting en personeel
{0} verbannen? Hij/zij wordt nu verwijderd en kan niet meer aan je spellen deelnemen tot je de verbanning opheft.
Verbannen op dag {0} · {1}
Steam-account
Via IP aangesloten
Alles verwijderen wat {0} bezit?"""

L["pl"] = """Nieznany gracz
Oferty dla ciebie
Twoje oferty
Pożyczki
Dostęp
Firma
Znajomi
Od
Oferta
Kwota
Szczegóły
Do
Status
Gracz
Pożyczka
Pozostało do spłaty
Dziennie
Pozostało dni
Pojazdy
Mieszkania
Firmy
Członek
Nazwa Steam
{0} dni
Online
Offline
Twoja firma
Zaproponuj pożyczkę
Zaproponuj prezent
Opuść fuzję
Wycofaj ofertę fuzji
Zaproponuj fuzję
{0}% na {1} dni: płacisz ${2} + ${3} dziennie
Akceptuj
Odrzuć
Brak ofert w tej chwili.
{0}% na {1} dni: płaci ${2} + ${3} dziennie
Oczekuje
Anuluj
Nie masz oczekujących ofert.
Jesteś winien
Jest ci winien
Zapłać wszystko
Potwierdź ${0}
Zapłać ${0}
Brak aktywnych pożyczek.
Twoja firma: dzieli wszystko
Offline · Twoja firma: dzieli wszystko
Proponuje fuzję firm
Ty
Twoja firma: {0}
Nie należysz do żadnej firmy. Wybierz gracza po lewej, a potem Zaproponuj fuzję.
Znajomy (także na Steam)
Znajomy
Usuń
Znajomy ze Steam
Chce być twoim znajomym
Zaproszenie wysłane
Dodaj znajomego
Brak innych graczy online. Znajomi widzą się na mapie miasta.
Prezent
Sprzedaż firmy
Gracze
Zbanowani gracze ({0})
Zbanuj
Odsetki
Okres
Spłata częściowa
Zaznaczone: ten gracz może korzystać z twojego, jakby było jego.
Funkcja kooperacji
Połącz swoją firmę z firmą gracza {0} i prowadźcie je jako jedną: oboje możecie korzystać ze wszystkiego, co ma drugi, w tym z firm, mieszkań i pojazdów. Każde z was może opuścić fuzję w dowolnej chwili.
Fuzja firm
Potwierdź
<b>Funkcja eksperymentalna.</b> Fuzje firm są nowe i mogą jeszcze zawierać błędy. Jeśli coś wygląda źle, powiedz administratorowi serwera, co wtedy robiłeś.
Fuzja prowadzi wasze firmy jako <b>jedną</b>: każdy członek ma pełny dostęp do wszystkiego, co mają pozostali — firm, kas i magazynów, mieszkań i pojazdów — łącznie ze sprzedażą i wydatkami w imieniu firmy. Każdy członek może opuścić fuzję w dowolnej chwili.
Zaproponować fuzję firm z graczem {0}?
Zostanie poproszony o akceptację.
Połączyć firmy z graczem {0}?
Jego firmy, mieszkania, magazyny, pojazdy i przedmioty zostaną usunięte, a budynki wrócą na rynek. Tego nie można cofnąć.
ZBANUJ GRACZA
Usuń też wszystko, co do niego należy
ZBANOWANI GRACZE
Nikt nie jest zbanowany.
Usuń majątek
Odbanuj
Zamknij
USUŃ MAJĄTEK
{0} musi zaakceptować, zanim pieniądze zostaną przelane. Pożyczka ${1} kosztuje go ${2} odsetek dziennie plus ${3} spłaty dziennie przez {4} dni: łącznie ${5} odsetek ({6}%). Bank bierze 20% za 244 dni.
{0}: firma z wyposażeniem i personelem
Zbanować gracza {0}? Zostanie teraz usunięty i nie dołączy do twoich gier, dopóki go nie odbanujesz.
Zbanowany w dniu {0} · {1}
Konto Steam
Dołączył przez IP
Usunąć wszystko, co należy do gracza {0}?"""

L["ru"] = """Неизвестный игрок
Предложения тебе
Твои предложения
Займы
Доступ
Компания
Друзья
От
Предложение
Сумма
Подробности
Кому
Статус
Игрок
Заём
Осталось выплатить
В день
Осталось дней
Транспорт
Жильё
Бизнесы
Участник
Имя в Steam
{0} дн.
В сети
Не в сети
Твоя компания
Предложить заём
Предложить подарок
Выйти из слияния
Отозвать предложение о слиянии
Предложить слияние
{0}% на {1} дн.: ты платишь ${2} + ${3} в день
Принять
Отклонить
Сейчас предложений нет.
{0}% на {1} дн.: он(а) платит ${2} + ${3} в день
Ожидание
Отмена
У тебя нет ожидающих предложений.
Ты должен
Должен тебе
Оплатить всё
Подтвердить ${0}
Оплатить ${0}
Нет активных займов.
Твоя компания: всё общее
Не в сети · Твоя компания: всё общее
Предлагает слияние компаний
Ты
Твоя компания: {0}
Ты не состоишь в компании. Выбери игрока слева и нажми «Предложить слияние».
Друг (также в Steam)
Друг
Удалить
Друг в Steam
Хочет стать твоим другом
Запрос отправлен
Добавить в друзья
Других игроков в сети нет. Друзья видят друг друга на карте города.
Подарок
Продажа бизнеса
Игроки
Заблокированные игроки ({0})
Заблокировать
Проценты
Срок
Частичный платёж
Отмечено: этот игрок может пользоваться твоим, как своим.
Совместная функция
Объедини свою компанию с компанией игрока {0} и ведите их как одну: вы оба можете пользоваться всем, что есть у другого, включая бизнесы, жильё и транспорт. Любой из вас может выйти из слияния в любой момент.
Слияние компаний
Подтвердить
<b>Экспериментальная функция.</b> Слияния компаний — новинка и могут содержать ошибки. Если что-то выглядит неправильно, расскажи администратору сервера, что ты в тот момент делал.
Слияние ведёт ваши компании как <b>одну</b>: каждый участник получает полный доступ ко всему, чем владеют другие, — бизнесам, кассам и складам, жилью и транспорту, — включая продажу и траты от имени компании. Любой участник может выйти из слияния в любой момент.
Предложить слияние компаний с игроком {0}?
Его попросят принять предложение.
Объединить компании с игроком {0}?
Его бизнесы, жильё, склады, транспорт и предметы будут удалены, а здания вернутся на рынок. Это нельзя отменить.
ЗАБЛОКИРОВАТЬ ИГРОКА
Также удалить всё, что ему принадлежит
ЗАБЛОКИРОВАННЫЕ ИГРОКИ
Никто не заблокирован.
Удалить имущество
Разблокировать
Закрыть
УДАЛИТЬ ИМУЩЕСТВО
{0} должен согласиться, прежде чем деньги будут переведены. Заём ${1} обойдётся ему в ${2} процентов в день плюс ${3} погашения в день в течение {4} дн.: всего ${5} процентов ({6}%). Банк берёт 20% за 244 дня.
{0}: бизнес с обстановкой и персоналом
Заблокировать игрока {0}? Он будет удалён сейчас и не сможет заходить в твои игры, пока ты его не разблокируешь.
Заблокирован в день {0} · {1}
Аккаунт Steam
Вошёл по IP
Удалить всё, что принадлежит игроку {0}?"""

L["uk"] = """Невідомий гравець
Пропозиції тобі
Твої пропозиції
Позики
Доступ
Компанія
Друзі
Від
Пропозиція
Сума
Подробиці
Кому
Статус
Гравець
Позика
Залишилось сплатити
На день
Залишилось днів
Транспорт
Житло
Бізнеси
Учасник
Ім'я в Steam
{0} дн.
У мережі
Не в мережі
Твоя компанія
Запропонувати позику
Запропонувати подарунок
Вийти зі злиття
Відкликати пропозицію злиття
Запропонувати злиття
{0}% на {1} дн.: ти платиш ${2} + ${3} на день
Прийняти
Відхилити
Зараз пропозицій немає.
{0}% на {1} дн.: він/вона платить ${2} + ${3} на день
Очікування
Скасувати
У тебе немає пропозицій в очікуванні.
Ти винен
Винен тобі
Сплатити все
Підтвердити ${0}
Сплатити ${0}
Немає активних позик.
Твоя компанія: усе спільне
Не в мережі · Твоя компанія: усе спільне
Пропонує злиття компаній
Ти
Твоя компанія: {0}
Ти не в компанії. Обери гравця ліворуч і натисни «Запропонувати злиття».
Друг (також у Steam)
Друг
Видалити
Друг у Steam
Хоче стати твоїм другом
Запит надіслано
Додати в друзі
Інших гравців у мережі немає. Друзі бачать одне одного на мапі міста.
Подарунок
Продаж бізнесу
Гравці
Заблоковані гравці ({0})
Заблокувати
Відсотки
Строк
Частковий платіж
Позначено: цей гравець може користуватися твоїм, як своїм.
Спільна функція
Об'єднай свою компанію з компанією гравця {0} і ведіть їх як одну: ви обидва можете користуватися всім, що має інший, зокрема бізнесами, житлом і транспортом. Будь-хто з вас може вийти зі злиття будь-коли.
Злиття компаній
Підтвердити
<b>Експериментальна функція.</b> Злиття компаній — новинка і можуть мати помилки. Якщо щось виглядає не так, розкажи адміністраторові сервера, що ти тоді робив.
Злиття веде ваші компанії як <b>одну</b>: кожен учасник має повний доступ до всього, чим володіють інші, — бізнесів, кас і складів, житла й транспорту, — включно з продажем і витратами від імені компанії. Будь-який учасник може вийти зі злиття будь-коли.
Запропонувати злиття компаній з гравцем {0}?
Його попросять погодитися.
Об'єднати компанії з гравцем {0}?
Його бізнеси, житло, склади, транспорт і предмети буде видалено, а будівлі повернуться на ринок. Це не можна скасувати.
ЗАБЛОКУВАТИ ГРАВЦЯ
Також видалити все, що йому належить
ЗАБЛОКОВАНІ ГРАВЦІ
Ніхто не заблокований.
Видалити майно
Розблокувати
Закрити
ВИДАЛИТИ МАЙНО
{0} має погодитися, перш ніж гроші буде переказано. Позика ${1} коштує йому ${2} відсотків на день плюс ${3} погашення на день протягом {4} дн.: усього ${5} відсотків ({6}%). Банк бере 20% за 244 дні.
{0}: бізнес з обстановкою та персоналом
Заблокувати гравця {0}? Його буде видалено зараз, і він не зможе приєднуватися до твоїх ігор, доки ти його не розблокуєш.
Заблоковано в день {0} · {1}
Акаунт Steam
Приєднався за IP
Видалити все, що належить гравцеві {0}?"""

L["tr"] = """Bilinmeyen oyuncu
Sana gelen teklifler
Tekliflerin
Krediler
Erişim
Şirket
Arkadaşlar
Kimden
Teklif
Tutar
Ayrıntılar
Kime
Durum
Oyuncu
Kredi
Kalan borç
Günlük
Kalan gün
Araçlar
Evler
İşletmeler
Üye
Steam adı
{0} gün
Çevrimiçi
Çevrimdışı
Şirketin
Kredi teklif et
Hediye teklif et
Birleşmeden ayrıl
Birleşme teklifini geri çek
Birleşme öner
{1} gün için %{0}: günde ${2} + ${3} ödersin
Kabul et
Reddet
Şu an teklif yok.
{1} gün için %{0}: günde ${2} + ${3} öder
Bekliyor
İptal
Bekleyen teklifin yok.
Borcun
Sana borçlu
Hepsini öde
${0} onayla
${0} öde
Aktif kredi yok.
Şirketin: her şeyi paylaşır
Çevrimdışı · Şirketin: her şeyi paylaşır
Şirket birleşmesi öneriyor
Sen
Şirketin: {0}
Bir şirkette değilsin. Soldan bir oyuncu seç, sonra Birleşme öner.
Arkadaş (Steam'de de)
Arkadaş
Kaldır
Steam arkadaşı
Arkadaşın olmak istiyor
İstek gönderildi
Arkadaş ekle
Çevrimiçi başka oyuncu yok. Arkadaşlar birbirini şehir haritasında görür.
Hediye
İşletme satışı
Oyuncular
Yasaklı oyuncular ({0})
Yasakla
Faiz
Vade
Kısmi ödeme
İşaretli: o oyuncu seninkini kendisininmiş gibi kullanabilir.
Ortak oyun özelliği
Şirketini {0} adlı oyuncunun şirketiyle birleştir ve tek şirket gibi yönetin: ikiniz de diğerinin sahip olduğu her şeyi, işletmeler, evler ve araçlar dahil, kullanabilirsiniz. İkinizden biri birleşmeden her an ayrılabilir.
Şirket birleşmesi
Onayla
<b>Deneysel özellik.</b> Şirket birleşmeleri yenidir ve hâlâ hata içerebilir. Bir şey yanlış görünürse, o sırada ne yaptığını sunucu yöneticisine anlat.
Birleşme şirketlerinizi <b>tek</b> şirket olarak yönetir: her üye, diğerlerinin sahip olduğu her şeye — işletmeler, kasalar ve depolar, evler ve araçlar — tam erişim alır; şirket adına satış ve harcama da buna dahildir. Her üye birleşmeden her an ayrılabilir.
{0} ile şirket birleşmesi önerilsin mi?
Kabul etmesi istenecek.
Şirketler {0} ile birleştirilsin mi?
İşletmeleri, evleri, depoları, araçları ve eşyaları kaldırılır, binaları yeniden piyasaya çıkar. Bu geri alınamaz.
OYUNCUYU YASAKLA
Sahip olduğu her şeyi de kaldır
YASAKLI OYUNCULAR
Kimse yasaklı değil.
Mülkü kaldır
Yasağı kaldır
Kapat
MÜLKÜ KALDIR
Para hareket etmeden önce {0} kabul etmeli. ${1} tutarındaki kredi ona {4} gün boyunca günde ${2} faiz artı günde ${3} geri ödemeye mal olur: toplam ${5} faiz (%{6}). Banka 244 gün için %20 alır.
{0}: işletme, mobilyaları ve personeli
{0} yasaklansın mı? Şimdi çıkarılır ve yasağı kaldırana kadar oyunlarına katılamaz.
{0}. günde yasaklandı · {1}
Steam hesabı
IP ile katıldı
{0} adlı oyuncunun sahip olduğu her şey kaldırılsın mı?"""

L["cs"] = """Neznámý hráč
Nabídky pro tebe
Tvoje nabídky
Půjčky
Přístup
Firma
Přátelé
Od
Nabídka
Částka
Podrobnosti
Komu
Stav
Hráč
Půjčka
Zbývá splatit
Denně
Zbývá dní
Vozidla
Bydlení
Podniky
Člen
Jméno na Steamu
{0} dní
Online
Offline
Tvoje firma
Nabídnout půjčku
Nabídnout dárek
Opustit fúzi
Stáhnout nabídku fúze
Navrhnout fúzi
{0} % na {1} dní: platíš ${2} + ${3} denně
Přijmout
Odmítnout
Momentálně žádné nabídky.
{0} % na {1} dní: platí ${2} + ${3} denně
Čeká
Zrušit
Nemáš žádné čekající nabídky.
Dlužíš
Dluží ti
Zaplatit vše
Potvrdit ${0}
Zaplatit ${0}
Žádné aktivní půjčky.
Tvoje firma: sdílí vše
Offline · Tvoje firma: sdílí vše
Navrhuje fúzi firem
Ty
Tvoje firma: {0}
Nejsi v žádné firmě. Vyber vlevo hráče a pak Navrhnout fúzi.
Přítel (i na Steamu)
Přítel
Odebrat
Přítel na Steamu
Chce být tvým přítelem
Žádost odeslána
Přidat přítele
Žádní další hráči online. Přátelé se vidí na mapě města.
Dárek
Prodej podniku
Hráči
Zablokovaní hráči ({0})
Zablokovat
Úrok
Doba
Částečná platba
Zaškrtnuto: ten hráč může používat tvoje, jako by bylo jeho.
Kooperativní funkce
Spoj svou firmu s firmou hráče {0} a veďte je jako jednu: oba můžete používat vše, co vlastní ten druhý, včetně podniků, bydlení a vozidel. Kdokoli z vás může fúzi kdykoli opustit.
Fúze firem
Potvrdit
<b>Experimentální funkce.</b> Fúze firem jsou nové a mohou ještě obsahovat chyby. Pokud něco vypadá špatně, řekni správci serveru, co jsi zrovna dělal.
Fúze vede vaše firmy jako <b>jednu</b>: každý člen má plný přístup ke všemu, co vlastní ostatní — podnikům, pokladnám a skladům, bydlení a vozidlům — včetně prodeje a utrácení jménem firmy. Každý člen může fúzi kdykoli opustit.
Navrhnout fúzi firem s hráčem {0}?
Bude požádán o souhlas.
Sloučit firmy s hráčem {0}?
Jeho podniky, bydlení, sklady, vozidla a předměty budou odstraněny a budovy se vrátí na trh. Nelze to vrátit zpět.
ZABLOKOVAT HRÁČE
Odstranit i vše, co mu patří
ZABLOKOVANÍ HRÁČI
Nikdo není zablokován.
Odstranit majetek
Odblokovat
Zavřít
ODSTRANIT MAJETEK
{0} musí souhlasit, než se pohnou peníze. Půjčka ${1} ho stojí ${2} úroku denně plus ${3} splátky denně po dobu {4} dní: celkem ${5} úroku ({6} %). Banka si účtuje 20 % za 244 dní.
{0}: podnik s vybavením a personálem
Zablokovat hráče {0}? Bude teď odebrán a nemůže se připojovat k tvým hrám, dokud ho neodblokuješ.
Zablokován v den {0} · {1}
Účet Steam
Připojen přes IP
Odstranit vše, co patří hráči {0}?"""

L["da"] = """Ukendt spiller
Tilbud til dig
Dine tilbud
Lån
Adgang
Firma
Venner
Fra
Tilbud
Beløb
Detaljer
Til
Status
Spiller
Lån
Mangler at betale
Pr. dag
Dage tilbage
Køretøjer
Boliger
Forretninger
Medlem
Steam-navn
{0} dage
Online
Offline
Dit firma
Tilbyd lån
Tilbyd gave
Forlad fusionen
Træk fusionstilbud tilbage
Foreslå fusion
{0}% i {1} dage: du betaler ${2} + ${3} om dagen
Accepter
Afvis
Ingen tilbud lige nu.
{0}% i {1} dage: han/hun betaler ${2} + ${3} om dagen
Venter
Annuller
Du har ingen ventende tilbud.
Du skylder
Skylder dig
Betal alt
Bekræft ${0}
Betal ${0}
Ingen aktive lån.
Dit firma: deler alt
Offline · Dit firma: deler alt
Foreslår en firmafusion
Dig
Dit firma: {0}
Du er ikke i et firma. Vælg en spiller til venstre og derefter Foreslå fusion.
Ven (også på Steam)
Ven
Fjern
Steam-ven
Vil være din ven
Anmodning sendt
Tilføj ven
Ingen andre spillere online. Venner ser hinanden på bykortet.
Gave
Salg af forretning
Spillere
Udelukkede spillere ({0})
Udeluk
Rente
Løbetid
Delbetaling
Afkrydset: den spiller kan bruge dit, som om det var hans/hendes eget.
Co-op-funktion
Fusionér dit firma med {0}s og driv dem som ét: I kan begge bruge alt, hvad den anden ejer, også forretninger, boliger og køretøjer. Hver af jer kan forlade fusionen når som helst.
Firmafusion
Bekræft
<b>Eksperimentel funktion.</b> Firmafusioner er nye og kan stadig have fejl. Hvis noget ser forkert ud, så fortæl serveradministratoren, hvad du lavede.
En fusion driver jeres firmaer som <b>ét</b>: hvert medlem får fuld adgang til alt, hvad de andre ejer — forretninger, kasser og lagre, boliger og køretøjer — også salg og forbrug på firmaets vegne. Ethvert medlem kan forlade fusionen når som helst.
Foreslå at fusionere firmaerne med {0}?
Han/hun bliver bedt om at acceptere.
Fusionér firmaerne med {0}?
Hans/hendes forretninger, boliger, lagre, køretøjer og genstande fjernes, og bygningerne kommer på markedet igen. Det kan ikke fortrydes.
UDELUK SPILLER
Fjern også alt, hvad han/hun ejer
UDELUKKEDE SPILLERE
Ingen er udelukket.
Fjern ejendom
Ophæv udelukkelse
Luk
FJERN EJENDOM
{0} skal acceptere, før der flyttes penge. Et lån på ${1} koster ham/hende ${2} i renter om dagen plus ${3} i afdrag om dagen i {4} dage: ${5} i renter i alt ({6}%). Banken tager 20% for 244 dage.
{0}: forretningen med inventar og personale
Udeluk {0}? Han/hun fjernes nu og kan ikke deltage i dine spil, før du ophæver udelukkelsen.
Udelukket på dag {0} · {1}
Steam-konto
Kom ind via IP
Fjern alt, hvad {0} ejer?"""

L["fi"] = """Tuntematon pelaaja
Tarjoukset sinulle
Omat tarjouksesi
Lainat
Käyttöoikeudet
Yritys
Ystävät
Lähettäjä
Tarjous
Summa
Tiedot
Vastaanottaja
Tila
Pelaaja
Laina
Maksamatta
Päivässä
Päiviä jäljellä
Ajoneuvot
Asunnot
Liikkeet
Jäsen
Steam-nimi
{0} päivää
Paikalla
Poissa
Yrityksesi
Tarjoa lainaa
Tarjoa lahja
Poistu fuusiosta
Peru fuusiotarjous
Ehdota fuusiota
{0} % {1} päiväksi: maksat ${2} + ${3} päivässä
Hyväksy
Hylkää
Ei tarjouksia juuri nyt.
{0} % {1} päiväksi: hän maksaa ${2} + ${3} päivässä
Odottaa
Peruuta
Sinulla ei ole odottavia tarjouksia.
Olet velkaa
On sinulle velkaa
Maksa kaikki
Vahvista ${0}
Maksa ${0}
Ei käynnissä olevia lainoja.
Yrityksesi: jakaa kaiken
Poissa · Yrityksesi: jakaa kaiken
Ehdottaa yritysfuusiota
Sinä
Yrityksesi: {0}
Et ole missään yrityksessä. Valitse vasemmalta pelaaja ja sitten Ehdota fuusiota.
Ystävä (myös Steamissa)
Ystävä
Poista
Steam-ystävä
Haluaa ystäväksesi
Pyyntö lähetetty
Lisää ystäväksi
Muita pelaajia ei ole paikalla. Ystävät näkevät toisensa kaupungin kartalla.
Lahja
Liikkeen myynti
Pelaajat
Estetyt pelaajat ({0})
Estä
Korko
Laina-aika
Osamaksu
Valittu: pelaaja voi käyttää sinun omaasi kuin omaansa.
Yhteistyöominaisuus
Yhdistä yrityksesi pelaajan {0} yritykseen ja johtakaa niitä yhtenä: voitte molemmat käyttää kaikkea, mitä toinen omistaa, myös liikkeitä, asuntoja ja ajoneuvoja. Kumpi tahansa voi poistua fuusiosta milloin vain.
Yritysfuusio
Vahvista
<b>Kokeellinen ominaisuus.</b> Yritysfuusiot ovat uusia ja niissä voi vielä olla virheitä. Jos jokin näyttää väärältä, kerro palvelimen ylläpitäjälle, mitä olit tekemässä.
Fuusio johtaa yrityksiänne <b>yhtenä</b>: jokainen jäsen saa täyden pääsyn kaikkeen, mitä muut omistavat — liikkeisiin, kassoihin ja varastoihin, asuntoihin ja ajoneuvoihin — myös myyntiin ja kuluttamiseen yrityksen puolesta. Kuka tahansa jäsen voi poistua fuusiosta milloin vain.
Ehdotetaanko yritysten fuusiota pelaajan {0} kanssa?
Häntä pyydetään hyväksymään.
Yhdistetäänkö yritykset pelaajan {0} kanssa?
Hänen liikkeensä, asuntonsa, varastonsa, ajoneuvonsa ja tavaransa poistetaan, ja rakennukset palaavat markkinoille. Tätä ei voi perua.
ESTÄ PELAAJA
Poista myös kaikki, mitä hän omistaa
ESTETYT PELAAJAT
Ketään ei ole estetty.
Poista omaisuus
Poista esto
Sulje
POISTA OMAISUUS
Pelaajan {0} on hyväksyttävä ennen kuin rahaa siirtyy. ${1} laina maksaa hänelle ${2} korkoa päivässä sekä ${3} lyhennystä päivässä {4} päivän ajan: korkoa yhteensä ${5} ({6} %). Pankki perii 20 % 244 päivältä.
{0}: liike kalusteineen ja henkilökuntineen
Estetäänkö {0}? Hänet poistetaan nyt, eikä hän voi liittyä peleihisi ennen kuin poistat eston.
Estetty päivänä {0} · {1}
Steam-tili
Liittyi IP:llä
Poistetaanko kaikki, mitä pelaaja {0} omistaa?"""

L["hu"] = """Ismeretlen játékos
Neked szóló ajánlatok
Ajánlataid
Kölcsönök
Hozzáférés
Cég
Barátok
Feladó
Ajánlat
Összeg
Részletek
Címzett
Állapot
Játékos
Kölcsön
Még fizetendő
Naponta
Hátralévő napok
Járművek
Lakások
Üzletek
Tag
Steam-név
{0} nap
Online
Offline
A céged
Kölcsön ajánlása
Ajándék ajánlása
Kilépés az egyesülésből
Egyesülési ajánlat visszavonása
Egyesülés javaslata
{0}% {1} napra: naponta ${2} + ${3} fizetsz
Elfogadás
Elutasítás
Jelenleg nincs ajánlat.
{0}% {1} napra: naponta ${2} + ${3} fizet
Várakozik
Mégse
Nincs függő ajánlatod.
Tartozol
Tartozik neked
Mindet kifizetem
${0} megerősítése
${0} fizetése
Nincs aktív kölcsön.
A céged: mindent megoszt
Offline · A céged: mindent megoszt
Cégegyesülést javasol
Te
A céged: {0}
Nem vagy cégben. Válassz bal oldalt egy játékost, majd Egyesülés javaslata.
Barát (Steamen is)
Barát
Eltávolítás
Steam-barát
A barátod szeretne lenni
Kérés elküldve
Barát hozzáadása
Nincs más játékos online. A barátok látják egymást a város térképén.
Ajándék
Üzlet eladása
Játékosok
Kitiltott játékosok ({0})
Kitiltás
Kamat
Futamidő
Részfizetés
Bejelölve: az a játékos úgy használhatja a tiédet, mintha az övé lenne.
Kooperatív funkció
Egyesítsd a cégedet {0} cégével, és vezessétek egyként: mindketten használhattok mindent, ami a másiké, az üzleteket, lakásokat és járműveket is. Bármelyikőtök bármikor kiléphet az egyesülésből.
Cégegyesülés
Megerősítés
<b>Kísérleti funkció.</b> A cégegyesülések újak, és még lehetnek bennük hibák. Ha valami rossznak tűnik, mondd el a szerver adminjának, mit csináltál éppen.
Az egyesülés a cégeiteket <b>egyként</b> vezeti: minden tag teljes hozzáférést kap mindenhez, ami a többieké — üzletekhez, kasszákhoz és raktárakhoz, lakásokhoz és járművekhez —, beleértve az eladást és a költést a cég nevében. Bármelyik tag bármikor kiléphet az egyesülésből.
Javaslod a cégek egyesítését vele: {0}?
Meg kell majd erősítenie.
Egyesíted a cégeket vele: {0}?
Az üzletei, lakásai, raktárai, járművei és tárgyai törlődnek, az épületei pedig visszakerülnek a piacra. Ez nem vonható vissza.
JÁTÉKOS KITILTÁSA
Mindent törölj, ami az övé
KITILTOTT JÁTÉKOSOK
Senki sincs kitiltva.
Vagyon törlése
Kitiltás feloldása
Bezárás
VAGYON TÖRLÉSE
{0} jóváhagyása kell, mielőtt pénz mozdul. ${1} kölcsön {4} napon át naponta ${2} kamatba és ${3} törlesztésbe kerül neki: összesen ${5} kamat ({6}%). A bank 20%-ot kér 244 napra.
{0}: az üzlet berendezéssel és személyzettel
Kitiltod: {0}? Most eltávolítjuk, és nem csatlakozhat a játékaidhoz, amíg fel nem oldod a kitiltást.
Kitiltva a(z) {0}. napon · {1}
Steam-fiók
IP-cím alapján csatlakozott
Mindent törölsz, ami {0} tulajdona?"""

L["el"] = """Άγνωστος παίκτης
Προσφορές προς εσένα
Οι προσφορές σου
Δάνεια
Πρόσβαση
Εταιρεία
Φίλοι
Από
Προσφορά
Ποσό
Λεπτομέρειες
Προς
Κατάσταση
Παίκτης
Δάνειο
Υπόλοιπο πληρωμής
Ανά ημέρα
Ημέρες που απομένουν
Οχήματα
Κατοικίες
Επιχειρήσεις
Μέλος
Όνομα Steam
{0} ημέρες
Συνδεδεμένος
Αποσυνδεδεμένος
Η εταιρεία σου
Πρόταση δανείου
Πρόταση δώρου
Αποχώρηση από τη συγχώνευση
Ακύρωση πρότασης συγχώνευσης
Πρόταση συγχώνευσης
{0}% για {1} ημέρες: πληρώνεις ${2} + ${3} την ημέρα
Αποδοχή
Απόρριψη
Καμία προσφορά αυτή τη στιγμή.
{0}% για {1} ημέρες: πληρώνει ${2} + ${3} την ημέρα
Σε αναμονή
Ακύρωση
Δεν έχεις εκκρεμείς προσφορές.
Χρωστάς
Σου χρωστάει
Πληρωμή όλων
Επιβεβαίωση ${0}
Πληρωμή ${0}
Κανένα ενεργό δάνειο.
Η εταιρεία σου: τα μοιράζεται όλα
Αποσυνδεδεμένος · Η εταιρεία σου: τα μοιράζεται όλα
Προτείνει συγχώνευση εταιρειών
Εσύ
Η εταιρεία σου: {0}
Δεν είσαι σε καμία εταιρεία. Διάλεξε έναν παίκτη αριστερά και μετά Πρόταση συγχώνευσης.
Φίλος (και στο Steam)
Φίλος
Αφαίρεση
Φίλος στο Steam
Θέλει να γίνει φίλος σου
Το αίτημα στάλθηκε
Προσθήκη φίλου
Κανένας άλλος παίκτης συνδεδεμένος. Οι φίλοι βλέπουν ο ένας τον άλλον στον χάρτη της πόλης.
Δώρο
Πώληση επιχείρησης
Παίκτες
Αποκλεισμένοι παίκτες ({0})
Αποκλεισμός
Τόκος
Διάρκεια
Μερική πληρωμή
Επιλεγμένο: αυτός ο παίκτης μπορεί να χρησιμοποιεί το δικό σου σαν να ήταν δικό του.
Λειτουργία συνεργασίας
Συγχώνευσε την εταιρεία σου με του/της {0} και διοικήστε τες ως μία: μπορείτε και οι δύο να χρησιμοποιείτε ό,τι έχει ο άλλος, μαζί με επιχειρήσεις, κατοικίες και οχήματα. Οποιοσδήποτε από εσάς μπορεί να αποχωρήσει από τη συγχώνευση οποτεδήποτε.
Συγχώνευση εταιρειών
Επιβεβαίωση
<b>Πειραματική λειτουργία.</b> Οι συγχωνεύσεις εταιρειών είναι καινούριες και μπορεί να έχουν ακόμα σφάλματα. Αν κάτι φαίνεται λάθος, πες στον διαχειριστή του διακομιστή τι έκανες εκείνη τη στιγμή.
Η συγχώνευση διοικεί τις εταιρείες σας ως <b>μία</b>: κάθε μέλος έχει πλήρη πρόσβαση σε ό,τι έχουν οι άλλοι — επιχειρήσεις, ταμεία και αποθήκες, κατοικίες και οχήματα — μαζί με πωλήσεις και έξοδα για λογαριασμό της εταιρείας. Κάθε μέλος μπορεί να αποχωρήσει από τη συγχώνευση οποτεδήποτε.
Πρόταση συγχώνευσης εταιρειών με {0};
Θα του/της ζητηθεί να αποδεχτεί.
Συγχώνευση εταιρειών με {0};
Οι επιχειρήσεις, οι κατοικίες, οι αποθήκες, τα οχήματα και τα αντικείμενά του/της αφαιρούνται και τα κτίριά του/της επιστρέφουν στην αγορά. Αυτό δεν αναιρείται.
ΑΠΟΚΛΕΙΣΜΟΣ ΠΑΙΚΤΗ
Αφαίρεση και όλων όσων του/της ανήκουν
ΑΠΟΚΛΕΙΣΜΕΝΟΙ ΠΑΙΚΤΕΣ
Κανείς δεν είναι αποκλεισμένος.
Αφαίρεση περιουσίας
Άρση αποκλεισμού
Κλείσιμο
ΑΦΑΙΡΕΣΗ ΠΕΡΙΟΥΣΙΑΣ
Ο/Η {0} πρέπει να αποδεχτεί πριν μετακινηθούν χρήματα. Ένα δάνειο ${1} του/της κοστίζει ${2} τόκο την ημέρα συν ${3} αποπληρωμή την ημέρα για {4} ημέρες: συνολικά ${5} τόκος ({6}%). Η τράπεζα χρεώνει 20% για 244 ημέρες.
{0}: η επιχείρηση με τον εξοπλισμό και το προσωπικό της
Αποκλεισμός του/της {0}; Αφαιρείται τώρα και δεν μπορεί να μπει στα παιχνίδια σου μέχρι να άρεις τον αποκλεισμό.
Αποκλείστηκε την ημέρα {0} · {1}
Λογαριασμός Steam
Μπήκε μέσω IP
Αφαίρεση όλων όσων ανήκουν στον/στην {0};"""

L["ro"] = """Jucător necunoscut
Oferte pentru tine
Ofertele tale
Împrumuturi
Acces
Companie
Prieteni
De la
Ofertă
Sumă
Detalii
Către
Stare
Jucător
Împrumut
Rest de plată
Pe zi
Zile rămase
Vehicule
Locuințe
Afaceri
Membru
Nume Steam
{0} zile
Online
Offline
Compania ta
Oferă împrumut
Oferă cadou
Părăsește fuziunea
Retrage oferta de fuziune
Propune fuziune
{0}% pentru {1} zile: plătești ${2} + ${3} pe zi
Acceptă
Refuză
Nicio ofertă acum.
{0}% pentru {1} zile: plătește ${2} + ${3} pe zi
În așteptare
Anulează
Nu ai oferte în așteptare.
Datorezi
Îți datorează
Plătește tot
Confirmă ${0}
Plătește ${0}
Niciun împrumut activ.
Compania ta: împarte totul
Offline · Compania ta: împarte totul
Propune o fuziune de companii
Tu
Compania ta: {0}
Nu ești într-o companie. Alege un jucător în stânga, apoi Propune fuziune.
Prieten (și pe Steam)
Prieten
Elimină
Prieten pe Steam
Vrea să-ți fie prieten
Cerere trimisă
Adaugă prieten
Niciun alt jucător online. Prietenii se văd pe harta orașului.
Cadou
Vânzare de afacere
Jucători
Jucători blocați ({0})
Blochează
Dobândă
Durată
Plată parțială
Bifat: acel jucător îl poate folosi pe al tău ca și cum ar fi al lui.
Funcție cooperativă
Unește-ți compania cu a lui {0} și conduceți-le ca pe una singură: amândoi puteți folosi tot ce deține celălalt, inclusiv afaceri, locuințe și vehicule. Oricare dintre voi poate părăsi fuziunea oricând.
Fuziune de companii
Confirmă
<b>Funcție experimentală.</b> Fuziunile de companii sunt noi și pot avea încă erori. Dacă ceva pare greșit, spune-i administratorului serverului ce făceai atunci.
O fuziune conduce companiile voastre ca pe <b>una</b>: fiecare membru are acces deplin la tot ce dețin ceilalți — afaceri, case de marcat și depozite, locuințe și vehicule —, inclusiv vânzări și cheltuieli în numele companiei. Orice membru poate părăsi fuziunea oricând.
Propui fuziunea companiilor cu {0}?
I se va cere să accepte.
Unești companiile cu {0}?
Afacerile, locuințele, depozitele, vehiculele și obiectele sale sunt eliminate, iar clădirile revin pe piață. Acțiunea nu poate fi anulată.
BLOCHEAZĂ JUCĂTORUL
Elimină și tot ce deține
JUCĂTORI BLOCAȚI
Nimeni nu este blocat.
Elimină proprietățile
Deblochează
Închide
ELIMINĂ PROPRIETĂȚILE
{0} trebuie să accepte înainte să se miște bani. Un împrumut de ${1} îl costă ${2} dobândă pe zi plus ${3} rambursare pe zi, timp de {4} zile: ${5} dobândă în total ({6}%). Banca cere 20% pentru 244 de zile.
{0}: afacerea, cu mobilierul și personalul ei
Blochezi pe {0}? Este eliminat acum și nu se poate alătura jocurilor tale până nu îl deblochezi.
Blocat în ziua {0} · {1}
Cont Steam
Intrat prin IP
Elimini tot ce deține {0}?"""

L["lt"] = """Nežinomas žaidėjas
Pasiūlymai tau
Tavo pasiūlymai
Paskolos
Prieiga
Įmonė
Draugai
Nuo
Pasiūlymas
Suma
Išsami informacija
Kam
Būsena
Žaidėjas
Paskola
Liko sumokėti
Per dieną
Liko dienų
Transportas
Būstai
Verslai
Narys
Steam vardas
{0} d.
Prisijungęs
Atsijungęs
Tavo įmonė
Pasiūlyti paskolą
Pasiūlyti dovaną
Palikti susijungimą
Atšaukti susijungimo pasiūlymą
Pasiūlyti susijungimą
{0} % {1} d.: moki ${2} + ${3} per dieną
Priimti
Atmesti
Šiuo metu pasiūlymų nėra.
{0} % {1} d.: jis/ji moka ${2} + ${3} per dieną
Laukiama
Atšaukti
Neturi laukiančių pasiūlymų.
Tu skolingas
Skolingas tau
Sumokėti viską
Patvirtinti ${0}
Sumokėti ${0}
Aktyvių paskolų nėra.
Tavo įmonė: dalijasi viskuo
Atsijungęs · Tavo įmonė: dalijasi viskuo
Siūlo įmonių susijungimą
Tu
Tavo įmonė: {0}
Nesi jokioje įmonėje. Kairėje pasirink žaidėją, tada Pasiūlyti susijungimą.
Draugas (ir Steam)
Draugas
Pašalinti
Steam draugas
Nori būti tavo draugu
Užklausa išsiųsta
Pridėti draugą
Kitų prisijungusių žaidėjų nėra. Draugai mato vienas kitą miesto žemėlapyje.
Dovana
Verslo pardavimas
Žaidėjai
Užblokuoti žaidėjai ({0})
Užblokuoti
Palūkanos
Terminas
Dalinis mokėjimas
Pažymėta: tas žaidėjas gali naudotis tavo, lyg būtų jo.
Bendradarbiavimo funkcija
Sujunk savo įmonę su žaidėjo {0} įmone ir valdykite jas kaip vieną: abu galite naudotis viskuo, ką turi kitas, įskaitant verslus, būstus ir transportą. Bet kuris iš jūsų gali bet kada palikti susijungimą.
Įmonių susijungimas
Patvirtinti
<b>Eksperimentinė funkcija.</b> Įmonių susijungimai yra nauji ir dar gali turėti klaidų. Jei kas nors atrodo ne taip, papasakok serverio administratoriui, ką tuo metu darei.
Susijungimas valdo jūsų įmones kaip <b>vieną</b>: kiekvienas narys gauna visišką prieigą prie visko, ką turi kiti — verslų, kasų ir sandėlių, būstų ir transporto — įskaitant pardavimą ir išlaidas įmonės vardu. Bet kuris narys gali bet kada palikti susijungimą.
Pasiūlyti įmonių susijungimą su {0}?
Jo bus paprašyta sutikti.
Sujungti įmones su {0}?
Jo verslai, būstai, sandėliai, transportas ir daiktai pašalinami, o pastatai grįžta į rinką. To negalima atšaukti.
UŽBLOKUOTI ŽAIDĖJĄ
Pašalinti ir viską, kas jam priklauso
UŽBLOKUOTI ŽAIDĖJAI
Niekas neužblokuotas.
Pašalinti turtą
Atblokuoti
Uždaryti
PAŠALINTI TURTĄ
{0} turi sutikti, kol pinigai dar nepervesti. ${1} paskola jam kainuoja ${2} palūkanų per dieną ir ${3} grąžinimo per dieną {4} d.: iš viso ${5} palūkanų ({6} %). Bankas ima 20 % už 244 dienas.
{0}: verslas su baldais ir darbuotojais
Užblokuoti {0}? Jis bus pašalintas dabar ir negalės prisijungti prie tavo žaidimų, kol jo neatblokuosi.
Užblokuotas {0} dieną · {1}
Steam paskyra
Prisijungė per IP
Pašalinti viską, kas priklauso {0}?"""

L["ja"] = """不明なプレイヤー
あなたへのオファー
あなたのオファー
ローン
アクセス
会社
フレンド
送り主
オファー
金額
詳細
宛先
状態
プレイヤー
ローン
残りの返済額
1日あたり
残り日数
車両
住居
ビジネス
メンバー
Steam名
{0}日
オンライン
オフライン
あなたの会社
ローンを提案
ギフトを贈る
合併を離脱
合併の提案を取り消す
合併を提案
{1}日間 {0}%: 1日あたり ${2} + ${3} を支払う
承諾
拒否
現在オファーはありません。
{1}日間 {0}%: 相手が1日あたり ${2} + ${3} を支払う
待機中
キャンセル
保留中のオファーはありません。
あなたの借り
あなたへの借り
全額支払う
${0} を確定
${0} を支払う
有効なローンはありません。
あなたの会社: すべてを共有
オフライン · あなたの会社: すべてを共有
会社の合併を提案しています
あなた
あなたの会社: {0}
会社に所属していません。左でプレイヤーを選び、「合併を提案」を押してください。
フレンド(Steamでも)
フレンド
削除
Steamフレンド
フレンドになりたがっています
リクエスト送信済み
フレンドに追加
ほかにオンラインのプレイヤーはいません。フレンド同士は街の地図でお互いが見えます。
ギフト
ビジネス売却
プレイヤー
追放したプレイヤー ({0})
追放
利息
期間
一部支払い
チェック: そのプレイヤーはあなたのものを自分のもののように使えます。
協力機能
あなたの会社を{0}の会社と合併し、ひとつとして経営します: お互いが相手の持つすべて(ビジネス、住居、車両を含む)を使えます。どちらもいつでも合併を離脱できます。
会社の合併
確定
<b>実験的な機能です。</b>会社の合併は新しい機能で、まだ不具合があるかもしれません。おかしな点があれば、そのとき何をしていたかをサーバー管理者に伝えてください。
合併すると会社は<b>ひとつ</b>として運営されます: 各メンバーはほかのメンバーが持つすべて — ビジネス、レジと倉庫、住居、車両 — に完全にアクセスでき、会社の名義で売却や支出もできます。どのメンバーもいつでも合併を離脱できます。
{0}に会社の合併を提案しますか?
相手に承諾を求めます。
{0}と会社を合併しますか?
そのプレイヤーのビジネス、住居、倉庫、車両、アイテムは削除され、建物は市場に戻ります。元に戻せません。
プレイヤーを追放
所有物もすべて削除する
追放したプレイヤー
追放されたプレイヤーはいません。
資産を削除
追放を解除
閉じる
資産を削除
お金が動く前に{0}の承諾が必要です。${1} のローンは相手に{4}日間、1日あたり利息 ${2} と返済 ${3} がかかります: 利息は合計 ${5}（{6}%）。銀行は244日で20%です。
{0}: ビジネス、その家具とスタッフ
{0}を追放しますか？今すぐ退出させられ、解除するまであなたのゲームに参加できません。
{0}日目に追放 · {1}
Steamアカウント
IPで参加
{0}の所有物をすべて削除しますか？"""

L["ko"] = """알 수 없는 플레이어
받은 제안
보낸 제안
대출
접근 권한
회사
친구
보낸 사람
제안
금액
세부 정보
받는 사람
상태
플레이어
대출
남은 상환액
하루
남은 일수
차량
주거
사업체
멤버
Steam 이름
{0}일
온라인
오프라인
내 회사
대출 제안
선물 제안
합병 탈퇴
합병 제안 취소
합병 제안
{1}일 동안 {0}%: 하루 ${2} + ${3} 지불
수락
거절
현재 제안이 없습니다.
{1}일 동안 {0}%: 상대가 하루 ${2} + ${3} 지불
대기 중
취소
대기 중인 제안이 없습니다.
내가 갚을 돈
나에게 갚을 돈
전액 지불
${0} 확인
${0} 지불
진행 중인 대출이 없습니다.
내 회사: 모든 것을 공유
오프라인 · 내 회사: 모든 것을 공유
회사 합병을 제안함
나
내 회사: {0}
회사에 속해 있지 않습니다. 왼쪽에서 플레이어를 고른 뒤 합병 제안을 누르세요.
친구 (Steam에서도)
친구
삭제
Steam 친구
친구가 되고 싶어 합니다
요청 보냄
친구 추가
온라인인 다른 플레이어가 없습니다. 친구끼리는 도시 지도에서 서로 보입니다.
선물
사업체 매각
플레이어
차단된 플레이어 ({0})
차단
이자
기간
부분 상환
체크: 그 플레이어는 내 것을 자기 것처럼 쓸 수 있습니다.
협동 기능
내 회사를 {0}의 회사와 합병해 하나로 운영합니다: 둘 다 상대가 가진 모든 것(사업체, 주거, 차량 포함)을 쓸 수 있습니다. 누구든 언제든지 합병에서 탈퇴할 수 있습니다.
회사 합병
확인
<b>실험적 기능입니다.</b> 회사 합병은 새로운 기능이라 아직 버그가 있을 수 있습니다. 이상한 점이 보이면 그때 무엇을 하고 있었는지 서버 관리자에게 알려 주세요.
합병하면 회사들이 <b>하나</b>로 운영됩니다: 모든 멤버가 다른 멤버가 가진 모든 것 — 사업체, 계산대와 창고, 주거, 차량 — 에 완전히 접근하며, 회사 명의로 판매하고 지출할 수도 있습니다. 어떤 멤버든 언제든지 합병에서 탈퇴할 수 있습니다.
{0}에게 회사 합병을 제안할까요?
상대에게 수락을 요청합니다.
{0}와 회사를 합병할까요?
그 플레이어의 사업체, 주거, 창고, 차량, 아이템이 삭제되고 건물은 시장에 다시 나옵니다. 되돌릴 수 없습니다.
플레이어 차단
소유한 모든 것도 삭제
차단된 플레이어
차단된 플레이어가 없습니다.
재산 삭제
차단 해제
닫기
재산 삭제
돈이 오가기 전에 {0}의 수락이 필요합니다. ${1} 대출은 상대에게 {4}일 동안 하루 이자 ${2}와 하루 상환 ${3}이 듭니다: 이자 합계 ${5} ({6}%). 은행은 244일에 20%를 받습니다.
{0}: 사업체와 가구, 직원
{0}을(를) 차단할까요? 지금 바로 내보내지며, 차단을 해제할 때까지 내 게임에 참가할 수 없습니다.
{0}일째 차단 · {1}
Steam 계정
IP로 참가
{0}의 소유물을 모두 삭제할까요?"""

L["zh-cn"] = """未知玩家
给你的报价
你的报价
贷款
权限
公司
好友
来自
报价
金额
详情
发给
状态
玩家
贷款
剩余待还
每天
剩余天数
车辆
住所
生意
成员
Steam 名称
{0} 天
在线
离线
你的公司
提供贷款
赠送礼物
退出合并
撤回合并报价
提议合并
{1} 天 {0}%：你每天支付 ${2} + ${3}
接受
拒绝
目前没有报价。
{1} 天 {0}%：对方每天支付 ${2} + ${3}
等待中
取消
你没有待处理的报价。
你欠
欠你
全部还清
确认 ${0}
支付 ${0}
没有进行中的贷款。
你的公司：共享一切
离线 · 你的公司：共享一切
提议公司合并
你
你的公司：{0}
你不在任何公司中。在左侧选择一名玩家，然后点击“提议合并”。
好友（Steam 上也是）
好友
移除
Steam 好友
想成为你的好友
请求已发送
添加好友
没有其他在线玩家。好友可以在城市地图上看到彼此。
礼物
出售生意
玩家
被封禁的玩家（{0}）
封禁
利息
期限
部分还款
已勾选：该玩家可以像使用自己的一样使用你的。
合作功能
将你的公司与{0}的公司合并并作为一家公司经营：你们都可以使用对方拥有的一切，包括生意、住所和车辆。你们任何一方都可以随时退出合并。
公司合并
确认
<b>实验性功能。</b>公司合并是新功能，可能仍有错误。如果有异常，请告诉服务器管理员你当时在做什么。
合并后你们的公司作为<b>一家</b>经营：每名成员都可完全使用其他成员拥有的一切——生意、收银台和仓库、住所和车辆——包括以公司名义出售和花钱。任何成员都可以随时退出合并。
向{0}提议合并公司？
将请对方接受。
与{0}合并公司？
其生意、住所、仓库、车辆和物品将被移除，建筑将重新上市。此操作无法撤销。
封禁玩家
同时移除其拥有的一切
被封禁的玩家
没有被封禁的玩家。
移除财产
解除封禁
关闭
移除财产
资金流动前必须由{0}接受。${1} 的贷款在 {4} 天内每天需支付利息 ${2} 外加还款 ${3}：利息共计 ${5}（{6}%）。银行收取 244 天 20%。
{0}：生意及其家具和员工
封禁{0}？对方现在会被移除，在你解除封禁前无法加入你的游戏。
第 {0} 天封禁 · {1}
Steam 账户
通过 IP 加入
移除{0}拥有的一切？"""

L["zh-tw"] = """未知玩家
給你的報價
你的報價
貸款
權限
公司
好友
來自
報價
金額
詳情
發給
狀態
玩家
貸款
剩餘待還
每天
剩餘天數
車輛
住所
生意
成員
Steam 名稱
{0} 天
線上
離線
你的公司
提供貸款
贈送禮物
退出合併
撤回合併報價
提議合併
{1} 天 {0}%：你每天支付 ${2} + ${3}
接受
拒絕
目前沒有報價。
{1} 天 {0}%：對方每天支付 ${2} + ${3}
等待中
取消
你沒有待處理的報價。
你欠
欠你
全部還清
確認 ${0}
支付 ${0}
沒有進行中的貸款。
你的公司：共享一切
離線 · 你的公司：共享一切
提議公司合併
你
你的公司：{0}
你不在任何公司中。在左側選擇一名玩家，然後點擊「提議合併」。
好友（Steam 上也是）
好友
移除
Steam 好友
想成為你的好友
請求已送出
加為好友
沒有其他線上玩家。好友可以在城市地圖上看到彼此。
禮物
出售生意
玩家
被封鎖的玩家（{0}）
封鎖
利息
期限
部分還款
已勾選：該玩家可以像使用自己的一樣使用你的。
合作功能
將你的公司與{0}的公司合併並作為一家公司經營：你們都可以使用對方擁有的一切，包括生意、住所和車輛。你們任何一方都可以隨時退出合併。
公司合併
確認
<b>實驗性功能。</b>公司合併是新功能，可能仍有錯誤。如果有異常，請告訴伺服器管理員你當時在做什麼。
合併後你們的公司作為<b>一家</b>經營：每名成員都可完全使用其他成員擁有的一切——生意、收銀台和倉庫、住所和車輛——包括以公司名義出售和花錢。任何成員都可以隨時退出合併。
向{0}提議合併公司？
將請對方接受。
與{0}合併公司？
其生意、住所、倉庫、車輛和物品將被移除，建築將重新上市。此操作無法復原。
封鎖玩家
同時移除其擁有的一切
被封鎖的玩家
沒有被封鎖的玩家。
移除財產
解除封鎖
關閉
移除財產
資金流動前必須由{0}接受。${1} 的貸款在 {4} 天內每天需支付利息 ${2} 外加還款 ${3}：利息共計 ${5}（{6}%）。銀行收取 244 天 20%。
{0}：生意及其家具和員工
封鎖{0}？對方現在會被移除，在你解除封鎖前無法加入你的遊戲。
第 {0} 天封鎖 · {1}
Steam 帳號
透過 IP 加入
移除{0}擁有的一切？"""

L["ar"] = """لاعب غير معروف
عروض لك
عروضك
القروض
الوصول
الشركة
الأصدقاء
من
العرض
المبلغ
التفاصيل
إلى
الحالة
اللاعب
قرض
المتبقي للسداد
يوميًا
الأيام المتبقية
المركبات
المساكن
الأعمال
عضو
اسم Steam
{0} يوم
متصل
غير متصل
شركتك
عرض قرض
عرض هدية
مغادرة الاندماج
سحب عرض الاندماج
اقتراح اندماج
{0}% لمدة {1} يوم: تدفع ${2} + ${3} يوميًا
قبول
رفض
لا توجد عروض حاليًا.
{0}% لمدة {1} يوم: يدفع ${2} + ${3} يوميًا
بانتظار الرد
إلغاء
ليست لديك عروض معلّقة.
أنت مدين
مدين لك
دفع الكل
تأكيد ${0}
دفع ${0}
لا توجد قروض نشطة.
شركتك: تتشارك كل شيء
غير متصل · شركتك: تتشارك كل شيء
يقترح اندماج الشركات
أنت
شركتك: {0}
لست في أي شركة. اختر لاعبًا على اليسار ثم اقتراح اندماج.
صديق (على Steam أيضًا)
صديق
إزالة
صديق على Steam
يريد أن يكون صديقك
تم إرسال الطلب
إضافة صديق
لا يوجد لاعبون آخرون متصلون. يرى الأصدقاء بعضهم على خريطة المدينة.
هدية
بيع عمل تجاري
اللاعبون
اللاعبون المحظورون ({0})
حظر
الفائدة
المدة
دفعة جزئية
محدد: يمكن لهذا اللاعب استخدام ما لديك كأنه له.
ميزة تعاونية
ادمج شركتك مع شركة {0} وأديراهما كشركة واحدة: يمكنكما استخدام كل ما يملكه الآخر، بما في ذلك الأعمال والمساكن والمركبات. يمكن لأي منكما مغادرة الاندماج في أي وقت.
اندماج الشركات
تأكيد
<b>ميزة تجريبية.</b> اندماج الشركات جديد وقد يحتوي على أخطاء. إذا بدا شيء خاطئًا، فأخبر مسؤول الخادم بما كنت تفعله حينها.
يدير الاندماج شركاتكم كشركة <b>واحدة</b>: يحصل كل عضو على وصول كامل إلى كل ما يملكه الآخرون — الأعمال والصناديق والمخازن والمساكن والمركبات — بما في ذلك البيع والإنفاق باسم الشركة. يمكن لأي عضو مغادرة الاندماج في أي وقت.
هل تقترح دمج الشركات مع {0}؟
سيُطلب منه القبول.
هل تدمج الشركات مع {0}؟
تُزال أعماله ومساكنه ومخازنه ومركباته وأغراضه، وتعود مبانيه إلى السوق. لا يمكن التراجع عن ذلك.
حظر اللاعب
إزالة كل ما يملكه أيضًا
اللاعبون المحظورون
لا يوجد أحد محظور.
إزالة الممتلكات
إلغاء الحظر
إغلاق
إزالة الممتلكات
يجب أن يوافق {0} قبل تحريك أي أموال. قرض بقيمة ${1} يكلّفه ${2} فائدة يوميًا إضافةً إلى ${3} سدادًا يوميًا لمدة {4} يوم: إجمالي الفائدة ${5} ({6}%). يفرض البنك 20% لمدة 244 يومًا.
{0}: العمل التجاري بأثاثه وموظفيه
حظر {0}؟ سيُزال الآن ولن يستطيع الانضمام إلى ألعابك حتى تلغي الحظر.
محظور في اليوم {0} · {1}
حساب Steam
انضم عبر IP
إزالة كل ما يملكه {0}؟"""

def _lines(s):
    return s.split("\n")

E = _lines(EN)
T = {}
for lang, block in L.items():
    tr = _lines(block)
    assert len(tr) == len(E), (lang, len(tr), len(E))
    T[lang] = dict(zip(E, tr))
