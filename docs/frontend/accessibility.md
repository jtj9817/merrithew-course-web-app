# Frontend Accessibility Measures & Considerations

> **Technical Assessment Answer**  
> **Question**: *"What are 3 accessibility considerations you would apply to the frontend?"*  
> **Context**: Merrithew Course Inquiry Dashboard (`sub.webui`, `backend/Pages/`, `frontend/src/`)  
> **Standards Applied**: Accessibility for Ontarians with Disabilities Act (AODA, 2005) / IASR §14, W3C Web Content Accessibility Guidelines (WCAG) 2.1 Level AA, and WAI-ARIA 1.2.

---

## Executive Summary

For the Merrithew Course Inquiry Dashboard frontend, accessibility was designed as a core architectural requirement rather than a post-hoc cosmetic pass. Operating as a single deployable ASP.NET Core process hosting a Razor Pages shell and an interactive React + TypeScript triage island, the frontend applies three primary accessibility considerations:

1. **Semantic HTML, Data Table Architecture & Screen Reader Identification** (WCAG 1.3.1, 4.1.2)
2. **Keyboard Operability, Focus Management & Bypass Navigation** (WCAG 2.1.1, 2.1.2, 2.4.1, 2.4.3, 2.4.7)
3. **Perceivable Asynchronous Feedback, Error Identification & Non-Color Dependent Communication** (WCAG 1.4.1, 1.4.3, 1.4.10, 3.3.1, 4.1.3)

Every measure outlined below is fully implemented in the production codebase and verified by automated unit tests (`safetyAndA11y.test.tsx`), backend host integration tests (`FrontendHostTests.cs`), and end-to-end browser walkthroughs.

---

## 1. Semantic Structure, Data Table Architecture & Screen Reader Identification

Screen reader users navigating queue interfaces need predictable semantic boundaries, explicit context for tabular data, and unambiguous control names.

### 1.1 Accessible Table Semantics (`InquiryTable.tsx`)
- **Programmatic Table Caption (WCAG 1.3.1 - Level A)**:
  The inquiry queue is structured as a standard HTML `<table>` containing an explicit screen-reader caption:
  ```tsx
  <caption className="sr-only">Incoming Course Inquiries Queue</caption>
  ```
  Assistive technologies announcing the table immediately communicate its purpose (*"Table, Incoming Course Inquiries Queue, 6 columns, N rows"*) rather than presenting anonymous tabular data.
- **Explicit Column Headers**:
  Header cells utilize `<th scope="col">` (`th-details`, `th-name`, `th-course`, `th-status`, `th-created`) to establish formal cell relationships for data cells across rows.

### 1.2 Dynamic Sort Direction Announcement (`GAP-3`, WCAG 1.3.1 - Level A)
- Sorting is controlled via the toolbar sort dropdown. To ensure screen reader users traversing the table headers understand the sort state, the `Created` header exposes dynamic `aria-sort`:
  ```tsx
  <th
    scope="col"
    className="th-created"
    aria-sort={sort === 'createdDateAsc' ? 'ascending' : 'descending'}
  >
    Created
  </th>
  ```
  When staff toggles the sort order, the column header announces its updated sort direction (`ascending` or `descending`).

### 1.3 Disambiguated Control Names
- **Context-Rich Accessible Labels**:
  In a data queue with repeating action buttons, generic labels like "Details" or "Apply" create severe navigation ambiguity for screen reader users browsing controls out of context. Every row control incorporates visitor context:
  - Detail drawer opener: `aria-label="Details for {displayName(inquiry)}"`
  - Status select dropdown: `aria-label="Status for {displayName(inquiry)}"`
  - Status mutation button: `aria-label="Apply for {displayName(inquiry)}"`
- **Explicit Form Control Labelling**:
  Toolbar controls use explicit `<label htmlFor="...">` bindings (`Filter by status`, `Sort order`), avoiding unassociated placeholders.

---

## 2. Keyboard Operability, Focus Management & Bypass Navigation

All administrative workflows—triaging leads, inspecting details, filtering, sorting, and changing statuses—are fully navigable without a mouse.

### 2.1 Bypass Blocks / Skip Navigation Link (`GAP-1`, WCAG 2.4.1 - Level A)
- **Persistent Bypass Target**:
  Keyboard users should not be forced to tab through the brand logo, appbar status chip, colorblind toggle, developer simulation panels, and toolbar filters before reaching the inquiry records.
