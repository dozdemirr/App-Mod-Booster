![Header image](https://github.com/DougChisholm/App-Mod-Booster/blob/main/repo-header-booster.png)

# App-Mod-Booster
A modernized .NET 8 solution with:

- Azure IaC (Bicep)
- App Service + user-assigned managed identity
- Azure SQL (Entra admin + AAD-only auth)
- API + Swagger
- Razor UI
- Chat UI + Azure OpenAI function-calling integration
- Deployment scripts (with/without GenAI)

## Deploy

```bash
bash deploy-all.sh app
```

Or with chat resources:

```bash
bash deploy-all.sh chat
```

Also available:

- `bash deploy.sh` (app + db)
- `bash deploy-with-chat.sh` (app + db + genai)

## Important URL guidance

After deployment, browse to:

- `https://<app-name>.azurewebsites.net/Index`

Do not use root URL only.

## App projects and assets

- `src/ModernExpenseApp` - .NET 8 Razor + API
- `chatui` - standalone chat UI frontend assets
- `infra` - Bicep templates
- `stored-procedures.sql` - database stored procs used by API (no inline app SQL)
- `docs/azure-services-diagram.md` - service diagram
