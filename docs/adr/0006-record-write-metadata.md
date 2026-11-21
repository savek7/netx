# Metadane zapisu uzupełniane w aplikacji

Encje wybierają niezależnie metadane utworzenia, edycji i miękkiego usunięcia. Dla każdej z tych operacji mogą też śledzić wykonawcę. Zapis przez `SaveChanges` automatycznie uzupełnia metadane zgodnie z rodzajem operacji i kontekstem jej wykonawcy. Handler może jawnie przekazać datę dla danej operacji, także gdy jest ona równa wczytanej wartości. Zapis respektuje ją i przelicza na UTC z zachowaniem tego samego momentu. Kod korzysta z `DateTimeOffset`, a PostgreSQL z `timestamptz`.

`CreatedAt` oznacza utworzenie, a `UpdatedAt` jest puste do pierwszej edycji danych rekordu. Sam zapis bez zmian pomija metadane edycji. Encja deklarująca `DeletedAt` obsługuje miękkie usuwanie. `Remove` takiej encji przekształca się przy zapisie w aktualizację metadanych usunięcia. `DeletedAt` wskazuje stan usunięcia, a pola ostatniej edycji nadal opisują edycję. Data i wykonawca pochodzą z pierwszego skutecznego usunięcia, również przy równoległych próbach. Zwykłe odczyty pomijają usunięte rekordy. Przypadek użycia może je uwzględnić, wyłączając filtr miękkiego usuwania dla danego zapytania.

Miękkie usunięcie obejmuje rekord wskazany przez `Remove`. Moduł jawnie wskazuje powiązane rekordy do usunięcia. Jeśli handler edytuje rekord i usuwa go w tym samym zapisie, zapis obejmuje wyłącznie metadane usunięcia tego rekordu. Równoległa próba usunięcia, która odczytała jeszcze aktywny rekord, zgłasza konflikt współbieżności po pierwszym skutecznym usunięciu. Ponowne `Remove` na rekordzie wczytanym już jako usunięty jest operacją bez zmian.

Przy zapisie w imieniu użytkownika metadane wskazują tego użytkownika również wtedy, gdy operację wykonuje zadanie w tle. Pola `…By` przechowują stabilny identyfikator tekstowy `user:<id>` albo `system:<nazwa>`. `NULL` oznacza nieznanego wykonawcę. Moduły dobierają indeksy do swoich zapytań.

Wspólny mechanizm sprawdza stan miękkiego usunięcia przy zapisie. Moduł sam ustala reguły współbieżności dla przejść biznesowych, na przykład jednokrotnego przejścia z `pending` do `success`.

Operacje masowe i bezpośredni SQL stosują te reguły jawnie. Pełna historia zmian stanowi osobne wymaganie.

`Tools.Persistence` udostępnia kontrakty pól niezależne od typów EF Core, interceptor zapisu i rejestrację nazwanego filtra miękkiego usuwania. Moduł `Default` zastosuje pełny zestaw pól w `Example`. Dotychczasowe rekordy `Example` nie mają historycznej daty utworzenia, więc ich `CreatedAt` będzie puste. Wartość domyślna w bazie wypełni datę rekordów zapisanych przez poprzednią wersję hosta po zastosowaniu migracji.
