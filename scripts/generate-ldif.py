#!/usr/bin/env python3
"""
Gera o LDIF de bootstrap do OpenLDAP local a partir de dados sintéticos
(4devs em docker/ldap/data/seed-4devs.json).

Reproduz o cenário do LDAP institucional Unifesspa:
- 7 entries com CPF de 11 dígitos (caso feliz)
- 3 entries com CPF de 10 dígitos (zero à esquerda truncado, bug histórico
  do brPersonCPF institucional)

Para os 3 entries do grupo malformado, são gerados CPFs sintéticos NOVOS
que iniciam com 0 (com DVs válidos calculados), e armazenados no LDAP
truncados (sem o zero) — simulando o caso real do LDAP institucional.

Uso:
    python3 scripts/generate-ldif.py

Saída:
    docker/ldap/bootstrap/01-users.ldif (commitado no repo)

O script é idempotente — rodar 2x produz output idêntico (seed determinístico).

Os dados são CLARAMENTE SINTÉTICOS: gerados pela 4devs (validador de CPF
brasileiro com DV correto, mas CPF não pertencente a pessoa real).
"""

from __future__ import annotations

import hashlib
import json
import sys
import unicodedata
from pathlib import Path

# Caminhos relativos ao repo root
REPO_ROOT = Path(__file__).resolve().parent.parent
SEED_PATH = REPO_ROOT / "docker" / "ldap" / "data" / "seed-4devs.json"
LDIF_OUT = REPO_ROOT / "docker" / "ldap" / "bootstrap" / "01-users.ldif"

LDAP_BASE_DN = "dc=unifesspa,dc=edu,dc=br"
USERS_OU = f"ou=Users,{LDAP_BASE_DN}"
DEFAULT_PASSWORD = "Changeme!123"  # senha sintética em plaintext — dev local; facilita debug

# Domínio reservado para emails sintéticos (RFC 2606 / TLD .local).
# A 4devs gera emails com domínios reais misturados (eptv.com.br, live.dk, etc.);
# trocar por domínio reservado evita que mensagens sintéticas cheguem em
# catch-all de empresas reais durante testes E2E ou verificação de email
# do Keycloak.
SAFE_EMAIL_DOMAIN = "uniplus-test.local"

# Quantidade de entries com CPF malformado (10 dígitos no LDAP)
MALFORMED_COUNT = 3


def cpf_dvs(base9: str) -> str:
    """Calcula os 2 DVs do CPF a partir dos primeiros 9 dígitos. Retorna '##'."""
    if len(base9) != 9 or not base9.isdigit():
        raise ValueError(f"base9 deve ter 9 dígitos numéricos, veio: {base9!r}")

    def calc_dv(digits: str, weights: list[int]) -> int:
        s = sum(int(d) * w for d, w in zip(digits, weights))
        r = s % 11
        return 0 if r < 2 else 11 - r

    dv1 = calc_dv(base9, list(range(10, 1, -1)))            # pesos 10..2
    dv2 = calc_dv(base9 + str(dv1), list(range(11, 1, -1))) # pesos 11..2
    return f"{dv1}{dv2}"


def generate_cpf_with_leading_zero(seed: str) -> str:
    """
    Gera um CPF sintético DETERMINÍSTICO de 11 dígitos começando com 0
    (DVs válidos). Usado para simular CPFs que SERIAM corretos no gov.br
    mas estão truncados no LDAP. Determinismo via hash do seed.
    """
    h = hashlib.sha256(seed.encode()).hexdigest()
    # Extrai 8 dígitos numéricos do hash; combinados com '0' à esquerda
    # (linha abaixo) formam os 9 primeiros dígitos do CPF sintético —
    # DVs calculados a seguir garantem validade pela Receita Federal.
    digits_from_hash = "".join(c for c in h if c.isdigit())[:8]
    if len(digits_from_hash) < 8:
        digits_from_hash = digits_from_hash.ljust(8, "1")
    base9 = "0" + digits_from_hash
    return base9 + cpf_dvs(base9)


def first_word(name: str) -> str:
    return name.split()[0]


def last_word(name: str) -> str:
    parts = name.split()
    return parts[-1] if len(parts) > 1 else ""


def slug_uid(name: str, cpf: str) -> str:
    """uid = primeiroNome.ultimoSobrenome em lowercase, sem acentos. Fallback: cpf."""
    first = first_word(name)
    last = last_word(name)
    base = f"{first}.{last}".lower() if last else first.lower()
    nfkd = unicodedata.normalize("NFKD", base)
    ascii_only = "".join(c for c in nfkd if not unicodedata.combining(c) and (c.isalnum() or c == "."))
    return ascii_only or cpf


