---
status: proposed
---

# Walidacja żądań przed wykonaniem endpointu

Kontrakt wejściowy HTTP sprawdzamy po odczytaniu ciała JSON i przed uruchomieniem metody endpointu. Reguły obowiązujące przy każdym wywołaniu operacji egzekwuje przypadek użycia lub domena, również przy wywołaniu przez publiczne API modułu. Dzięki temu reguły kontraktu HTTP pozostają przy modelach żądań, a przypadki użycia egzekwują własne warunki niezależnie od sposobu wywołania.

Moduły definiują reguły żądań w osobnych walidatorach FluentValidation. Wybieramy tę bibliotekę, ponieważ w kolejnych modułach spodziewamy się reguł warunkowych i zależności między polami. Osobne walidatory pozwalają grupować te reguły i składać walidację z walidatorów zagnieżdżonych obiektów oraz elementów kolekcji.

Moduł rejestruje walidatory swoich modeli żądań. Wspólny filtr Minimal APIs wywołuje właściwy walidator po odczytaniu ciała JSON, przekazuje token anulowania i kończy obsługę żądania przy błędzie walidacji. Reguły obejmujące kilka pól danego obiektu konfigurujemy tak, by wykonywały się po pomyślnej walidacji jego pól. Wybór FluentValidation oznacza dodatkową zależność i utrzymanie integracji filtra z Minimal APIs.

Naruszenie reguł zwraca HTTP 400 w formacie `ValidationProblemDetails`. Wspólny mechanizm HTTP mapuje klucze w `errors` zgodnie z konfiguracją serializacji żądania, w tym z `JsonPropertyName`, i zachowuje indeksy elementów kolekcji, np. `osoby[1].pesel`. Dzięki temu zmiana nazwy właściwości w C# przy zachowaniu nazwy w JSON nie wymaga zmiany obsługi błędów po stronie klienta. W pierwszej wersji wartości w `errors` są listami komunikatów tekstowych.

## Pierwsze wdrożenie

Pilotaż obejmuje wymaganą nazwę w `POST /default/examples`. Zachowujemy dotychczasową obsługę błędów odczytu JSON. Zakres walidacji obejmuje ciało żądania. Reguły NIP i PESEL odkładamy do czasu wdrożenia żądań zawierających te pola.

Zakres wdrożenia i kryteria weryfikacji opisuje [specyfikacja w zgłoszeniu #29](https://github.com/savek7/netx/issues/29).
