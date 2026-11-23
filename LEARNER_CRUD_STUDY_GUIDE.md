# School Management System: from requirements to working CRUD

This guide explains how to build and understand the application in this repository. It presents the intended construction sequence, the decisions behind the implementation, and the principles you can reuse in another project. Read it alongside the source and follow a record from the browser to the database and back.

Our current milestone is a Blazor application with persistent learners, sections, filtering, pagination, dashboard reads, validation, and update concurrency. Authentication, historical enrollment, teachers, and attendance are later milestones. Treat the current application as a learning and development foundation.

## How to study this guide

Use three passes:

1. **Follow the flow.** Read chapters 1–5 and identify the responsibility of each layer.
2. **Trace the implementation.** Read chapters 6–14 with the corresponding files open. Predict what each method will do before running it.
3. **Transfer the ideas.** Complete the exercises and design a small feature in a different domain using the same reasoning.

For each chapter, answer: What problem are we solving? Where does the solution belong? What guarantees it? What remains outside that guarantee?

## 1. Turn requirements into behaviors

We begin with tasks a school staff member needs to perform:

| Staff task | Required behavior |
| --- | --- |
| Register a learner | Validate information and save a permanent record |
| Find learners | Search, filter, order, and page saved records |
| Correct learner details | Load all editable values and save a consistent update |
| Organize a class | Manage sections and assign learners to the correct grade's section |
| Remove a record | Confirm the action and protect referenced data |
| Review school activity | Calculate totals and show records with known creation times |

A requirement becomes more useful when it has an observable example:

- A valid LRN contains exactly 12 digits; a leading zero remains part of the identifier.
- A Grade 1 learner can select an active Grade 1 section in the current school.
- Grade 1 “Rizal” and Grade 4 “Rizal” are different sections.
- Renaming a section preserves its learner assignments.
- A stale edit cannot silently replace a more recent update.

These examples guide the model, queries, forms, and verification. They also reveal decisions we still need to make, such as enrollment history across academic years.

**Apply elsewhere:** Before designing a shopping app, inventory system, or appointment system, write the user actions and the conditions under which each action should succeed. A screen is a way to perform an action; the action's rules should also make sense without that screen.

**Study checkpoint:** Describe Add Learner without mentioning a button, color, or framework.

## 2. Choose the application's execution model

The project uses:

| Technology | Its job in this application |
| --- | --- |
| ASP.NET Core / .NET 10 | Host the server, configure services, and process requests |
| Blazor Razor components | Render pages and handle UI events |
| Interactive Server rendering | Execute interactive component logic on the server through a browser connection |
| EF Core | Map entities to relational data and translate supported queries into SQL |
| Npgsql | Connect EF Core to PostgreSQL |
| Supabase PostgreSQL | Host the database |
| Tailwind CSS and npm | Generate the stylesheet used by Razor markup |
| A small JavaScript module | Open native modal dialogs and manage browser focus behavior |

The core path is:

```text
Browser interaction
    ↓
Razor component: editable state and rendering
    ↓
Application service: operation and business rules
    ↓
SchoolDbContext: EF mapping and change tracking
    ↓
Npgsql → PostgreSQL
    ↓
Result returned to the component → updated page
```

This is a single application with logical modules. `Modules/Academics` and `Modules/Enrollment` are folders in the same project, not independently deployed services. There is no separate REST API in the current implementation.

Supabase currently provides PostgreSQL hosting. The application connects directly from the server. Supabase Auth, Storage, and the browser Data API are not part of this milestone.

**Apply elsewhere:** Identify where code runs before deciding where secrets, validation, state, and permissions belong. A browser application calling an API has a different boundary from this server-executed Blazor application, even if both display similar forms.

## 3. Establish the project structure and page shell

[SchoolManagementSystem.csproj](SchoolManagementSystem.csproj) targets `net10.0`, enables nullable reference types and implicit usings, and references EF Core tooling and the PostgreSQL provider.

The repository separates responsibilities like this:

| Location | Responsibility | Start reading here |
| --- | --- | --- |
| Root configuration | Project dependencies, startup, logging, local URLs | [Program.cs](Program.cs) |
| `Components` | HTML document, routing, layouts, and pages | [App.razor](Components/App.razor) |
| `Modules/Academics/Domain` | School reference entities | [GradeLevel.cs](Modules/Academics/Domain/GradeLevel.cs) |
| `Modules/Academics/Application` | School name and active-year reads | [SchoolInfoService.cs](Modules/Academics/Application/SchoolInfoService.cs) |
| `Modules/Enrollment/Domain` | Learner and section entities | [Learner.cs](Modules/Enrollment/Domain/Learner.cs) |
| `Modules/Enrollment/Application` | Contracts, validation, queries, and writes | [LearnerService.cs](Modules/Enrollment/Application/LearnerService.cs) |
| `Data` | EF context and read-only data audit | [SchoolDbContext.cs](Data/SchoolDbContext.cs) |
| `Migrations` | Versioned schema changes and model snapshots | [current snapshot](Migrations/SchoolDbContextModelSnapshot.cs) |
| `Styles` / `wwwroot` | Stylesheet source, generated CSS, and browser assets | [tailwind.css source](Styles/tailwind.css) |
| `Tests` | Executable regression checks and an optional database audit | [Program.cs](Tests/Program.cs) |

