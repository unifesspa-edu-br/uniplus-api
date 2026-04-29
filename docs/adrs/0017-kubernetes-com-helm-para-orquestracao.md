---
status: "accepted"
date: "2026-04-28"
decision-makers:
  - "Tech Lead (CTIC)"
---

# ADR-0017: Kubernetes com Helm para orquestração do `uniplus-api`

## Contexto e enunciado do problema

O `uniplus-api` precisa ser deployado com zero downtime durante atualizações, auto-recuperação de falhas e capacidade de escalar durante picos de inscrição (5000+ candidatos simultâneos). A Unifesspa possui infraestrutura própria com equipe que tem experiência em containers Docker.

Os módulos Seleção e Ingresso são aplicações independentes (ADR-0001) que precisam de deploy coordenado com dependências (PostgreSQL, Kafka, Redis, MinIO, Keycloak).

## Drivers da decisão

- Zero downtime para atualizações em janela de inscrição.
- Self-healing de pods para reduzir intervenção operacional.
- Auto-scaling para absorver picos.
- Infraestrutura como código versionada.

## Opções consideradas

- Kubernetes com Helm + Kustomize
- Docker Compose em produção
- VMs com deploy via Ansible
- PaaS gerenciado (Heroku, Railway)

## Resultado da decisão

**Escolhida:** Kubernetes como plataforma de orquestração e Helm como gerenciador de templates do `uniplus-api`.

Práticas e ferramentas:

- **Helm charts** parametrizáveis para cada módulo, com `values` por ambiente.
- **Kustomize overlays** para ajustes adicionais por ambiente (dev, staging, prod).
- **Deploy blue-green** — nova versão é provisionada em paralelo, tráfego alterna após health check verde.
- **Horizontal Pod Autoscaler (HPA)** com base em CPU/memória.
- **NGINX Ingress Controller** com TLS termination, rate limiting de borda e roteamento por path.
- **cert-manager + Let's Encrypt** para TLS automático.
- **Sealed Secrets ou HashiCorp Vault** para gestão de segredos.
- **Container registry institucional** (Harbor ou registro próprio).
- **Docker multi-stage builds** com separação de build e runtime.
- **`AutoBuildMessageStorageOnStartup = AutoCreate.None`** em produção (ver ADR-0004) — role de banco do nó Wolverine sem permissão DDL.

Ambientes:

| Ambiente | Finalidade | Infraestrutura |
|----------|------------|----------------|
| Dev local | Desenvolvimento individual | Docker Compose |
| Dev K8s | Feature branches, integração | Cluster compartilhado |
| Staging | Validação pré-produção, load tests | Cluster dedicado |
| Produção | Sistema em operação | Cluster HA |

## Consequências

### Positivas

- Self-healing — pods recriados automaticamente em caso de falha.
- Rolling deploys sem downtime durante inscrições.
- Auto-scaling provisiona réplicas adicionais sob demanda.
- Infraestrutura como código versionada no monorepo.
- Ecossistema rico de operadores (Strimzi, CloudNativePG, MinIO Operator) facilita gestão de dependências.

### Negativas

- Complexidade operacional significativa — Kubernetes exige conhecimento especializado.
- Curva de aprendizado para a equipe (kubectl, Helm, Kustomize).
- Overhead de recursos pelo control plane.
- Debugging distribuído mais complexo.

### Riscos

- **Complexidade para equipe pequena.** Mitigado por capacitação, ferramentas visuais (Lens, k9s) e automação CI/CD.
- **Falha do cluster.** Mitigado por backups regulares do `etcd`, múltiplos master nodes em produção, e DR plan testado.
- **Drift entre ambientes.** Mitigado por Kustomize overlays versionados e CI que valida charts antes do deploy.

## Confirmação

- Liveness/readiness probes em todos os deployments.
- Pipeline de CI executa `helm lint` e `kubeval` em cada chart antes do merge.
- HPA configurado e validado em ambiente de staging com load test antes do release.

## Prós e contras das opções

### Kubernetes + Helm

- Bom, porque é o padrão de mercado para orquestração de containers em produção.
- Ruim, porque a complexidade operacional é a maior dentre as opções.

### Docker Compose em produção

- Bom, porque é simples e familiar à equipe.
- Ruim, porque sem self-healing robusto, sem auto-scaling, sem rolling deploys nativos.

### VMs com Ansible

- Bom, porque é familiar a operadores tradicionais.
- Ruim, porque sem auto-scaling, deploys lentos, rollback complexo, não aproveita experiência da equipe com containers.

### PaaS gerenciado

- Bom, porque elimina operação.
- Ruim, porque custo recorrente, dados fora da infraestrutura institucional, limitações de customização.

## Mais informações

- ADR-0001 estabelece monolito modular com deploy independente por módulo.
- ADR-0006, ADR-0007, ADR-0008, ADR-0009, ADR-0014 definem os componentes que esta orquestração cobre.
- ADR-0018 define a stack de observabilidade que opera sobre este cluster.
- **Origem:** revisão da ADR interna Uni+ ADR-013 (não publicada).
