# Deployment instructions

Run the summary script in one line:

```bash
bash deploy-all.sh app
```

For full GenAI experience:

```bash
bash deploy-all.sh chat
```

After deployment, open:

- `https://<app-name>.azurewebsites.net/Index`
- `https://<app-name>.azurewebsites.net/Chat`
- `https://<app-name>.azurewebsites.net/swagger`

## Local run note

For local development, use `ConnectionStrings:SqlDbLocal` with:

`Authentication=Active Directory Default`

Run `az login` first so the local user can authenticate to Azure SQL.