`Components/App.razor` is the document shell. It references styles, the router, the dialog module, and the Blazor script. `Routes.razor` maps a URL to a component and supplies the default layout. `MainLayout.razor` provides the sidebar, school identity, top bar, and page body. `NavMenu.razor` links the three main pages.

The .NET package references currently use `10.*` version ranges. The npm dependencies have a committed lockfile. These are different dependency-resolution strategies; choose an explicit .NET version/lock strategy when reproducible releases become a requirement.

The main routes are `/`, `/learners`, and `/sections`. `/database-check` is a development-only database read. Counter and Weather remain template demonstrations. Error and NotFound handle unsuccessful requests.

The main pages declare `@rendermode InteractiveServer`. The route shell is not globally configured as interactive. With prerendering, a page can initialize for initial server rendering and initialize again when its interactive instance starts. Keep initialization reads repeatable; do not put record creation in `OnInitializedAsync`.

The layout reads the institution name and an active academic year whose date range includes today. It has a fallback if that information is unavailable. “School Office / Administration” describes the workspace; it does not identify an authenticated user.

**Apply elsewhere:** Folder names do not enforce architecture. Dependencies and responsibilities do. Start with enough structure to keep related code understandable; create separate projects or services when there is a concrete reason.

## 4. Model stored facts and relationships

A domain entity represents state the application persists. It should describe the business concept, not a particular table layout on a page.

| Entity | Stored concept |
| --- | --- |
| Institution | The school that owns records |
| Campus | A campus belonging to a school |
| AcademicYear | A school's year label, dates, and state |
| GradeLevel | A grade definition belonging to a school |
| Section | A grade's class grouping, adviser text, room, capacity, status, and version |
| Learner | Learner identity, current grade/section, guardian information, status, and timestamps |

### Identity differs from a display name

We use GUIDs for record identity. A section can change its name while retaining its ID. A learner stores `SectionId` to identify the relationship and has a nullable `Section` navigation property to let EF compose related queries.

Names are not reliable relationship keys. They can change, repeat, or differ only in formatting. A foreign key identifies a record; the related record supplies its current display name.

A GUID is useful for generating IDs in the application, but it is not a permission check. Someone who knows an ID must still be authorized before accessing that record in a secured application.

### Choose data types by meaning

- **LRN is a string:** it contains digits but represents an identifier. Arithmetic is meaningless and leading zeros matter.
- **BirthDate is DateOnly:** a birthday is a calendar date, not an instant with a time-zone offset.
- **CreatedAt/UpdatedAt are DateTimeOffset?:** they represent instants. Application writes use UTC. Null means a stored timestamp is unknown.
- **SectionId is Guid?:** a learner may have no confirmed section assignment.
- **Version is long:** it identifies the revision a form loaded.
- **Status is string:** allowed values are enforced by validation and database checks. Future enum or state-machine choices would require keeping contracts and storage aligned.

Nullable reference types help the compiler detect potential null use. They do not validate external input or existing database contents. Initializing a string to `string.Empty` does not make it nonempty or correct.

### Model the boundary we currently support

Learner currently stores its current grade, section, and status. This is not a history of every academic year's enrollment. AdviserName remains text because a Teacher entity is not implemented. Capacity is stored as a nonnegative number but is not yet an enforced enrollment limit.

**Apply elsewhere:** Ask whether a fact belongs to a person, an assignment, a transaction, or a historical event. An employee can have many job assignments over time; a customer can have many orders. Storing every changing fact directly on the person often loses history.

**Study checkpoint:** Explain why FirstName and MiddleName are stored separately even though the table displays one FullName.

## 5. Map the model and define database guarantees

[SchoolDbContext.cs](Data/SchoolDbContext.cs) exposes six DbSets and configures the `school` schema. A DbSet is an entry point for querying or tracking a kind of entity; it is not a fully loaded collection of all rows.

Our mapping defines:

| Database mechanism | Current use | Guarantee |
| --- | --- | --- |
| Primary key | Entity `Id` | Identifies a row uniquely |
| Foreign key | School, grade, and section references | Referenced rows must exist |
| Restricted deletion | Referenced academic records and sections | Prevents deleting a row that is still referenced |
| Composite unique index | `(InstitutionId, Lrn)` | No duplicate stored LRN inside one school |
| Composite unique index | `(InstitutionId, GradeLevelId, Name)` | No duplicate stored section name inside a school's grade |
| CHECK constraints | Learner and section value rules | Reject violating inserts and updates |
| Concurrency token | Learner and Section `Version` | Includes the original version when EF updates/deletes a tracked row |

