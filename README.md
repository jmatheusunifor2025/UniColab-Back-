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
