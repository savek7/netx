# netx

Modułowa aplikacja, w której przypadki użycia są dostępne przez HTTP oraz publiczne API modułów.

## Język projektu

**Walidacja żądania**

Sprawdzenie, czy dane klienta spełniają kontrakt wejściowy API HTTP przed uruchomieniem metody endpointu. Warunki wymagane przy każdym wywołaniu operacji obowiązują również w przypadku użycia lub domenie.

**Identyfikator publiczny**

Stałe odwołanie do rekordu, którym posługują się klienci HTTP i inne moduły.

**Metadane zapisu**

Informacja o tym, kiedy i przez kogo rekord został utworzony, ostatnio zmodyfikowany lub miękko usunięty. Każdy rodzaj operacji ma co najwyżej jeden zestaw metadanych na rekord.

**Miękkie usunięcie**

Wycofanie rekordu ze zwykłego użycia z zachowaniem go do celowego odczytu. Metadane usunięcia wskazują jego czas i wykonawcę, jeśli jest znany.

**Wykonawca operacji**

Użytkownik, w którego imieniu wykonano operację, albo system, gdy działa z własnej inicjatywy. Jeśli ustalenie wykonawcy jest niemożliwe, uznajemy go za nieznanego.
