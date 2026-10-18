namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Text.Json.Serialization;

using Domain.Interfaces;

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
public sealed record LocalidadeCertameDto(string CodigoIbge, string Nome, string Uf, FusoHorarioDto FusoHorario);

/// <summary>
/// Taxa de inscrição. <see langword="null"/> no lugar deste bloco quando a publicação não
/// configurou taxa — a ausência é estado válido, não erro.
/// </summary>
public sealed record TaxaInscricaoCertameDto(bool Cobra, string? Valor, IReadOnlyList<string> Fundamentos);

/// <summary>
/// O documento do edital: o identificador do documento, o seu resumo criptográfico e o endereço de
/// onde baixá-lo.
/// </summary>
/// <param name="UrlDownload">
/// Endereço do arquivo no acervo público (ADR-0132): permanente, de leitura anônima e nunca
/// pré-assinado. É do ato que publicou a versão, e continua servindo o mesmo arquivo depois de uma
/// retificação. O certame só é divulgado depois de a cópia para o acervo concluir, então o endereço
/// nunca aponta para arquivo ausente.
/// </param>
public sealed record DocumentoEditalCertameDto(Guid DocumentoEditalId, string HashSha256, Uri UrlDownload);

/// <summary>
/// Um documento de um ato no acervo público: o edital ou um anexo dele. É a forma que o contrato
/// público usa para todo documento de ato, e a que os atos avulsos reaproveitam.
/// </summary>
/// <param name="Papel"><c>principal</c> para o documento que é o próprio ato, <c>anexo</c> para o que o acompanha.</param>
/// <param name="Nome">Nome com que o arquivo é salvo por quem o baixa.</param>
/// <param name="TipoDeMidia">Tipo de mídia do arquivo.</param>
/// <param name="TamanhoBytes">Tamanho do arquivo, em bytes.</param>
/// <param name="HashSha256">Hash SHA-256 do arquivo em hexadecimal minúsculo, o que o ato atestou.</param>
/// <param name="UrlDownload">Endereço permanente e de leitura anônima, como o de <see cref="DocumentoEditalCertameDto"/>.</param>
public sealed record DocumentoDoAtoCertameDto(
    string Papel,
    string Nome,
    string TipoDeMidia,
    long TamanhoBytes,
    string HashSha256,
    Uri UrlDownload);

/// <summary>
/// O edital de uma versão já divulgada do certame: o ato que a criou, o endereço do arquivo no
/// acervo público e os documentos dele.
/// </summary>
/// <param name="AtoId">O ato normativo que criou a versão — a chave que a linha do tempo de publicações já tem.</param>
/// <param name="UrlDownload">Endereço permanente e de leitura anônima, como o de <see cref="DocumentoEditalCertameDto"/>.</param>
/// <param name="Documentos">
/// O edital como documento principal e os anexos em seguida, na ordem em que o edital os apresenta.
/// Vazia quando o registro da divulgação não traz os documentos.
/// </param>
public sealed record EditalPublicadoCertameDto(Guid AtoId, Uri UrlDownload, IReadOnlyList<DocumentoDoAtoCertameDto> Documentos);

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
/// Uma oferta de curso do certame, com o que a nomeia para o candidato — curso, grau, unidade
/// ofertante e turnos —, como estava na definição da distribuição de vagas publicada.
/// </summary>
public sealed record OfertaDeCursoCertameDto(
    Guid OfertaCursoOrigemId,
    string CursoNome,
    string CursoGrau,
    string UnidadeOfertanteNome,
    IReadOnlyList<string> Turnos);

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
    FormatosAceitosCertameDto Formatos,
    ModeloDocumentalCertameDto? Modelo);

/// <summary>
/// O modelo editável que a exigência oferece ao candidato, como o edital o congelou: o nome do
/// arquivo, o formato, o hash que atesta o conteúdo e o endereço de onde baixá-lo.
/// </summary>
/// <param name="Formato"><c>DOCX</c> ou <c>ODT</c>.</param>
/// <param name="UrlDownload">
/// Endereço do arquivo no acervo público (ADR-0132): permanente, de leitura anônima e nunca
/// pré-assinado. É do ato que publicou o modelo, e continua servindo o mesmo arquivo depois de uma
/// retificação.
/// </param>
public sealed record ModeloDocumentalCertameDto(string NomeArquivo, string Formato, string HashSha256, Uri UrlDownload);

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
/// <see cref="IdentificadorLegivel"/> é o endereço público do certame, lido da configuração
/// congelada — nunca do cadastro vivo: o endereço de um certame publicado não muda quando o cadastro
/// muda, e é por ele que a página pública e o acervo localizam o certame.
/// </para>
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
/// <para>
/// <see cref="Situacao"/> é a única parte da resposta que não vem da projeção: depende do instante
/// da leitura, e por isso é calculada a cada consulta e nunca guardada no documento divulgado.
/// </para>
/// <para>
/// <see cref="EditaisPublicados"/> é a outra parte que a leitura acrescenta: os documentos de cada ato
/// divulgado do processo, que cresce a cada retificação e a cada ato que não cria versão e por isso não
/// pertence ao documento da versão vigente.
/// </para>
/// </remarks>
public sealed record CertamePublicadoDto(
    Guid ProcessoSeletivoId,
    string IdentificadorLegivel,
    Guid AtoCriadorId,
    string Nome,
    string VersaoProjecao,
    string HashConfiguracao,
    TipoCatalogadoCertameDto TipoProcesso,
    PeriodoInscricaoCertameDto Periodo,
    LocalidadeCertameDto Localidade,
    UnidadeAdministradoraCertameDto UnidadeAdministradora,
    DocumentoEditalCertameDto DocumentoEdital,
    IReadOnlyList<OfertaDeCursoCertameDto> Ofertas,
    IReadOnlyList<string> ModalidadesOfertadas,
    IReadOnlyList<QuadroDeVagasCertameDto> Vagas,
    IReadOnlyList<EtapaCertameDto> Etapas,
    string OrigemCandidatos,
    IReadOnlyList<FaseCronogramaCertameDto> CronogramaFases,
    IReadOnlyList<ExigenciaDocumentalCertameDto> DocumentosExigidos,
    AtendimentoCertameDto Atendimento,
    TaxaInscricaoCertameDto? TaxaInscricao,
    RetificacaoCertameDto? Retificacao)
{
    /// <summary>
    /// Situação da janela de inscrição no instante da leitura, pela mesma regra da vitrine.
    /// </summary>
    /// <remarks>
    /// Fica fora do construtor e é omitida quando nula: o documento divulgado é gravado sem ela, e a
    /// leitura a acrescenta. Na resposta da consulta ela está sempre presente.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SituacaoDoCertame? Situacao { get; init; }

    /// <summary>
    /// Os documentos de cada ato divulgado do processo: o edital de cada versão, da mais antiga à mais
    /// recente, seguido dos atos que não criam versão. O de <see cref="DocumentoEdital"/> é o da versão
    /// vigente e também consta aqui.
    /// </summary>
    /// <remarks>
    /// Mesma forma de <see cref="Situacao"/>: fora do construtor e omitida quando nula, para que o
    /// documento já divulgado, gravado sem ela, continue legível. Na resposta da consulta ela está
    /// sempre presente — vazia quando nenhum edital foi registrado ainda.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<EditalPublicadoCertameDto>? EditaisPublicados { get; init; }
}
