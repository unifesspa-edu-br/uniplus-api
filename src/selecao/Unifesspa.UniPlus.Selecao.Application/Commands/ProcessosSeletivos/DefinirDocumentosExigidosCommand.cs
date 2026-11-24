namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.ComponentModel;
using System.Text.Json;

using Domain.Enums;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Entrada de uma condição do gatilho DNF, usada por <see cref="AplicabilidadeDaExigenciaInput"/>.
/// <see cref="Operador"/>/<see cref="Valor"/> seguem o mesmo formato de <c>CondicaoPrecondicaoInput</c>
/// (códigos declarados em <see cref="Regras.Enums.Operador"/>; <see cref="Valor"/> é texto,
/// interpretado como JSON quando possível).
/// </summary>
public sealed record CondicaoGatilhoInput(int Clausula, string Fato, [AceitaCodigosDe<Operador>] string? Operador, string Valor);

/// <summary>
/// Entrada de uma base legal (ADR-0074), usada por <see cref="IndeferimentoDoDocumentoInput"/> e por
/// <see cref="IndeferimentoDoGrupoInput"/>. <see cref="Abrangencia"/>/<see cref="Status"/>
/// são códigos canônicos (<see cref="Domain.Enums.TipoAbrangencia"/>/
/// <see cref="Domain.Enums.StatusBaseLegal"/>). A validação de <b>gate</b> (≥1
/// RESOLVIDO por exigência que determina resultado) é da publicação, não deste PUT —
/// aqui só a forma de cada item é validada. No pedido, use os códigos de abrangência e situação lidos no GET.
/// </summary>
public sealed record BaseLegalInput(string Referencia, string Abrangencia, string Situacao, string? Observacao)
{
    /// <summary>O caminho da lista de bases legais no pedido, igual na exigência de documento e no grupo.</summary>
    public const string CaminhoDaLista = "indeferimento.basesLegais";
}

/// <summary>O tipo de documento do catálogo que a exigência pede; o handler lê dele só a identidade.</summary>
/// <param name="Id">Identidade do tipo de documento no catálogo.</param>
public sealed record TipoDocumentoDaExigenciaInput(Guid Id);

/// <summary>
/// O momento em que o documento é coletado: a fase, a etapa dela quando a fase se subdivide e o formulário.
/// </summary>
/// <param name="FaseId">Fase do cronograma que exige o documento. A raiz confere que ela é do processo.</param>
/// <param name="EtapaId">
/// Etapa dessa fase em que o documento é coletado; ausente, o documento é da fase inteira. A raiz confere
/// que a etapa é do processo e acontece naquela fase.
/// </param>
/// <param name="Finalidade">
/// Código do formulário lido no GET, a que o documento pertence (<c>INSCRICAO</c>, <c>ISENCAO_TAXA</c> ou <c>HABILITACAO</c>):
/// obrigatória quando a fase responde algum formulário, e um dos que ela responde; ausente quando a fase não
/// responde nenhum. É ela que diz em qual formulário o candidato apresenta o documento, porque inscrição e
/// isenção podem dividir a fase.
/// </param>
public sealed record MomentoDaExigenciaInput(
    Guid FaseId, Guid? EtapaId = null, [property: AceitaCodigosDe<FinalidadeFormulario>] string? Finalidade = null)
{
    /// <summary>O caminho da finalidade no pedido.</summary>
    public const string CaminhoDaFinalidade = "momento.finalidade";
}

/// <summary>
/// A quem a exigência se aplica: <c>GERAL</c> ou <c>CONDICIONAL</c>. A lista de condições vazia ou ausente é
/// coerente com <c>GERAL</c> e com <c>CONDICIONAL</c> "exigida de ninguém".
/// </summary>
/// <param name="Tipo">Código <c>GERAL</c> ou <c>CONDICIONAL</c>, lido no GET; ausente, o domínio recusa a exigência.</param>
/// <param name="Condicoes">As condições do gatilho, em forma normal disjuntiva.</param>
public sealed record AplicabilidadeDaExigenciaInput([property: AceitaCodigosDe<Aplicabilidade>] string? Tipo, IReadOnlyList<CondicaoGatilhoInput>? Condicoes)
{
    /// <summary>O caminho do tipo da aplicabilidade no pedido.</summary>
    public const string CaminhoDoTipo = "aplicabilidade.tipo";

