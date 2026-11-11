"""Who owns which file: one rule table (RULES), checked against the real folders of both repos.

    python scripts/code_map.py            check only: files per owner, and every file no rule covers
    python scripts/code_map.py --write    also rewrite docs/CODE_MAP.md and the "Code map" tab of docs/flows.drawio

The frontend repo is expected next to this one (../Self-Storage-Facility-FRONTEND); set STOREASE_FRONTEND to point
elsewhere. Exits 1 while any file has no owner: add it to RULES (first match wins). Then copy docs/flows.drawio to
the frontend repo's flows.drawio, as with every diagram change.
"""
import os, re, sys
from fnmatch import fnmatch
from collections import defaultdict
from xml.sax.saxutils import escape

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BE = os.path.join(REPO, 'BE')
FE = os.environ.get('STOREASE_FRONTEND') or os.path.join(os.path.dirname(REPO), 'Self-Storage-Facility-FRONTEND')
COLORS = {'Khang': ('#dae8fc', '#6c8ebf'), 'Trường': ('#d5e8d4', '#82b366'), 'Thiện': ('#ffe6cc', '#d79b00'), 'Shared': ('#f5f5f5', '#999999')}

# (area, owner, flows, patterns). First match wins, so specific files come before their folder.
# Owners follow docs/TEAM_FLOWS.md (one owner per assignment flow); the person headers below are historical.
RULES = [
    # ---- Khang
    ('Account & sign-in', 'Khang', 'F01', [
        'BE:StorEase.Api/Controllers/AuthController.cs', 'BE:StorEase.Application/Features/Auth/*',
        'BE:tests/*/AuthServiceTests.cs', 'BE:tests/*/AccountAndPricingTests.cs', 'BE:tests/*/SmtpEmailServiceTests.cs',
        'FE:src/customer/auth/*', 'FE:src/staff/AdminLoginPage.jsx', 'FE:src/features/auth/*', 'FE:e2e/01-account.spec.js']),
    ('Profile & settings', 'Khang', 'F02', [
        'BE:StorEase.Api/Controllers/ProfileController.cs', 'BE:StorEase.Application/Features/Users/*', 'FE:src/customer/account/ProfilePage.jsx']),
    ('Reservations, deposit & walk-in', 'Khang', 'F03 F04', [
        'BE:StorEase.Api/Controllers/ReservationsController.cs', 'BE:StorEase.Application/Features/Reservations/*',
        'BE:tests/*/ReservationServiceTests.cs', 'BE:tests/*/WalkInTests.cs',
        'FE:src/customer/booking/*', 'FE:src/staff/pages/ReservationsPage.jsx', 'FE:src/staff/pages/WalkInBookingPage.jsx',
        'FE:e2e/03-desk.spec.js']),
    ('Contracts & customers', 'Trường', 'F05', [
        'BE:StorEase.Api/Controllers/ContractsController.cs', 'BE:StorEase.Api/Controllers/CustomersController.cs',
        'BE:StorEase.Application/Features/Contracts/*', 'BE:tests/*/ContractServiceTests.cs', 'BE:tests/*/ContractLifecycleTests.cs',
        'FE:src/staff/pages/ContractsPage.jsx', 'FE:src/staff/pages/CustomerPage.jsx', 'FE:src/staff/print.js']),
    ('Access logs', 'Thiện', '—', [
        'BE:StorEase.Api/Controllers/AccessLogsController.cs', 'BE:StorEase.Application/Features/AccessCodes/AccessLogService.cs',
        'BE:StorEase.Application/Features/AccessCodes/IAccessLogService.cs']),
    ('Staff accounts & audit', 'Thiện', 'F12', [
        'BE:StorEase.Api/Controllers/StaffController.cs', 'BE:StorEase.Api/Controllers/ActivityLogsController.cs',
        'BE:StorEase.Api/Middleware/ActivityLogFilter.cs', 'BE:StorEase.Application/Features/ActivityLogs/*',
        'BE:StorEase.Application/Features/Staff/StaffAccount*', 'BE:StorEase.Application/Features/Staff/*/StaffAccount*',
        'BE:tests/*/StaffAccountServiceTests.cs', 'BE:tests/*/StaffScopeTests.cs', 'BE:tests/*/ActivityLog*Tests.cs',
        'FE:src/staff/pages/UsersPage.jsx', 'FE:src/staff/pages/InviteUserPage.jsx', 'FE:src/staff/pages/PendingBackendPages.jsx',
        'FE:src/staff/activityLabels.js', 'FE:src/staff/permissions.js', 'FE:src/staff/components/FacilityPicker.jsx',
        'FE:tests/activityLabels.test.mjs']),
    ('Shifts & staff tasks', 'Thiện', 'F12', [
        'BE:StorEase.Api/Controllers/ShiftsController.cs', 'BE:StorEase.Api/Controllers/StaffTasksController.cs',
        'BE:StorEase.Application/Features/Staff/*', 'BE:tests/*/ShiftServiceTests.cs', 'BE:tests/*/StaffTaskServiceTests.cs',
        'FE:src/staff/pages/ShiftsPage.jsx']),
    ('Language', 'Khang', 'F14', ['FE:src/i18n/*', 'FE:tests/fill.test.mjs', 'FE:e2e/06-language.spec.js']),
    # ---- Trường
    ('Invoices, payments & pricing', 'Trường', 'F07', [
        'BE:StorEase.Api/Controllers/InvoicesController.cs', 'BE:StorEase.Api/Controllers/PaymentsController.cs',
        'BE:StorEase.Api/Controllers/PricingPoliciesController.cs', 'BE:StorEase.Api/Controllers/FacilityPricesController.cs',
        'BE:StorEase.Application/Features/Invoices/*',
        'BE:StorEase.Application/Features/Payments/*', 'BE:StorEase.Application/Features/Pricing/*',
        'BE:tests/*/VnPay*Tests.cs', 'BE:tests/*/Pricing*Tests.cs', 'BE:tests/*/FacilityPriceTests.cs', 'BE:tests/*/StorageUnitPricingTests.cs', 'BE:tests/*/Truong*Tests.cs',
        'FE:src/customer/account/PaymentsPage.jsx', 'FE:src/customer/account/PayInvoiceButton.jsx',
        'FE:src/customer/account/VnPayReturnPage.jsx', 'FE:src/staff/pages/RecordPaymentPage.jsx', 'FE:src/staff/pages/PricingPage.jsx', 'FE:src/staff/pages/FacilityPriceList.jsx',
        'FE:src/staff/pages/PriceHistory.jsx', 'FE:src/staff/pages/priceChanges.js', 'FE:tests/priceChanges.test.mjs']),
    ('Billing job & overdue', 'Trường', 'F08', [
        'BE:StorEase.Api/Controllers/BillingController.cs', 'BE:StorEase.Api/Controllers/OverdueCasesController.cs',
        'BE:StorEase.Api/BillingWorker.cs', 'BE:StorEase.Application/Features/Billing/*', 'BE:StorEase.Application/Features/OverdueCases/*',
        'BE:tests/*/BillingCycleTests.cs', 'BE:tests/*/ScopeTermsAndBillingTests.cs', 'FE:src/staff/pages/OverdueCasesPage.jsx']),
    ('Renewals', 'Trường', 'F09', [
        'BE:StorEase.Api/Controllers/RenewalsController.cs', 'BE:StorEase.Application/Features/Renewals/*', 'BE:tests/*/Renewal*Tests.cs']),
    ('Hand-back & deposit settlement', 'Thiện', 'F10', [
        'BE:StorEase.Api/Controllers/ReturnsController.cs', 'BE:StorEase.Api/Controllers/DepositRefundsController.cs',
        'BE:StorEase.Application/Features/Returns/*', 'BE:StorEase.Application/Features/DepositRefunds/*',
        'BE:tests/*/DepositRefundTests.cs', 'BE:tests/*/DepositSettlementTests.cs', 'BE:tests/*/ReturnSettlementTests.cs', 'BE:tests/*/MoveOutDeadlineTests.cs',
        'FE:src/customer/account/ReturnPage.jsx', 'FE:src/staff/pages/ReturnsPage.jsx', 'FE:src/staff/pages/DepositRefundsPage.jsx']),
    # ---- Thiện
    ('Handover, visits & access codes', 'Thiện', 'F06', [
        'BE:StorEase.Api/Controllers/HandoversController.cs', 'BE:StorEase.Api/Controllers/CheckInsController.cs',
        'BE:StorEase.Api/Controllers/AccessCodesController.cs', 'BE:StorEase.Application/Features/Handovers/*',
        'BE:StorEase.Application/Features/CheckIns/*', 'BE:StorEase.Application/Features/AccessCodes/*',
        'BE:tests/*/Handover*Tests.cs', 'BE:tests/*/CheckInTests.cs', 'BE:tests/*/AccessCodeManagementTests.cs', 'BE:tests/StorEase.Application.Tests/TestCodes.cs',
        'FE:src/customer/account/MyStoragePage.jsx', 'FE:src/customer/account/UnitDetailPage.jsx',
        'FE:src/staff/pages/HandoverPage.jsx', 'FE:src/staff/pages/CheckInPage.jsx', 'FE:src/staff/components/HandoverPhotos.jsx',
        'FE:src/staff/photoRules.js', 'FE:tests/photoRules.test.mjs']),
    ('Facilities, units & maintenance', 'Thiện', 'F11', [
        'BE:StorEase.Api/Controllers/FacilitiesController.cs', 'BE:StorEase.Api/Controllers/FloorsController.cs',
        'BE:StorEase.Api/Controllers/StorageUnitsController.cs', 'BE:StorEase.Api/Controllers/UnitTypesController.cs',
        'BE:StorEase.Api/Controllers/MaintenanceOrdersController.cs', 'BE:StorEase.Application/Features/Facilities/*',
        'BE:StorEase.Application/Features/StorageUnits/*', 'BE:StorEase.Application/Features/Maintenance/*',
        'BE:tests/*/FloorStatusTests.cs', 'BE:tests/*/MaintenanceServiceTests.cs', 'BE:tests/*/UnstaffedFacilityTests.cs',
        'FE:src/customer/locations/*', 'FE:src/customer/sizes/*', 'FE:src/staff/pages/FacilitiesPage.jsx', 'FE:src/staff/pages/FacilityDetailPage.jsx',
        'FE:src/staff/pages/UnitsPage.jsx', 'FE:src/staff/pages/UnitSizesPage.jsx', 'FE:src/staff/pages/MaintenancePage.jsx',
        'FE:tests/management-csv.test.mjs']),  # tests the old src/admin screens
    ('Reports & dashboard', 'Trường', 'F13', [
        'BE:StorEase.Api/Controllers/ReportsController.cs', 'BE:StorEase.Application/Features/Reports/*',
        'FE:src/staff/pages/ReportsPage.jsx', 'FE:src/staff/pages/DashboardPage.jsx']),
    ('Support tickets', 'Khang', 'F15', [
        'BE:StorEase.Api/Controllers/SupportTicketsController.cs', 'BE:StorEase.Api/Controllers/TicketQueueController.cs',
        'BE:StorEase.Api/Controllers/TicketCategoriesController.cs', 'BE:StorEase.Application/Features/SupportTickets/*',
        'BE:tests/*/SupportTicketServiceTests.cs', 'BE:tests/*/TicketQueueServiceTests.cs',
        'FE:src/customer/support/*', 'FE:src/staff/pages/TicketsPage.jsx', 'FE:src/staff/pages/TicketDetailPage.jsx']),
    # ---- Shared: anyone edits, Khang reviews
    ('Shared', 'Shared', '—', [
        'BE:StorEase.Api/Program.cs', 'BE:StorEase.Api/CurrentUser.cs', 'BE:StorEase.Api/Controllers/ApiControllerBase.cs', 'BE:StorEase.Api/Middleware/*',
        'BE:tests/*/CoreFlowSeed.cs', 'BE:tests/*/TestDb.cs', 'BE:tests/*/Fake*.cs', 'BE:tests/*/ErrorTextTests.cs', 'BE:tests/*/ExistingDataCleanupTests.cs',
        'BE:tests/StorEase.Api.Tests/*',
        'FE:src/main.tsx', 'FE:src/api/axiosClient.js', 'FE:src/customer/components/*', 'FE:src/customer/layout/*', 'FE:src/customer/home/*',
        'FE:src/customer/account/AccountLayout.jsx', 'FE:src/customer/account/queries.js', 'FE:src/staff/AdminLayout.jsx',
        'FE:src/staff/components/ui.jsx', 'FE:src/staff/queries.js', 'FE:e2e/helpers.js', 'FE:e2e/start-backend.mjs',
        'FE:e2e/02-rental.spec.js', 'FE:e2e/04-admin.spec.js', 'FE:e2e/05-cancel.spec.js']),  # several people's tests each
]
SHARED_FOLDERS = ['StorEase.Domain (entities, enums)', 'StorEase.Infrastructure (DbContext, migrations, email, file storage)',
                  'StorEase.Application/Common', 'src/api/generated (regenerate with npm run api:gen; never edit by hand)']


