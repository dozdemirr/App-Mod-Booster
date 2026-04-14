```mermaid
flowchart LR
    U[User Browser] --> W[Azure App Service<br/>ModernExpenseApp]
    W --> A[Swagger API<br/>/api/*]
    A --> S[(Azure SQL Database<br/>northwind)]
    W --> C[Chat UI<br/>/chatui]
    C --> API[Chat API<br/>/api/chat]
    API --> O[Azure OpenAI<br/>gpt-4o swedencentral]
    API --> R[Azure AI Search]
    API --> A
    MI[User-assigned Managed Identity] --> W
    MI --> S
    MI --> O
    MI --> R
```
