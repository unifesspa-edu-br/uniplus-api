namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que o comando de publicação declara ao registrar um ato do processo, crie ele versão de
/// configuração ou não.
/// </summary>
/// <param name="AtoId">Identificador do ato, decidido pela Seleção — o mesmo que viaja na requisição de registro.</param>
/// <param name="ProcessoSeletivoId">O processo a que o ato pertence.</param>
/// <param name="TipoCodigo">Código do tipo do ato no catálogo de Publicações.</param>
/// <param name="TipoNome">Nome do tipo, como o catálogo o declarava na conferência.</param>
/// <param name="Numero">Número do ato, quando o tipo o tem.</param>
/// <param name="DataDoAto">Data que o documento declara.</param>
/// <param name="Assinante">Quem assina o ato.</param>
/// <param name="UnidadeAdministradora">
/// Slug da unidade administradora da versão vigente divulgada — segmento da chave do documento no acervo.
/// </param>
/// <param name="IdentificadorLegivelDoProcesso">
/// Identificador legível do processo na versão vigente divulgada — segmento da chave do documento no acervo.
/// </param>
/// <param name="DocumentoPrincipalId">
/// O documento a publicar confirmado que é o principal do ato — o do edital, no ato que cria versão.
/// </param>
/// <param name="Anexos">Anexos do ato, na ordem declarada.</param>
/// <param name="ItemDoEdital">
/// Identidade do item de fase ou de etapa que o ato cumpre, quando é um resultado declarado no edital.
/// </param>
/// <param name="AtoCorrigidoId">O ato que este corrige, quando é errata ou retificação.</param>
/// <param name="MotivoDaCorrecao">Motivo da correção; obrigatório com <paramref name="AtoCorrigidoId"/>.</param>
/// <param name="PoliticaDeDivulgacaoConfirmadaPorSub">
/// Sub do usuário que confirmou que o documento segue a política de divulgação do certame — pedida
/// na publicação de resultado. Nulo quando a confirmação não foi pedida.
/// </param>
public sealed record NovoAtoDoProcesso(
    Guid AtoId,
    Guid ProcessoSeletivoId,
    string TipoCodigo,
    string TipoNome,
    string? Numero,
    DateOnly DataDoAto,
    string Assinante,
    string UnidadeAdministradora,
    string IdentificadorLegivelDoProcesso,
    Guid DocumentoPrincipalId,
    IReadOnlyList<AnexoDeclarado> Anexos,
    IdentidadeDoItemDoEdital? ItemDoEdital = null,
    Guid? AtoCorrigidoId = null,
    string? MotivoDaCorrecao = null,
    string? PoliticaDeDivulgacaoConfirmadaPorSub = null);
