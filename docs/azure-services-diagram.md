```mermaid
flowchart LR
  U[User Browser /Index] --> APP[Azure App Service .NET 8]
  APP --> API[REST APIs + Swagger]
  APP --> CHAT[Chat Service Function Calling]
  API --> SQL[(Azure SQL Database Northwind)]
  CHAT --> AOAI[Azure OpenAI gpt-4o swedencentral]
  CHAT --> SEARCH[Azure AI Search]
  APP -->|User Assigned Managed Identity| AOAI
  APP -->|User Assigned Managed Identity| SEARCH
  APP -->|User Assigned Managed Identity| SQL
```
