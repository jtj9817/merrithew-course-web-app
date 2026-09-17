# [A11Y-101] Front-end AODA & WCAG 2.1 AA Accessibility Audit and Remediation

| Field | Value |
| :--- | :--- |
| **Issue Type** | Task / Compliance Improvement |
| **Key** | A11Y-101 |
| **Component** | `sub.webui` (`frontend`, `backend/Pages`) |
| **Priority** | High |
| **Status** | Closed |
| **Closed At** | 2026-09-17 |
| **Resolution** | Resolved -- implemented skip link navigation targeting a persistent queue container (#inquiry-queue with tabindex="-1") present across all queue phases (pre-mount, loading, empty, error, ready) (GAP-1), table caption and aria-sort on Created column header (GAP-2, GAP-3), aria-invalid on failed status select controls (GAP-4), aria-current on active pagination indicator and polite live announcements for filter and page transitions reporting on-page item count (GAP-5), and fallback focus recovery on modal close for detached openers (GAP-6). Verified with unit, integration, and end-to-end browser QA tests. |
| **Labels** | `accessibility`, `aoda`, `wcag-2.1-aa`, `compliance`, `frontend`, `screen-reader`, `keyboard-navigation` |
| **Regulatory Standard** | Accessibility for Ontarians with Disabilities Act (AODA, 2005) / IASR §14 (WCAG 2.0/2.1 Level AA) |

---

## 1. Summary & Background

Under the **Accessibility for Ontarians with Disabilities Act (AODA, 2005)** and the **Integrated Accessibility Standards Regulation (IASR, O. Reg. 191/11, Section 14)**, all public and large private-sector web applications in Ontario must comply with **WCAG 2.0/2.1 Level AA**.

This ticket records the findings of an accessibility audit conducted on the Merrithew Course Inquiry Dashboard front-end (`backend/Pages/` Razor shell and `frontend/src/` React triage island), categorizes the currently compliant principles (Perceivable, Operable, Understandable, Robust), details the compliance deficiencies currently present in the codebase, and specifies acceptance criteria and implementation tasks for remediation.

---

## 2. Assessment of Current Implementation (Compliant Principles)

The existing front-end baseline incorporates significant accessibility architecture aligned with the four core principles of WCAG/AODA:

### 2.1. Principle 1: Perceivable
* **Non-Color Dependent Status Indication (WCAG 1.4.1 – Level A)**:
  * Status is never communicated by color alone. Every badge renders a plain-text status name (`New`, `Contacted`, `Pending`, `Registered`, `Closed`) via `StatusBadge.tsx`.
  * **Colorblind Mode**: Includes an Okabe-Ito / Tol CVD palette exceeding WCAG AAA contrast ($\ge 7:1$), distinct border patterns (solid, dashed, dotted, double), semantic M3 SVG icons with `aria-hidden="true"`, and a `.status-legend` guide. Initial preference respects system `(prefers-contrast: more)`.
* **Color Contrast Ratios (WCAG 1.4.3 & 1.4.11 – Level AA)**:
  * CSS tokens in `styles.css` are computationally verified for body text ($\ge 4.5:1$), large text ($\ge 3:1$), and UI components/borders ($\ge 3:1$).
* **Semantic Structure & Disambiguated Labels (WCAG 1.3.1 – Level A)**:
  * Semantic HTML5: `<header>`, `<main class="shell">`, `<h1>`, `<h2>`, `<table class="inquiry-table">`, `<thead>`, `<tbody>`, `<th scope="col">`, `<dl>`, `<dt>`, `<dd>`, and `<nav aria-label="Pagination">`.
  * Form controls have programmatic labels (`<label htmlFor="status-filter">`, `<label htmlFor="sort-order">`).
  * Per-row controls disambiguate targets for screen reader navigation (`aria-label="Details for {name}"`, `aria-label="Status for {name}"`, `aria-label="Apply for {name}"`).
* **Responsive Reflow & Text Scaling (WCAG 1.4.4 & 1.4.10 – Level AA)**:
  * Layout relies on relative units (`rem`, `%`, flexbox, CSS grid) permitting 200% zoom and responsive reflow down to 375px viewports without horizontal scrolling or control clipping (`MAN-UI-004`).
* **Hover & Focus Persistence (WCAG 1.4.13 – Level AA)**:
  * Snackbar toasts in `LiveRegions.tsx` pause their auto-dismiss timer on mouse hover (`onPointerEnter`) and keyboard focus (`onFocus`).

### 2.2. Principle 2: Operable
* **Complete Keyboard Navigability (WCAG 2.1.1 – Level A)**:
  * All interactive controls (`<button>`, `<select>`, modal close, toggle switches, pagination) are operable via standard keyboard commands (`Tab`, `Shift+Tab`, `Enter`, `Space`, `Escape`). Verified in test `IT-UI-027`.
* **No Keyboard Trap & Modal Focus Management (WCAG 2.1.2 & 2.4.3 – Level A)**:
  * Modal drawer (`DetailPanel.tsx`):
    * Focus shifts into the dialog on mount (`containerRef.current?.focus()`).
    * Implements an active focus trap cycling `Tab` and `Shift+Tab` within the dialog boundaries.
    * Background is marked `inert={isDetailOpen ? true : undefined}` and document body scrolling is locked (`overflow: hidden`), removing background elements from the accessibility tree.
    * Closes cleanly on `Escape`.
    * Restores focus directly to the originating row opener button on close (`focusDetailOpener`).
* **Visible Focus Indicators (WCAG 2.4.7 – Level AA)**:
  * Global `:focus-visible` styling provides a high-contrast outline (`outline: 3px solid var(--md-sys-color-primary); outline-offset: 2px;`).
  * Table rows provide `:focus-within` styling.
  * Enhanced high-contrast focus rings applied under `[data-colorblind="true"]`.
* **Timing & Motion Control (WCAG 2.2.1 & 2.3.3 – Level A / AAA)**:
  * Queue triage operations carry no session time limits.
  * Snackbars provide an accessible "Dismiss" button alongside auto-dismiss.
  * System preferences for reduced motion (`prefers-reduced-motion: reduce`) disable CSS transitions and animations.

### 2.3. Principle 3: Understandable
* **Page Language Declaration (WCAG 3.1.1 – Level A)**:
  * Root HTML specifies `<html lang="en">` in `backend/Pages/Shared/_Layout.cshtml`.
* **Predictable Context & State Changes (WCAG 3.2.1 & 3.2.2 – Level A)**:
  * Changing a row's status `<select>` stages a draft state without triggering an immediate, unexpected network request or page jump; updates require explicit click/activation of the "Apply" button.
* **Readable Error Messages (WCAG 3.3.1 – Level A)**:
  * Formatted, descriptive error messages for network outages, 404s, or invalid data without exposing stack traces.

### 2.4. Principle 4: Robust
* **Standard ARIA Markup (WCAG 4.1.2 – Level A)**:
  * Dialog: `role="dialog"`, `aria-modal="true"`, `aria-labelledby="detail-modal-title"`.
  * Switch: `role="switch"`, `aria-checked={enabled}`, and matching accessible name.
  * Progress: `aria-busy={fetching || undefined}` on the inquiry table and `aria-busy="true"` on loading indicators.
* **Asynchronous Live Announcements (WCAG 4.1.3 – Level AA)**:
  * Dual live-region architecture (`LiveRegions.tsx`):
    * `role="status"` / `aria-live="polite"` for non-disruptive feedback (e.g., successful status save, colorblind mode toggled).
    * `role="alert"` / `aria-live="assertive"` for critical failures (e.g., mutation failure, 404 record missing).
  * Live regions exist in the initial DOM tree to ensure assistive technologies observe updates.

---

## 3. Identified Compliance Gaps (What We Are Lacking)

The following 6 deficiencies must be addressed to achieve full AODA / WCAG 2.0 & 2.1 Level AA compliance:

### [GAP-1] Missing "Skip to Main Content" Bypass Block
* **WCAG Criteria**: 2.4.1 Bypass Blocks (Level A)
* **Affected Files**: `backend/Pages/Shared/_Layout.cshtml`, `backend/Pages/Dashboard.cshtml`, `frontend/src/styles.css`
* **Description**: There is no skip-navigation link before the application header. Keyboard and screen reader users must tab through the brand logo, appbar chip, colorblind toggle, developer scenario controls, and toolbar filters before reaching the inquiry queue.
* **Remediation**: Add a visually hidden `<a href="#inquiry-queue" class="skip-link">Skip to inquiry queue</a>` at the top of the body that becomes visible when focused.

### [GAP-2] Missing Table Caption / Accessible Name
* **WCAG Criteria**: 1.3.1 Info and Relationships (Level A), 4.1.2 Name, Role, Value (Level A)
* **Affected Files**: `frontend/src/components/InquiryTable.tsx`
* **Description**: The inquiry `<table>` lacks a `<caption>` element or an `aria-label`/`aria-labelledby` attribute. Assistive technologies announcing the table state ("Table, 6 columns, N rows") provide no programmatic name explaining what data is in the table.
* **Remediation**: Add `<caption className="sr-only">Incoming Course Inquiries Queue</caption>` inside `<table className="inquiry-table">`.

### [GAP-3] Missing Table Column Header Sort Indication (`aria-sort`)
* **WCAG Criteria**: 1.3.1 Info and Relationships (Level A), 4.1.2 Name, Role, Value (Level A)
* **Affected Files**: `frontend/src/components/InquiryTable.tsx`, `frontend/src/App.tsx`
* **Description**: Sorting is selected through a dropdown in `Toolbar.tsx`. However, the table column header `<th scope="col" className="th-created">Created</th>` does not carry an `aria-sort` attribute. Screen reader users traversing the table headers cannot discover that the table is sorted by created date or determine the sort direction.
* **Remediation**: Pass the active sort direction to `InquiryTable` and render `aria-sort={sort === 'createdDateAsc' ? 'ascending' : 'descending'}` on the `Created` header (and `aria-sort="none"` on unsorted sortable headers).

### [GAP-4] Missing Form Control Error Association (`aria-invalid` / `aria-describedby`)
* **WCAG Criteria**: 3.3.1 Error Identification (Level A), 3.3.2 Labels or Instructions (Level A)
* **Affected Files**: `frontend/src/components/InquiryTable.tsx`, `frontend/src/App.tsx`
* **Description**: When a status update fails, the error message is broadcast to the global `role="alert"` live region. However, the specific row's `<select>` element is not marked with `aria-invalid="true"` or linked to the error state. If a keyboard user tabs back to the control, assistive technology does not announce that the field is in an error state.
* **Remediation**: Track failed mutation row IDs and set `aria-invalid={hasError ? 'true' : undefined}` on the row's `<select>`.

### [GAP-5] Missing Programmatic Feedback for Page Navigation & Filter Result Counts
* **WCAG Criteria**: 4.1.3 Status Messages (Level AA), 1.3.1 Info and Relationships (Level A)
* **Affected Files**: `frontend/src/components/Pagination.tsx`, `frontend/src/App.tsx`
* **Description**:
  1. In `Pagination.tsx`, the current page indicator is a non-semantic `<span>Page {page} of {lastPage}</span>` lacking `aria-current="page"`.
  2. When navigating between pages or switching status filters, focus remains on the clicked control and the table updates asynchronously without a live-region announcement. Screen reader users do not receive confirmation that page 2 has loaded or how many items match the filter.
* **Remediation**:
  * Set `aria-current="page"` on the active page indicator.
  * Announce page transitions and filter updates via the polite live region (e.g., "Page 2 of 5 loaded, showing 10 inquiries" or "Filtered by Contacted: 4 inquiries found").

### [GAP-6] Focus Loss on Detached Modal Opener Element
* **WCAG Criteria**: 2.4.3 Focus Order (Level A)
* **Affected Files**: `frontend/src/App.tsx`
* **Description**: In `focusDetailOpener()`, if the row that opened the detail modal was removed from the DOM (e.g., deleted via Swagger/API, filtered out, or evacuated across pagination while the drawer was open), `opener.isConnected` evaluates to `false`. Focus then resets to `document.body`, forcing keyboard users to restart navigation from the top of the page.
* **Remediation**: Add a fallback focus target (e.g., the inquiry table container `#inquiry-queue` or toolbar filter) when `opener.isConnected` is `false`.

---

## 4. Acceptance Criteria

```gherkin
Scenario: Bypass blocks via keyboard navigation
  Given a keyboard-only user loads the dashboard page
  When the user presses Tab on the initial page view
  Then the "Skip to inquiry queue" link becomes visible and focused
  And pressing Enter moves focus directly to the inquiry queue section

Scenario: Screen reader identification of the data table
  Given a screen reader user navigates to the inquiry table
  When the table element receives focus or is read
  Then the table announces its caption "Incoming Course Inquiries Queue"
  And the "Created" column header announces its active sort state (ascending or descending) via aria-sort

Scenario: Programmatic announcement of pagination and filter changes
  Given the user changes the active page or status filter
  When the new inquiry list data finishes loading
  Then the polite live region announces the updated page index and matching inquiry count
  And the pagination component exposes aria-current="page" on the active page indicator

Scenario: Error state association on failed row status mutation
  Given an inquiry status update fails due to network or validation error
  When the mutation response completes
  Then the assertive live region announces the failure message
  And the specific row's status select element receives aria-invalid="true"

Scenario: Safe fallback focus management on modal closure
  Given the detail modal is open for an inquiry
  And the underlying row element is removed from the DOM while the modal is open
  When the user closes the modal via Escape or the Close button
  Then focus moves to a designated fallback container within the inquiry queue
  And focus does not reset to document.body
```

---

## 5. Technical Implementation Tasks

- [x] **Bypass Navigation (`GAP-1`)**:
  - [x] Add `<a href="#inquiry-queue" class="skip-link">Skip to inquiry queue</a>` in `backend/Pages/Shared/_Layout.cshtml`.
  - [x] Add `id="inquiry-queue"` with `tabindex="-1"` to pre-mount shell and persistent queue container in `App.tsx`.
  - [x] Implement `.skip-link` CSS in `styles.css` (`position: absolute; top: -100px;` transitioning to `top: 16px;` on `:focus-visible`).
- [x] **Table Semantics (`GAP-2`, `GAP-3`)**:
  - [x] Add `<caption className="sr-only">Incoming Course Inquiries Queue</caption>` to `InquiryTable.tsx`.
  - [x] Forward `sort` prop to `InquiryTable.tsx` and bind `aria-sort` to the Created date column header.
- [x] **Form Error State (`GAP-4`)**:
  - [x] Track failed update IDs in `App.tsx` and pass `failedIds: readonly number[]` to `InquiryTable.tsx`.
  - [x] Apply `aria-invalid={isFailed ? 'true' : undefined}` on the row `<select>`.
- [x] **Live Announcements & Pagination (`GAP-5`)**:
  - [x] Add `aria-current="page"` to the active page indicator in `Pagination.tsx`.
  - [x] Emit polite status announcements in `App.tsx` when page changes (reporting on-page item count) or filter changes settle.
- [x] **Focus Target Fallback (`GAP-6`)**:
  - [x] Update `focusDetailOpener()` in `App.tsx` to query an anchor element (e.g. `document.getElementById('status-filter')?.focus()`) if `opener.isConnected` is false.
- [x] **Verification**:
  - [x] Add unit and integration tests in `safetyAndA11y.test.tsx` verifying skip link target, `<caption>`, `aria-sort`, `aria-invalid`, and fallback focus.
  - [x] Run full test suite (`pnpm test` and `dotnet test`).
