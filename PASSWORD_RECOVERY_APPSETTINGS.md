# Password recovery / change-password tab - appsettings configuration

## ENG

### Purpose

This document explains how to configure `appsettings` in `apiv3` so the password recovery / change-password tab flow works correctly.

In practice, this covers the backend endpoints:

- `POST /api/forgot-password`
- `POST /api/reset-password`

and the correct generation of the frontend link that opens the screen for setting a new password.

---

### What was wired in code

Based on the current `apiv3` implementation, the feature is connected through the following pieces:

1. **Public API endpoints**
   - `LgymApi.Api/Features/User/Controllers/UserController.cs`
   - `forgot-password` accepts an email and starts the password recovery email flow.
   - `reset-password` accepts a token + new password and stores the new password hash.

2. **Password reset service**
   - `LgymApi.Application/Features/PasswordReset/PasswordResetService.cs`
   - generates a reset token,
   - invalidates previous active tokens for the user,
   - sets token expiry to **30 minutes**,
   - revokes all active user sessions after a successful reset.

3. **Recovery email composition**
   - `LgymApi.Infrastructure/Services/PasswordRecoveryEmailTemplateComposer.cs`
   - the frontend link is built as:

```text
{Email:PasswordRecoveryBaseUrl}?token={TOKEN}
```

4. **Configuration options and validation**
   - `LgymApi.Infrastructure/Options/EmailOptions.cs`
   - `LgymApi.Infrastructure/Configuration/EmailOptionsFactory.cs`
   - when `Email:Enabled=true`, the backend validates required fields and throws on missing/invalid configuration.

5. **Infrastructure registration**
   - `LgymApi.Infrastructure/ServiceCollectionExtensions.cs`
   - `EmailOptions` are loaded and validated there.

Additionally, repository history shows the feature was introduced in:

- `ae70162 feat(auth): add secure password recovery via email (#250)`

---

### Most important requirement for the change-password tab

The frontend must expose a public password reset screen, and the backend must know its **absolute base URL**.

In this implementation, the critical setting is:

```json
"Email": {
  "PasswordRecoveryBaseUrl": "http://localhost:5173/reset-password"
}
```

This is **not** an API endpoint URL. It is the frontend page URL the user opens from the email.

Example generated link:

```text
http://localhost:5173/reset-password?token=ABC123...
```

---

### Required appsettings values

If you want email-based password recovery / password change to work, configure the `Email` section correctly.

#### Minimal working local example

```json
"Email": {
  "Enabled": true,
  "DeliveryMode": "Dummy",
  "DummyOutputDirectory": "EmailOutbox",
  "FromAddress": "no-reply@lgym.local",
  "FromName": "LGYM Trainer",
  "SmtpHost": "localhost",
  "SmtpPort": 1025,
  "Username": "",
  "Password": "",
  "UseSsl": false,
  "InvitationBaseUrl": "https://app.lgym.local/invitations",
  "PasswordRecoveryBaseUrl": "http://localhost:5173/reset-password",
  "TemplateRootPath": "EmailTemplates",
  "DefaultCulture": "en-US"
}
```

This version writes emails to `EmailOutbox` instead of sending them through SMTP.

#### Production SMTP example

```json
"Email": {
  "Enabled": true,
  "DeliveryMode": "Smtp",
  "FromAddress": "no-reply@your-domain.com",
  "FromName": "LGYM",
  "SmtpHost": "smtp.your-domain.com",
  "SmtpPort": 587,
  "Username": "SMTP_LOGIN",
  "Password": "SMTP_PASSWORD",
  "UseSsl": true,
  "InvitationBaseUrl": "https://app.your-domain.com/invitations",
  "PasswordRecoveryBaseUrl": "https://app.your-domain.com/reset-password",
  "TemplateRootPath": "EmailTemplates",
  "DefaultCulture": "en-US"
}
```

---

### What is required when `Email:Enabled=true`

According to `EmailOptionsFactory.Validate(...)`, the backend requires:

- `Email:InvitationBaseUrl` - required, absolute URL
- `Email:PasswordRecoveryBaseUrl` - required, absolute URL
- `Email:TemplateRootPath` - required
- `Email:DefaultCulture` - required
- `Email:FromAddress` - required valid email address

plus mode-specific requirements:

#### For `Email:DeliveryMode=Dummy`

- `Email:DummyOutputDirectory` - required

#### For `Email:DeliveryMode=Smtp`

- `Email:SmtpHost` - required
- `Email:SmtpPort` - must be `> 0`

---

### What happens if configuration is invalid

When `Email:Enabled=true`, application startup may fail with exceptions such as:

- `Email:PasswordRecoveryBaseUrl is required.`
- `Email:PasswordRecoveryBaseUrl must be a valid absolute URL.`
- `Email:TemplateRootPath is required when email is enabled.`
- `Email:FromAddress must be a valid email address.`

---

### Note about current config files

Currently:

- `LgymApi.Api/appsettings.json` contains the full `Email` section, including `PasswordRecoveryBaseUrl`
- `LgymApi.Api/appsettings.Development.json` shows the local frontend example `http://localhost:5173/reset-password`
- `appsettings.container.example.json` currently only contains:

```json
"Email": {
  "Enabled": false
}
```

This means that for container / production environments, you must **manually extend** the `Email` section if password recovery email should work.

---

### Environment variable equivalents

If you configure the app through environment variables, use:

- `Email__Enabled`
- `Email__DeliveryMode`
- `Email__DummyOutputDirectory`
- `Email__FromAddress`
- `Email__FromName`
- `Email__SmtpHost`
- `Email__SmtpPort`
- `Email__Username`
- `Email__Password`
- `Email__UseSsl`
- `Email__InvitationBaseUrl`
- `Email__PasswordRecoveryBaseUrl`
- `Email__TemplateRootPath`
- `Email__DefaultCulture`

Example:

```env
Email__Enabled=true
Email__DeliveryMode=Smtp
Email__FromAddress=no-reply@your-domain.com
Email__FromName=LGYM
Email__SmtpHost=smtp.your-domain.com
Email__SmtpPort=587
Email__Username=SMTP_LOGIN
Email__Password=SMTP_PASSWORD
Email__UseSsl=true
Email__InvitationBaseUrl=https://app.your-domain.com/invitations
Email__PasswordRecoveryBaseUrl=https://app.your-domain.com/reset-password
Email__TemplateRootPath=EmailTemplates
Email__DefaultCulture=en-US
```

---

### Checklist

To make the password recovery / change-password tab flow work correctly:

1. Set `Email:Enabled=true`
2. Set a correct `Email:PasswordRecoveryBaseUrl`
3. Set a valid `Email:FromAddress`
4. Set `TemplateRootPath`
5. Configure either `Dummy` or `Smtp`
6. Ensure the frontend exposes a public `/reset-password` route
7. Verify that the email link opens the frontend with `?token=...`