The service additionally verifies that a selected grade belongs to the current school and that a selected section belongs to that school and grade. The current foreign keys are separate references. They do not form a composite database constraint guaranteeing that all three scopes agree. Direct SQL writers must preserve those relationships too.

Names and uniqueness comparisons currently follow the database's configured comparison behavior; the application does not promise case-insensitive section-name uniqueness.

`SectionName` remains as preserved text/a write-time snapshot while assignments are confirmed. Display queries prefer the current related section name when SectionId exists. Requests still contain a compatibility SectionName field, but new writes resolve membership from SectionId. The name field is not the authority for assigning a learner.

**Apply elsewhere:** Write down each rule and its enforcement boundary. A foreign key verifies existence; it does not automatically verify eligibility, permissions, capacity, or every relationship between multiple records.

## 6. Configure startup, dependencies, and secrets

[Program.cs](Program.cs) is the composition point: it reads configuration, registers services, builds the application, configures middleware, and maps endpoints.

The relevant registration is:

```csharp
builder.Services.AddDbContextFactory<SchoolDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<ILearnerService, LearnerService>();
builder.Services.AddScoped<ISectionService, SectionService>();
```

A Razor page requests a dependency with `@inject`. The dependency injection container constructs the implementation and supplies its dependencies. The page does not manually construct a service or connection.

### Interfaces and lifetimes

`ILearnerService` describes operations the page needs. `LearnerService` implements them using EF. The interface creates a clear contract and a place to substitute an implementation when there is a real testing or architectural need. It does not make the code automatically correct or faster.

A scoped service in an interactive server circuit can live for that circuit. Static prerendering uses a request scope. Transient services are created on resolution; singleton services are shared through the application. Choose a lifetime based on the state and dependencies the service holds.

Our services are stateless and use a context factory. Each operation does this:

```csharp
await using var db = await dbFactory.CreateDbContextAsync();
```

The context belongs to that operation and is disposed when it finishes. EF contexts track mutable state and are not safe for simultaneous operations. A context per operation avoids sharing that tracked state across overlapping UI events. Do not run parallel queries against the same context.

### Configuration stays on the server

The application requires `ConnectionStrings:SchoolDatabase`. Development can use the project's .NET User Secrets; deployment can supply `ConnectionStrings__SchoolDatabase` through the host's secret configuration.

Do not put credentials in Razor markup, browser JavaScript, Git, screenshots, or study notes. User Secrets keeps development values outside the repository; it is not an encrypted production secret vault. `.gitignore` prevents selected files from being tracked, but does not encrypt them or remove values already committed.

This project already has a UserSecretsId. In a new .NET project, initialize one with `dotnet user-secrets init`. From this project's root, a Bash prompt can capture a development connection string without typing its literal contents into a command saved in shell history:

```bash
read -rsp "Development PostgreSQL connection string: " school_db_connection
printf '\n'
dotnet user-secrets set "ConnectionStrings:SchoolDatabase" "$school_db_connection"
unset school_db_connection
```

Obtain the PostgreSQL host, port, database, username, password, and TLS settings from the intended database host. Keep certificate validation enabled. If the connection references a local certificate file, that path must exist on the machine running the server. A database password and a hosted service's browser API key are different credentials.

`appsettings.json` supplies logging and host configuration. `Properties/launchSettings.json` defines development URLs and environment names; it does not configure a production host.

**Apply elsewhere:** Keep configuration separate from code. Supply the same setting name with environment-specific values rather than editing source for every machine.

## 7. Evolve the database with migrations

Changing a C# property, generating a migration, and updating a database are three different operations.

```text
Entity/mapping change
    → generate a migration against the model snapshot
    → review the migration and generated SQL
    → apply it to the intended database
    → verify the application with that schema
```

The current migration sequence establishes academic tables, adds learners, adds sections, seeds the sample school and Grades 1–6, and adds assignment IDs, timestamps, and validation checks through `HardenEnrollmentRecords`.

A migration's `Up` describes moving forward. `Down` describes its rollback operations. The designer metadata and model snapshot help EF describe schema versions and calculate future differences. A snapshot is not a backup of actual records.

For an existing checkout:

```bash
dotnet tool restore
dotnet restore
dotnet build
# Inspect the changes before applying them:
dotnet ef migrations script --no-build
# Apply pending migrations to the configured development database:
dotnet ef database update --no-build
```

For a future model change, generate a new migration with a descriptive name, review its SQL, and apply it deliberately. Do not repeatedly add migrations just to start an unchanged application. Do not rewrite a migration already shared and applied to change the schema further; add another migration.

### Preserve stored records as the model grows

Our assignment migration matches existing text against institution + grade + section name. Unmatched text is preserved for explicit confirmation. It does not assign a learner to a different grade just because a section has the same name.

