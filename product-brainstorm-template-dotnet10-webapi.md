# Prompt Template: Product Brainstorming — .NET 10 Web API

Copy everything below the line and paste it into Claude Code.

**How to reuse this template:** keep the structure and replace the content in sections 1 to 9 for your next product. Leave section 10 (how to run the session) and section 11 (outputs) as they are.

---

Use the `product-brainstorm` skill for the product below. Brainstorm with me first. Do not write application code until I approve the plan.

## 1. Product summary

| Item | Value |
|---|---|
| Product name | Orders API (working name) |
| Type | Back-end REST API (no UI in this phase) |
| Purpose | Manage products, customers, and orders, with user accounts and roles |
| Users | Admins (staff) and Customers |
| Stage | Proof of concept that should follow production-grade practices |
| Tracker | `<Linear or Azure DevOps Boards>` |

## 2. Technology stack (fixed decisions)

These decisions are already made. Do not change them. You may suggest risks, trade-offs, or better options in an "Advice" section, but the plan must use this stack unless I approve a change.

| Area | Decision |
|---|---|
| Runtime | .NET 10 Web API |
| Endpoints | FastEndpoints |
| Architecture | Clean Architecture + Vertical Slice + CQRS |
| Validation | FluentValidation |
| Database | PostgreSQL running in Docker |
| Cache | Redis |
| Writes, CRUD, migrations | Entity Framework Core |
| Stored procedures and SQL views | Dapper |
| Users and roles | ASP.NET Core Identity |
| Authentication and authorization | JWT bearer tokens, role-based and resource-based authorization |
| Resilience | Retries, timeouts, and circuit breakers for database, cache, and outgoing calls |
| Rate limiting | ASP.NET Core rate limiting |

## 3. Roles

| Role | Description |
|---|---|
| Admin | Staff user. Manages products, orders, and customers. |
| Customer | Registered customer. Manages only their own data. |

## 4. Entities

| Entity | Notes |
|---|---|
| ASP.NET Core Identity | Users, roles, user-role links, claims, tokens |
| Products | Items for sale |
| Orders | Orders placed for a customer |
| Customers | Customer profile, linked one-to-one with an Identity user |

Propose the fields, keys, relationships, indexes, and constraints for each entity. Mark them as "proposed" until I confirm.

## 5. Endpoints and permissions

### Products

| Action | Who can do it |
|---|---|
| Create | Admin only |
| Read | Admin, Customer |
| Update | Admin only |
| Delete | Admin only |

### Orders

| Action | Who can do it |
|---|---|
| Create | Admin only |
| Read | Admin, Customer |
| Update | Admin only |
| Delete | Admin only |

### Customers

| Action | Who can do it |
|---|---|
| Create | Customer only. Creates the Customer record **and** the Identity account in one step. |
| Read | Admin, Customer |
| Update | Admin, Customer |
| Delete | Admin, Customer |

### ASP.NET Core Identity

Implement **all useful endpoints** for accounts and roles. Propose the full list, for example: register, login, refresh token, logout (revoke refresh token), get current user, change password, forgot and reset password, confirm email, resend confirmation, and Admin-only user and role management (list users, get user, lock and unlock, assign and remove roles, list roles).

### Rules for all endpoints

- **Read for Customers means their own data only**, unless I say otherwise. A Customer must never read, change, or delete another customer's record or orders (prevent BOLA/IDOR).
- Every list endpoint has paging, filtering, and sorting.
- Errors use ProblemDetails.
- Every endpoint has request and response DTOs (never return entities), FluentValidation rules, an authorization rule, and an OpenAPI description.

## 6. Data access rules

- **EF Core** for all writes, standard reads, and migrations.
- **Dapper** for stored procedures and SQL views. Propose useful ones, for example:
  - A view for order summaries (order, customer, item count, total).
  - A stored procedure or function for a customer's order history with paging.
  - A stored procedure or function for a sales report by date range.
- Both share the same connection and transaction when needed.
- Note that PostgreSQL uses **functions** and **procedures** differently from SQL Server. Explain the choice.
- Database objects (views, functions, procedures) are created through EF Core migrations, so the database can be rebuilt from scratch.

## 7. Non-functional requirements

