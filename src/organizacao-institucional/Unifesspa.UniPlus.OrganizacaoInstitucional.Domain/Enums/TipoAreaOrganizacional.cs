namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;

/// <summary>
/// Classificação organizacional de uma <c>AreaOrganizacional</c> (ADR-0055).
/// Identifica o tipo institucional da unidade — usado para agrupamentos em
/// admin UI e governance reports, sem efeito em autorização (que segue a
/// convenção de roles Keycloak <c>{codigo}-admin</c>/<c>{codigo}-leitor</c>).
/// </summary>
/// <remarks>
/// Persistido como <c>int</c> via EF <c>HasConversion&lt;int&gt;()</c> em
/// <c>AreaOrganizacionalConfiguration</c>. Valores numéricos são parte do
/// contrato — não renumerar entradas existentes; novos tipos recebem números
/// sequenciais; <see cref="Outra"/> = 99 é sentinel para casos não cobertos.
/// </remarks>
public enum TipoAreaOrganizacional
{
    /// <summary>Sentinel <c>default(TipoAreaOrganizacional)</c> — exigido por CA1008. Nunca produzido por <c>AreaOrganizacional.Criar</c>; presença em coluna persistida indica corrupção.</summary>
    Nenhum = 0,

    /// <summary>Pró-Reitoria (PROEG, PROGEP, PROEX, …).</summary>
    ProReitoria = 1,

    /// <summary>Centro institucional especializado (CEPS, CRCA, …).</summary>
    Centro = 2,

    /// <summary>Coordenadoria sob alguma Pró-Reitoria ou Centro.</summary>
    Coordenadoria = 3,

    /// <summary>Plataforma / CTIC — administra catálogos universais e configurações cross-area.</summary>
    Plataforma = 4,

    /// <summary>Outras unidades institucionais não categorizadas acima.</summary>
    Outra = 99,
}