Timestamp columns allow null for records whose actual creation dates are unknown. Creating a column today does not establish when an older learner was created.

The migration adds value checks with PostgreSQL `NOT VALID`. This skips checking all old rows while installing the constraint; it still enforces the rule for new inserts and updates. The [read-only audit](Data/EnrollmentDataAudit.sql) identifies records requiring correction. After correcting stored values, `VALIDATE CONSTRAINT` can confirm that old rows meet each check too.

Rollback removes these checks before updating preserved section names, then removes the new columns. Dropping columns loses their stored contents; testing Down is not a substitute for backups and a rollback plan.

**Apply elsewhere:** A schema change has both a structure problem and an existing-data problem. Expanding with nullable fields, backfilling reliable matches, reviewing unmatched records, and tightening constraints later is useful in inventory, billing, and scheduling systems too.

## 8. Design contracts for each operation

Open [LearnerDtos.cs](Modules/Enrollment/Application/LearnerDtos.cs) and [ILearnerService.cs](Modules/Enrollment/Application/ILearnerService.cs).

| Contract | Reason it exists |
| --- | --- |
| `LearnerFilter` | Scope and requested search/filter/page values |
| `LearnerSummary` | Fields needed for a table row |
| `LearnerDetail` | Every stored value needed to populate an edit form, including separate names and Version |
| `CreateLearnerRequest` | Input to create a row; the service generates its primary key and initial version |
| `UpdateLearnerRequest` | Input to modify a row, including its ID and the form's version |
| `LearnerListResult` | Page items, matching count, school total, statistics, and effective paging values |
| `DashboardStats` | Dashboard values and recent activity |

A DTO is a data transfer object. It makes the boundary explicit and lets the service return only what an operation needs. A summary can format FullName for display; a detail must preserve the stored first, middle, and last names for editing.

Positional records give us concise contracts and value equality. Their generated properties support initialization; immutability is shallow, so a `List<T>` inside a record can still change. Form models use mutable classes because users edit one field at a time through binding.

We do not use one entity as every input and output. A create input should not let the form choose arbitrary server-managed timestamps, versions, or row identity. An edit output must contain fields that would otherwise be accidentally omitted when submitting an update.

`SectionSummary` supports cards. `SectionDetail` includes grade ID, capacity, and version for editing. `SectionOption` combines a stored option ID with a readable grade/section label.

**Apply elsewhere:** Design inputs around permitted changes and outputs around a caller's task. This reduces accidental overposting and makes ownership of each field easier to understand. Authorization is still required separately.

## 9. Compose efficient reads with LINQ

A read starts as an `IQueryable`, which describes work for a provider to translate. Building the expression is not the same as executing it. `ToListAsync`, `CountAsync`, and `FirstOrDefaultAsync` trigger database operations.

`GetLearnersAsync` follows this order:

1. Scope learners by InstitutionId.
2. Add the supplied search, grade, section, and status predicates.
3. Count all matching rows before paging.
4. Clamp page size to 1–100 and clamp the page to the available range.
5. Order by last name, first name, and ID.
6. Apply Skip/Take and project the selected rows into summaries.
7. Calculate school-wide statistics separately.

For a page size of 25, page 2 skips 25 rows and takes at most 25. If the school has 100 learners and 40 match a filter, the result can contain 25 page items, a FilteredCount of 40, and a TotalCount of 100. These are different measurements.

The ID tie-breaker gives rows with equal names a stable order. Pagination without a stable ordering can repeat or skip items unexpectedly across requests. Offset pagination is sufficient for this milestone; large or frequently changing datasets may need a different strategy.

### Filter entity columns before constructing output records

The detail query applies `Where(l => l.Id == id)` to mapped entity columns before constructing LearnerDetail. A concise example from the current service is:

```csharp
return await Summaries(
    db.Learners.AsNoTracking().Where(l => l.Id == id), db)
    .FirstOrDefaultAsync();
```

Keep database predicates expressed in terms the provider can translate. Avoid loading all rows just to filter them in memory. C# compilation verifies language and types; provider translation and actual database execution have additional requirements.

`AsNoTracking()` avoids tracking read-only entities. Projections select the required fields. `Join` supplies grade names, and the Section navigation lets EF select the current related section name. Using a navigation in a query does not inherently require loading an entire collection; EF can translate supported projections and counts into SQL.

Section learner counts use SectionId. They currently count all assigned learner statuses. The dashboard counts active sections, and enrolled learner statistics apply the Enrolled status explicitly. Always read the predicate before deciding what a number means.

Search currently uses substring matching and does not promise case-insensitive or tokenized full-name search.

**Apply elsewhere:** Decide which work belongs in SQL before materializing rows. Give each count a definition. A report labeled “customers” may mean all customers, matching customers, active customers, or only the current page.

## 10. Build dependent choices and meaningful form state

