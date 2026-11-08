---
status: accepted
---

# Niezależność obsługi poleceń i zapytań od biblioteki mediatora

Mediator obsługuje polecenia i zapytania wewnątrz modułu, z dokładnie jednym handlerem na komunikat. Komunikacja między modułami odbywa się przez ich publiczne API. Komunikaty i handlery korzystają z kontraktów należących do projektu, aby wymiana biblioteki mediatora nie wymagała ich zmiany. Akceptujemy koszt utrzymywania adapterów, aby zachować tę niezależność.

Pierwszą implementacją będzie MediatR. Polecenia i zapytania mają osobne kontrakty projektu: `ICommand<TResult>` i `IQuery<TResult>` oraz odpowiadające im kontrakty handlerów. Własny kontrakt pipeline’u pozwala wykonywać wspólną logikę przed i po handlerze w określonej kolejności, bez uzależniania tej logiki od MediatR.

Zachowania pipeline’u wykonują się w kolejności rejestracji: pierwsze otacza kolejne i handler. Zachowanie może zwrócić wynik bez uruchomienia dalszej części pipeline’u. Mechanizm nie ponawia automatycznie wykonania handlera.

Polecenia bez wyniku korzystają z własnego kontraktu `ICommand`, bez udostępniania typu `Unit` z MediatR. Brak handlera lub więcej niż jeden handler dla komunikatu powoduje błąd podczas uruchamiania aplikacji. Rejestracja i weryfikacja obejmują wyłącznie włączone moduły; wyłączone moduły nie udostępniają handlerów.

Wywołujący korzystają z metod `SendAsync` dla poleceń i `QueryAsync` dla zapytań, z typowanymi odpowiedziami i obsługą `CancellationToken`. Każdy handler i metoda mediatora zwracają `Result<T>` dla operacji z wartością lub `Result` dla polecenia bez wartości, opakowane w `Task`. Parametr `T` w `ICommand<T>` i `IQuery<T>` określa typ danych sukcesu. Błędy biznesowe są zwracane w wyniku; wyjątki techniczne i anulowanie są propagowane. Mediator nie mapuje wyników na HTTP.

Błąd w `Result` zawiera stabilny kod, opis i kategorię, taką jak `Validation`, `NotFound`, `Conflict` lub `BusinessRule`, bez zależności od HTTP. Zapytanie `GetExampleById` zwraca niepowodzenie kategorii `NotFound`, gdy obiekt nie istnieje; endpoint mapuje je na HTTP 404. Pusta kolekcja może być poprawnym wynikiem zapytania o kolekcję.

Niepowodzenie zawiera co najmniej jeden błąd i może zawierać ich wiele. Sukces nie zawiera błędów. Publiczne API modułu również zwraca `Result`, aby przekazywać błędy biznesowe bez zamiany na wyjątki; metody `IExampleApi` będą zwracać `Result<ExampleRecord>`.

W module przykładowym `Default` handlery tworzenia i pobierania obiektu zastąpią `ExampleService`. Endpointy oraz implementacja `IExampleApi` będą wywoływać te same przypadki użycia przez mediator.

Pierwsza wersja dostarcza mechanizm rozszerzania pipeline’u bez gotowych zachowań walidacji, logowania, transakcji ani ponawiania operacji. Nie obejmuje powiadomień ani wielu odbiorców.

## Podział implementacji

- `Tools.Results`: wyniki i błędy, współdzielone także przez publiczne API modułów.
- `Tools.Mediation`: jeden projekt z folderem `Abstractions` na kontrakty poleceń, zapytań, handlerów, wywoływania mediatora i pipeline’u oraz folderem `MediatR` na adaptery i rejestrację biblioteki. Projekt zależy od `Tools.Results`. Typy MediatR nie przenikają do własnych kontraktów ani implementacji przypadków użycia w modułach. Podział na foldery upraszcza strukturę rozwiązania; niezależność kontraktów od biblioteki jest konwencją wewnątrz wspólnego projektu, a nie granicą osobnych zestawów.

Rejestracja respektuje deklaracje włączonych modułów. Czas życia handlerów i adapterów musi umożliwiać korzystanie ze scoped `DbContext`. Wymiana biblioteki wymaga zastąpienia adapterów i konfiguracji integracji, z zachowaniem kontraktów i semantyki wykonania.

Weryfikacja wdrożenia obejmie wyniki biznesowe i wiele błędów, propagację wyjątków i anulowania, kolejność oraz przerwanie pipeline’u, wykrywanie brakujących i zduplikowanych handlerów, pomijanie wyłączonych modułów oraz zachowanie endpointów i publicznego API modułu przykładowego.

Zakres wdrożenia i kryteria weryfikacji opisuje [specyfikacja w issue #17](https://github.com/savek7/netx/issues/17).
