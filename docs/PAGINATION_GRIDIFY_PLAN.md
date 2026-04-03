# Generic Pagination Query Service Plan (Gridify-based)

## TL;DR

Wprowadzamy **generyczny serwis paginacji** oparty o **Gridify jako silnik filtrowania/sortowania**, ale zamkniety za naszymi kontraktami i fasadą, aby:
- zachowac obecne warstwy `Controller -> Service -> Repository`,
- nie wypuscic `IQueryable` poza infrastrukture,
- miec whitelisty pol i operatorow,
- uniknac materializacji calej kolekcji przed `Skip/Take`.

## Dlaczego Gridify

Gridify dobrze pokrywa wymagania v1:
- multi-sort,
- filtrowanie tekstowe i porownania,
- mapowanie nazw pol (whitelist),
- integracja z EF Core (`IQueryable`) bez reimplementowania parsera od zera.

Dzieki temu redukujemy ryzyko i czas implementacji: zamiast budowac pelny expression-engine od zera, dodajemy **adaptery + walidacje + polityki domenowe**.

## Architektura docelowa

### Application (kontrakty)
- `Pagination<TProjection>`
- `QueryPageRequest` (`Page`, `PageSize`)
- `QuerySort` (`Field`, `Direction`)
- `FilterGroup`/`FilterCondition` (nasz model wejscia API/serwisu)
- `IPaginationQueryService` (fasada aplikacyjna)

### Infrastructure (implementacja)
- `GridifyFilterTranslator` (mapuje `FilterGroup` -> bezpieczny Gridify filter string)
- `GridifyMapperFactory<TProjection>` (whitelist/mapa pol)
- `GridifyPaginationExecutor`:
  1. `AsNoTracking()`
  2. `Where` (Gridify)
  3. `OrderBy` (Gridify + tie-breaker)
  4. `CountAsync`
  5. `Skip/Take`
  6. `ToListAsync`
- `PaginationValidationPolicy` (max page size, max depth, allowed operators)

## Guardrails (bezwzgledne)

1. Brak `ToListAsync()` przed `Skip/Take`.
2. Brak dynamicznego sortowania po dowolnym stringu bez mapy.
3. Brak wycieku `IQueryable` do `Application`/`Api`.
4. Brak masowej migracji endpointow - tylko PoC (`TrainerDashboard`).
5. Deterministyczne sortowanie (tie-breaker, np. `Id`).

## Plan wdrozenia (zaktualizowany)

### Wave 1 - kontrakty + testy RED
1. Dodac generyczne kontrakty paginacji i filtrow.
2. Dodac kontrakty whitelist/polityk walidacji.
3. Dodac unit testy RED (page bounds, duplicate sort, invalid operator).
4. Dodac integration testy RED dla trainer dashboard (PoC).

### Wave 2 - Gridify adapter + executor
5. Implementacja `PaginationValidationPolicy` (depth/page/pageSize/operators).
6. Implementacja `GridifyFilterTranslator` (z `FilterGroup` na Gridify syntax).
7. Implementacja `GridifyPaginationExecutor` + DI.
8. Dodanie deterministic tie-breaker w sortowaniu.

### Wave 3 - PoC adopcja
9. Podlaczyc `TrainerRelationshipRepository` do nowego executora.
10. Usunac branch in-memory sortowania statusu.
11. Przepuscic RED->GREEN dla integration testow PoC.

### Wave 4 - hardening
12. Testy architektoniczne (brak EF/IQueryable w kontraktach Application).
13. Regression suite: empty IN, page > total, duplicate sort, max depth.
14. Pelne testy unit + integration w trybie CI-like.

## TDD i weryfikacja

Minimalny zestaw komend:

```bash
dotnet test LgymApi.UnitTests/LgymApi.UnitTests.csproj --configuration Release --no-build
dotnet test LgymApi.IntegrationTests/LgymApi.IntegrationTests.csproj --configuration Release --no-build
```

Dodatkowo per-scenario (filtry):

```bash
dotnet test LgymApi.UnitTests/LgymApi.UnitTests.csproj --filter "FullyQualifiedName~Pagination|FullyQualifiedName~Gridify|FullyQualifiedName~Sorting"
```

## Zakres v1

W scope v1 zostaje:
- offset pagination,
- jeden flow PoC (Trainer Dashboard),
- wrapper/fasada nad Gridify.

Poza v1:
- keyset/cursor pagination,
- migracja wszystkich endpointow listowych,
- zaawansowane provider-specific operatory SQL.

## Ryzyka i mitigacje

- **Ryzyko**: Gridify syntax leak do API.
  - **Mitigacja**: API przyjmuje nadal nasz `FilterGroup`; translacja tylko w Infrastructure.
- **Ryzyko**: niedeterministyczne sortowanie.
  - **Mitigacja**: globalny tie-breaker i testy stabilnosci.
- **Ryzyko**: zbyt szeroki rollout.
  - **Mitigacja**: feature flag / PoC-only wiring.

## Definition of Done

- PoC endpoint trenera dziala na nowym pipeline.
- Brak in-memory sortowania przed pagingiem.
- Wszystkie pola filtrowania/sortowania sa whitelisted.
- Testy unit + integration przechodza.
- Kontrakty Application pozostaja EF-agnostyczne.
