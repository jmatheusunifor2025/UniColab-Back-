# UniColab-Backend

API do UniColab em ASP.NET Core 8.

## Segredos

A senha do MySQL e a chave JWT ficam fora do Git.

1. Copie `appsettings.Local.json.example` para `appsettings.Local.json`.
2. Preencha a senha do banco e uma chave JWT com pelo menos 32 caracteres.
3. Não faça commit de `appsettings.Local.json`.

No Docker ou no servidor, use variáveis de ambiente no lugar do arquivo local:

- `ConnectionStrings__Unicolab`
- `Jwt__Key`

`appsettings.json` permanece no repositório só com valores vazios. A API recusa subir se `Jwt:Key` tiver menos de 32 bytes.

## Deploy

Quem faz o build é o GitHub Actions (`.github/workflows/deploy.yml`). O Dokploy só implanta a imagem pronta. A cada merge na `main`:

1. O Actions compila a imagem Docker.
2. Publica no GHCR: `ghcr.io/jmatheusunifor2025/unicolab-back:sha-<commit>` (e `:latest`). O pacote fica público.
3. Chama o Dokploy (`nodilabs/dokploy-deploy-action`) para implantar essa versão.

O container escuta na porta **5080**. No Dokploy, a aplicação usa source **Docker** e as variáveis `ConnectionStrings__Unicolab` e `Jwt__Key`.

No GitHub (Settings → Secrets and variables → Actions):

- `DOKPLOY_SERVER_URL`: URL do painel do Dokploy
- `DOKPLOY_API_KEY`: chave de API do Dokploy
- `DOKPLOY_APPLICATION_IDS`: ID da aplicação no Dokploy

Enquanto os secrets não existirem, o workflow só publica a imagem e avisa que o deploy foi ignorado. Para reimplantar sem novo commit: aba Actions → "Build & Deploy" → Run workflow.
