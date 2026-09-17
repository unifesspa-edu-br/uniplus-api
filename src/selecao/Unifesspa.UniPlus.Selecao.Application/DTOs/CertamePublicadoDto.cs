namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>Tipo do processo, como congelado na publicação.</summary>
public sealed record TipoProcessoCertameDto(string Codigo, string Nome);

/// <summary>
/// Janela de inscrição e o identificador legível do edital. O número é opcional: nem toda
/// publicação o declara.
/// </summary>
public sealed record PeriodoInscricaoCertameDto(string? Numero, DateTimeOffset Inicio, DateTimeOffset Fim);

/// <summary>Município e fuso institucional congelados na publicação.</summary>
public sealed record LocalidadeCertameDto(string CodigoIbge, string Nome, string Uf, string FusoHorario);

/// <summary>
/// Taxa de inscrição. <see langword="null"/> no lugar deste bloco quando a publicação não
/// configurou taxa — a ausência é estado válido, não erro.
/// </summary>
public sealed record TaxaInscricaoCertameDto(bool Cobra, string? Valor, IReadOnlyList<string> Fundamentos);

/// <summary>
/// Resumo do documento do edital: o identificador do documento e o seu resumo criptográfico. O
/// endereço do arquivo no acervo público não trafega por aqui — o contrato o carregará como campo
/// próprio quando a cópia para o acervo existir.
/// </summary>
public sealed record DocumentoEditalCertameDto(Guid DocumentoEditalId, string HashSha256);

/// <summary>
/// Presente só quando o edital foi emendado: informa ao candidato que houve retificação e por quê.
/// </summary>
public sealed record RetificacaoCertameDto(Guid AtoRetificadoId, string Motivo);

/// <summary>
/// O certame como o cidadão o vê — projetado campo a campo da configuração congelada, nunca por
/// recorte de subárvore do documento: é a forma declarada que impede um bloco novo do domínio de
/// atravessar a fronteira pública por omissão.
/// </summary>
/// <remarks>
/// <see cref="AtoCriadorId"/> é o ato normativo que criou a versão servida. Ele acompanha a
/// resposta porque quem compõe a página do certame precisa casá-lo com a linha do tempo de
/// publicações — sem isso, "a linha do tempo está completa" e "a linha do tempo está atrasada"
/// seriam indistinguíveis para o consumidor.
/// </remarks>
public sealed record CertamePublicadoDto(
    Guid ProcessoSeletivoId,
    Guid AtoCriadorId,
    TipoProcessoCertameDto TipoProcesso,
    PeriodoInscricaoCertameDto Periodo,
    LocalidadeCertameDto Localidade,
    DocumentoEditalCertameDto DocumentoEdital,
    IReadOnlyList<Guid> Ofertas,
    TaxaInscricaoCertameDto? TaxaInscricao,
    RetificacaoCertameDto? Retificacao);
