namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

/// <summary>A revisão das concessões do perfil, com a lista das concessões avaliadas.</summary>
/// <param name="Situacao">
/// <c>aberta</c> enquanto há pendência e o prazo não venceu; <c>encerrando</c> quando já não há o que decidir e a próxima
/// operação ou a rotina a encerra; <c>encerrada</c> depois disso.
/// </param>
/// <param name="Forma">Como a revisão terminou: <c>concluida</c>, <c>prazo</c> ou <c>manual</c>; nulo enquanto não terminou.</param>
/// <param name="DecisoesEnviadas">A gestão de acessos já enviou as decisões.</param>
public sealed record RevisaoDasConcessoesDto(
    Guid Id,
    Guid PerfilId,
    int VersaoNova,
    int VersaoAlvo,
    DateTimeOffset AbertaEm,
    DateTimeOffset TerminaEm,
    DateTimeOffset? EncerradaEm,
    string Situacao,
    string? Forma,
    bool DecisoesEnviadas,
    IReadOnlyList<ConcessaoDaRevisaoDto> Itens);

/// <summary>
/// Uma concessão da revisão. A situação é <c>aguardando_decisoes</c> para a elegível que ainda não tem linha, ou a da linha:
/// <c>retida</c>, <c>aguardando_protecao_de_dados</c>, <c>migrada</c>, <c>recusada</c>, <c>nao_migrada</c> ou <c>fora_da_lista</c>.
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
    string Situacao,
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
public sealed record RevisaoDoPerfilDto(Guid Id, DateTimeOffset TerminaEm, string Situacao);

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
