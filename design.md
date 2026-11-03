# Design: BATT Staff workspace

This system is locked for Staff screens. Other roles and the shared login/forgot-password pages retain their existing design.

## System
- Genre: modern-minimal
- Shape: app screen within the existing sidebar shell
- Theme: BATT project brand
- Tone: practical factory operations board
- Signature: shift assignments with every employee's work visible
- Green sidebar, warm light work surface; no decorative images or invented figures

## Tokens
`BATT PPMS/wwwroot/css/staff/tokens.css` is the source of truth.
- Primary: #193f35, paired text #fffefa
- Page: #f4f2ea, panel #fffefa, inset #e5eee7
- Ink: #243d35, secondary #52675d
- Field border: #789183
- Font: existing Segoe UI/system stack
- Body 16px, labels 14px, screen heading 32px
- Controls 54px, compact actions 44px; retained large fields requested by the user
- Radius: 12px panels, 8px controls
- Tables use tabular numerals; rows become labelled blocks on mobile without dropping fields

## Behaviour
- Preserve routes, input names, handlers and permissions
- Staff sees every employee's assignments for the displayed date
- Today takes priority; if no plan exists today, display the latest prior plan with its real date and an explicit notice
- Date selection and All dates are URL filters
- Reports and incidents continue using existing server handlers and Bootstrap modal behaviour
- Focus is instant, reduced-motion supported, no entrance animations

## Exports
- `BATT PPMS/wwwroot/css/staff/tokens.css`
- `BATT PPMS/wwwroot/css/staff/base.css`
- `BATT PPMS/wwwroot/css/staff-workspace.css`