def scan():
    files = []
    for root, sub, ext in [(BE, 'StorEase.Api/Controllers', '.cs'), (BE, 'StorEase.Api', '.cs'), (BE, 'StorEase.Api/Middleware', '.cs'),
                           (BE, 'StorEase.Application/Features', '.cs'), (BE, 'tests', '.cs')]:
        top = os.path.join(root, sub)
        walk = os.walk(top) if sub in ('StorEase.Application/Features', 'tests') else [(top, [], os.listdir(top))]
        for d, dirs, names in walk:
            dirs[:] = [x for x in dirs if x not in ('bin', 'obj')]
            for n in names:
                if n.endswith(ext) and os.path.isfile(os.path.join(d, n)):
                    files.append('BE:' + os.path.relpath(os.path.join(d, n), root).replace('\\', '/'))
    for d, dirs, names in os.walk(os.path.join(FE, 'src')):
        rel = os.path.relpath(d, FE).replace('\\', '/')
        if rel.startswith('src/api/generated') or rel.startswith('src/admin'):
            continue
        files += ['FE:' + f'{rel}/{n}' for n in names if n.endswith(('.js', '.jsx', '.ts', '.tsx'))]
    files += ['FE:e2e/' + n for n in os.listdir(os.path.join(FE, 'e2e')) if n.endswith(('.js', '.mjs'))]
    files += ['FE:tests/' + n for n in os.listdir(os.path.join(FE, 'tests')) if n.endswith('.test.mjs')]
    return sorted(set(files))