    /// <summary>O caminho da lista de condições no pedido.</summary>
    public const string CaminhoDasCondicoes = "aplicabilidade.condicoes";
}

/// <summary>O que acontece quando o documento não é apresentado, e a base legal da obrigação.</summary>
/// <param name="Obrigatorio">Se a falta do documento impede a inscrição de seguir.</param>
/// <param name="Consequencia">Código da consequência do indeferimento, lido no GET; ausente, a exigência não determina resultado.</param>
/// <param name="BasesLegais">As bases legais da exigência; ausentes valem lista vazia, válida na escrita e pendente na publicação quando exigida.</param>
public sealed record IndeferimentoDoDocumentoInput(bool Obrigatorio, string? Consequencia, IReadOnlyList<BaseLegalInput>? BasesLegais)
{
    /// <summary>O caminho da consequência no pedido.</summary>
    public const string CaminhoDaConsequencia = "indeferimento.consequencia";
}

/// <summary>O modelo de documento que a exigência oferece ao candidato; o handler lê dele só a identidade.</summary>
/// <param name="ModeloId">Identidade do modelo, que precisa ser deste processo.</param>
public sealed record ModeloDaExigenciaInput(Guid ModeloId);

/// <summary>
/// O que restringe o arquivo que o candidato envia: formatos, tamanho, prazo de emissão, validade e modelo.
/// </summary>
/// <param name="FormatosPermitidos">
/// Valor JSON polimórfico: a string <c>"QUALQUER"</c> ou um array de itens, cada um um texto ou um objeto
/// <c>{formato, tamanhoMaximoBytesMax}</c>. Ausente (propriedade ausente ou <c>null</c>) é recusado com
/// <c>FormatosPermitidos.Obrigatorio</c>; presente em forma inválida, com <c>FormatosPermitidos.FormaInvalida</c>.
/// </param>
/// <param name="TamanhoMaximoBytes">Tamanho máximo do arquivo, em bytes, quando presente.</param>
/// <param name="PrazoDeEmissao">
/// Prazo de emissão do documento; omitido, a escolha é apagada e a publicação a recusa como pendente.
/// </param>
/// <param name="Validade">
/// Validade do documento; omitida, a escolha é apagada e a publicação a recusa como pendente.
/// </param>
/// <param name="Modelo">Modelo que a exigência oferece; ausente, a exigência não tem modelo.</param>
public sealed record RequisitosDoDocumentoInput(
    [property: Description("Código do formato permitido; devolva só o código de cada formato lido no GET. Informe QUALQUER ou uma lista de códigos ou objetos com formato e tamanhoMaximoBytesMax.")]
    JsonElement? FormatosPermitidos,
    int? TamanhoMaximoBytes,
    PrazoDeEmissaoInput? PrazoDeEmissao,
    ValidadeDoDocumentoInput? Validade,
    ModeloDaExigenciaInput? Modelo)
{
    /// <summary>O caminho do tamanho máximo no pedido.</summary>
    public const string CaminhoDoTamanhoMaximo = "requisitos.tamanhoMaximoBytes";
}

