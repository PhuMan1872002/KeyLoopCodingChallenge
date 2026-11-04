# Keyloop Inventory Dashboard

**Keyloop Technical Assessment — Scenario B: The Intelligent Inventory Dashboard**
Domain: **Supply** · Implemented layer: **Frontend** (backend mocked)

> 🎥 **Video walkthrough:** _<!-- TODO: paste the unlisted YouTube link here before submitting -->_
> 📐 **System design document:** [`docs/system-design.md`](docs/system-design.md)
> 🤖 **AI collaboration narrative:** [below](#ai-collaboration-narrative)

[![CI](https://github.com/PhuMan1872002/KeyLoopCodingChallenge/actions/workflows/ci.yml/badge.svg)](https://github.com/PhuMan1872002/KeyLoopCodingChallenge/actions/workflows/ci.yml)

A dashboard for a dealership manager: see what is in stock, find the vehicles that have
been sitting too long, and record what is going to be done about them.

---

## Which layer this implements

The brief asks for one layer to be built fully and the other mocked. **This submission is
the frontend.** The backend is mocked by a JSON-file-backed adapter sitting behind
`IInventoryClient`, a port declared in the domain project.

Blazor Server runs UI logic **on the server** over a SignalR circuit. That is a deliberate
choice rather than an incidental one — Scenario B asks for a *real-time* overview, and an
always-open circuit makes server-pushed updates native rather than bolted on. The design
document is explicit about what that costs at scale (stateful circuits, session affinity,
reconnection) in [Section 7](docs/system-design.md#7-scalability-performance-reliability).

## What it does

| Requirement from the brief | Where to see it |
|---|---|
| **Inventory visualisation** — filterable list of vehicles | Virtualised list over a 150-vehicle lot across two branches; branch selector, search over stock number / VIN / model, filters on make, model and age bucket, three sort orders, an age-distribution chart whose columns are themselves filters, and a CSV export that re-applies the same filter |
| **Aging stock identification** — vehicles in stock >90 days | Four age bands (Fresh / Due soon / Aging / Critical), flagged per row, emphasised in the grid, counted and valued in the KPI strip, and filterable on their own |
| **Actionable insights** — log and persist a status | "Log action" writes a status, an optional proposed price, a review date, a note and a UTC timestamp; the history is append-only, shown in the panel, and survives a restart |

Beyond the acceptance criteria:

- **Aging stock with no plan** is counted separately from aging stock. The second number
  is the size of the problem; the first is the part of it nobody has dealt with yet, and
  it is the only figure on the screen a manager can act on directly.
- **An aging review board** at `/aging-review` groups aging vehicles by the decision
  logged against them and highlights review dates that have passed. Logging a decision
  that nobody revisits is paperwork; this is the screen that closes the loop.
- **Real time.** An action logged in one browser window appears in every other open
  window with no refresh.
- **The review board is a working kanban board, not a picture of one.** A card is
  draggable; dropping it on another status column pre-selects that status and opens the
  same confirmation panel the buttons do — a drag is a shortcut into logging a decision,
  not a way to silently record one with no note or price attached. Built with Blazor's own
  `ondragstart`/`ondragover`/`ondrop` event bindings, no JavaScript.
- **The dashboard's "View all N in list" button goes to the review board**, carrying the
  selected branch with it. It used to filter the dashboard's own table in place instead,
  which left the manager looking at an unchanged screen whenever that table was below the
  fold — the board is what actually groups those vehicles by decision, which a filtered
  list cannot show.
- **The left rail collapses.** The inventory table already scrolls horizontally in its own
  right (`min-width: 1040px`); the toggle gives back the rail's width when the table needs
  it instead.

---

## Running it

**Prerequisites:** .NET SDK 9.0 or later. Nothing else — no database, no containers,
no external services.

```bash
git clone https://github.com/PhuMan1872002/KeyLoopCodingChallenge.git
cd KeyLoopCodingChallenge

dotnet restore
dotnet test                                   # 96 tests
dotnet run --project src/Keyloop.Inventory.Web
```

Then open the URL printed in the console (by default <http://localhost:5210>).

On first load the application generates a deterministic inventory of 150 vehicles
across two branches into `src/Keyloop.Inventory.Web/App_Data/inventory.json`. The dataset
is seeded from a fixed value, so every machine sees the same numbers, and roughly two in
five aging vehicles start with a decision already logged so that the review board is not
empty on a first run.

**Delete that file to reset the data**, including any actions logged against vehicles.
The path resolves against the host content root, so if the application is launched some
other way the store follows; the resolved path is printed to the console on first load
(`Seeded 150 vehicles into …`) rather than left to be guessed at. A store written by an
older build is regenerated rather than treated as a fatal error.

> Note: the dashboard disables prerendering, so the first HTML response is an empty shell
> and the data is not loaded until a browser opens a SignalR circuit. A `curl` returning
> 200 is not evidence that the application works.

### Seeing the real-time behaviour

Open the dashboard in **two browser windows side by side**, log an action in one, and
watch the other update without a refresh.

### Seeing the telemetry

Traces and metrics are exported to the console. Filter the grid or log an action, and
spans named `inventory.query` and `inventory.action.append` appear in the terminal
running the app, carrying the filter shape and result count. In production the one line
that changes is the exporter — `AddOtlpExporter()` pointed at a collector.

---

## Structure

Four layers, dependencies pointing one way only.

```
src/Keyloop.Inventory.Domain/          Vehicle, VehicleAction, AgingPolicy, enums. Pure C#, references nothing.
src/Keyloop.Inventory.Application/     InventoryQueryService, VehicleFilter, statistics,
                                       the CSV export, and the interfaces the outside
                                       world has to answer - IInventoryClient first of all.
src/Keyloop.Inventory.Infrastructure/  The classes that answer those interfaces:
                                       JsonInventoryClient, the seed data, the live-update
                                       service, telemetry.
src/Keyloop.Inventory.Web/             Blazor Server UI (/ overview, /aging-review board),
                                       hand-written markup and CSS to the design, view models.
src/Keyloop.Inventory.Tests/           xUnit for the rules and the adapter; bUnit for components.
docs/                                  The system design document.
```

`Domain` references no framework: no Blazor, no file I/O, no configuration. Every interface
is declared in `Application` and implemented in `Infrastructure`, so the dependency at each
boundary points inward. Outside one line of `Program.cs`, no file in `Web` names a type
from `Infrastructure`. That is the seam where a real Inventory service replaces the
JSON file, and it is why doing so touches one class.

It applies the Dependency Rule, and leaves out the ceremony that usually travels with the
"Clean Architecture" label: no mediator, no command/query handler per action, no DTO mapping
tier, no repository per aggregate. At two screens and one business rule those cost real time
and return nothing.

There is also only **one entity**: `Vehicle`. Everything else in `Domain` is either a
closed vocabulary (`ActionStatus`, `AgeBand`, `AgeBucket`), a query shape (`VehicleFilter`,
which describes a search, not a vehicle), an aggregate over the whole lot
(`InventoryStatistics`, `MakeExposure`) or the append-only child value (`VehicleAction`).
A `VehicleSummary` type — `Vehicle` plus the day-dependent facts about it — existed for
part of this project's history and was deleted once a method was enough:
`Vehicle.DaysInStock(DateOnly today)` keeps "today" a caller-supplied parameter, same as
`AgingPolicy`, without a second "vehicle" type for the UI to keep in sync with the first.

## Tests

```bash
dotnet test
```

96 tests, chosen for the places where this application can actually be wrong rather than
for a coverage figure:

| What is tested | Why it matters |
|---|---|
| Aging boundary at 89 / 90 / 91 days | The brief says ">90 days". Exactly 90 is **not** aging, and that reading is pinned rather than implied. |
| All four band boundaries: 75/76, 90/91, 150/151 | Three places to be off by one instead of one. |
| Critical stock is still aging stock | The failure mode is silent: the oldest vehicles on the lot dropping out of the aging view while every screen still looks right. |
| Filter composition and empty results | A filter that silently returns everything is worse than one that errors. |
| Aging *with no plan* is distinguished from aging | The two numbers drive different parts of the screen; conflating them would make the worklist meaningless. |
| Current status = most recent action | Proven with actions logged out of chronological order. |
| An action survives a restart | Exercises the real file, because "persisted" cannot be shown with an in-memory fake. |
| 20 concurrent appends all land | One Blazor Server process serves many circuits; without the write lock, actions are lost. |
| Statistics over an empty inventory | The average-age tile divides by the vehicle count. |
| The seeded dataset's shape | Determinism, aging as a minority, every band populated, no decision logged against a vehicle inside the threshold. The demo data is what a reviewer sees, so it is part of the deliverable. |
| Aging flag and band chip render (bUnit) | The visual signal the whole scenario turns on. |
| The dashboard page renders end to end (bUnit) | Headline figures, aging flags in the table, the chart working as a filter, and the decision panel opened from a row and saved — with the unattended count dropping to zero in the same render. Without these the suite would stay green through any layout at all. |
| Headline figures follow the branch selection | A selector that left the KPI strip unchanged would be worse than no selector. |
| Sort order is applied after filtering | The export endpoint and the on-screen list must agree about what "oldest first" means. |
| A store from an older build is regenerated | Otherwise a reviewer with a stale file meets a crash that reads as "the app is broken". |
| A re-render on the same vehicle does not clear the decision draft (bUnit) | Caught by review: another circuit's push re-renders the host page while the panel is open, which used to wipe a note mid-sentence. |
| A broadcast failure in one subscriber does not stop the rest | Caught by review: the original catch only handled a closed circuit's own exception, aborting the fan-out for every dashboard still waiting behind it. |
| A toast clears itself, and a second save keeps the newer message | Caught by review: the toast was set but nothing ever cleared it. |
| "View all" navigates to the review board (bUnit) | Replaces the quieter failure of filtering an off-screen table and leaving the manager looking at an apparently unchanged page. |
| Dragging a card onto a status column pre-selects it (bUnit) | The behaviour the board's kanban layout implies but did not previously have. |

Five of these exist **because a code review found the defect first** — see
[Defects found in generated code](#defects-found-in-generated-code) below.

`AgingPolicy` never reads the system clock — "today" is a parameter. That is what makes
these tests deterministic, and it is the reason the rule is testable at all.

---

## AI Collaboration Narrative

How AI shaped the **design** is section 9 of the
[design document](docs/system-design.md#9-genai-in-the-design-phase). This is the rest of
it: what it produced, how it was checked, and where it was wrong.

### Strategy: my idea first, AI as the check on it

I did not start by asking what to build. I decided, then used AI to try to break the
decision. The scenario was mine, chosen against a test I set — *does the value of this
scenario live in the layer I am implementing?* — and handing that test to the AI did not
change the answer; it sharpened the argument, pointing at the wording that settles
Scenario D (*"the **backend** must make parallel requests"*). The result was a choice I
could defend rather than one I merely preferred.

I also used AI to check the ground I was standing on rather than guessing at it: that
Keyloop's Fusion platform is organised into the same four domains the brief names, that its
public developer portal exposes products matching these scenarios, and that OpenTelemetry is
the observability vocabulary used there. That shaped the design document's language and its
production-evolution section.

### Verification: a failing test before generated code

The core business rule was developed test-first. The boundary tests at 89, 90 and 91 days
were written by hand and left **failing** — `AgingPolicy` did not exist yet — and only then
was the rule implemented to meet them. That order gave me an objective pass/fail signal for
generated code instead of a subjective read of whether it looked right.

This matters more with AI than without it. Generated code is fluent by default, and fluency
is exactly the quality that makes a wrong implementation hard to spot by reading.

### Where I overrode the AI

Four decisions went against the first suggestion, each recorded in the log with reasoning:

1. **SQLite was rejected for persistence.** It was proposed as the more professional option.
   Checked against the brief, the frontend path names only data-level mocks — building a
   database would mean building the layer I was asked to mock, and would imply this
   application owns inventory data it does not own.
2. **The "Clean Architecture" label was refused, then earned.** The early three-project
   structure was offered that label and I turned it down: there was no use-case layer and
   the query service sat next to the file adapter, so the term would not have survived
   questioning. The structure was later split into four layers, and the design document
   was corrected again — this time to claim the Dependency Rule while still naming what is
   deliberately absent. Both times the rule was the same: the document describes the code,
   not the other way round.
3. **A Blazor-specific defect class was anticipated, not discovered later.** Generated Blazor
   code routinely subscribes to a service without disposing the subscription, which pins the
   circuit after the user closes the tab, and routinely reaches for `DateTime.Now` inside a
   business rule, which makes it untestable and timezone-dependent. Both were designed out
   up front: `AgingPolicy` takes `today` as a parameter, and the dashboard implements
   `IAsyncDisposable`.
4. **A UI mock-up was triaged rather than implemented.** I designed the screens and the
   user journey separately, and the mock-up was better than the UI that existed — which
   made the temptation to build all of it the actual risk. Four parts were refused
   because this application could not deliver them honestly: an owner on each decision
   when the design explicitly does not own user identity, a "synced 2 minutes ago" label
   on an architecture that pushes changes live, and week-on-week trend tiles with no time
   series behind them. A fourth, the branch selector, was refused on the same grounds and
   then **built** once the mock inventory was generated across two branches — the
   objection was to a control that lies about its data, not to the control.

### What verification cost, and what it caught

Changing the age bands invalidated assumptions already sitting in tests, so the three new
boundaries were pinned before any UI was touched.

Reseeding to check them produced the most useful mistake of the project. The dashboard
kept showing the old distribution, I found a store file in the build output directory,
and I concluded the README's reset instructions were wrong — and "corrected" them. They
were not wrong. The file in `bin` was a stale artefact of an earlier run launched a
different way, and the store does resolve where the README said. One run with the logging
already in the adapter settled it in a second. The lesson I took is not about file paths:
a confident explanation that fits the symptom is the easiest thing in the world to
generate, and the check that would have falsified it cost nothing.

The seeded dataset itself is now test-covered, because it is what a reviewer actually
sees and reading the generated JSON by hand is the kind of check that stops happening
after the second time.

### Defects found in generated code

None of these failed a build. Each is the kind of defect that ships quietly.

- **Silent JSON drift.** `Vehicle.CurrentAction` and `VehicleAction.StatusLabel` are
  computed. `System.Text.Json` writes get-only properties but ignores them on the way back
  in, so every save would have persisted two derived fields nothing ever read. Caught by
  reasoning about the serialisation boundary, not by a test — this defect produces no
  error, only an untidy store a reviewer would eventually open.
- **CSV injection in the export.** There is no SQL anywhere in this project, so there is
  no SQL-injection surface — but the analogous sink is real: a free-text note lands in a
  spreadsheet that evaluates formulas. Fields starting `=`, `+`, `-`, `@` or a control
  character are now escaped.
- **A crash hiding in a short VIN.** `Selected.Vin[^8..]` throws for any VIN under eight
  characters. Every seeded VIN is seventeen, so it never fires in the demo — found while
  *building a test fixture*, which is itself a way of probing what the implementation was
  quietly assuming.
- **One duplication hiding three bugs.** A delegated code review found the save →
  broadcast → toast sequence copied between two pages, which is how the same defects came
  to exist twice: a toast that was never cleared, and a broadcast loop that caught only
  one exception type and so dropped the notification for every subscriber behind a failing
  one. Extracted into a single `ActionLogWorkflow`. A fourth finding — the decision panel
  resetting its draft on *every* re-render, wiping a note mid-sentence when another user's
  change arrived — was fixed in place.

I verified each finding against the file before changing anything, rather than applying
them on the reviewer's word.

### What I own

Every test in this repository is one I can explain in a sentence. Anything I could not
explain was removed rather than kept for the count.

---

## Assumptions

Recorded in full in [Section 2](docs/system-design.md#2-assumptions) of the design document.
The ones that would change behaviour if read differently:

- Aging means **strictly more than** 90 days, so a vehicle at exactly 90 days is not aging.
- Two dealership branches, selectable in the header. `DealershipId` is on the entity and
  every figure on the screen follows the selection, so partitioning across more branches
  is a query change rather than a migration.
- No authentication: in production that belongs to the platform's gateway, not this
  application.
- Action history is append-only, and "current status" is the most recent entry — which
  satisfies both readings of *"log and persist a status"* without choosing between them.
