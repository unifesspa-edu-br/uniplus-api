namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

/// <summary>A janela de migração do perfil, com a lista de concessões que ela decide.</summary>
/// <param name="Situacao">
/// <c>aberta</c> enquanto há pendência e o prazo não venceu; <c>encerrando</c> quando já não há o que decidir e a próxima
/// operação ou a rotina a encerra; <c>encerrada</c> depois disso.
/// </param>
/// <param name="Forma">Como a janela terminou: <c>concluida</c>, <c>prazo</c> ou <c>manual</c>; nulo enquanto não terminou.</param>
/// <param name="LoteEnviado">O lote da gestão de acessos já foi enviado.</param>
public sealed record JanelaDeMigracaoDto(
    Guid Id,
    Guid PerfilId,
    int VersaoNova,
    int VersaoAlvo,
    DateTimeOffset AbertaEm,
    DateTimeOffset Prazo,
    DateTimeOffset? EncerradaEm,
    string Situacao,
    string? Forma,
    bool LoteEnviado,
    IReadOnlyList<ItemDaJanelaDto> Itens);

/// <summary>
/// Uma concessão da janela. A situação é <c>aguardando_lote</c> para a elegível que ainda não tem linha, ou a da linha:
/// <c>retida</c>, <c>aguardando_protecao_de_dados</c>, <c>migrada</c>, <c>recusada</c>, <c>nao_migrada</c> ou <c>fora_da_lista</c>.
/// </summary>
/// <param name="Acrescimo">Os códigos que a versão-alvo acrescenta ao que a concessão dá hoje.</param>
/// <param name="EhDoLeitor">A concessão é de quem lê, que não pode decidi-la.</param>
public sealed record ItemDaJanelaDto(
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

/// <summary>Uma janela de migração na fila de uma etapa, com o perfil a que pertence. Não traz nome de beneficiário.</summary>
/// <param name="VersaoAlvo">O número da versão para a qual as concessões migram.</param>
/// <param name="Prazo">O fim do prazo da janela.</param>
/// <param name="LoteEnviado">O lote da gestão de acessos já foi enviado.</param>
/// <param name="QuantidadeADecidir">Quantas concessões quem lê pode decidir naquela etapa.</param>
public sealed record ItemDaFilaDeJanelasDto(
    Guid PerfilId,
    string PerfilNome,
    int VersaoAlvo,
    DateTimeOffset Prazo,
    bool LoteEnviado,
    int QuantidadeADecidir);

/// <summary>A janela como o perfil a resume.</summary>
public sealed record JanelaDoPerfilDto(Guid Id, DateTimeOffset Prazo, string Situacao);

/// <summary>Uma decisão do lote, da retida ou da etapa da proteção de dados: migrar ou recusar a concessão.</summary>
public sealed record DecisaoDaMigracaoRequest(Guid ConcessaoId, string? Decisao);

/// <summary>O lote da gestão de acessos: uma decisão por concessão e a confirmação de que recusar revoga.</summary>
public sealed record EnviarLoteDaMigracaoRequest(IReadOnlyList<DecisaoDaMigracaoRequest>? Decisoes, bool ConfirmacaoDeExclusao);

/// <summary>A decisão sobre a concessão retida.</summary>
public sealed record DecidirRetidaRequest(string? Decisao, bool ConfirmacaoDeExclusao);

/// <summary>A remessa da proteção de dados: de uma concessão a várias.</summary>
public sealed record DecidirEtapaDaProtecaoRequest(IReadOnlyList<DecisaoDaMigracaoRequest>? Decisoes, bool ConfirmacaoDeExclusao);

/// <summary>O encerramento manual da janela, com motivo e a confirmação de que quem não migrou perde o acesso.</summary>
public sealed record EncerrarJanelaRequest(string? Motivo, bool ConfirmacaoDeExclusao);
