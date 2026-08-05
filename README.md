docker compose up -d

dotnet run --project src/Systems/Space/Classic/WaaS.Space.Classic.Worker/WaaS.Space.Classic.Worker.csproj

dotnet run --project src/WaaS.WebApi/WebApi.csproj

Temporal UI: http://localhost:8080 — API: http://localhost:5026/scalar

The compose file applies `sql/` on first start, seeding stack instance 1 / system instance 5000000001.

    curl -X PUT http://localhost:5026/api/demo/stack-instances/1/stretchspaces/5000000001 \
      -H 'Content-Type: application/json' -H 'Transaction-Id: demo-1' \
      -d '{"data":{"platform":1},"limits":{"resourceLevel":"XS","diskQuota":50000000},
           "mailConfiguration":{"host":"mail.example.com","hostPort":587,"username":"demo"}}'

    curl -X PUT http://localhost:5026/api/actual-state \
      -H 'Content-Type: application/json' \
      -d '{"stackInstanceId":1,"systemInstanceId":5000000001,"tenant":1,"zone":1,"data":{"webspace":{}}}'



Request:
  - Receive request data (View Model) 
  - Validate Input
  - Set advisory lock -------------------------------------------------+
  - Read Desired State from database                                   |
  - Validate ViewModel against Desired State (with database queries)   +-- Database Transaction
  - Apply ViewModel to Desired State (also database dependent)         |
  - Save Desired State to database                                     |
  - Write outbox message [Publish Workflow] ---------------------------+
  - (Release lock on commit)
  - Wait for backend result
  - Respond

Publish Workflow:
  - Send Desired State to backend
  - Return backend result to workflow trigger
  - Start publishing Desired State from another system, based on backend result
    - [Wait for dependency notification]
  - [Wait for ACK]

Wait for ACK Workflow:
  - Send notification to client

Wait for dependency notification Workflow:
  - Send notification to client