def owner_of(path):
    for area, who, flows, pats in RULES:
        if any(fnmatch(path, p) for p in pats):
            return area, who, flows
    return None


def routes(path):
    """Controller routes, read from its attributes: ['GET /api/x', 'POST /api/x/{id}/y']."""
    src = open(os.path.join(BE, path[3:]), encoding='utf-8-sig').read()
    base = re.search(r'\[Route\("([^"]+)"\)\]', src)
    base = base.group(1) if base else ''
    out = []
    for verb, sub in re.findall(r'\[Http(Get|Post|Put|Patch|Delete)(?:\("([^"]*)"\))?\]', src):
        sub = re.sub(r':\w+', '', sub or '')
        url = sub[1:] if sub.startswith('~') else '/' + '/'.join(x for x in [base, sub] if x)
        out.append(f'{verb.upper()} {url}')
    return out


def e2e_ids():
    ids = defaultdict(list)
    for n in os.listdir(os.path.join(FE, 'e2e')):
        if n.endswith('.spec.js'):
            for m in re.finditer(r"test\('(E\d+\w?) ([^']*)", open(os.path.join(FE, 'e2e', n), encoding='utf-8').read()):
                ids[n].append((m.group(1), m.group(2)))
    return ids


# Which person each E2E test belongs to (spec files hold several people's tests).
E2E_OWNER = {'E01': 'Khang', 'E02': 'Khang', 'E03': 'Khang', 'E04': 'Khang', 'E09': 'Khang', 'E10': 'Khang', 'E13': 'Khang', 'E14': 'Khang', 'E17': 'Khang', 'E18': 'Khang', 'E20': 'Khang', 'E21': 'Khang', 'E05': 'Thiện', 'E05b': 'Thiện', 'E05c': 'Thiện', 'E08': 'Thiện', 'E11': 'Thiện', 'E12': 'Thiện', 'E15': 'Thiện', 'E19': 'Thiện', 'E22': 'Thiện', 'E06': 'Trường', 'E07': 'Trường', 'E16': 'Trường', 'E23': 'Trường'}