| Area | Requirement |
|---|---|
| Security | OWASP API Security Top 10, least privilege, no secrets in source control, HTTPS, security headers, CORS allow-list |
| Tokens | Short-lived access tokens, refresh token rotation, token revocation on logout and password change |
| Caching | Cache product reads in Redis; clear or update the cache when products change |
| Rate limiting | Stricter limits on login, register, and password endpoints; general limits per user or IP |
| Resilience | Retries with backoff for transient database and Redis errors; the API still works (slower) if Redis is down |
| Observability | Structured logging, health checks (API, PostgreSQL, Redis), OpenTelemetry traces and metrics |
| Local setup | One `docker compose up` for PostgreSQL and Redis (optionally .NET Aspire); seed data with one Admin user from configuration |
| Testing | Unit tests for handlers and validators; integration tests with Testcontainers (real PostgreSQL and Redis); authorization tests for every endpoint and role |
| API testing files | `.http` files and a Postman collection for every endpoint, with environments and no secrets |
| Documentation | OpenAPI document, `README.md` file with setup and run steps |

## 8. Out of scope (this phase)

- Front-end UI.
- Payments, shipping, and invoices.
- Email sending (use a fake email sender that logs the message, unless I decide otherwise).
- Cloud deployment (local Docker only), unless I add it later.

## 9. Open questions to resolve with me

Ask me these first, a few at a time, and record my answers:

1. **Orders by Customers:** Orders "Create" is Admin only. Should Customers be able to place their own orders? Should they be able to cancel them?
2. **Order items:** Should an order have line items (order → many products with quantity and price)? If yes, add an `OrderItems` entity.
3. **Customer create:** "Customer only" means a new, not-yet-logged-in user signs up and becomes a Customer. Confirm this is an anonymous **registration** endpoint, and whether an Admin can also create customers.
4. **Customer delete:** soft delete or hard delete? What happens to the Identity account and to existing orders?
5. **Customer read and update:** confirm Customers can see and change only their own record, and Admins can see and change all.
6. **Product delete:** soft delete, since old orders reference products?
7. **Stock and prices:** do products have stock levels? Is the order price saved at order time?
8. **Order statuses:** for example Pending, Confirmed, Shipped, Completed, Cancelled. Who can change each status?
9. **Email confirmation:** required before login?
10. **Admin accounts:** how is the first Admin created (seed from configuration)? Can Admins create other Admins?
11. **CQRS mediator:** use FastEndpoints' built-in command bus, hand-written handlers, or another mediator? Check licenses (for example MediatR is now commercial).
12. **Refresh tokens:** stored in the database and rotated? Lifetime of access and refresh tokens?
13. **Multi-tenancy, currency, and time zones:** single tenant, one currency, and UTC dates?

## 10. How to run the session

1. Use my existing agents and skills: `product-strategist`, `requirements-analyst`, `solution-architect`, `security-privacy-reviewer`, `qa-strategist`, `delivery-planner`, `devils-advocate`, and the tracker agent. For .NET specifics, follow my `dotnet-`, `db-`, and `api-` skills if they exist.
2. Check the current versions and licenses of .NET 10, FastEndpoints, FluentValidation, EF Core and the Npgsql provider, Dapper, PostgreSQL, Redis (and Valkey as an alternative), and the JWT and resilience packages before recommending anything.
3. Ask the open questions in section 9 first.
4. Skip phases that do not apply (UX research, UI design, Figma). Note that this is an API-only product.
5. Challenge my requirements where they seem wrong or risky, and explain why. Use `devils-advocate` for a pre-mortem.
6. Show me each phase's output and wait for my approval before moving on.

## 11. Outputs I expect

Save everything in `docs/product/`:

1. **Concept:** short problem statement, users, and goals.
2. **PRD:** scope, requirements, non-goals, and the answers to the open questions.
3. **User stories** with Given/When/Then acceptance criteria for every endpoint, including the "forbidden" and "not found" cases.
4. **Permission matrix:** every endpoint × role (Admin, Customer, anonymous), including own-data rules.
5. **Data model:** entities, fields, relationships, indexes, and an ER diagram in Mermaid; the list of views, functions, and procedures used through Dapper.
6. **Endpoint catalog:** method, route, request, response, status codes, auth rule, rate limit, and cache rule for each endpoint.
7. **Architecture:** C4 context and container diagrams in Mermaid, solution and folder structure (Clean Architecture layers with vertical slices per feature), and ADRs for the key decisions (FastEndpoints, CQRS approach, EF Core + Dapper, JWT and refresh tokens, Redis caching, rate limiting).
8. **Threat model:** STRIDE table with mitigations.
9. **Test strategy:** unit, integration (Testcontainers), authorization, and API tests (`.http` files and Postman).
10. **Delivery plan:** MVP slices and milestones, then work items in the tracker (preview first, create only after I approve).
11. **Summary:** `00-summary.md` file with decisions, open questions, and links.
