# netx

## Moduł Default

`Default` jest wzorcem nowego modułu o spójnej odpowiedzialności, a nie miejscem
na wszystkie nowe funkcje. Host jawnie rejestruje go przez `IModule`; kontrakty
HTTP i wewnętrzne API `IExampleApi` są od siebie niezależne. Dane są zapisywane
w PostgreSQL w schemacie `default`. Każdy moduł może mieć własny schemat i
connection string, również korzystając ze wspólnej bazy.

## Konfiguracja modułów

Host deklaruje moduły w `src/Web/HostModules.cs` przez `ModuleDeclaration.Enabled`
lub `ModuleDeclaration.Disabled`. Można wybrać stan na podstawie
`builder.Environment`; zmiana wymaga ponownego uruchomienia. Moduł usunięty
z projektu nie wymaga deklaracji. Wyłączony moduł pozostaje jawnie wymieniony,
ale nie rejestruje usług ani endpointów.

Każdy `IModule` podaje niepusty identyfikator i wymagane identyfikatory innych
modułów w `RequiredDependencies`. Identyfikatory są porównywane z uwzględnieniem
wielkości liter. `ModuleConfiguration` przed rejestracją usług sprawdza duplikaty,
brakujące lub wyłączone zależności oraz cykle. Usługi są rejestrowane w kolejności
deklaracji, więc w razie potrzeby zależności należy zadeklarować wcześniej.

## Środowisko lokalne

Wymagane są .NET SDK 10 i Docker Compose. Uruchom bazę i zastosuj migracje
osobnym runnerem (PowerShell):

```powershell
Copy-Item .env.example .env
docker compose up -d postgres
$env:ConnectionStrings__Default = 'Host=localhost;Database=netx;Username=netx;Password=netx-local-only'
dotnet run --project src/Migrations -- Default
docker compose up --build -d web
```

Host jest dostępny pod `http://localhost:8080`. W środowisku Development wyślij
POST do `/default/examples` z JSON `{"name":"Przykład"}`, a następnie GET pod
adres z nagłówka `Location`. Endpointy przykładowe nie są mapowane w innych
środowiskach; wewnętrzne API jest dostępne w każdym środowisku. Dane pozostają
w katalogu `.volumes/postgres` po restarcie hosta.

Alternatywnie, po uruchomieniu bazy i migracji można uruchomić host lokalnie:
`dotnet run --project src/Web --launch-profile http`. Wymaga on ustawionego
wyżej `ConnectionStrings__Default`. Dane dostępowe Compose są przeznaczone
wyłącznie do lokalnego środowiska.

Migracje są osobnym krokiem wdrożenia i nie są wykonywane przy starcie hosta.
Runner wymaga nazwy modułu i zmiennej `ConnectionStrings__Default`. Historia
migracji FluentMigrator znajduje się w `default."Migrations"`.
Obraz wdrożeniowy i blokada równoległych uruchomień są zakresem #13.

FluentMigrator wybrano zgodnie z ADR 0002, aby jawnie pisać migracje niezależnie
od modelu EF Core. EF Core generuje zmiany ze snapshotu modelu; FluentMigrator
wymaga ręcznego utrzymania zgodności modelu i schematu. Wersjonowane skrypty SQL,
np. DbUp, pozwalają używać natywnego SQL PostgreSQL, lecz wymagają ręcznego
utrzymania skryptów. FluentMigrator pozwala pisać migracje w C# w module.

Testy bez bazy: `dotnet test tests/Default.Tests/Default.Tests.csproj`.
Pełny zestaw: `dotnet test netx.slnx`; testy integracyjne wymagają działającego
Dockera i uruchamiają własny kontener PostgreSQL.

## Dokumentacja OpenAPI

W środowisku Development uruchomiony host udostępnia interaktywną dokumentację
Scalar pod adresem `http://localhost:8080/scalar` oraz kontrakt JSON pod adresem
`http://localhost:8080/openapi/v1.json`. Scalar umożliwia wysyłanie żądań do
uruchomionego API. Adresy wymagają dostępu sieciowego do hosta i nie są dostępne
w środowiskach Staging ani Production.

Każde `dotnet build src/Web/Web.csproj` generuje kontrakt Development w pliku
`src/Web/obj/openapi/netx.json`, bez uruchomionej bazy danych i bez migracji.
Ten ignorowany przez Git plik wynikowy można udostępnić niezależnie od hosta.
Przykładowe polecenie dla agenta: „Przeczytaj `src/Web/obj/openapi/netx.json`
i zaimplementuj klienta endpointów examples”. Warstwa Presentation modułu
Default pokazuje, jak dodawać metadane operacji i przykłady żądań w kolejnych
modułach.

## Korzystanie z API

Po uruchomieniu środowiska utwórz przykładowy rekord:

```bash
curl -X POST http://localhost:8080/default/examples -H 'Content-Type: application/json' -d '{"name":"Przykład"}'
```

Następnie użyj identyfikatora `id` z odpowiedzi, aby odczytać rekord:

```bash
curl 'http://localhost:8080/default/examples/<id>'
```

Inny moduł może odwołać się do projektu `Default`, korzystając wyłącznie z jego
kontraktów `Modules.Defaults.PublicApi`. Po zadeklarowaniu `Default` jako wymaganej
zależności własnego `IModule` może wstrzyknąć `IExampleApi` i użyć go w swoim
serwisie, bez HTTP i dostępu do repozytorium lub kontekstu bazy `Default`:

```csharp
using Modules.Defaults.PublicApi;

// W metodzie serwisu, do którego wstrzyknięto IExampleApi api:
ExampleRecord created = await api.CreateAsync("Przykład", cancellationToken);
ExampleRecord? found = await api.FindByIdAsync(created.Id, cancellationToken);
```

## Nowy moduł z Default

Nowy moduł wydzielaj dla odrębnej odpowiedzialności biznesowej. Funkcję należącą
do istniejącej odpowiedzialności dodaj do istniejącego modułu; `Default` to
przykład do kopiowania, a nie domyślne miejsce na dowolne funkcje.

1. Skopiuj `src/modules/Default` do `src/modules/Orders` (bez katalogów `bin`
   i `obj`). Zmień nazwę `Default.csproj` na `Orders.csproj`, przestrzenie nazw
   `Modules.Defaults` na własne, `DefaultModule` i pozostałe nazwy klas na
   właściwe. Nazwa assembly musi odpowiadać nazwie projektu. Pozostaw jeden
   projekt i podział na Domain, Application, Infrastructure, Presentation oraz
   PublicApi; projekt powinien implementować `IModule` i udostępniać publiczną
   statyczną fabrykę zwracającą `IModule`.
2. Usuń przykładowe funkcje i skopiowane migracje. Przygotuj własne klasy
   migracji FluentMigrator w `Infrastructure/Migrations`.
3. Nadaj modułowi unikalny `Id`, własny schemat i connection string. Dodaj jego
   referencję do `src/Migrations/Migrations.csproj` i wpis w tablicy `modules`
   w `src/Migrations/Program.cs`: fabrykę `IModule`, assembly zawierające jego
   migracje oraz nazwę zmiennej środowiskowej z connection stringiem. Runner
   wybiera moduł po `Id` i skanuje tylko wskazane assembly. Wersje migracji
   muszą być unikalne w obrębie modułu. Nie kopiuj sekretów do repozytorium.
4. Dodaj projekt do rozwiązania i referencję hosta:

   ```powershell
   dotnet sln netx.slnx add src/modules/Orders/Orders.csproj
   dotnet add src/Web/Web.csproj reference src/modules/Orders/Orders.csproj
   ```

   W `src/Web/HostModules.cs` zadeklaruj `ModuleDeclaration.Enabled(OrdersModule.Create())`
   albo świadomie `ModuleDeclaration.Disabled(OrdersModule.Create())`. W
   `RequiredDependencies` wpisz identyfikatory wymaganych modułów. Nieobecna lub
   wyłączona wymagana zależność uniemożliwia uruchomienie hosta. Usunięty projekt
   nie wymaga deklaracji; nie dodawaj automatycznego skanowania do hosta.
5. Przygotuj migrację początkową i uruchom ją osobnym krokiem. Następnie uruchom
   testy i sprawdź ścieżki HTTP oraz PublicApi nowego modułu.

## Weryfikacja i granice izolacji

```powershell
dotnet test tests/Default.Tests/Default.Tests.csproj
dotnet test tests/Architecture.Tests/Architecture.Tests.csproj
dotnet test netx.slnx
```

Pierwsze dwa polecenia nie wymagają Dockera ani bazy. Pełny zestaw, również w
CI, uruchamia integrację z prawdziwym PostgreSQL w kontenerze Testcontainers;
wymaga działającego Dockera i możliwości pobrania obrazów. Testy architektoniczne
sprawdzają projekty w `src/modules`, ich skompilowane typy i zależności oraz
deklaracje hosta w środowiskach Development, Staging i Production. Przy dodaniu
innego środowiska warunkowego należy rozszerzyć tę listę testową. Stosują
konwencję jednej biblioteki na moduł, o nazwie assembly takiej jak plik projektu.

Statyczna analiza nie wykrywa wszystkich zależności runtime (np. refleksji,
dynamicznego ładowania lub konfiguracji). Osobne schematy i connection stringi
same nie ograniczają uprawnień bazy: przed produkcją należy zaprojektować konta,
uprawnienia i bezpieczną konfigurację. Migracje produkcyjne uruchamia operator
jako osobny krok wdrożenia, nie podczas startu hosta.