def page(owned):
    """A draw.io page: one column per part of the code, one box per file in its owner's color."""
    cells, nid = [], [0]

    def cell(value, style, x, y, w, h):
        nid[0] += 1
        cells.append(f'<mxCell id="cm-{nid[0]}" value="{escape(value, {chr(34): "&quot;"})}" style="{style}" vertex="1" parent="1">'
                     f'<mxGeometry x="{x}" y="{y}" width="{w}" height="{h}" as="geometry"/></mxCell>')

    cell('<b>StorEase · Code map</b>', 'text;html=1;align=left;verticalAlign=middle;fontSize=24;', 0, 0, 900, 36)
    cell('Each box is a file, colored by its owner. Shared files: anyone edits, Khang reviews. Source of truth: docs/CODE_MAP.md',
         'text;html=1;align=left;verticalAlign=middle;fontSize=12;fontColor=#555555;', 0, 36, 1200, 22)
    x = 0
    for i, who in enumerate(COLORS):
        fill, line = COLORS[who]
        n = sum(1 for v in owned.values() if v[1] == who)
        cell(f'{who} ({n})', f'rounded=1;whiteSpace=wrap;html=1;fillColor={fill};strokeColor={line};fontSize=12;fontStyle=1;', 1250 + i * 150, 4, 140, 30)
    groups = [
        ('Backend · API controllers', lambda p: p.startswith('BE:StorEase.Api/Controllers/')),
        ('Backend · API (jobs, middleware)', lambda p: p.startswith('BE:StorEase.Api/') and '/Controllers/' not in p),
        ('Backend · Features A–M', lambda p: p.startswith('BE:StorEase.Application/Features/') and p.split('/')[2][0] <= 'M'),
        ('Backend · Features N–Z', lambda p: p.startswith('BE:StorEase.Application/Features/') and p.split('/')[2][0] > 'M'),
        ('Backend · tests', lambda p: p.startswith('BE:tests/')),
        ('Frontend · customer site (src/customer)', lambda p: p.startswith('FE:src/customer/')),
        ('Frontend · staff portal (src/staff)', lambda p: p.startswith('FE:src/staff/')),
        ('Frontend · app, auth, i18n, tests', lambda p: p.startswith('FE:') and not p.startswith(('FE:src/customer/', 'FE:src/staff/'))),
    ]
    col_w, box_h, gap, top = 300, 20, 4, 80
    for title, test in groups:
        # A folder's own files first, then its Dtos/ and Validators/.
        paths = sorted((p for p in owned if test(p)), key=lambda p: (p.rsplit('/', 1)[0].count('/'), p) if p.startswith('FE:') else (p.split('/')[:3], p.count('/'), p))
        y = top
        cell(f'<b>{title}</b>', 'rounded=0;whiteSpace=wrap;html=1;fillColor=#37474f;fontColor=#ffffff;strokeColor=none;fontSize=12;align=left;spacingLeft=8;', x, y, col_w, 28)
        y += 34
        last_dir = None
        for p in paths:
            rel = p.split(':', 1)[1]
            for prefix in ('StorEase.Api/Controllers/', 'StorEase.Api/', 'StorEase.Application/Features/', 'tests/', 'src/customer/', 'src/staff/'):
                if rel.startswith(prefix):
                    rel = rel[len(prefix):]
                    break
            folder, name = (rel.split('/', 1) if p.startswith('BE:') else rel.rsplit('/', 1)) if '/' in rel else ('', rel)
            if folder != last_dir and folder:
                cell(folder + '/', 'text;html=1;align=left;verticalAlign=bottom;fontSize=10;fontStyle=1;fontColor=#37474f;', x, y, col_w, 16)
                y += 18
            last_dir = folder
            fill, line = COLORS[owned[p][1]]
            label = name.rsplit('/', 1)[-1] if p.startswith('FE:') else name.replace('Controller.cs', '').replace('.cs', '')
            cell(label, f'rounded=1;whiteSpace=wrap;html=1;fillColor={fill};strokeColor={line};fontSize=10;align=left;spacingLeft=6;', x + (10 if folder else 0), y, col_w - (10 if folder else 0), box_h)
            y += box_h + gap
        x += col_w + 30
    return ('<diagram id="codemap" name="Code map"><mxGraphModel dx="1400" dy="800" grid="1" gridSize="10" guides="1" tooltips="1" '
            'connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="2200" pageHeight="1400" background="#ffffff" math="0" shadow="0">'
            '<root><mxCell id="0"/><mxCell id="1" parent="0"/>' + ''.join(cells) + '</root></mxGraphModel></diagram>')


