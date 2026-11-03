# netx

## Przykładowy moduł

`Default` jest wzorcem nowego modułu o spójnej odpowiedzialności, a nie miejscem
na wszystkie nowe funkcje. Host jawnie rejestruje go przez `IModule`; kontrakty
HTTP i wewnętrzne API `IExampleApi` są od siebie niezależne.

Uruchom hosta poleceniem `dotnet run --project src/Web --launch-profile http`
(lub ustaw `ASPNETCORE_ENVIRONMENT=Development`). W środowisku Development wyślij
żądanie POST do `/default/examples` z treścią JSON, np. `{"name":"Przykład"}`,
a następnie żądanie GET pod adres zwrócony w nagłówku `Location`. Przykładowe
endpointy nie są mapowane w innych środowiskach. Publiczne API wewnętrzne jest
dostępne przez kontener usług w każdym środowisku.

Przykładowe dane są przechowywane **wyłącznie w pamięci**: zostają utracone po
zatrzymaniu aplikacji i nie są współdzielone między kontenerami usług ani
instancjami aplikacji. Nie jest to baza danych ani mechanizm trwałego zapisu do
użytku produkcyjnego. PostgreSQL i walidacja zależności modułów są zaplanowane
osobno.

Testy zachowania można uruchomić bez Dockera i PostgreSQL: `dotnet test netx.slnx`.
