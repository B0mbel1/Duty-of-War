# Duty of War ⚔️

Taktyczna gra strategiczna czasu rzeczywistego (RTS) stworzona w silniku **Unity (C#)**, w której dowodzisz oddziałami za pomocą poleceń w języku naturalnym, interpretowanych na żywo przez model **Google Gemini AI**.

---

## ✨ Główne funkcjonalności

* **Dowodzenie w języku naturalnym (LLM Commander):** Wydawanie rozkazów tekstem po polsku (np. *"Jazda, oflankuj ich od wschodu"*, *"Łucznicy zająć północne wzgórze"*, *"Wszyscy formować żółwia"*). Model AI tłumaczy intencję gracza na ustrukturyzowane komendy JSON (`MOVE`, `ATTACK`, `FORM`, `STOP`) z uwzględnieniem aktualnego kontekstu pola bitwy i zaznaczonych jednostek.
* **Taktyczne AI przeciwnika (Enemy General):** Przeciwnik działa w dwóch trybach — **historycznym** (opartym na fazach bitwy i wyzwalaczach takich jak czas, dystans czy procent strat) oraz **dynamicznym** (adaptującym taktykę w zależności od osobowości generała: *Aggressive*, *Defensive*, *Balanced*, *Tactician*).
* **Automatyczna analiza terenu (Terrain Scanner):** System skanujący mapę pod kątem ukształtowania wysokościowego i warstw terenu, automatycznie wyznaczający punkty strategiczne (wzgórza, lasy, rzeki) oraz ich krawędzie (*Front*, *Back*, *Top*).
* **Formacje i manewry:** Obsługa wielu szyków bojowych (*Line*, *Double Line*, *Wedge*, *Turtle*, *Box*, *Square*, *Loose*, *Column*) oraz sekwencyjnych manewrów oskrzydlających.

---

## 🎮 Jak zagrać (Szybki start bez Unity)

Gotową, skompilowaną wersję gry dla systemu Windows znajdziesz w zakładce **[Releases](../../releases)** po prawej stronie repozytorium:

1. Pobierz najnowsze archiwum `.zip`.
2. Wypakuj całą zawartość archiwum do dowolnego folderu.
3. Uruchom plik wykonywalny gry (`.exe`).

### Przykładowe rozkazy, które możesz wpisać w trakcie bitwy:
* *"Wszyscy do ataku!"*
* *"Jazda, oflankować ich od zachodu!"*
* *"Łucznicy zająć wzgórze na północy!"*
* *"Wybrani, formacja żółwia!"*
* *"Piechota 50 metrów do przodu!"*

---

## 🛠️ Uruchomienie z kodu źródłowego (Unity)

Jeśli chcesz otworzyć projekt w edytorze Unity:

1. Sklonuj repozytorium:
   ```bash
   git clone [https://github.com/B0mbel1/Duty-of-War.git](https://github.com/B0mbel1/Duty-of-War.git)
