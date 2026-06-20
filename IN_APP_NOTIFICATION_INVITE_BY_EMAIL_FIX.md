# In-app notification fix for trainer invite by email

## What was changed

Updated:

- `LgymApi.Application/TrainerRelationships/TrainerRelationshipService.InvitationCreation.cs`

Inside `CreateInvitationByEmailAsync(...)` I added enqueueing of:

- `TrainerInvitationCreatedInAppNotificationCommand`

but only when the invited email already belongs to an existing, non-deleted user.

## Why this was needed

The previous implementation of `CreateInvitationByEmailAsync(...)` only enqueued:

- `InvitationCreatedCommand`

which leads to email scheduling and a row in `NotificationMessages`.

That flow did **not** enqueue the in-app notification command, so for existing users invited by email:

- email/log entry existed,
- but no `InAppNotification` row was created,
- no SignalR push was triggered,
- and mobile had nothing to fetch from `/api/{id}/notifications`.

## Observed symptom

As trainer, after inviting an existing user by email:

- backend showed an entry in `NotificationMessages`,
- but invited trainee did not see any notification in mobile.

## Effect of the fix

Now, when a trainer invites an email that already matches an existing user account:

1. email flow still works,
2. in-app notification is also enqueued,
3. background handler can create `InAppNotification`,
4. SignalR push can be sent,
5. mobile REST notifications list can return the invitation.

## Duplicate notification root cause and fix

There was also a separate backend issue causing duplicated in-app notifications.

### Root cause

`CommandDispatcher.EnqueueAsync(...)` persisted a `CommandEnvelope` and then immediately called the action scheduler.
At the same time, `EfUnitOfWork.SaveChangesAsync(...)` triggered `CommittedIntentDispatcher`, which also enqueued the same pending envelope.

That meant the same command envelope could be scheduled twice, which in practice could create two identical `InAppNotification` rows.

### Fix applied

Updated:

- `LgymApi.BackgroundWorker/CommandDispatcher.cs`

Removed the direct scheduler enqueue from `CommandDispatcher`.

Now there is only **one** durable dispatch path:

1. persist `CommandEnvelope`,
2. save changes,
3. `CommittedIntentDispatcher` schedules undispatched envelopes once.

### Why this is safer

This keeps dispatch consistent with the committed-intent/outbox pattern already used by the application and avoids race-like duplicate scheduling for the same envelope.

## Important note

This fix does **not** create in-app notifications for emails that do not belong to an existing user account yet.
That is intentional, because there is no recipient user id to target.

## No git commit / no push

Per request, this backend change was left uncommitted and unpushed.