/// <summary>
/// Entrada de uma exigência documental, usada por <see cref="RegraDeComprovacaoInformada"/>. O handler resolve o tipo
/// de documento contra o módulo Configuração e congela os atributos vigentes por valor (ADR-0061): o cliente
/// não os declara.
/// </summary>
/// <param name="TipoDocumento">O tipo de documento exigido.</param>
/// <param name="Momento">Quando e em qual formulário o documento é coletado.</param>
/// <param name="Aplicabilidade">A quem a exigência se aplica e sob qual gatilho.</param>
/// <param name="Indeferimento">A obrigatoriedade, a consequência do indeferimento e as bases legais.</param>
/// <param name="Requisitos">O que restringe o arquivo enviado.</param>
/// <param name="Chave">A chave devolvida pela leitura identifica a exigência a preservar.</param>
/// <param name="FatosComprovados">
/// Códigos dos fatos do próprio processo que o documento comprova; só eles contam para conferir a proteção do
/// tipo de documento, e o fato que apenas aciona a exigência pelo gatilho não entra. A publicação recusa a
/// exigência que ficar menos protegida que o fato mais protegido entre os comprovados.
/// </param>
public sealed record ItemDocumentoExigidoInput(
    TipoDocumentoDaExigenciaInput? TipoDocumento,
    MomentoDaExigenciaInput? Momento,
    AplicabilidadeDaExigenciaInput? Aplicabilidade,
    IndeferimentoDoDocumentoInput? Indeferimento,
    RequisitosDoDocumentoInput? Requisitos,
    IReadOnlyList<string>? FatosComprovados = null,
    string? Chave = null);

/// <summary>
/// Como as apresentações da mesma exigência se distinguem, na forma do <see cref="DistincaoDasApresentacoes"/>
/// do domínio: <c>COMPETENCIA_MENSAL</c>/<c>EXERCICIO_ANUAL</c> (calendário derivável, exige
/// <see cref="DataReferencia"/>) ou <c>OCORRENCIA</c> (<see cref="OcorrenciasEsperadas"/> opcional).
/// </summary>
/// <param name="CriterioDeDistincao">Código do critério de distinção lido no GET (<see cref="Domain.Enums.CriterioDeDistincaoDasApresentacoes"/>).</param>
/// <param name="DataReferencia">Data a partir da qual o calendário é derivado.</param>
/// <param name="OcorrenciasEsperadas">Identificadores das ocorrências esperadas.</param>
public sealed record DistincaoDasApresentacoesInput(string? CriterioDeDistincao, DateOnly? DataReferencia, IReadOnlyList<string>? OcorrenciasEsperadas)
{
    /// <summary>O caminho da distinção no pedido.</summary>
    public const string Caminho = "cardinalidade.distincao";
}

/// <summary>
/// Quantas apresentações a exigência pede. A quantidade conta APRESENTAÇÕES, não arquivos; a distinção é
/// exclusiva da regra de documento. Ausente, a contagem não exige identificação específica.
/// </summary>
/// <param name="QuantidadeMinima">Quantidade mínima de apresentações; ausente vale 1 no domínio.</param>
/// <param name="Distincao">Como as apresentações se distinguem entre si.</param>
public sealed record CardinalidadeQualificadaInput(int? QuantidadeMinima = null, DistincaoDasApresentacoesInput? Distincao = null);

/// <summary>A consequência do indeferimento de um grupo com quantidade mínima de itens e as bases legais dela.</summary>
/// <param name="Consequencia">Código da consequência lido no GET; só o grupo com quantidade mínima de itens a declara.</param>
/// <param name="BasesLegais">As bases legais, só quando a consequência está declarada.</param>
public sealed record IndeferimentoDoGrupoInput(string? Consequencia = null, IReadOnlyList<BaseLegalInput>? BasesLegais = null);

/// <summary>Regra informada para comprovar uma exigência documental.</summary>
public sealed record RegraDeComprovacaoInformada(
    [property: AceitaCodigosDe<FormaDeComprovacao>] string? Forma,
    ItemDocumentoExigidoInput? Documento = null,
    CardinalidadeQualificadaInput? Cardinalidade = null,
    IndeferimentoDoGrupoInput? Indeferimento = null,
    IReadOnlyList<RegraDeComprovacaoInformada>? Itens = null,
    string? GrupoDoFormularioParaRepeticao = null);

/// <summary>
/// Substitui integralmente as regras de comprovação documental do processo.
/// </summary>
public sealed record DefinirDocumentosExigidosCommand(
    Guid ProcessoSeletivoId,
    IReadOnlyList<RegraDeComprovacaoInformada> RegrasDeComprovacao) : ICommand<Result>;