[Learners.razor](Components/Pages/Learners.razor) owns a mutable form model, filter values, loading flags, modal visibility, and user messages. Bound controls update component state. On submission, the page constructs a request DTO and calls the service.

The grade and section controls form a dependent choice:

```text
Select a grade
    → discard the previous section selection
    → display sections with the same GradeLevelId
    → submit the selected SectionId
    → service verifies school, grade, and section eligibility
```

If a learner is in Grade 1, an active Grade 4 section is not an eligible choice even when it has a familiar name. The page explains when a grade has no available sections. A blank list can be a valid business state and deserves guidance.

Dropdown choices refresh when an Add or Edit form opens. This accounts for section changes made elsewhere while the page remains open. Form-opening controls are guarded while those choices are loading.

New assignments use active sections. The current inactive section can be preserved when editing other learner details. An informational notice shows a previous section name when an assignment still needs confirmation; it is separate from validation errors and disappears when a section is selected.

The page stores the edit version separately and returns it unchanged in UpdateLearnerRequest. The form displays user-editable data, while the service controls the resulting new stored version.

**Apply elsewhere:** Country → city, category → product, and department → team follow the same dependency pattern. Refresh reference data at an intentional point, clear invalid child selections, and enforce the dependency again on the server.

## 11. Validate at three boundaries

Validation serves different purposes at each boundary:

| Boundary | Purpose | Example |
| --- | --- | --- |
| Form | Immediate guidance during a user task | Required field message; numeric input hint |
| Service | Enforce operation rules for every caller | Grade belongs to the school; valid learner status |
| Database | Protect stored state and resolve write races | Unique LRN index; section foreign key |

Current rules include exactly 12 ASCII digits for LRN; required first/last names up to 100 characters; optional middle name up to 100; guardian name/contact up to 200/50; birth date after DateOnly.MinValue and no later than today; the current allowed sex/status choices; and valid school/grade/section relationships.

Section rules include a required name up to 100 characters, adviser up to 200, room up to 100, nonnegative capacity, and Active/Inactive status. Text is trimmed before persistence and duplicate checks.

Form validation uses DataAnnotations and a date attribute. [EnrollmentValidation.cs](Modules/Enrollment/Application/EnrollmentValidation.cs) checks service inputs and related records. Database CHECK expressions enforce stored value rules; our text columns remain PostgreSQL text with explicit checks rather than configured varchar length limits.

`inputmode="numeric"` is a keyboard hint, not a guarantee. `maxlength` is a UI constraint, not a guarantee for every client. The service must validate even when no browser form is involved.

Names may legitimately contain spaces, accents, or punctuation. Guardian contact is length-limited but is not currently verified as a telephone number. Date-based rules use UTC dates in the application; projects with school-local day boundaries should define and apply a consistent time-zone policy.

**Apply elsewhere:** Distinguish format, meaning, eligibility, uniqueness, and permissions. Each requires its own check. A validly formatted product ID can still refer to a product the caller may not purchase.

## 12. Implement writes and define consistency

### Create a learner

Follow `OpenAddModal` → `HandleFormSubmit` → `CreateAsync`:

```text
Refresh choices and open an empty form
    → user fills fields
    → form validation succeeds
    → construct CreateLearnerRequest
    → validate values and related records
    → normalize and check LRN uniqueness
    → create Learner with generated ID, Version 1, and UTC timestamps
    → Add to the context
    → SaveChangesAsync
    → close form, refresh displayed data, show success
```

Adding an entity to a context starts tracking it as a new row. Saving sends the insert to the database. The generated ID and current defaults belong to the service, not the form.

The duplicate pre-check gives useful feedback. Two simultaneous requests can both pass it before either saves, so the unique index is still the final protection. The save helper translates known duplicate, relationship, and check-constraint violations into business messages.

### Update a learner or section

An update loads the entity into a fresh tracked context, validates the request, checks the submitted version, assigns allowed fields, increments the version, and saves.

Consider two forms that load version 3:

| Moment | Stored version | Result |
| --- | --- | --- |
| Both forms load | 3 | Both hold version 3 |
| First form saves | 4 | Service accepts version 3 and advances it |
| Second form submits version 3 | 4 | Service rejects the stale edit |

There is also a smaller interval between loading the row in the service and saving it. EF retains the original loaded version and includes it in the update predicate. If a competing save changes that version, EF can detect zero affected rows and report a concurrency conflict.

`Version` is a manually incremented long configured with `IsConcurrencyToken()`. It is not PostgreSQL xmin or an automatically advancing rowversion column. Other writers must follow the version protocol to preserve this guarantee.

A concurrency token prevents a lost update to the same row. It does not automatically protect a rule involving several different rows, such as a section's capacity or simultaneous section-grade changes and learner assignments. Such rules need a coordinated transaction, locking, or suitable database constraints when implemented.

### Delete a record

