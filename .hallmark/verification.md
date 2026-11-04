# Staff redesign verification, 2026-11-04

## Data issue resolved
The database retained eight Manager assignments dated 2026-11-03. The Vietnam date was already 2026-11-04. Staff's hard equality filter hid those records after midnight.

Staff now prefers today, otherwise shows the latest prior plan date with an explicit notice and accurate heading. Users can select any date or All dates. The view still includes every employee's work. Manager creation remains unchanged except its default date consistently uses Vietnam time.

Eleven integration checks passed against the actual page handlers and database, including Manager creation, Staff receipt, all employees' assignments, historical selection, empty date, shift filtering and read-only permissions. Test inserts were rolled back; original business data was unchanged.

## Interface verification
- Build succeeded; seven existing warnings remain.
- 100 browser/wiring checks passed at 320, 390, 768, 1366 and 1920px.
- Actual Razor pages were served by a temporary loopback-only, GET-only Staff preview; no login credentials were changed.
- Checked page overflow, every mobile field label, modal bounds, input sizes, footer labels, dropdown bounds, filters, all original field names, destinations and POST handlers, and absence of JavaScript exceptions.
- Fifteen before and fifteen after captures; `compare.html` shows them side by side.
- Fresh-context finish review found desktop status clipping. Fixed by using two-column labelled records on laptop, single-column records on phone and a table on large screens. Reviewer scored the finding resolved.

## Hallmark scan
Before: 46 Floor findings, 12 Reflex findings, 6 warnings.
After: no visual/static Floor or Reflex findings; the large account trigger is explicitly retained.

The `--kept` comparison reports one false positive: six hidden ASP.NET anti-forgery token *values* rotated between captures. The token fields, form actions and original field names are all still present (verified independently by `check-browser.mjs`). Those secret random values must not be frozen or copied back to silence a comparison tool. The raw report is retained as `final-verified.txt`.

## Retained material and copy
- All destinations, input names, POST handlers, permissions and server-side forms retained.
- BATT logo, green palette and original font family retained.
- Uppercase plan labels are sentence case for Staff only; their meaning is unchanged.
- Personnel heading shows the real selected date instead of calling an older plan today's plan, as required by the reported data issue.
- At the user's subsequent request, production targets and per-machine kg inputs were removed from plan creation and display, including the legacy hardcoded 25,000kg value. Planning retains the date, shift and worker assignments.
- Existing server-rendered full/empty/error/validation states retained. No asynchronous loading state was added because these pages load through normal server navigation.
- No image placeholders or additional brand assets are needed for this Staff work interface.
