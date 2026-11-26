# Instalação dos perfis de operação

Na instalação do Uni+ em um ambiente novo, os perfis de operação (conjuntos de permissões que as unidades usam no dia
a dia) precisam existir antes de qualquer pedido de acesso. O roteiro `scripts/instalacao-dos-perfis-de-operacao.sh`
cria esses perfis pela API, no mesmo caminho da tela de perfis, e depois confere o que ficou em vigor. Serve à
homologação (com documento de teste) e à produção (com o documento real).

O roteiro **só cria perfis**. Ele não pede acesso, não aprova nada, não cria unidade e não acessa o banco de dados.
Roda só quando a liderança técnica autorizar e é executado por ela; em produção, só com ordem expressa dela.

## Quem executa e como entra

Executa um dos dois gestores de acessos instalados (têm a permissão `autorizacao:perfis:manter`). O roteiro não
recebe senha: o login é por código de dispositivo. Ele mostra um endereço e o gestor entra com a própria conta no
navegador. O token fica só na memória do processo e some ao fim.

Isso exige um cliente público no Keycloak do ambiente, só com o login por código de dispositivo ligado (sem fluxo do
navegador nem login por senha) e com o mapeador de audiência `uniplus`. O nome do cliente vai no parâmetro `KC_CLIENT_ID`.

## Parâmetros

Todos obrigatórios e sem valor padrão; chegam do cofre de segredos ou são informados por quem executa. Nada fica gravado
no roteiro nem no repositório.

| Parâmetro | O que é |
|---|---|
| `API_URL` | Endereço da API, em https |
| `KC_URL` | Endereço do Keycloak, em https |
| `KC_REALM` | Realm do Keycloak |
| `KC_CLIENT_ID` | Cliente público com o login por código de dispositivo |
| `DOCUMENTO_INSTITUIDOR` | Caminho do PDF que institui os perfis (só em `criar`); um só para todos, de até 10 MB |
| `PROTOCOLO_DO_DOCUMENTO` | Número do processo ou protocolo desse documento (só em `criar`) |

Opcional: `ARQUIVO_DOS_PERFIS` aponta outro arquivo de perfis, no formato de `scripts/perfis-de-operacao.json`.

## Etapa `criar`

1. Confere os perfis que já existem, sem gravar nada. Se algum não pode seguir, o roteiro para antes de gravar qualquer
   perfil e diz qual perfil e por quê.
2. Cria os perfis que faltam como versões pendentes, envia o documento e espera a confirmação.
3. Mostra, para cada perfil, a etapa que falta, se a proteção de dados também aprova, a situação do documento e o prazo.

| Situação do perfil | O que o roteiro faz |
|---|---|
| Não existe | cria |
| Em vigor, com as mesmas permissões | nada |
| Em vigor, com permissões diferentes | informa o que falta e o que sobra e para |
| Versão pendente igual, com documento | informa que aguarda aprovação e o prazo |
| Versão pendente igual, sem documento | envia o documento |
| Versão pendente com permissões diferentes | para |
| Última versão expirada | cria nova versão |
| Última versão recusada ou cancelada, desativado ou suspenso | para |

O alcance de cada perfil (instituição ou unidade) não é enviado: a API o deriva das permissões. Se o arquivo misturar
permissões dos dois alcances, a API recusa com `alcance_misto` e o roteiro para nesse erro. A API só valida as
permissões de um perfil novo ao criá-lo; por isso, se um perfil novo for recusado, os criados antes dele ficam criados,
e rodar `criar` de novo, depois de corrigir o arquivo, não duplica nenhum deles.

## Aprovação pela tela

As versões pendentes entram em vigor quando o outro gestor de acessos as aprova pela tela de perfis. Nos perfis com
permissão de dado pessoal, identificador ou sensível, o Representante do controlador também aprova. A versão pendente
expira em 15 dias; se expirar, rodar `criar` de novo cria uma nova versão.

## Etapa `conferir`

Depois das aprovações, confere cada perfil com o arquivo: o alcance (`escopo` em `GET /api/autorizacao/perfis-para-pedido`,
com o mesmo código do contrato, `INSTITUICAO` ou `UNIDADE`) e as permissões em vigor (`GET /api/autorizacao/perfis/{id}`).
Antes da aprovação o perfil não aparece na lista para pedido, e o roteiro orienta aguardar.

Saída: `0` tudo conforme; `1` há diferença; `2` há perfil ainda aguardando a aprovação.
