temporal server start-dev --ui-port 8080

dotnet run --project Worker/Worker.csproj

dotnet run --project Api/Api.csproj



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
