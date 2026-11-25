namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>A revisão das concessões do perfil, com a lista das concessões avaliadas.</summary>
/// <param name="Situacao">
/// <c>ABERTA</c> enquanto há pendência e o prazo não venceu; <c>ENCERRANDO</c> quando já não há o que decidir e a próxima
/// operação ou a rotina a encerra; <c>ENCERRADA</c> depois disso.
/// </param>
/// <param name="Forma">Como a revisão terminou: <c>CONCLUIDA</c>, <c>PRAZO</c> ou <c>MANUAL</c>; nulo enquanto não terminou.</param>
/// <param name="DecisoesEnviadas">A gestão de acessos já enviou as decisões.</param>
public sealed record RevisaoDasConcessoesDto(
    Guid Id,
    Guid PerfilId,
    int VersaoNova,
    int VersaoAlvo,
    DateTimeOffset AbertaEm,
    DateTimeOffset TerminaEm,
    DateTimeOffset? EncerradaEm,
    CodigoComRotulo<SituacaoDaRevisaoDasConcessoes> Situacao,
    CodigoComRotulo<FormaDoEncerramento>? Forma,
    bool DecisoesEnviadas,
    IReadOnlyList<ConcessaoDaRevisaoDto> Itens);

/// <summary>
/// Uma concessão da revisão. A situação é <c>AGUARDANDO_DECISOES</c> para a elegível que ainda não tem linha, ou a da linha:
/// <c>RETIDA</c>, <c>AGUARDANDO_PROTECAO_DE_DADOS</c>, <c>MIGRADA</c>, <c>RECUSADA</c>, <c>NAO_MIGRADA</c> ou <c>FORA_DA_LISTA</c>.
/// </summary>
/// <param name="Acrescimo">Os códigos que a versão-alvo acrescenta ao que a concessão dá hoje.</param>
/// <param name="EhDoLeitor">A concessão é de quem lê, que não pode decidi-la.</param>
public sealed record ConcessaoDaRevisaoDto(
    Guid ConcessaoId,
    string? BeneficiarioNome,
    string EscopoTipo,
    Guid? UnidadeId,
    DateTimeOffset? ValidaAte,
    int? VersaoAtual,
    IReadOnlyList<string> Acrescimo,
    bool ExigeProtecaoDeDados,
    CodigoComRotulo<SituacaoDaConcessaoNaRevisao> Situacao,
    bool EhDoLeitor);

/// <summary>Uma revisão de concessões na lista de uma etapa, com o perfil a que pertence. Não traz nome de beneficiário.</summary>
/// <param name="VersaoAlvo">O número da versão para a qual as concessões migram.</param>
/// <param name="TerminaEm">O instante em que a revisão termina.</param>
/// <param name="DecisoesEnviadas">A gestão de acessos já enviou as decisões.</param>
/// <param name="QuantidadeADecidir">Quantas concessões quem lê pode decidir naquela etapa.</param>
public sealed record ItemDaListaDeRevisoesDto(
    Guid PerfilId,
    string PerfilNome,
    int VersaoAlvo,
    DateTimeOffset TerminaEm,
    bool DecisoesEnviadas,
    int QuantidadeADecidir);

/// <summary>A revisão resumida no perfil.</summary>
public sealed record RevisaoDoPerfilDto(Guid Id, DateTimeOffset TerminaEm, CodigoComRotulo<SituacaoDaRevisaoDasConcessoes> Situacao);

/// <summary>Uma decisão da gestão, sobre a concessão retida ou da proteção de dados: migrar ou recusar a concessão.</summary>
public sealed record DecisaoDaMigracaoRequest(Guid ConcessaoId, string? Decisao);

/// <summary>O envio de decisões da gestão de acessos: uma decisão por concessão e a confirmação de que recusar revoga.</summary>
public sealed record EnviarDecisoesDaRevisaoRequest(IReadOnlyList<DecisaoDaMigracaoRequest>? Decisoes, bool ConfirmacaoDeExclusao);

/// <summary>A decisão sobre a concessão retida.</summary>
public sealed record DecidirRetidaRequest(string? Decisao, bool ConfirmacaoDeExclusao);

/// <summary>A remessa da proteção de dados: de uma concessão a várias.</summary>
public sealed record DecidirEtapaDaProtecaoRequest(IReadOnlyList<DecisaoDaMigracaoRequest>? Decisoes, bool ConfirmacaoDeExclusao);

/// <summary>O encerramento manual da revisão, com motivo e a confirmação de que quem não migrou perde o acesso.</summary>
public sealed record EncerrarRevisaoRequest(string? Motivo, bool ConfirmacaoDeExclusao);