The page asks for confirmation and prevents repeat submission while deleting. The service loads the latest record and removes it. Section deletion first checks assignments, and the restricted foreign key also protects against references appearing before the delete completes.

Delete currently uses the version loaded in the delete operation; it does not compare a version from when the confirmation dialog first opened. Hard deletes also remove history. Version-aware confirmations, audit trails, and soft deletion are separate design choices for later milestones.

`SaveChangesAsync` normally groups the changes in that save atomically when the provider supports transactions. The earlier service reads and pre-checks are not all automatically part of the same transaction just because they are in one method. An exception likewise does not undo arbitrary external operations that happened before it.

**Apply elsewhere:** Ask what must succeed together. Reserving a seat, reducing inventory, and charging a payment are consistency problems with boundaries beyond an ordinary row update.

## 13. Manage asynchronous UI behavior

`async`/`await` lets the application wait for database or network work without blocking a thread for the whole wait. It does not make the operation instant or guarantee that multiple requests finish in the order they started.

The components manage:

- **Loading:** display a meaningful waiting state.
- **Form opening:** refresh choices once and guard overlapping opens.
- **Submitting:** prevent repeat requests while a save/delete is pending.
- **Search debounce:** wait 300 ms to reduce queries during rapid typing.
- **Response order:** use sequence counters so an older result does not replace a newer load.
- **Disposal:** cancel and dispose the debounce cancellation source when the component is removed.

The search cancellation token cancels the delay. It is not passed to the database operation, so an already running query can still finish. Sequence checks prevent stale display updates; they do not recover the cost of work already sent to the database.

Messages also have lifetimes. Successful reloads preserve the success message for the completed action. Expected business failures appear in the open form or confirmation dialog. Previous-section guidance is independent of an action failure.

**Apply elsewhere:** Any search box, address lookup, or live filter can receive out-of-order responses. Separate “stop waiting,” “stop underlying work,” and “ignore an outdated result”; they are different guarantees.

## 14. Render accessible UI and keep technical details in logs

Tailwind utility classes handle layout and responsive styling; shared CSS variables define the palette and typography. A bounded table with wrapping supports long stored values without letting one cell dominate the page.

The styles have different sources:

- `Styles/tailwind.css` configures Tailwind and which components it scans.
- `wwwroot/tailwind.css` is generated output.
- `wwwroot/app.css` contains shared variables and focused custom rules.
- Layout `.razor.css` files contain scoped component styles.

Rebuild Tailwind after changing utility classes. Rebuilding the .NET assembly does not run the CSS generation script.

Our modal shell is a native `<dialog>`. [dialogs.js](wwwroot/dialogs.js) opens newly rendered dialogs with `showModal()`, which enables browser modal behavior and contains keyboard focus. It routes Escape to the component's close handler, respects saving guards, and attempts to restore focus to a still-present triggering element. Labels identify form controls, and status/alert roles communicate feedback.

JavaScript manages browser behavior; C# owns whether the modal exists and what its submission does. Keep those responsibilities coordinated rather than allowing two independent sources of modal state.

`EnrollmentException` identifies business messages safe to show to a user. Unexpected failures go to the server through `ILogger`, while the page gives a concise message. A user needs the next action; a developer needs diagnostic context. Do not expose raw SQL, connection details, or stack traces in a page.

**Apply elsewhere:** Model empty, loading, invalid, successful, and unavailable states deliberately. They are part of the feature, not finishing touches after its main button works.

## 15. Derive dashboard data from stored facts

The dashboard queries enrolled learner count, active section count, and a small section overview. Recent activity uses known CreatedAt values, ordered by timestamp with ID as a tie-breaker. It describes record creation rather than claiming to be a complete audit log.

CreatedAt and UpdatedAt are recorded on application learner writes. Existing unknown creation dates remain null, and those records are excluded from “recent creation” activity. A random GUID is identity, not a reliable creation clock.

Attendance currently returns placeholder zeros with “Coming soon” context, and teachers show a placeholder. Those values are not attendance observations. Academic year and school name come from the academic information service, but management screens for those entities are still future work.

**Apply elsewhere:** Every metric needs a definition, source, time range, and scope. Distinguish “unknown” from a measured zero and “most recently created records” from a complete history of actions.

## 16. Build, run, and verify each boundary

Different tools change different things:

| Command/action | What it does | What it does not establish |
| --- | --- | --- |
| `dotnet restore` | Resolve .NET dependencies | Schema compatibility or correct business behavior |
| `dotnet build` | Compile C# and Razor | Successful execution of every SQL query |
| `npm ci` | Install dependencies from the lockfile | Generate CSS |
| `npm run css:build` | Generate Tailwind output | Recompile the server or migrate the database |
| `dotnet ef migrations script` | Generate schema SQL | Apply that SQL |
| `dotnet ef database update` | Apply pending migrations to the configured database | Correct every pre-existing data value |
| `dotnet run` | Build/start the application by default | Automatically apply this project's migrations |
| Refresh browser | Request/render the running application's page | Replace an already running server process with a newly compiled one |