- **Implementation**:
  - `backend/Pages/Shared/_Layout.cshtml` renders a skip link as the very first focusable element inside `<body>`:
    ```html
    <a href="#inquiry-queue" class="skip-link">Skip to inquiry queue</a>
    ```
  - `frontend/src/App.tsx` renders a persistent `<section id="inquiry-queue" tabIndex={-1} aria-label="Inquiry queue">` wrapping all queue states (loading, error, empty, and ready table).
  - `backend/Pages/Dashboard.cshtml` applies `id="inquiry-queue"` with `tabindex="-1"` on the pre-mount loading shell, ensuring keyboard focus lands reliably even before JavaScript hydration finishes.
- **Styling (`styles.css`)**:
  The skip link is positioned off-screen (`top: -100px`) and slides into view (`top: 16px`) when focused, featuring high-contrast focus rings (`outline: 3px solid var(--md-sys-color-on-primary); outline-offset: 2px`).

### 2.2 Modal Drawer Focus Trap & Inert Background (`DetailPanel.tsx`, WCAG 2.1.2 & 2.4.3 - Level A)
- **Initial Focus Transfer**: Opening an inquiry drawer moves focus immediately into the dialog container (`containerRef.current?.focus()`).
- **Active Focus Trap**: An internal keyboard handler traps `Tab` and `Shift+Tab` cycling strictly within the dialog's interactive elements (close button, inquiry fields).
- **Background Inactivity**: When the drawer opens, the background `#dashboard-root` receives `inert={true}` and document body scrolling is locked (`overflow: hidden`), removing background elements from the accessibility tree.
- **Clean Escape Dismissal**: Pressing `Escape` closes the drawer immediately.

### 2.3 Resilient Focus Restoration (`GAP-6`, WCAG 2.4.3 - Level A)
- When the modal drawer closes, focus must return to the originating row opener button (`focusDetailOpener`).
- **Detached Opener Fallback**: If an inquiry record was deleted via API or evacuated across pagination while the drawer was open, `opener.isConnected` evaluates to `false`. Instead of allowing focus to reset to `document.body` (forcing users to start from the page top), `App.tsx` queries a graceful fallback:
  ```ts
  const fallback =
    document.getElementById('inquiry-queue') ??
    document.getElementById('status-filter');
  fallback?.focus();
  ```

### 2.4 High-Contrast Visible Focus Indicators (WCAG 2.4.7 - Level AA)
- Global `:focus-visible` CSS rules enforce a 3px solid outline with 2px offset (`var(--md-sys-color-primary)`), exceeding the 3:1 contrast requirement against adjacent surfaces.
- Table rows provide `:focus-within` background highlighting.
- High-contrast colorblind mode elevates the focus indicator to a prominent 3px outline with a 5px semi-transparent outer glow (`rgba(0, 51, 102, 0.25)`).

---

## 3. Perceivable State Feedback, Error Identification & Non-Color Dependent Communication

Single-page and island applications must proactively communicate state transitions, mutations, and error conditions to assistive technologies.

### 3.1 Dual ARIA Live Regions (`LiveRegions.tsx`, WCAG 4.1.3 - Level AA)
Visual updates occurring without page reloads are invisible to screen readers unless announced through live regions present in the initial DOM tree:
- **Polite Live Region (`role="status"`, `aria-live="polite"`)**:
  - **Status Updates**: Announces `"Status for {name} updated to {status}."` and `"Status saved."`.
  - **Filter Updates**: Announces `"Filtered by {filter}: {count} matching {inquiries} found"`.
  - **Pagination Transitions**: Announces on-page item count upon navigation, e.g., `"Page 2 (of 3) loaded, showing 20 matching inquiries"`.
  - **Colorblind Mode Toggle**: Announces `"Colorblind accessibility mode enabled/disabled"`.
- **Assertive Live Region (`role="alert"`, `aria-live="assertive"`)**:
  - Broadcasts critical failures immediately (network outages, 500 errors, 404 record missing / deleted concurrently).

### 3.2 Form Error State Association (`GAP-4`, WCAG 3.3.1 - Level A)
- When a status mutation fails, the assertive live region speaks the error message, and `App.tsx` records the failed inquiry ID in `failedIds`.
- The row's status `<select>` receives `aria-invalid="true"`:
  ```tsx
  <select
    aria-label={`Status for ${name}`}
    aria-invalid={isFailed ? 'true' : undefined}
    ...
  />
  ```
  Screen reader users returning to the control are explicitly informed that the field is in an error state. Successful saves automatically clear the invalid state.

