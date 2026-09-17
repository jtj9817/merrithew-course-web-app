# Option 1: Course Inquiry Dashboard

**Web Developer (.NET) Technical Assessment**

## Scenario

Merrithew needs a small internal web application to manage course registration inquiries submitted through the website. A visitor submits an inquiry about a course. Staff should be able to view submitted inquiries, filter them by status, and update the inquiry status as it progresses. The system should also demonstrate how the inquiry could be sent to an external business system such as a CRM.

## Part 1: Backend API - .NET / C#

Build an ASP.NET Core Web API for managing course inquiries.

**Inquiry fields**

- Id
- FirstName
- LastName
- Email
- Phone
- CourseName
- PreferredLocation
- Message
- Status
- CreatedDate
- UpdatedDate

Suggested statuses: New, Contacted, Pending, Registered, Closed.

**Required endpoints**

| Endpoint                        | Purpose                                                        |
| ------------------------------- | -------------------------------------------------------------- |
| `POST /api/inquiries`           | Create a new inquiry                                           |
| `GET /api/inquiries`            | Return all inquiries; include optional status filtering        |
| `GET /api/inquiries/{id}`       | Return one inquiry by ID                                       |
| `PUT /api/inquiries/{id}/status`| Update inquiry status                                          |
| `DELETE /api/inquiries/{id}`    | Delete the inquiry or mark it as Closed/Archived               |

> **Note:** You may implement delete as either a hard delete or a soft delete/archive. Please explain your choice in README.

**Backend requirements**

- Create, view, filter, update status, and delete/archive inquiries
- Validate required fields and email format
- Default status should be New
- Set CreatedDate and UpdatedDate automatically
- Return appropriate error responses for invalid input and missing records

## Part 2: Database / SQL

SQL Server is preferred, but SQLite, LocalDB, or in-memory storage is acceptable if setup is clearly documented.

Regardless of the database used, include a `database.sql` file with:

- Table creation script for `CourseInquiries`
- At least 5 sample records
- Query to return inquiries created in the last 7 days
- Query to count inquiries by status
- Query to find duplicate inquiries by email address

## Part 3: Frontend

Create a simple frontend interface using HTML/CSS/JavaScript, Razor Pages, React, TypeScript, or jQuery. The frontend does not need to be visually polished. Functionality and clarity are more important than design.

- View a list of inquiries
- Filter inquiries by status
- View basic inquiry details
- Update the status of an inquiry
- See clear success or error messages

A simple frontend is acceptable. You may use Swagger/Postman for create/update operations if you clearly document the API and provide at least a basic list/filter interface.

## Part 4: Simulated CRM Integration

Create a simulated integration showing how a new inquiry could be sent to an external CRM. You do not need to connect to a real CRM or use external API keys.

- Accept inquiry data
- Log or store that a CRM sync was attempted
- Handling success and failure cases
- Avoid exposing sensitive data in logs

**Bonus:** demonstrate retry logic or structured error handling.

## Part 5: Written Questions

### Troubleshooting

A staff member reports that some course inquiries submitted through the website do not appear in the admin list. Describe how you would investigate this issue. Include:

- What logs or systems you would check
- How you would determine whether the issue is frontend, backend, database, or integration related
- What SQL queries you might run
- How you would communicate progress to non-technical stakeholders
- How you would prevent the issue from happening again

### Security

What steps would you take to ensure this inquiry form and API are secure? Mention input validation, SQL injection prevention, authentication/authorization, error handling, logging, and sensitive data handling.

### Accessibility

What are 3 accessibility considerations you would apply to the frontend?

### Code quality

Briefly explain how you organized your solution and why.

## Submission Requirements

Your submission should include:

1. Source code for the project
2. A `README.md` with setup/run instructions, assumptions, and what you would improve with more time
3. A `database.sql` file
4. Written answers to the troubleshooting, security, accessibility, and code quality questions (concise bullet points are acceptable; focus on your reasoning rather than length)
5. At least one meaningful automated test covering a business rule, validation rule, or service method. Additional tests are optional.

Submit your work as a Git repository link, ZIP file, or shared folder link. Use of documentation and standard online references is allowed. If you use AI tools to assist with the assessment, please briefly disclose how they were used.

The `README.md` should include setup instructions, run instructions, assumptions, what you would improve with more time, and any AI tools used, if applicable.

## Suggested Submission Structure

```text
assessment-submission/
|-- backend/
| |-- Controllers/
| |-- Models/
| |-- Services/
| |-- ...
|-- frontend/
| |-- index.html
| |-- styles.css
| |-- app.js
|-- database/
| |-- database.sql
|-- tests/
| |-- ...
|-- README.md
|-- written-answers.md
```

## Optional Bonus Tasks

These are optional. Please only attempt them if you have time.

Optional examples: additional unit/integration tests; Swagger/OpenAPI documentation; pagination or sorting; basic authentication/authorization note or implementation; structured logging; brief CMS or CRM integration note.
