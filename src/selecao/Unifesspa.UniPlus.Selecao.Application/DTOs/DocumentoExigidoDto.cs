namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Text.Json;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// DTO de leitura de <see cref="Domain.Entities.DocumentoExigido"/>. Compõe <c>ProcessoSeletivoDto</c> — não há
/// rota aninhada própria de leitura. As consequências incluem código e rótulo; o pedido recebe o código.
/// Nome, categoria e proteção do tipo são cópias congeladas e não são lidos na escrita.
/// </summary>
/// <param name="Id">Identidade da exigência.</param>
/// <param name="TipoDocumento">O tipo de documento, com os atributos congelados na exigência.</param>
/// <param name="Momento">Fase, etapa e formulário em que o documento é coletado.</param>
/// <param name="Aplicabilidade">A quem a exigência se aplica e sob qual gatilho.</param>
/// <param name="Indeferimento">A obrigatoriedade, a consequência do indeferimento e as bases legais.</param>
/// <param name="Requisitos">O que restringe o arquivo enviado.</param>
/// <param name="FatosComprovados">Os fatos do processo que o documento comprova; voltam no PUT.</param>
public sealed record DocumentoExigidoDto(
    Guid Id,
    TipoDocumentoDaExigenciaDto TipoDocumento,
    MomentoDaExigenciaDto Momento,
    AplicabilidadeDaExigenciaDto Aplicabilidade,
    IndeferimentoDoDocumentoDto Indeferimento,
    RequisitosDoDocumentoDto Requisitos,
    IReadOnlyList<string> FatosComprovados)
{
    /// <summary>Chave local que identifica a exigência no processo.</summary>
    public required string Chave { get; init; }
}

/// <summary>O tipo de documento do catálogo, copiado por valor na exigência; só <see cref="Id"/> volta no PUT.</summary>
public sealed record TipoDocumentoDaExigenciaDto(
    Guid Id, string Codigo, string Nome, string Categoria, ProtecaoDeDadosDto Protecao);

/// <summary>
/// A proteção de dados congelada na exigência, copiada do tipo de documento (não volta no PUT).
/// </summary>
/// <param name="Classificacao"><c>PUBLICO</c>, <c>INTERNO</c>, <c>PESSOAL</c>, <c>IDENTIFICADOR</c> ou <c>SENSIVEL</c>.</param>
/// <param name="Finalidade">A finalidade do tratamento.</param>
/// <param name="HipoteseLegal">A hipótese legal do tratamento.</param>
public sealed record ProtecaoDeDadosDto(string Classificacao, string Finalidade, string HipoteseLegal);

/// <param name="FaseId">Fase do cronograma que exige o documento.</param>
/// <param name="EtapaId">Etapa da fase que coleta o documento; nula quando é da fase inteira.</param>
/// <param name="Finalidade">O formulário a que o documento pertence (<c>INSCRICAO</c>, <c>ISENCAO_TAXA</c> ou <c>HABILITACAO</c>); nula quando a fase não responde formulário nenhum.</param>
public sealed record MomentoDaExigenciaDto(Guid FaseId, Guid? EtapaId, string? Finalidade);

/// <param name="Tipo">Código (<c>GERAL</c> ou <c>CONDICIONAL</c>) e rótulo da aplicabilidade.</param>
/// <param name="Condicoes">As condições do gatilho.</param>
public sealed record AplicabilidadeDaExigenciaDto(CodigoComRotulo<Aplicabilidade> Tipo, IReadOnlyList<CondicaoGatilhoDto> Condicoes);

public sealed record IndeferimentoDoDocumentoDto(bool Obrigatorio, CodigoComRotulo<ConsequenciaDoIndeferimento>? Consequencia, IReadOnlyList<BaseLegalDto> BasesLegais);