For the existing project, a normal development sequence is:

```bash
# Project root
npm ci
dotnet restore
dotnet tool restore
npm run css:build
dotnet build
# Review the target and pending migration SQL before updating the development database.
dotnet ef database update --no-build
dotnet run
```

Use `dotnet run --launch-profile https` when testing the configured HTTPS profile. The default HTTP profile uses port 5094. Keep one intended process per port. Stop an existing process with Ctrl+C in its terminal before restarting it to load rebuilt server code.

For stylesheet work, `npm run css:watch` can run in a separate terminal. Server code, generated assets, configuration, and the database schema must remain compatible throughout development and deployment.

### What each verification level establishes

| Level | Current evidence or appropriate check |
| --- | --- |
| Compilation | Build the actual project |
| Input rules | Execute the console regression checks |
| Query translation | Run queries through the real Npgsql provider with intercepted execution |
| Mapping and migration consistency | Check for pending model changes and inspect migration operations |
| Live reads | Load dashboard, learners, sections, and database-check against the development database |
| Real persistence | Exercise create/update/delete and constraints in a disposable integration database |
| Browser interaction | Verify focus, keyboard operation, dependent dropdowns, dialogs, and responsive layout |
| Access control | Verify identity and permissions after implementing authentication/authorization |

Run the current checks with:

```bash
dotnet build --no-restore
npm run css:build
node --check wwwroot/dialogs.js
dotnet run --project Tests/SchoolManagementSystem.RegressionChecks.csproj
# Run after building the current model:
dotnet ef migrations has-pending-model-changes --no-build
git diff --check
```

The Tests project is an executable console runner, not an xUnit project. `dotnet run` executes its checks; `dotnet test` is not the documented entry point. `npm test` remains the package template's placeholder, not the application test runner.

The 25 checks cover learner validation, leading-zero identifiers, query translation, paging, section-ID counts, concurrency metadata, restricted relationships, and migration rollback ordering. Connection/command interception permits those checks without connecting to a database. They do not prove real writes, lock behavior, full transaction races, or browser interactivity.

The explicit read-only audit mode uses the configured development connection:

```bash
dotnet run --project Tests/SchoolManagementSystem.RegressionChecks.csproj -- --audit-database
```

It reports applied migrations, learner count, and issue counts from `Data/EnrollmentDataAudit.sql`. It neither changes records nor prints credentials or learner details. A development audit describes that database at the time it runs; it is not a permanent guarantee about later records.

**Apply elsewhere:** A passing check only supports what it exercised. Choose verification based on the boundary where behavior matters, and report what remains untested.

## 17. Understand security as its own milestone

The current pages use a hardcoded institution ID. Lists filter by it, and service writes verify related grade/section ownership. ID-based lookups and mutations do not yet require an authenticated school scope. GUIDs, DTOs, hidden controls, and database foreign keys do not solve this.

A secured version needs identity, roles or policies, a trusted school context, and checks on every relevant service operation. Direct PostgreSQL access does not automatically acquire a Supabase user's identity or enforce a user-specific RLS context. Database role permissions and application authorization must be designed intentionally.

The application already includes antiforgery middleware, HTTPS handling, and production exception handling. These each address a different concern and do not replace login or authorization. Development diagnostics and database-check also have a different role from public production pages.

**Apply elsewhere:** Authentication answers who the caller is. Authorization answers what that caller may do with this particular record. Test both reads and writes, including requests that use another record's ID.

## 18. Use the design in your next project

The reusable structure is a reasoning process, not a rule to copy every folder or library:

| In this project | Comparable problem elsewhere | Reusable idea |
| --- | --- | --- |
| InstitutionId | CompanyId, workspace, account | Define scope and enforce access at trusted boundaries |
| LearnerDetail | Product edit details, appointment details | Return every field needed for a safe edit |
| Grade → Section | Country → City, Category → Product | Dependent choices need UI and service enforcement |
| SectionId | CategoryId, RoomId, DepartmentId | Preserve relationships using stable identity |
| LRN | SKU, employee code, tracking identifier | Treat identifiers according to meaning, including leading zeros |
| Composite uniqueness | SKU per company, room name per site | Define the scope of uniqueness explicitly |
| Version | Inventory revision, document revision | Detect stale writes to the same record |
| Context per operation | Unit of work/request session in another ORM | Give tracked mutable state a clear lifetime |
| Server-side paging | Orders, audit events, stock movements | Bound data transfer and order consistently |
| Migration + audit | Any evolving stored schema | Plan for existing data, not just empty databases |
| Business exception + logs | Any user-facing operation | Separate actionable feedback from diagnostic context |
| Query/persistence/browser checks | Any layered system | Verify the boundary responsible for the guarantee |

