# netx

## Moduł Default

`Default` jest wzorcem nowego modułu o spójnej odpowiedzialności, a nie miejscem
na wszystkie nowe funkcje. Host jawnie rejestruje go przez `IModule`; kontrakty
HTTP i wewnętrzne API `IExampleApi` są od siebie niezależne. Dane są zapisywane
w PostgreSQL w schemacie `default`. Każdy moduł może mieć własny schemat i
connection string, również korzystając ze wspólnej bazy.

## Konfiguracja modułów

Host deklaruje moduły w `src/Web/Program.cs` przez `ModuleDeclaration.Enabled`
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

Wymagane są .NET SDK 10 i Docker Compose. Uruchom bazę, przywróć lokalne
narzędzie EF i zastosuj migracje (PowerShell):

```powershell
Copy-Item .env.example .env
docker compose up -d postgres
dotnet tool restore
$env:ConnectionStrings__Default = 'Host=localhost;Database=netx;Username=netx;Password=netx-local-only'
dotnet ef database update --project src/modules/Default --startup-project src/Web --context DefaultDbContext
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
W produkcji należy zapewnić odpowiedni connection string przez konfigurację
środowiska i uruchomić `dotnet ef database update` jako krok wdrożenia z
uprawnieniami do zmiany schematu. Historia migracji znajduje się w schemacie
`default`.

Testy bez bazy: `dotnet test tests/Default.Tests/Default.Tests.csproj`.
Pełny zestaw: `dotnet test netx.slnx`; testy integracyjne wymagają działającego
Dockera i uruchamiają własny kontener PostgreSQL.
