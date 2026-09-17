namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Par código/nome de um item de catálogo congelado na publicação — serve tanto ao tipo do
/// processo quanto ao tipo de uma etapa.
/// </summary>
/// <remarks>
/// Nome deliberadamente genérico: um schema chamado "tipo de processo" referenciado pelo campo
/// <c>tipoEtapa</c> diria ao integrador que o tipo de uma etapa é um tipo de processo.
/// </remarks>
public sealed record TipoCatalogadoCertameDto(string Codigo, string Nome);

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

/// <summary>A unidade que administra o certame, e onde ela fica.</summary>
public sealed record UnidadeAdministradoraCertameDto(
    string Sigla,
    string Nome,
    string Tipo,
    string? CidadeNome,
    string? CidadeUf);

/// <summary>Uma etapa do processo, como o edital a comunica.</summary>
/// <remarks>
/// Sem bancas, sem regras de recurso e sem produtos: bancas e produtos são maquinário de execução
/// interna, e a regra de recurso viaja no envelope como referência ao catálogo de regras, com
/// código, versão e resumo criptográfico — publicá-la exporia o modelo interno de regras a um
/// consumidor anônimo sem lhe dizer nada que o edital já não diga em prosa.
/// </remarks>
public sealed record EtapaCertameDto(
    string Nome,
    string Carater,
    TipoCatalogadoCertameDto TipoEtapa,
    string? Peso,
    string? NotaMinima,
    int? Ordem,
    string? FaseCodigo,
    DateTimeOffset? Inicio,
    DateTimeOffset? Fim,
    bool EmiteParecerIndividual);

/// <summary>Quantas vagas cada modalidade recebe numa oferta de curso.</summary>
public sealed record VagaPorModalidadeCertameDto(string ModalidadeCodigo, int Quantidade);

/// <summary>
/// O quadro de vagas de uma oferta de curso.
/// </summary>
/// <remarks>
/// Só o quadro e o total publicado. Os campos de conferência da distribuição — nominal, final,
/// estouro e o corte no volume de oferta — são o rastro aritmético de como o total foi alcançado, e
/// pertencem à auditoria da distribuição, não ao que o edital comunica.
/// </remarks>
public sealed record QuadroDeVagasCertameDto(
    Guid OfertaCursoOrigemId,
    IReadOnlyList<VagaPorModalidadeCertameDto> Quadro,
    int TotalPublicado);

/// <summary>Uma fase do cronograma, com a janela que o candidato precisa observar.</summary>
public sealed record FaseCronogramaCertameDto(
    int Ordem,
    string Codigo,
    DateTimeOffset? Inicio,
    DateTimeOffset? Fim,
    bool ColetaInscricao,
    bool ColetaSolicitacaoIsencao,
    bool PermiteComplementacao);

/// <summary>Formatos de arquivo aceitos numa exigência documental.</summary>
/// <remarks>
/// <see cref="Qualquer"/> verdadeiro e <see cref="Lista"/> nula são a mesma afirmação vista de dois
/// ângulos — o envelope os congela em bicondicional, e a projeção preserva a forma.
/// </remarks>
public sealed record FormatosAceitosCertameDto(bool Qualquer, IReadOnlyList<string>? Lista);

/// <summary>
/// Uma exigência documental, reduzida ao que o candidato precisa saber para reunir os documentos.
/// </summary>
/// <remarks>
/// Rótulo, aplicabilidade, obrigatoriedade e formatos aceitos, e nada mais. O que motiva
/// juridicamente cada exigência, a condição que a dispara, a fase em que ela incide e os metadados
/// dos fatos que a condicionam ficam fora: a página do certame não explica o requisito legal de
/// cada exigência.
/// <para>
/// <see cref="Aplicabilidade"/> acompanha <see cref="Obrigatorio"/> porque sozinha a
/// obrigatoriedade mente: uma exigência condicional incide apenas sobre quem satisfaz o gatilho, e
/// publicá-la como obrigatória faria o candidato de ampla concorrência reunir documento que não lhe
/// é pedido.
/// </para>
/// </remarks>
public sealed record ExigenciaDocumentalCertameDto(
    string Rotulo,
    string Aplicabilidade,
    bool Obrigatorio,
    FormatosAceitosCertameDto Formatos);

/// <summary>Uma condição de atendimento especializado ofertada no certame.</summary>
public sealed record CondicaoAtendimentoCertameDto(string Codigo, string Nome);

/// <summary>
/// O atendimento especializado ofertado: as condições que o candidato pode declarar, os recursos
/// disponíveis e os tipos de deficiência contemplados.
/// </summary>
public sealed record AtendimentoCertameDto(
    IReadOnlyList<CondicaoAtendimentoCertameDto> Condicoes,
    IReadOnlyList<string> Recursos,
    IReadOnlyList<CondicaoAtendimentoCertameDto> TiposDeficiencia);

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
/// <para>
/// <see cref="Nome"/> é o título do certame no instante da publicação. Ele não vive na configuração
/// congelada — é atributo do processo —, e congelá-lo aqui é o que impede a página pública de
/// exibir um título editado depois, sob uma retificação que ainda não tem publicidade.
/// </para>
/// <para>
/// <see cref="VersaoProjecao"/> e <see cref="HashConfiguracao"/> são o selo do conteúdo, e viajam
/// no corpo porque quem compõe a página precisa deles para montar o próprio selo. O hash sozinho
/// não basta: ele identifica o ENVELOPE congelado, e acrescentar um campo a esta projeção não o
/// altera — um cache endereçado só por ele continuaria servindo a resposta anterior depois do
/// deploy. A versão da projeção é o que separa as duas.
/// </para>
/// </remarks>
public sealed record CertamePublicadoDto(
    Guid ProcessoSeletivoId,
    Guid AtoCriadorId,
    string Nome,
    string VersaoProjecao,
    string HashConfiguracao,
    TipoCatalogadoCertameDto TipoProcesso,
    PeriodoInscricaoCertameDto Periodo,
    LocalidadeCertameDto Localidade,
    UnidadeAdministradoraCertameDto UnidadeAdministradora,
    DocumentoEditalCertameDto DocumentoEdital,
    IReadOnlyList<Guid> Ofertas,
    IReadOnlyList<string> ModalidadesOfertadas,
    IReadOnlyList<QuadroDeVagasCertameDto> Vagas,
    IReadOnlyList<EtapaCertameDto> Etapas,
    string OrigemCandidatos,
    IReadOnlyList<FaseCronogramaCertameDto> CronogramaFases,
    IReadOnlyList<ExigenciaDocumentalCertameDto> DocumentosExigidos,
    AtendimentoCertameDto Atendimento,
    TaxaInscricaoCertameDto? TaxaInscricao,
    RetificacaoCertameDto? Retificacao);
