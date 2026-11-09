# Who owns which flow

Revised 9 Nov 2026: **one owner per assignment flow**, so each person presents and demos one continuous story.
The team is **Khang** (SE171577, leader), **Trường** (SE184087) and **Thiện** (SE185085). Each person owns their
flows end to end — backend, frontend and tests — and answers for them at the defense. The flow tabs of
`docs/flows.drawio` name their owner; `docs/CODE_MAP.md` names the owner of every file.

| Person | Story | Assignment flows | Flow tabs | Steps | Unfinished |
|---|---|---|---|---|---|
| **Khang** (leader) | The customer's front door | 1 Storage Unit Reservation · 7 Support Request and Issue Handling | F01 F02 F03 F04 F14 F15 | 115 | 18 |
| **Trường** | The money side | 3 Rented Storage Unit Management · 4 Business Rules, Fee Management and Revenue Monitoring · 6 Storage Renewal and Overdue Handling | F05 F07 F08 F09 F13 | 102 | 29 |
| **Thiện** | The facility side | 2 Storage Check-in and Handover · 5 Facility Storage and Staff Management | F06 F10 F11 F12 | 122 | 31 |

## Contribution split (agreed 9 Nov 2026)

| Member | Role | Backend | Frontend | **Total** |
|---|---|---|---|---|
| **Khang** (SE171577) | Leader, backend | 30% | 5% | **35%** |
| **Trường** (SE184087) | Backend | 30% | 5% | **35%** |
| **Thiện** (SE185085) | Frontend | 0% | 30% | **30%** |
| **Total** | | 60% | 40% | **100%** |

- **Thiện builds the frontend only.** His flows keep him as the presenter, but their backend moves:
  F06 handover, visits and access codes, and F10 hand-back and deposit settlement go to **Trường**;
  F11 facilities, units and maintenance, F12 users, audit, shifts and tasks, and access logs go to **Khang**.
- **Frontend kept by the backend owners:** Khang keeps the sign-in, sign-up, verify and reset screens and
  the language switch; Trường keeps Pricing & Policies and Reports. Thiện builds every other screen.
- **Basis:** lines of code each person owns in `docs/CODE_MAP.md`, with the shared backend split between
  Khang and Trường and the shared frontend split three ways, then rounded and agreed. Every task with its
  owner and status is in `docs/SWP391_Topic5_Project_Tracking.xlsx`.

F02 Profile & Settings and F14 Language Switch serve every flow; Khang keeps them. Khang also merges, keeps the
overview tab, SRD, diagram, presentation and AI report.

**BUILT** means the code exists but no test has run it end to end: test it in the browser or with an E2E test, then
turn its badge to DONE in the diagram. **NO SCREEN** means the backend works and a screen is missing.
**NOT BUILT** means nothing exists yet.

## Khang — Flow 1 Reservation, Flow 7 Support

- **Emails (F01, F03, F04).** They only send once real SMTP is set: run `BE/set-gmail-smtp.ps1` with a Gmail App
  Password, then check each one. This also unblocks Trường's reminder emails.
  - F01: verification email, and the verify link turning the account Active.
  - F03: booking received; cancelled or expired, saying whether the deposit comes back.
  - F04: booking confirmed with the unit code.
- **F03:** mark a no-show, only after the move-in date.
- **F04:** change the unit at handover: another of the same size; the old unit becomes Available, the new one
  Reserved, and the draft contract moves with its signature cleared.
- **F02 SMS reminders (NOT BUILT):** the switch is saved but nothing sends SMS. Proposal: out of scope in the SRD.
- **F15 Support Requests (Flow 7):** opening a request is tested (E05b). Still BUILT: the staff queue (assign,
  priority, due date), replies and internal notes, the status changes (InProgress, WaitingForCustomer, Resolved), a
  customer reply reopening it, closing it, and opening a maintenance order from a ticket. Add an E2E test for the
  staff queue.

## Trường — Flow 3 Rented Unit, Flow 4 Rules & Revenue, Flow 6 Renewal & Overdue

- **F05 Contract & Customer:** look up a customer and their contacts; export contracts to CSV with spreadsheet
  formulas neutralised; terminate a contract (codes off, Occupied unit back to Available).
- **F07 Rent Payment**
  - VNPay sandbox round trip: the customer pays, VNPay calls back; a failed payment leaves the invoice open and the
    customer can try again.
  - Adjust an invoice; refund a Succeeded payment; send a reminder by hand (Business Ops can too).
  - **NO SCREEN:** manage fee types in Pricing & Policies.
- **F08 Billing & Overdue**
  - **NO SCREEN:** run billing by hand.
  - Reminder emails 3 days before due, the day after, then weekly, if opted in.
  - Late fee per full week past grace; stage ClearOut with the contract still open; restore access.
- **F09 Renewal:** reject a renewal; the contract stays unchanged.
- **F13 Reports:** each role sees only its facilities; dashboard, occupancy and revenue show the right numbers.

## Thiện — Flow 2 Check-in & Handover, Flow 5 Facility & Staff

- **F06 Handover & Access**
  - Undo a handover: Handover Cancelled, Contract Terminated, codes off, unit Available.
  - The hourly job closes visits at closing time; the customer reads the new code and sees their visit history.
  - The customer asks for a new code; staff revoke a code.
- **F10 Move-out & Settlement:** the hourly job turns the codes off and staff clear the unit; the unit goes to
  Maintenance; a settlement with nothing to pay back shows any shortfall.
- **F11 Facility & Unit Setup:** deactivate a facility; close, reopen or remove a floor; edit, retire or restore a
  unit; the maintenance order cycle from create to done or cancelled.
- **F12 Users & Audit:** staff accounts and the activity log are done and tested (E12, E22). Section D, shifts and
  tasks, is BUILT: add a shift (only for an active person assigned to the facility, not in the past), give a task
  inside it, the assignee moves it to Done, only a manager cancels.

## Shared rules

- Write or extend E2E tests (`e2e/`) for your flows, and turn their badges to DONE in `flows.drawio`.
- Khang reviews and merges to `main`.
- Defense: Khang presents the overview, then each person presents and demos their own story with the demo data.
