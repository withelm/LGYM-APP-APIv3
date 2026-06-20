# Nowe moduły - dokumentacja i instrukcja obsługi

## 1. Moduł zdjęć raportowych

### Cel

Moduł umożliwia dodawanie zdjęć do raportów podopiecznych bez przesyłania binarki przez bazę danych. Backend zarządza tylko autoryzacją, signed URL i metadanymi.

### Główne elementy

- `IPhotoStorageProvider` - kontrakt storage providera,
- `ReportingService.Photos.cs` - logika biznesowa upload-init, complete-upload, signed read i historii,
- `CloudflareR2PhotoStorageProvider` - produkcyjny/dev-realistic provider signed URL,
- `LocalPhotoStorageProvider` - development-only provider lokalny,
- `LocalPhotoDevelopmentController` - endpointy pomocnicze dla lokalnego flow.

### Konfiguracja

Sekcja `PhotoStorage`:

```json
{
  "PhotoStorage": {
    "Provider": "Local",
    "BucketName": "lgym-report-photos-dev",
    "Endpoint": "https://YOUR_ACCOUNT_ID.r2.cloudflarestorage.com",
    "SignedUploadExpirationMinutes": 10,
    "SignedReadExpirationMinutes": 15,
    "MaxFileSizeBytes": 5242880,
    "AllowedMimeTypes": ["image/jpeg", "image/png", "image/heic"]
  }
}
```

Sekrety, których **nie wolno commitować**:

- `PhotoStorage:AccessKeyId`
- `PhotoStorage:SecretAccessKey`
- lokalne pliki z override konfiguracji

### Obsługa - flow użytkownika

#### Krok 1 - inicjalizacja uploadu

Trainee lub trainer woła:

- `POST /api/trainee/reporting/photos/upload-init`
- albo `POST /api/trainer/reporting/photos/upload-init`

Przykładowe body:

```json
{
  "reportRequestId": "REPORT_REQUEST_ID",
  "viewType": "Front",
  "mimeType": "image/jpeg",
  "sizeBytes": 2451200
}
```

Backend:

- sprawdza dostęp do requestu,
- waliduje MIME/type i rozmiar,
- generuje `storageKey`,
- zapisuje pending upload-init,
- zwraca `uploadUrl`, `storageKey`, `expiresAt`.

#### Krok 2 - wysłanie binarki

- dla `CloudflareR2`: klient robi `PUT` bezpośrednio na signed URL,
- dla `Local`: upload trafia przez development endpoint `PUT /dev/photos/upload/{storageKey}`.

#### Krok 3 - finalizacja uploadu

Wywołanie:

- `POST /api/trainee/reporting/photos/complete-upload`
- albo `POST /api/trainer/reporting/photos/complete-upload`

Przykładowe body:

```json
{
  "storageKey": "photos/.../Front/...jpg",
  "mimeType": "image/jpeg",
  "sizeBytes": 2451200,
  "checksum": "OPTIONAL_CLIENT_CHECKSUM",
  "reportRequestId": "REPORT_REQUEST_ID",
  "viewType": "Front"
}
```

Backend:

- sprawdza zgodność z upload-init,
- pobiera metadata obiektu ze storage,
- odrzuca niezgodne lub podejrzane pliki,
- zapisuje encję `Photo`,
- usuwa poprzedni plik dla tego samego `viewType`, jeśli został zastąpiony.

#### Krok 4 - historia i podgląd

- `GET /api/trainee/reporting/photos/history?requestId=...`
- `GET /api/trainer/reporting/photos/history?traineeId=...&requestId=...`
- `GET /api/trainer/reporting/photos/{photoId}/signed-url`

### Zasady bezpieczeństwa

- `Local` provider ma działać tylko w Development/Testing,
- dostęp do zdjęć jest sprawdzany przez backend,
- klient nie może traktować `storageKey` jako dowodu własności,
- signed URL są krótkotrwałe,
- finalizacja weryfikuje realne metadata obiektu, nie tylko dane deklarowane przez klienta.

---

## 2. Rozszerzony moduł reporting

### Co doszło

- `moduleConfig` w polach szablonu,
- wsparcie dla typów modułowych, w tym `Photos` i `Measurements`,
- feedback trenera per raport i per pole,
- dodatkowe in-app powiadomienia po requestach i feedbacku.

### Tworzenie szablonu z konfiguracją modułu

Endpoint:

- `POST /api/trainer/report-templates`

Przykład pola zdjęciowego:

