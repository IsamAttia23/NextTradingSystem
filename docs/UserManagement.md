# User management and notifications

The current signed-in account is excluded from the users query before projection.
Account status is changed only through POST /Users/SetActive, protected by the existing
administrator policy and antiforgery validation. The request includes IsActive and
ExpectedIsActive. Missing values are rejected; stale state returns HTTP 409. Self
status changes and deactivating the last active administrator are blocked. Status
changes rotate the security stamp, so older sessions cannot return after reactivation.
Administrative edit and profile requests preserve the persisted account status.

The users table renders an accessible switch with keyboard focus and a loading state.
A successful response updates its label and position; failures preserve the previous
state. A stale or uncertain network result refreshes the list.

The recycle bin includes an ApplicationUser section, with user names, identifiers,
deleted timestamps and deletion reasons. It lists only soft-deleted users. Restoration
clears deletion fields, rotates the security stamp and leaves the account inactive;
activation is explicit from the users table. Existing permanent-deletion audit rules
and database relationships still apply. No new migration is needed.

Notifications of every kind, including errors, dismiss after seven seconds. Hover or
keyboard focus pauses the timer. Up to four cards stack with the newest in front;
hover/focus expands the cards for reading and closing. Text is inserted with textContent,
and reduced-motion preferences and Arabic/English layout are supported.

Deactivated accounts use the locked-account page after correct login credentials.
Pending accounts also recognize a matching, unexpired activation OTP for displaying
that page only; no grant or authenticated session is issued. Wrong credentials,
expired/revoked codes and deleted accounts retain the generic failed-login response.
The locked-page description covers administrator deactivation and failed-attempt locks
in both Arabic and English.
