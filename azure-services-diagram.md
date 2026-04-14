# Azure services diagram

```mermaid
flowchart LR
    user[User Browser] --> app[Azure App Service<br/>AppModAssist]
    app --> api[Expense + Chat APIs]
    api --> sql[(Azure SQL Northwind)]
    app --> mi[User Assigned Managed Identity]
    mi --> sql
    mi --> aoai[Azure OpenAI GPT-4o<br/>Sweden Central]
    mi --> search[Azure AI Search]
    search --> rag[RAG Context]
```