```json
{
  "key": "progressPhotos",
  "label": "Zdjęcia sylwetki",
  "type": "Photos",
  "isRequired": true,
  "order": 3,
  "moduleConfig": {
    "requiredViews": ["Front", "Back", "Side"]
  }
}
```

Ważne:

- `moduleConfig` jest przechowywane i zwracane do klienta,
- przy submit backend sprawdza, czy wymagane widoki zdjęć faktycznie istnieją,
- komentarze trenera do pól muszą odnosić się tylko do kluczy z template.

Przykład pola pomiarowego:

```json
{
  "key": "checkInMeasurements",
  "label": "Pomiary kontrolne",
  "type": "Measurements",
  "isRequired": true,
  "order": 4,
  "moduleConfig": {
    "measurementTypes": ["BodyWeight", "Waist", "Chest"]
  }
}
```

W praktyce validator oczekuje dla typu `Measurements` obiektu z tablicą `measurementTypes`.

### Feedback trenera

Endpoint:

- `POST /api/trainer/trainees/{traineeId}/report-submissions/{submissionId}/feedback`

Przykład:

```json
{
  "trainerOverallComment": "Dobra robota, popraw tempo progresji.",
  "trainerFieldComments": {
    "weight": "Dodaj kontekst co do nawodnienia.",
    "progressPhotos": "Zrób kolejną serię w bardziej równym świetle."
  }
}
```

### Operacyjnie

- request raportu tworzy powiadomienie dla trainee,
- feedback trenera tworzy powiadomienie dla trainee,
- template z polem `Photos` wymusza komplet wymaganych zdjęć przed submit.

---

## 3. Moduł in-app notifications dla reporting/invitations

### Nowe zdarzenia

- zaproszenie od trenera dla istniejącego użytkownika,
- utworzenie requestu raportu,
- dodanie feedbacku do submission.

### Jak to działa

1. serwis aplikacyjny enqueueuje komendę,
2. `CommandEnvelope` jest zapisywany w tej samej granicy UoW,
3. po commicie uruchamiany jest dispatch,
4. handler tworzy `InAppNotification`,
5. publisher może wypchnąć zdarzenie przez SignalR.

### Dlaczego to ważne

Poprzednio łatwo było o podwójne schedulowanie tego samego envelope. Teraz istnieje jedna główna ścieżka durable dispatch po zapisie transakcji.

---

## 4. Moduł measurements - bulk i trendy

### Nowe możliwości

- dodawanie wielu pomiarów jednym requestem,
- liczenie trendu pojedynczego body partu,
- liczenie listy trendów dla wszystkich grup pomiarowych,
- automatyczna konwersja jednostek w odczycie.

### Endpointy

- `POST /api/measurements/add`
- `POST /api/measurements/add-bulk`
- `GET /api/measurements/{id}/getHistory`
- `GET /api/measurements/{id}/list`
- `GET /api/measurements/{id}/trend`
- `GET /api/measurements/{id}/trends`

### Przykład bulk add

```json
{
  "measurements": [
    { "bodyPart": "BodyWeight", "unit": "Kilograms", "value": 82.4 },
    { "bodyPart": "Waist", "unit": "Centimeters", "value": 86.0 }
  ]
}
```

### Zasady walidacji

- jednostka musi pasować do body part,
- wartości muszą być dodatnie,
- trendy są zwracane dopiero po poprawnej autoryzacji do użytkownika,
- gdy jest za mało punktów danych, trend zwraca stan `insufficient_data` zamiast błędnych wniosków.

---

## 5. Moduł trainee self-service

### Nowe endpointy

- `GET /api/trainee/trainer` - zwraca podstawowy profil aktualnego trenera,
- `GET /api/trainee/plan/active` - zwraca aktywny plan przypisany przez trenera.

### Zastosowanie

- mobile może od razu pokazać, z kim trainee jest połączony,
- mobile może pobrać aktualny plan bez szukania go po innych ścieżkach.

---

## 6. Google auth fallback

### Co się zmieniło

- endpointy Google auth/linkingu przyjmują także `accessToken`,
- jeśli poprawny ID token nie niesie emaila lub profilu, backend próbuje pobrać dane z Google `userinfo`.

### Po co

Niektóre flow po stronie Google zwracają poprawny subject, ale niepełne claimy. Ten fallback pozwala nie zrywać logowania i linkowania kont, o ile userinfo potwierdzi ten sam `sub`.

### Uwaga

- fallback nie omija walidacji bezpieczeństwa,
- email nadal jest wymagany do bieżącego flow rejestracji i deduplikacji użytkowników.
