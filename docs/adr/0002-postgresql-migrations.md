# Migracje PostgreSQL przez FluentMigrator

W ramach [issue #11](https://github.com/savek7/netx/issues/11) wybieramy FluentMigrator jako narzędzie do wersjonowania i wykonywania zmian schematu PostgreSQL. Chcemy jawnie pisać migracje niezależnie od modelu EF Core. Akceptujemy obowiązek utrzymywania zgodności modelu i schematu zamiast generowania zmian z modelu EF. EF Core pozostaje narzędziem dostępu do danych.

CI przygotowuje osobny obraz kontenera z programem uruchamiającym migracje, wykonywany jednorazowo jako krok przed wdrożeniem hosta. Obraz zawiera wymagany runtime, a konfigurację połączenia otrzymuje przy uruchomieniu. Uruchomienie tego kroku nie wymaga kodu źródłowego ani SDK na środowisku docelowym. Host nie wykonuje migracji podczas startu.

Migracje pozostają w module, w `Infrastructure/Migrations`, jako klasy `internal`. Osobny program uruchamiający migracje otrzymuje jawny sposób wykrywania tych klas, aby zachować ograniczenie publicznego API modułu bez tworzenia osobnego projektu migracji dla każdego modułu.

Runner utrzymuje blokadę PostgreSQL na czas wykonywania migracji danego modułu w danej bazie. Ochrona działa również przy uruchomieniu poza CI. Próba równoległego wykonania dla tego samego modułu kończy się czytelnym błędem zamiast oczekiwania na zwolnienie blokady.

CI buduje i sprawdza obraz migracyjny oraz udostępnia go jako artefakt do pobrania. Dokumentacja opisuje załadowanie i uruchomienie obrazu. Publikowanie w rejestrze obrazów i automatyczne wdrażanie pozostają poza zakresem issue #11.

Jedno uruchomienie kontenera migruje jeden jawnie wskazany moduł, korzystając z jego connection stringa, schematu i historii. Jeden obraz zawiera migracje obsługiwanych modułów. Pipeline wdrożeniowy jawnie uruchamia moduły kolejno, a host wdraża dopiero po sukcesie wszystkich kroków.

Błąd migracji zatrzymuje kolejne kroki wdrożenia. Zakończone migracje wcześniejszych modułów pozostają zastosowane; nie zakładamy wspólnej transakcji ani automatycznego cofania zmian między modułami. Historia migracji umożliwia ponowienie kroków bez powtarzania zastosowanych zmian. Zmiany schematu powinny umożliwiać działanie dotychczasowej wersji hosta do zakończenia wdrożenia.

Przejście baz developerskich z EF Core na FluentMigrator odbywa się przez świadome, ręczne odtworzenie bazy od zera. Akceptujemy utratę lokalnych danych; README musi ją wyraźnie opisać. Runner ani host nie kasują automatycznie istniejącej bazy. Nie konwertujemy historii EF Core ani nie wdrażamy procedury zachowania istniejących danych. Projekt nie posiada trwałej bazy produkcyjnej wymagającej takiego przejścia.

Migracja początkowa FluentMigrator tworzy schemat równoważny dotychczasowej migracji EF Core. Zachowujemy schemat `default` i osobną historię migracji modułu. Nowy format historii nie jest zgodny z formatem EF Core. Usuwamy migracje EF Core, snapshot i narzędzia projektowe potrzebne wyłącznie do ich obsługi.

Program zwraca kod zakończenia `0` po sukcesie, również gdy nie ma nowych migracji, a kod niezerowy po błędzie. Diagnostyka wskazuje moduł i przyczynę niepowodzenia. Dokumentacja opisuje sprawdzenie stanu bazy, naprawę przyczyny i ponowienie kroku; nie zakłada automatycznego rollbacku całego wdrożenia.

## Podział realizacji

[Issue #11](https://github.com/savek7/netx/issues/11) jest zadaniem nadrzędnym. [Issue #12](https://github.com/savek7/netx/issues/12) obejmuje wybór i zastosowanie FluentMigrator, migrację początkową, podstawowy osobny runner oraz test zgodności schematu z HTTP i PublicApi. [Issue #13](https://github.com/savek7/netx/issues/13) dopracowuje krok wdrożeniowy: obraz i artefakt CI, blokadę równoległego wykonania, diagnostykę błędów, bezpieczne ponowienie i instrukcje operacyjne. Powyższe ustalenia opisują docelowe rozwiązanie dla obu zadań.
