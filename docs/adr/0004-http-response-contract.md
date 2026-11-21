---
status: accepted
---

# Wspólny kontrakt odpowiedzi HTTP modułów

Odpowiedzi sukcesu zwracają dane określone przez endpoint bez wspólnej koperty. `201` wskazuje utworzony zasób w nagłówku `Location`, a `204` ma puste ciało. Błędy API zwracają `ProblemDetails` w formacie `application/problem+json` zgodnie z [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457). Dzięki temu klient obsługuje błędy wszystkich modułów w ten sam sposób, a statusy i nagłówki zachowują znaczenie określone w [RFC 9110](https://www.rfc-editor.org/rfc/rfc9110).

## Kontrakt błędów

Każda odpowiedź z ciałem `ProblemDetails` zawiera `type`, `title`, `status` i `traceId`. Pole `status` odpowiada statusowi HTTP. `detail` jest opcjonalnym komunikatem dla klienta. W pierwszej wersji pomijamy `instance`. Klient rozpoznaje własny typ problemu projektu po stabilnym adresie `type` prowadzącym do opisu w dokumentacji repozytorium. Problemy ogólne mają `type` równe `about:blank`, a ich znaczenie wynika ze statusu HTTP. `title` i `detail` służą do odczytu przez człowieka.

Układ katalogu typów problemów i docelowe adresy ustalimy w osobnym zgłoszeniu przed opublikowaniem pierwszych własnych typów.

`ValidationProblemDetails` rozszerza ten format o `errors`, mapę ścieżek pól JSON na listy komunikatów. Błąd walidacji żądania po odczytaniu JSON zwraca `400` z tą mapą zgodnie z [ADR 0003](0003-http-request-validation.md). Niepoprawny JSON daje `400` z ogólnym `ProblemDetails` i bezpiecznym komunikatem. Błąd `Validation` z przypadku użycia również zwraca `400`. Warstwa HTTP dodaje do niego `errors`, gdy może przypisać komunikaty do konkretnych pól żądania.

Host formatuje błędy routingu i odczytu żądania oraz obsługuje nieprzechwycone wyjątki. Odpowiedź `500` zawiera ogólny komunikat, a host loguje wyjątek z tym samym `traceId`. Format błędu zachowuje status i nagłówki danej odpowiedzi, np. `Allow` dla `405`. Zapis `application/problem+json` obowiązuje także wtedy, gdy `Accept` wskazuje inny format. Zasada obejmuje odpowiedzi, których ciało aplikacja może jeszcze zapisać i dla których HTTP dopuszcza ciało. Odpowiedź na `HEAD` zachowuje status i nagłówki oraz ma puste ciało.

## Mapowanie wyników przypadków użycia

Przypadek użycia wskazuje w `PrimaryError` główny błąd wyniku. Wynik z jednym błędem wskazuje go automatycznie. Przy kilku błędach wybór jest jawny, a pełna lista `Errors` pozostaje dostępna dla wywołań między modułami. Warstwa HTTP wyznacza jeden status i jeden `type` na podstawie głównego błędu. Kod błędu modułu wskazuje odpowiadający mu typ z katalogu. Typy `Result` pozostają niezależne od HTTP zgodnie z [ADR 0001](0001-module-mediator-boundary.md).

| Kategoria | Domyślny status HTTP |
| --- | --- |
| `Validation` | `400 Bad Request` |
| `NotFound` | `404 Not Found` |
| `Conflict` | `409 Conflict` |
| `BusinessRule` | `422 Unprocessable Content` |

Moduły korzystają ze wspólnego mechanizmu mapowania w warstwie HTTP. Przykładowo błąd `example.not_found` z modułu `Default` i nieznana trasa mają status `404`, ale różnią się wartością `type`.

## Pierwsze wdrożenie

Pilotaż obejmuje `POST /default/examples`, `GET /default/examples/{id}` oraz wspólną obsługę błędów hosta. OpenAPI opisuje oba warianty `400` dla `POST` i odpowiedź `404` dla `GET` wraz z formatem `application/problem+json`.

Istniejące testy HTTP i OpenAPI mają potwierdzić bezpośrednie dane dla `200` i `201`, `Location` dla `201`, `errors.name` przy pominięciu nazwy i ogólny problem przy niepoprawnym JSON. Mają też potwierdzić odrębne typy `404` dla brakującego obiektu i nieznanej trasy, zachowanie `Allow` przy `405`, zgodność statusu i typu mediów, `traceId` w odpowiedziach oraz możliwość powiązania `500` z logiem bez ujawniania szczegółów wyjątku klientowi. Test kontraktu OpenAPI obejmuje dokument działającej aplikacji i artefakt kompilacji.