def markdown(owned):
    ids = e2e_ids()
    by_owner = defaultdict(lambda: defaultdict(list))
    areas = {}
    for p, (area, who, flows) in owned.items():
        by_owner[who][area].append(p)
        areas[area] = flows
    L = ['# Code map: who owns which file', '',
         'Generated from the real folders by `python scripts/code_map.py --write` (the same rules draw the "Code map" tab in `docs/flows.drawio`).',
         'Each file has one owner, who answers for it, writes its tests and reviews changes to it. **Shared** files: anyone',
         'edits, Khang reviews. Flow owners are in `docs/TEAM_FLOWS.md`.', '',
         'Paths start with `BE/` (backend repo) or the frontend repo root. Old unrouted screens in `src/admin/` are not listed.', '',
         '| Person | Flows | Backend files | Frontend files | E2E tests |', '|---|---|---|---|---|']
    for who in ['Khang', 'Trường', 'Thiện']:
        fl = sorted({f for a in by_owner[who] for f in areas[a].split() if f.startswith('F')})
        be = sum(1 for a in by_owner[who].values() for p in a if p.startswith('BE:'))
        fe = sum(1 for a in by_owner[who].values() for p in a if p.startswith('FE:'))
        tests = ', '.join(t for t, o in E2E_OWNER.items() if o == who)
        L.append(f'| **{who}** | {", ".join(fl)} | {be} | {fe} | {tests} |')
    for who in ['Khang', 'Trường', 'Thiện', 'Shared']:
        L += ['', f'## {who}', '']
        for area, paths in by_owner[who].items():
            flows = areas[area]
            L += [f'### {area}' + (f' ({flows})' if flows != '—' else ' (no flow tab)'), '']
            for kind, test in [('Controllers and routes', lambda p: '/Controllers/' in p),
                               ('Services, DTOs, validators, jobs', lambda p: p.startswith('BE:') and '/Controllers/' not in p and not p.startswith('BE:tests/')),
                               ('Backend tests', lambda p: p.startswith('BE:tests/')),
                               ('Frontend', lambda p: p.startswith('FE:') and not p.startswith(('FE:e2e/', 'FE:tests/'))),
                               ('Frontend tests', lambda p: p.startswith(('FE:e2e/', 'FE:tests/')))]:
                sel = [p for p in paths if test(p)]
                if not sel:
                    continue
                L.append(f'- **{kind}**')
                for p in sel:
                    shown = ('BE/' if p.startswith('BE:') else '') + p.split(':', 1)[1]
                    extra = ''
                    if '/Controllers/' in p:
                        r = routes(p)
                        extra = (': ' + ', '.join(f'`{x}`' for x in r)) if r else ''
                    if p.startswith('FE:e2e/') and p.endswith('.spec.js'):
                        mine = [f'{t}' for t, _ in ids[p.split('/')[-1]] if E2E_OWNER.get(t) == who]
                        others = [f'{t} ({E2E_OWNER.get(t, "?")})' for t, _ in ids[p.split('/')[-1]] if E2E_OWNER.get(t) != who]
                        extra = (f': {", ".join(mine)}' if mine else '') + (f'; also holds {", ".join(others)}' if others else '')
                    if p == 'FE:src/customer/account/UnitDetailPage.jsx':
                        extra = ': its renewal part is co-owned by Trường (F09)'
                    L.append(f'  - `{shown}`{extra}')
            L.append('')
    L += ['## Shared folders (not listed file by file)', ''] + [f'- `{f}`' for f in SHARED_FOLDERS]
    L += ['', '## Where do I start?', '',
          '1. Open your flow tab in `docs/flows.drawio`; each step names the screen or job.',
          '2. Find the controller above for the route the screen calls (the browser Network tab shows it), then its service in `Features/`.',
          '3. Run your E2E tests: `npx playwright test -g "E06"`; backend tests: `dotnet test --filter <TestClass>`.',
          '4. A new file? Add it to `RULES` in `scripts/code_map.py` and run `python scripts/code_map.py --write`, so the map stays complete.']
    return '\n'.join(L) + '\n'


if __name__ == '__main__':
    files = scan()
    owned, unowned = {}, []
    for f in files:
        o = owner_of(f)
        (owned.__setitem__(f, o) if o else unowned.append(f))
    counts = defaultdict(int)
    for v in owned.values():
        counts[v[1]] += 1
    print('files', len(files), dict(counts))
    print('unowned', len(unowned), *unowned, sep='\n  ')
    if unowned or '--write' not in sys.argv:
        sys.exit(1 if unowned else 0)
    diagram, doc = os.path.join(REPO, 'docs', 'flows.drawio'), os.path.join(REPO, 'docs', 'CODE_MAP.md')
    drawio = open(diagram, encoding='utf-8').read()
    drawio = re.sub(r'<diagram id="codemap".*?</diagram>', '', drawio, flags=re.S)
    drawio = drawio.replace('</mxfile>', page(owned) + '</mxfile>')
    open(diagram, 'w', encoding='utf-8', newline='').write(drawio)
    open(doc, 'w', encoding='utf-8', newline='\n').write(markdown(owned))
    print('written', diagram, doc)