### 3.3 Non-Color Dependent Status Indication & Colorblind Mode (WCAG 1.4.1 - Level A)
- **Text-First Status Presentation**: Status is never communicated solely by color. Every status badge renders the explicit status string (`New`, `Contacted`, `Pending`, `Registered`, `Closed`).
- **Shape and Icon Cues (`StatusBadge.tsx`)**:
  Under `Colorblind Mode` (`[data-colorblind="true"]`), distinct geometric border treatments and unique unicode iconography reinforce each workflow state:
  - `New`: Solid pill border + Star icon (★)
  - `Contacted`: Dashed pill border + Speech bubble icon (💬)
  - `Pending`: Dotted rectangular border + Clock icon (⏳)
  - `Registered`: Double solid border + Checkmark icon (✓)
  - `Closed`: Thin border with reduced contrast + Slash icon (⊘)
- **Status Legend Guide**: An accessible guide (`.status-legend`) renders below the queue, mapping each visual badge style to its descriptive label.

### 3.4 Responsive Reflow, Contrast & Reduced Motion (WCAG 1.4.3, 1.4.4, 1.4.10, 2.3.3)
- **Contrast Ratios**: All typography meets or exceeds WCAG AA 4.5:1 contrast against surface backgrounds (e.g., text `#1D1B20` on `#FDF8F5` achieves > 11:1).
- **Responsive Reflow down to 375px**: Layouts rely on relative units (`rem`, `%`, flexbox, CSS grid), supporting 200% zoom and 375px mobile viewports without horizontal scrolling or content clipping.
- **Reduced Motion**: System preferences (`@media (prefers-reduced-motion: reduce)`) suppress non-essential animations and instant-skip transitions.

---

## 4. Verification & Testing Matrix

The implementation is guarded by automated test suites and live browser verification:

| Test ID | Suite | Target / Verification |
|---|---|---|
| `IT-HOST-001` | xUnit (`FrontendHostTests.cs`) | Asserts presence of `<a href="#inquiry-queue" class="skip-link">` and `<section id="inquiry-queue">` in server-rendered Razor HTML. |
| `IT-UI-027` | Vitest (`safetyAndA11y.test.tsx`) | Verifies complete keyboard operability of all controls (`Tab`, `Space`, `Enter`, `Escape`). |
| `IT-UI-028` | Vitest (`safetyAndA11y.test.tsx`) | Verifies modal focus trap, `inert` background locking, and focus return on `Escape`. |
| `IT-UI-029` | Vitest (`safetyAndA11y.test.tsx`) | Verifies non-color dependent status badge text and colorblind shape/icon enhancements. |
| `IT-UI-031` | Vitest (`safetyAndA11y.test.tsx`) | Verifies `#inquiry-queue` bypass anchor, `<caption>`, and dynamic `aria-sort` on Created column header across ready and empty states. |
| `IT-UI-032` | Vitest (`safetyAndA11y.test.tsx`) | Verifies `aria-current="page"` on active pagination indicator and polite live announcements for filter and page transitions. |
| `IT-UI-033` | Vitest (`safetyAndA11y.test.tsx`) | Verifies `aria-invalid="true"` lifecycle on failed status select controls. |
| `IT-UI-034` | Vitest (`safetyAndA11y.test.tsx`) | Verifies fallback focus recovery to `#inquiry-queue` when modal opener button is detached from DOM. |
| `QA-BROWSER` | Playwright Dev QA | Live Chromium walkthrough verifying skip link execution, DOM activeElement focus shift, 375px mobile reflow, and zero console errors. |

---

## 5. Summary Mapping to Assessment Criteria

| Assessment Question Requirement | Codebase Implementation |
|---|---|
| **Consideration 1: Semantic Structure & Screen Reader Support** | Native `<table>` with `<caption class="sr-only">`, `aria-sort` on Created header, context-rich `aria-label`s (`"Status for {name}"`, `"Details for {name}"`), and explicit form `<label>` associations. |
| **Consideration 2: Keyboard Operability & Focus Management** | Skip navigation link to persistent queue section (`#inquiry-queue`), visible `:focus-visible` indicators (3:1 contrast), modal focus trap with `inert` background, and resilient opener focus return with fallback. |
| **Consideration 3: Feedback, Error Identification & Inclusivity** | Dual polite/assertive ARIA live regions, `aria-invalid="true"` on mutation failure, non-color dependent status badges with colorblind shape/icon modes, and responsive reflow down to 375px. |