def safe_email(raw_email: str) -> str:
    """Substitui o domínio do email sintético por domínio reservado de teste.

    A 4devs gera emails com domínios reais (eptv.com.br, live.dk, etc.) que
    podem chegar em catch-all de empresas reais se o sistema disparar email
    durante testes. Preserva o local-part do email original para manter a
    legibilidade dos dados sintéticos.
    """
    local = raw_email.split("@", 1)[0]
    return f"{local}@{SAFE_EMAIL_DOMAIN}"


def render_entry(idx: int, person: dict, cpf_in_ldap: str, cpf_canonical: str, malformed: bool) -> str:
    """Renderiza uma entry LDIF (inetOrgPerson + organizationalPerson + person)."""
    nome = person["nome"]
    email = safe_email(person["email"])
    given = first_word(nome)
    family = last_word(nome) or given
    uid = slug_uid(nome, cpf_canonical)

    note = (
        f"# CPF malformado (truncado): canonical={cpf_canonical} | armazenado={cpf_in_ldap}"
        if malformed
        else f"# CPF normal (11 dígitos): {cpf_in_ldap}"
    )

    return (
        f"{note}\n"
        f"dn: uid={uid},{USERS_OU}\n"
        f"objectClass: inetOrgPerson\n"
        f"objectClass: organizationalPerson\n"
        f"objectClass: person\n"
        f"objectClass: top\n"
        f"uid: {uid}\n"
        f"cn: {nome}\n"
        f"givenName: {given}\n"
        f"sn: {family}\n"
        f"mail: {email}\n"
        f"employeeNumber: {cpf_in_ldap}\n"
        f"userPassword: {DEFAULT_PASSWORD}\n"
    )


def main() -> int:
    if not SEED_PATH.exists():
        print(f"ERRO: seed não encontrado em {SEED_PATH}", file=sys.stderr)
        return 1

    seed_data = json.loads(SEED_PATH.read_text(encoding="utf-8"))
    if len(seed_data) < 10:
        print(f"ERRO: seed tem {len(seed_data)} registros, esperado >=10", file=sys.stderr)
        return 1

    # Os 3 primeiros viram malformados; os 7 restantes ficam normais
    # (escolha determinística — não depende de ordem aleatória).
    entries: list[str] = []
    entries.append(
        f"# OpenLDAP local — Uni+ ({LDAP_BASE_DN})\n"
        f"# Gerado por scripts/generate-ldif.py a partir de docker/ldap/data/seed-4devs.json.\n"
        f"# Dados SINTÉTICOS (4devs) — não pertencem a pessoas reais.\n"
        f"# Distribuição: 7 entries com CPF de 11 dígitos (caso feliz) +\n"
        f"#               3 entries com CPF de 10 dígitos (zero truncado — bug LDAP).\n"
        f"# Issue de referência: uniplus-api#217 / ADR-029.\n"
        f"#\n"
        f"# Senha sintética padrão de todos: {DEFAULT_PASSWORD}\n\n"
    )

    # OU base
    entries.append(
        f"dn: {USERS_OU}\n"
        f"objectClass: organizationalUnit\n"
        f"objectClass: top\n"
        f"ou: Users\n\n"
    )

    for idx, person in enumerate(seed_data[:10]):
        is_malformed = idx < MALFORMED_COUNT

        if is_malformed:
            cpf_canonical = generate_cpf_with_leading_zero(seed=person["nome"])
            cpf_in_ldap = cpf_canonical[1:]  # remove zero à esquerda — simula truncamento
            if len(cpf_in_ldap) != 10:
                raise ValueError(f"CPF truncado deveria ter 10 dígitos: {cpf_in_ldap}")
        else:
            cpf_canonical = person["cpf"]
            cpf_in_ldap = cpf_canonical
            if len(cpf_in_ldap) != 11:
                raise ValueError(f"CPF normal deveria ter 11 dígitos: {cpf_in_ldap}")

        entries.append(render_entry(idx, person, cpf_in_ldap, cpf_canonical, is_malformed))
        entries.append("\n")  # linha em branco entre entries

    LDIF_OUT.parent.mkdir(parents=True, exist_ok=True)
    LDIF_OUT.write_text("".join(entries), encoding="utf-8")

    print(f"OK gerado {LDIF_OUT.relative_to(REPO_ROOT)}")
    print(f"   total: 10 entries (7 normais + {MALFORMED_COUNT} malformados)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
