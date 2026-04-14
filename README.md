![Header image](https://github.com/DougChisholm/App-Mod-Booster/blob/main/repo-header-booster.png)

# App-Mod-Booster
A modernized Azure POC for an Expense Management app:
- .NET 8 Razor Pages UI (`/Index`)
- API-first backend with Swagger (`/swagger`)
- SQL access via managed identity + stored procedures only
- Optional Chat UI (`/chatui/index.html`) with Azure OpenAI function-calling + RAG context
- Modular Bicep IaC + deployment scripts

## Repository structure
- `AppModAssist/` - .NET 8 app, APIs, Swagger, Razor UI, chat endpoint
- `infra/` - modular bicep (`main`, `app-service`, `azure-sql`, `genai`)
- `Database-Schema/` - schema and stored procedures
- `chatui/` - chat documentation
- `RAG/` - retrieval context
- `deploy.sh` - deploy app + SQL
- `deploy-with-chat.sh` - deploy app + SQL + GenAI resources
- `deploy-summary.sh` - summary launcher script

## Prerequisites
- Azure CLI (`az`)
- .NET 8 SDK
- Python 3 + pip
- `jq` and `zip`
- Azure login: `az login`

## Required environment variables
```bash
export RESOURCE_GROUP="rg-appmodassist-poc"
export ADMIN_OBJECT_ID="<your-entra-object-id>"
export ADMIN_UPN="<your-upn@domain>"
export LOCATION="swedencentral"
```

## One-line deployment
Standard deployment (app + SQL):
```bash
bash deploy-summary.sh
```

Deployment with chat resources (AOAI + Search):
```bash
bash deploy-summary.sh chat
```

## App URL
After deployment, open:
```text
https://<app-url>/Index
```

## Local run
Use local Entra auth for SQL:
- `AppModAssist/appsettings.Development.json` uses `Authentication=Active Directory Default`
- Run `az login` first

Then:
```bash
dotnet run --project AppModAssist/AppModAssist.csproj
```
Open:
- `https://localhost:xxxx/Index`
- `https://localhost:xxxx/swagger`

## Managed identity SQL pattern
Production connection uses:
```text
Authentication=Active Directory Managed Identity;User Id=<managed-identity-client-id>;
```
Set both app settings:
- `ManagedIdentityClientId`
- `AZURE_CLIENT_ID`

## Azure best practices applied
- Secure-by-default identity access (managed identity over secrets)
- SQL Entra-only auth
- Modular IaC with deterministic naming (`uniqueString(resourceGroup().id)`)
- Separation of responsibilities: infrastructure, app deployment, data setup
