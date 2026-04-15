![Header image](https://github.com/DougChisholm/App-Mod-Booster/blob/main/repo-header-booster.png)

# App-Mod-Booster

A modernized Azure-ready expense management application generated from legacy screenshots and schema.

## What this repository now contains

- .NET 8 Razor Pages app (`AppModAssist`) with modern UI at `/Index`
- API-first backend with Swagger at `/swagger`
- SQL access through stored procedures only
- Managed identity authentication for SQL and Azure OpenAI
- Optional GenAI chat mode with function calling
- Bicep IaC split by service (`infra/*.bicep`)
- Deployment scripts with and without GenAI services
- Standalone `chatui` folder for separate chat UX assets

## Deployment entry points

- Deploy app + SQL only:

```bash
bash deploy-all.sh app
```

- Deploy app + SQL + GenAI:

```bash
bash deploy-all.sh chat
```

You can also call `bash deploy.sh` or `bash deploy-with-chat.sh` directly.

## Deployment best-practice flow implemented

1. Deploy resource group + infrastructure
2. Configure App Service settings (including `AZURE_CLIENT_ID`)
3. Wait for SQL readiness (30 seconds)
4. Add firewall rules for Azure services and current client IP
5. Install Python dependencies
6. Import database schema
7. Configure managed identity database role grants
8. Deploy app zip package

## Local development

1. Authenticate to Azure:

```bash
az login
```

2. For local runs, set `ConnectionStrings:DefaultConnection` to use:

`Authentication=Active Directory Default`

3. Run app:

```bash
dotnet run --project AppModAssist/AppModAssist.csproj
```

## Packaging

The scripts generate `app.zip` with compiled app files at zip root (no extra parent folder) and deploy with:

```bash
az webapp deploy --resource-group <rg> --name <appName> --src-path ./app.zip
```

## Azure architecture

See `docs/azure-services-diagram.md`.
