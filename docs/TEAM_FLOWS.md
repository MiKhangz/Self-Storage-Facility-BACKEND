# Who owns which flow

Agreed 8 Nov 2026. The team is now three: **Khang** (SE171577, leader), **Trường** (SE184087) and **Thiện** (SE185085).
Each person owns their flows end to end — backend, frontend and tests — and presents them at the defense.
The flows are the tabs of `docs/flows.drawio`; each tab's subtitle names its owner.

| Person | Theme | Flows | Steps | Unfinished |
|---|---|---|---|---|
| **Khang** (leader) | Customer journey & platform | F01 Account · F02 Profile · F03 Reservation & Deposit · F04 Unit Assignment · F05 Contract & Customer · F12 User & Audit · F14 Language | 140 | 15 (14 BUILT, 1 NOT BUILT) |
| **Trường** | Money & contract lifecycle | F07 Rent Payment · F08 Billing & Overdue · F09 Renewal · F10 Move-out & Settlement | 95 | 20 (18 BUILT, 2 NO SCREEN) |
| **Thiện** | Facility operations | F06 Handover & Access · F11 Facility & Unit Setup · F13 Reports | 81 | 31 (all BUILT) |

**BUILT** means the code exists but no test has run it end to end: test it in the browser or with an E2E test, then
turn its badge to DONE in the diagram. **NO SCREEN** means the backend works and a screen is missing.
**NOT BUILT** means nothing exists yet.

Khang has the fewest open items because he also merges, keeps the overview tab, SRD, diagram and AI report, and
writes the demo script.

## Khang

- **Emails (F01, F03, F04, F07, F08).** They only send once real SMTP is set: run `BE/set-gmail-smtp.ps1` with a
  Gmail App Password, then check each email. This also unblocks the email steps in Trường's flows.
  - F01: verification email, and the verify link turning the account Active.
  - F03: booking received; cancelled or expired, saying whether the deposit comes back.
  - F04: booking confirmed with the unit code.
- **F03:** mark a no-show, only after the move-in date.
- **F04:** change the unit at handover: another of the same size; the old unit becomes Available, the new one
  Reserved, and the draft contract moves with its signature cleared.
- **F05:** look up a customer and their contacts; export contracts to CSV with spreadsheet formulas neutralised;
  terminate a contract (codes off, Occupied unit back to Available).
- **F02 SMS reminders (NOT BUILT):** the switch is saved but nothing sends SMS. Proposal: state it as out of scope in
  the SRD rather than build it.

## Trường

- **F07 Rent Payment**
  - VNPay sandbox round trip: the customer pays, VNPay calls back; a failed payment leaves the invoice open and the
    customer can try again.
  - Adjust an invoice (line added, invoice retotalled); refund a Succeeded payment (invoice reopens for the
    balance); send a reminder by hand (Business Ops can too).
  - **NO SCREEN:** manage fee types in Pricing & Policies (was Thiện's TH-07).
- **F08 Billing & Overdue**
  - **NO SCREEN:** run billing by hand.
  - Reminder emails 3 days before due, the day after, then weekly, if opted in.
  - Late fee per full week past grace (stage LateFee); stage ClearOut with the contract still open.
  - Restore access (Business Ops can too): contract Overdue, codes work, the job will not lock again.
- **F09 Renewal:** reject a renewal; the contract stays unchanged.
- **F10 Move-out & Settlement:** the hourly job turns the codes off and staff clear the unit; the unit goes to
  Maintenance (F11); a settlement with nothing to pay back shows any shortfall.

## Thiện

- **F06 Handover & Access**
  - Undo a handover: Handover Cancelled, Contract Terminated, codes off, unit Available.
  - The hourly job closes visits at closing time; the customer reads the new code and sees their visit history.
  - The customer asks for a new code (old codes off, new one issued); staff revoke a code (no new one).
- **F11 Facility & Unit Setup**
  - Deactivate a facility (hidden from booking).
  - Close or reopen a floor (Available units go to Maintenance and come back); remove a floor (unused units deleted,
    the rest retired).
  - Edit a unit; retire or restore it.
  - Maintenance order from start to end: create (from the menu or a ticket), schedule, start (a free unit goes to
    Maintenance), finish with cost and note or cancel.
- **F13 Reports:** each role sees only the facilities it may see; dashboard, occupancy by facility and revenue for
  a range show the right numbers.

## Shared rules

- Write or extend E2E tests (`e2e/`) for your flows, and turn their badges to DONE in `flows.drawio`.
- Khang reviews and merges to `main`.
- Defense: Khang presents the overview tab and the end-to-end chain, then each person presents their own flows.
