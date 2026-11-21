# Oddzielne klucze techniczne i identyfikatory publiczne

Encje wymagające własnego klucza mają domyślnie techniczny klucz `long` generowany przez PostgreSQL. Encja udostępniana klientom HTTP lub innym modułom może dodatkowo mieć stały `PublicId` typu `Guid`, którego unikalność obowiązuje w obrębie tabeli. Dwa identyfikatory pozwalają niezależnie dobierać klucze do relacji bazodanowych i utrzymywać stabilne odwołania w kontraktach publicznych. Przy pierwszym zapisie aplikacja generuje UUIDv4, jeśli `PublicId` jest pusty. Zaufany import może podać dotychczasowy identyfikator.

Moduł `Default` wdroży tę konwencję jako pierwszy. Dotychczasowa kolumna z identyfikatorami GUID encji `Example` zostanie zmapowana na `PublicId`. Odpowiedź HTTP nadal zwraca GUID w polu `id`. Nowy klucz techniczny otrzyma osobną kolumnę `bigint`, aby poprzednia wersja hosta mogła czytać i zapisywać rekordy zgodnie z [ADR 0002](0002-postgresql-migrations.md).
