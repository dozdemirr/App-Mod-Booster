# Expense Management System – Azure Architecture

## Services Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                          AZURE SUBSCRIPTION                         │
│                                                                     │
│  ┌──────────────────────────── rg-expensemgmt-demo ──────────────┐  │
│  │                                                               │  │
│  │  ┌────────────────────┐    Managed Identity Auth              │  │
│  │  │  User-Assigned     │◄──────────────────────────────────┐  │  │
│  │  │  Managed Identity  │                                   │  │  │
│  │  │  mid-AppModAssist  │                                   │  │  │
│  │  └────────────────────┘                                   │  │  │
│  │           │                                               │  │  │
│  │           │ Identity assigned to                          │  │  │
│  │           ▼                                               │  │  │
│  │  ┌────────────────────┐   HTTPS    ┌───────────────────┐ │  │  │
│  │  │   Azure App        │◄──────────►│    Browser /      │ │  │  │
│  │  │   Service (S1)     │            │    Chat UI        │ │  │  │
│  │  │   .NET 8 Razor     │            └───────────────────┘ │  │  │
│  │  │   Pages + API      │                                   │  │  │
│  │  │                    │──────────────────────────────────►┘  │  │
│  │  │  /Index (UI)       │                                      │  │
│  │  │  /api/* (REST)     │                                      │  │
│  │  │  /swagger (Docs)   │                                      │  │
│  │  │  /chatui (Chat)    │   MI Auth (no passwords)             │  │
│  │  └────────────────────┘                                      │  │
│  │           │                         │                        │  │
│  │           │ Stored Procedures       │ Cognitive Services     │  │
│  │           │ (MI Auth)               │ OpenAI User role       │  │
│  │           ▼                         ▼                        │  │
│  │  ┌────────────────────┐   ┌────────────────────┐            │  │
│  │  │   Azure SQL        │   │   Azure OpenAI     │            │  │
│  │  │   Database         │   │   (GPT-4o)         │            │  │
│  │  │   (Northwind)      │   │   swedencentral    │            │  │
│  │  │   Basic tier       │   │   S0 SKU           │            │  │
│  │  │   AAD-only auth    │   └────────────────────┘            │  │
│  │  └────────────────────┘            │                        │  │
│  │                                    │                        │  │
│  │                          ┌────────────────────┐             │  │
│  │                          │   Azure AI Search  │             │  │
│  │                          │   (basic tier)     │             │  │
│  │                          │   swedencentral    │             │  │
│  │                          └────────────────────┘             │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

## How the Services Connect

| Connection | Method | Authentication |
|---|---|---|
| Browser → App Service | HTTPS | Public |
| App Service → Azure SQL | TCP 1433 | Managed Identity (AAD) |
| App Service → Azure OpenAI | HTTPS | Managed Identity (Cognitive Services OpenAI User role) |
| App Service → AI Search | HTTPS | Managed Identity (Search Index Data Contributor role) |
| Chat UI → App Service API | HTTPS /api/chat | Public (same origin) |

## Deployment Scripts

| Script | Deploys |
|---|---|
| `deploy.sh` | App Service + SQL + App Code (no GenAI) |
| `deploy-with-chat.sh` | Everything including Azure OpenAI + AI Search |

## Security Notes
- SQL Server uses **Azure AD-only authentication** (no SQL passwords) - MCAPS SFI-ID4.2.2 compliant
- App Service connects to SQL using **User-Assigned Managed Identity**
- No secrets stored in app code or configuration
- All resources named using `uniqueString(resourceGroup().id)` for deterministic, collision-free names
