namespace Unifesspa.UniPlus.Autorizacao.Application.DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Perfil de acesso como a API o expõe.</summary>
/// <param name="Nome">O nome corrente: o da versão vigente ou, sem vigente, o da pendente.</param>
/// <param name="Governanca">Papel de governança do perfil de sistema (<c>GESTOR_DE_ACESSOS</c>, <c>REPRESENTANTE_DO_CONTROLADOR</c> ou <c>GESTOR_DE_PROTECAO_DE_DADOS</c>); nulo nos perfis comuns.</param>
/// <param name="Sensibilidade">A mais alta entre as permissões da composição em vigor (sem vigente, da pendente), derivada do catálogo.</param>
/// <param name="SituacaoDoAcesso">Situação do acesso do perfil, como o enum <see cref="SituacaoDoAcessoDoPerfil"/> a explica.</param>
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
    CodigoComRotulo<TipoDeGovernanca>? Governanca,
    CodigoComRotulo<ClassificacaoDeProtecao> Sensibilidade,
    CodigoComRotulo<SituacaoDoAcessoDoPerfil> SituacaoDoAcesso,
    ComposicaoEmVigorDto? ComposicaoEmVigor,
    VersaoPendenteDto? VersaoPendente,
    UltimaVersaoEncerradaSemVigorDto? UltimaVersaoEncerradaSemVigor,
    SuspensaoDoPerfilDto? Suspensao,
    DateTimeOffset? DesativadoDesde,
    RevisaoDoPerfilDto? RevisaoDasConcessoes = null);

/// <summary>Perfil de acesso como a listagem o resume.</summary>
/// <param name="SituacaoDoAcesso">Situação do acesso do perfil, como o enum <see cref="SituacaoDoAcessoDoPerfil"/> a explica.</param>
/// <param name="TemVersaoPendente">Há versão esperando a aprovação.</param>
/// <param name="Suspensao">A suspensão aberta; nula quando o perfil não está suspenso.</param>
/// <param name="DesativadoDesde">Quando o perfil foi desativado; nulo quando está ativo.</param>
public sealed record PerfilListadoDto(
    Guid Id,
    string Nome,
    string Descricao,
    bool Ativo,
    CodigoComRotulo<TipoDeGovernanca>? Governanca,
    CodigoComRotulo<ClassificacaoDeProtecao> Sensibilidade,
    CodigoComRotulo<SituacaoDoAcessoDoPerfil> SituacaoDoAcesso,
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
/// <param name="EtapaAtual">A etapa que falta decidir, pelo catálogo atual: <c>GESTAO_DE_ACESSOS</c> ou <c>PROTECAO_DE_DADOS</c>.</param>
/// <param name="EtapasExigidas">As etapas que a aprovação exige, pelo catálogo atual: a gestão de acessos e, quando o acréscimo pede, a proteção de dados.</param>
/// <param name="ExigeProtecaoDeDados">O acréscimo é pessoal, identificador ou sensível, pelo catálogo atual.</param>
/// <param name="Acrescimo">As permissões que a versão acrescenta à em vigor.</param>
/// <param name="Remocao">As permissões que a versão retira da em vigor.</param>
/// <param name="Decisoes">As decisões do ciclo atual da aprovação.</param>
/// <param name="CriadorTemConcessao">Quem criou a versão tinha concessão do perfil.</param>
/// <param name="PropostaPorQuemLe">Quem lê é quem criou a versão; a identidade do autor não sai da API.</param>
public sealed record VersaoPendenteDto(
    Guid VersaoId,
    int Numero,
    string Nome,
    string Descricao,
    IReadOnlyList<PermissaoDoPerfilDto> Permissoes,
    string Protocolo,
    Guid DocumentoId,
    CodigoComRotulo<SituacaoDoAnexo> SituacaoDoDocumento,
    DateTimeOffset CriadaEm,
    DateTimeOffset ExpiraEm,
    CodigoComRotulo<EtapaDaVersao> EtapaAtual,
    IReadOnlyList<CodigoComRotulo<EtapaDaVersao>> EtapasExigidas,
    bool ExigeProtecaoDeDados,
    IReadOnlyList<PermissaoDoPerfilDto> Acrescimo,
    IReadOnlyList<PermissaoDoPerfilDto> Remocao,
    IReadOnlyList<DecisaoDaVersaoDto> Decisoes,
    bool CriadorTemConcessao,
    bool PropostaPorQuemLe);

/// <summary>Uma decisão dada a uma etapa da aprovação. O motivo da recusa não sai daqui.</summary>
public sealed record DecisaoDaVersaoDto(
    CodigoComRotulo<EtapaDaVersao> Etapa,
    CodigoComRotulo<ResultadoDaDecisao> Resultado,
    string? Nome,
    bool TemConcessao,
    DateTimeOffset Instante);

/// <summary>A versão mais recente do perfil, quando terminou sem entrar em vigor: recusada, cancelada ou expirada.</summary>
/// <param name="Situacao">A situação em que a versão terminou.</param>
/// <param name="Etapa">A etapa em que a aprovação parou.</param>
/// <param name="Instante">Quando a versão terminou.</param>
public sealed record UltimaVersaoEncerradaSemVigorDto(
    int Numero,
    CodigoComRotulo<SituacaoDaVersao> Situacao,
    CodigoComRotulo<EtapaDaVersao> Etapa,
    DateTimeOffset Instante);

/// <summary>Permissão reunida por um perfil, com nome e sensibilidade lidos do catálogo.</summary>
public sealed record PermissaoDoPerfilDto(string Codigo, string Nome, CodigoComRotulo<ClassificacaoDeProtecao> Sensibilidade);

/// <summary>Uma versão pendente na fila de aprovação de uma etapa, com o perfil a que pertence.</summary>
/// <param name="PropostaPorQuemLe">Quem lê é quem criou a versão, que não a aprova.</param>
public sealed record ItemDaFilaDeVersoesDto(Guid PerfilId, string PerfilNome, VersaoPendenteDto Versao, bool PropostaPorQuemLe);
