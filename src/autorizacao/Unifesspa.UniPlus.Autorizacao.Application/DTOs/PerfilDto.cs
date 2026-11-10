namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

/// <summary>Perfil de acesso como a API o expõe.</summary>
/// <param name="Nome">O nome corrente: o da versão vigente ou, sem vigente, o da pendente.</param>
/// <param name="Governanca">Papel de governança do perfil de sistema; nulo nos perfis comuns.</param>
/// <param name="Sensibilidade">A mais alta entre as permissões da composição em vigor (sem vigente, da pendente), derivada do catálogo.</param>
/// <param name="ComposicaoEmVigor">O que decide acesso hoje; nulo no perfil comum sem versão vigente.</param>
/// <param name="VersaoPendente">A versão que espera a aprovação; nula quando não há.</param>
/// <param name="UltimaVersaoEncerradaSemVigor">Preenchida quando a versão mais recente do perfil terminou sem entrar em vigor.</param>
/// <param name="Suspensao">A suspensão aberta; nula quando o perfil não está suspenso.</param>
/// <param name="DesativadoDesde">Quando o perfil foi desativado; nulo quando está ativo.</param>
public sealed record PerfilDto(
    Guid Id,
    string Nome,
    string Descricao,
    bool Ativo,
    string? Governanca,
    string Sensibilidade,
    ComposicaoEmVigorDto? ComposicaoEmVigor,
    VersaoPendenteDto? VersaoPendente,
    UltimaVersaoEncerradaSemVigorDto? UltimaVersaoEncerradaSemVigor,
    JanelaDoPerfilDto? Janela = null,
    SuspensaoDoPerfilDto? Suspensao = null,
    DateTimeOffset? DesativadoDesde = null);

/// <summary>Perfil de acesso como a listagem o resume.</summary>
/// <param name="TemVersaoVigente">O perfil comum tem composição em vigor; o de governança dispensa a versão e a tem sempre.</param>
/// <param name="TemVersaoPendente">Há versão esperando a aprovação.</param>
/// <param name="Suspensao">A suspensão aberta; nula quando o perfil não está suspenso.</param>
/// <param name="DesativadoDesde">Quando o perfil foi desativado; nulo quando está ativo.</param>
public sealed record PerfilListadoDto(
    Guid Id,
    string Nome,
    string Descricao,
    bool Ativo,
    string? Governanca,
    string Sensibilidade,
    bool TemVersaoVigente,
    bool TemVersaoPendente,
    SuspensaoDoPerfilDto? Suspensao,
    DateTimeOffset? DesativadoDesde);

/// <summary>A composição que decide acesso agora. Os campos da versão são nulos no perfil de governança, que não é versionado.</summary>
public sealed record ComposicaoEmVigorDto(
    Guid? VersaoId,
    int? Numero,
    IReadOnlyList<PermissaoDoPerfilDto> Permissoes,
    string? Protocolo,
    DateTimeOffset? DocumentoVerificadoEm,
    DateTimeOffset? Desde);

/// <summary>Versão pendente: o que ela passará a valer, o que a aprovação ainda espera e quem já decidiu.</summary>
/// <param name="ExpiraEm">O fim do prazo de 15 dias; a partir dele a versão não aprova mais.</param>
/// <param name="EtapaAtual">A etapa que falta decidir, pelo catálogo atual: <c>gestao_de_acessos</c> ou <c>protecao_de_dados</c>.</param>
/// <param name="ExigeProtecaoDeDados">O acréscimo é pessoal, identificador ou sensível, pelo catálogo atual.</param>
/// <param name="Acrescimo">As permissões que a versão acrescenta à em vigor.</param>
/// <param name="Remocao">As permissões que a versão retira da em vigor.</param>
/// <param name="Decisoes">As decisões do ciclo atual da aprovação.</param>
/// <param name="CriadorTemConcessao">Quem criou a versão tinha concessão do perfil.</param>
public sealed record VersaoPendenteDto(
    Guid VersaoId,
    int Numero,
    string Nome,
    string Descricao,
    IReadOnlyList<PermissaoDoPerfilDto> Permissoes,
    string Protocolo,
    Guid DocumentoId,
    string SituacaoDoDocumento,
    DateTimeOffset CriadaEm,
    DateTimeOffset ExpiraEm,
    string EtapaAtual,
    bool ExigeProtecaoDeDados,
    IReadOnlyList<PermissaoDoPerfilDto> Acrescimo,
    IReadOnlyList<PermissaoDoPerfilDto> Remocao,
    IReadOnlyList<DecisaoDaVersaoDto> Decisoes,
    bool CriadorTemConcessao);

/// <summary>Uma decisão dada a uma etapa da aprovação. O motivo da recusa não sai daqui.</summary>
public sealed record DecisaoDaVersaoDto(string Etapa, string Resultado, string? Nome, bool TemConcessao, DateTimeOffset Instante);

/// <summary>A versão mais recente do perfil, quando terminou sem entrar em vigor: recusada, cancelada ou expirada.</summary>
/// <param name="Situacao">A situação em que a versão terminou.</param>
/// <param name="Etapa">A etapa em que a aprovação parou.</param>
/// <param name="Instante">Quando a versão terminou.</param>
public sealed record UltimaVersaoEncerradaSemVigorDto(int Numero, string Situacao, string Etapa, DateTimeOffset Instante);

/// <summary>Permissão reunida por um perfil, com nome e sensibilidade lidos do catálogo.</summary>
public sealed record PermissaoDoPerfilDto(string Codigo, string Nome, string Sensibilidade);

/// <summary>Uma versão pendente na fila de aprovação de uma etapa, com o perfil a que pertence.</summary>
public sealed record ItemDaFilaDeVersoesDto(Guid PerfilId, string PerfilNome, VersaoPendenteDto Versao);
