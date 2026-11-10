namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

/// <summary>Como a permissão mudou no catálogo: a classe e o escopo, de e para.</summary>
public sealed record MudancaDaPermissaoDto(string ClassificacaoAnterior, string ClassificacaoNova, bool DeUnidadeAnterior, bool DeUnidadeNovo);

/// <summary>Um item da fila de uma etapa: o perfil e a permissão reclassificada, com as concessões que ainda aguardam a etapa.</summary>
public sealed record ItemDaFilaDeReclassificacoesDto(
    Guid Id,
    Guid PerfilId,
    string PerfilNome,
    string Codigo,
    MudancaDaPermissaoDto Mudanca,
    DateTimeOffset Instante,
    int ConcessoesAguardando);

/// <summary>A permissão como a versão a retratou.</summary>
public sealed record PermissaoDaVersaoEmUsoDto(string Codigo, string Classificacao, bool DeUnidade);

/// <summary>Uma versão do perfil em uso, com todas as permissões e o caminho do documento instituidor.</summary>
public sealed record VersaoEmUsoDto(
    Guid Id, int Numero, string Situacao, IReadOnlyList<PermissaoDaVersaoEmUsoDto> Permissoes, string? LinkDoDocumento);

/// <summary>Uma concessão suspensa pela reclassificação, com as etapas que a base dela exige e as já decididas.</summary>
public sealed record ConcessaoSuspensaDto(
    Guid ConcessaoId,
    string? BeneficiarioNome,
    string EscopoTipo,
    Guid? UnidadeId,
    int? Versao,
    IReadOnlyList<string> EtapasExigidas,
    IReadOnlyList<string> EtapasDecididas,
    bool EhDoLeitor);

/// <summary>O detalhe da reclassificação para quem a revê.</summary>
public sealed record ReclassificacaoDto(
    Guid Id,
    Guid PerfilId,
    string PerfilNome,
    string Codigo,
    MudancaDaPermissaoDto Mudanca,
    DateTimeOffset Instante,
    bool MaisRecente,
    IReadOnlyList<VersaoEmUsoDto> Versoes,
    IReadOnlyList<ConcessaoSuspensaDto> Concessoes);

/// <summary>Decide uma etapa para as concessões da lista ou, com <c>Todas</c>, para as que a aguardam.</summary>
public sealed record DecidirReclassificacaoRequest(string? Etapa, string? Resultado, IReadOnlyList<Guid>? Concessoes, bool Todas, string? Motivo);

/// <summary>O que a decisão fez: as concessões decididas, as que ganharam a base nova e as que o lote deixou de fora.</summary>
public sealed record DecisaoDaReclassificacaoDto(
    Guid LoteId,
    IReadOnlyList<Guid> Decididas,
    IReadOnlyList<Guid> Liberadas,
    IReadOnlyList<Guid> ParaOutraPessoa,
    IReadOnlyList<Guid> PuladasPorEscopo);

/// <summary>Uma divergência da governança para a tela.</summary>
public sealed record DivergenciaDaGovernancaDto(
    Guid Id, Guid PerfilId, string PerfilNome, string Codigo, string Tipo, string? De, string? Para, DateTimeOffset Instante);
