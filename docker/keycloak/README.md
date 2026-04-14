# Keycloak local

Este diretório contém a configuração do realm `unifesspa` utilizada no ambiente local.

## Por que existe um `realm-export.json` versionado

O arquivo `realm-export.json` foi versionado para garantir que qualquer desenvolvedor consiga subir o ambiente local com o realm `unifesspa` já configurado, sem necessidade de criar manualmente clients, roles, groups, usuários ou atributos no painel do Keycloak.

Com isso, a configuração de autenticação e autorização do projeto fica padronizada, reproduzível e compartilhada entre todos os membros do time.

## Usuários de teste

| Usuário           | Papel     | Senha inicial      |
|-------------------|-----------|--------------------|
| admin@teste       | admin     | definida no `realm-export.json` |
| gestor@teste      | gestor    | definida no `realm-export.json` |
| avaliador@teste   | avaliador | definida no `realm-export.json` |
| candidato@teste   | candidato | definida no `realm-export.json` |

* ⚠️ Troque a senha de todos os usuários no primeiro login.
* As credenciais iniciais podem ser consultadas no arquivo `realm-export.json`.
* Essas contas são destinadas exclusivamente ao ambiente local.
* Login: `http://localhost:8080/realms/unifesspa/account`
* O host e a porta podem variar conforme a configuração do ambiente.

## Como forçar a reimportação

Se o Keycloak já tiver sido inicializado anteriormente, o realm existente pode impedir a reimportação automática do arquivo.

Para forçar uma importação limpa, remova os containers e volumes e depois suba o ambiente novamente:

```bash
docker compose -f docker/docker-compose.yml down -v
docker compose -f docker/docker-compose.yml up --build
```

Esse processo recria o ambiente e permite que o Keycloak importe novamente o realm-export.json.