/// <summary>
/// O que restringe o arquivo enviado. <see cref="FormatosPermitidos"/> é o mesmo valor JSON polimórfico do
/// pedido — a string <c>"QUALQUER"</c> ou o array de <c>{formato, tamanhoMaximoBytesMax}</c> —, devolvido sem
/// transformação para o cliente reenviar.
/// </summary>
public sealed record RequisitosDoDocumentoDto(
    JsonElement FormatosPermitidos,
    int? TamanhoMaximoBytes,
    PrazoDeEmissaoDto? PrazoDeEmissao,
    ValidadeDoDocumentoDto? Validade,
    ModeloDaExigenciaDto? Modelo);

/// <summary>
/// O modelo de documento que a exigência oferece ao candidato. O <see cref="ModeloId"/> volta no
/// PUT, que substitui as exigências inteiras: omiti-lo retira o modelo da exigência.
/// </summary>
/// <param name="Formato"><c>DOCX</c> ou <c>ODT</c>.</param>
public sealed record ModeloDaExigenciaDto(Guid ModeloId, string NomeArquivo, CodigoComRotulo<FormatoDeDocumento> Formato, string HashSha256);

/// <summary>
/// DTO de leitura de <see cref="Domain.Entities.CondicaoGatilho"/> (Story #554, PR #896,
/// issue #892). <see cref="Operador"/>/<see cref="Valor"/> seguem o mesmo formato flat de
/// <c>CondicaoGatilhoInput</c> (comando de escrita) — <see cref="Valor"/> é o texto JSON
/// canônico (<c>JsonElement.GetRawText()</c>), round-tripável direto de volta pelo mesmo
/// PUT (<c>ConferenciaDasCondicoesDaExigencia</c> o reparseia como JSON).
/// </summary>
public sealed record CondicaoGatilhoDto(Guid Id, int Clausula, string Fato, [property: AceitaCodigosDe<Operador>] string Operador, string Valor);

/// <summary>
/// DTO de leitura de <see cref="Domain.Entities.DocumentoExigidoBaseLegal"/> (Story #554,
/// PR #898, issue #549). Mesmo formato flat de <c>BaseLegalInput</c> (comando de escrita) —
/// round-trip GET→PUT direto, incluindo bases <c>PENDENTE</c> (a projeção "somente
/// RESOLVIDO" é interna ao domínio — <c>DocumentoExigido.BasesLegaisResolvidas</c> — e da
/// materialização do envelope na PR #903, não deste DTO de edição).
/// </summary>
public sealed record BaseLegalDto(Guid Id, string Referencia, CodigoComRotulo<TipoAbrangencia> Abrangencia, CodigoComRotulo<StatusBaseLegal> Situacao, string? Observacao);

/// <summary>Regra de comprovação com os documentos e itens que a compõem.</summary>
public sealed record RegraDeComprovacaoDto(
    Guid Id,
    CodigoComRotulo<FormaDeComprovacao> Forma,
    DocumentoExigidoDto? Documento,
    CardinalidadeQualificadaDto Cardinalidade,
    IndeferimentoDoGrupoDto Indeferimento,
    IReadOnlyList<RegraDeComprovacaoDto> Itens,
    string? GrupoDoFormularioParaRepeticao);

/// <summary>Quantas apresentações a regra pede e como elas se distinguem.</summary>
public sealed record CardinalidadeQualificadaDto(int? QuantidadeMinima, DistincaoDasApresentacoesDto? Distincao);

/// <summary>Como as apresentações se distinguem: a chave, a data de referência e as ocorrências esperadas.</summary>
public sealed record DistincaoDasApresentacoesDto(CodigoComRotulo<CriterioDeDistincaoDasApresentacoes>? CriterioDeDistincao, DateOnly? DataReferencia, IReadOnlyList<string>? OcorrenciasEsperadas);

/// <summary>A consequência do indeferimento do grupo e as bases legais dela.</summary>
public sealed record IndeferimentoDoGrupoDto(CodigoComRotulo<ConsequenciaDoIndeferimento>? Consequencia, IReadOnlyList<BaseLegalDto> BasesLegais);