A small one-user tool may need fewer layers. A public multi-company system needs substantially more access control and operational design. Begin with actual requirements and explain the trade-off behind each abstraction you add.

### A repeatable feature workflow

1. Describe the user action and examples of valid/invalid outcomes.
2. Identify stored facts, ownership, relationships, and history requirements.
3. Define create/update/read contracts and server-managed fields.
4. Decide which rules belong in UI feedback, service validation, and database constraints.
5. Plan schema changes and existing-data handling.
6. Implement service queries and writes with clear context and transaction boundaries.
7. Build UI state, dependent choices, loading, feedback, and accessibility.
8. Verify input rules, provider behavior, persistence, browser behavior, and permissions as appropriate.
9. Document current behavior, limits, setup, and the reason for decisions.
10. Review changes before committing and deploying compatible code, assets, configuration, and schema.

## 19. Practice by tracing and extending

### Trace without changing code

- Follow Add Learner from `@onclick` to the insert and back to the success message.
- Follow Edit from the ID lookup to separate name fields and the submitted version.
- Follow a grade change and explain why the selected section is cleared.
- Follow a section card click to the paged learner query filtered by SectionId.
- Find the two defenses against duplicate LRNs and explain the race between them.
- Identify which counts are filtered, school-wide, active, or enrolled.
- Explain which operations do and do not use the form's original version.

### Predict the outcome

Before trying each scenario, write what should happen and which boundary enforces it:

1. Submit an LRN with letters, 11 digits, or a leading zero with 12 digits total.
2. Select Grade 1 when only Grade 4 sections exist.
3. Create a section in another tab, then open a learner form.
4. Rename an assigned section.
5. Edit only a section's room and confirm its capacity is retained.
6. Open two learner forms for the same version and submit both.
7. Attempt to delete an assigned section.
8. Apply a schema expansion to a database that already contains records.
9. Search rapidly while earlier requests are still running.
10. Ask for page 100 when a filter has only one page of results.

Use a disposable development database for exercises that modify data. Preserve actual school records and use clearly fictional test information.

### Transfer exercise: inventory

Design Product, Category, and Warehouse entities. Choose a business identifier, its uniqueness scope, a dependent dropdown, summary/detail DTOs, and a versioned update. Then explain why preventing two edits to one Product row does not automatically prevent overselling across multiple reservation rows.

### Transfer exercise: appointments

Design Patient, Clinician, and Appointment entities. Decide whether dates represent local calendar dates or global instants. Explain why a client-side “available slot” dropdown cannot guarantee that two users do not book the same slot simultaneously. Propose the database/transaction boundary that would enforce the rule.

### Extend this project deliberately

The next substantial milestones are authentication and authorization; academic setup screens; teacher/adviser relationships; enrollment history by academic year; attendance and class records; coordinated capacity enforcement; audit trails/soft deletion; and real persistence/browser automation tests.

For one chosen feature, write its requirements, contracts, migration plan, consistency boundary, UI states, and verification plan before implementing it. Do not count an entity or disabled button as a complete feature.

## 20. Recall checklist and study schedule

You understand the foundation when you can explain these without reading an implementation:

- [ ] Where C#, SQL, CSS generation, and JavaScript execute.
- [ ] How the router, layout, page, service, and context connect.
- [ ] Why entity, DTO, and form model have different jobs.
- [ ] Why an identifier can be numeric-looking text.
- [ ] What primary keys, foreign keys, unique indexes, and checks each guarantee.
- [ ] Why service pre-checks need database protection during simultaneous writes.
- [ ] When an IQueryable becomes a database operation.
- [ ] Why filtering, stable ordering, and paging happen before materializing rows.
- [ ] How context lifetime differs from component/circuit lifetime.
- [ ] What async, cancellation, debounce, and response sequencing each accomplish.
- [ ] How submitted and original database versions prevent stale updates.
- [ ] Which multi-record rules need more than a version token.
- [ ] How migrations, existing data, backups, and application releases relate.
- [ ] Why a successful build, live read, and end-to-end interaction are different evidence.
- [ ] Why school scope and authorization are necessary even with GUIDs.
- [ ] Which features are complete at this milestone and which remain future work.

Suggested sessions:

| Session | Focus | Produce something you can explain |
| --- | --- | --- |
| 1 | Chapters 1–4 | A responsibility map and entity relationships |
| 2 | Chapters 5–7 | A rule/enforcement table and schema rollout plan |
| 3 | Chapters 8–10 | A traced read query and dependent form flow |
| 4 | Chapters 11–12 | Create/update/delete explanations with concurrency examples |
| 5 | Chapters 13–16 | UI state transitions and a verification plan |
| 6 | Chapters 17–19 | Security requirements and one different-domain design |
| 7 | Recall and implementation | A small feature built from your own written plan |

Finish each session by explaining the relevant flow aloud and finding the exact files that implement it. The transferable skill is connecting requirements, state, boundaries, guarantees, and evidence—not memorizing method names.
