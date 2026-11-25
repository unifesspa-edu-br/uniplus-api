namespace Unifesspa.UniPlus.Configuracao.Contracts;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Uma base legal de uma exigência do modelo; tokens de abrangência e situação como os da exigência do processo.</summary>
public sealed record BaseLegalDoModeloInput(string Referencia, [property: AceitaCodigosDe<TipoAbrangencia>] string Abrangencia, [property: AceitaCodigosDe<StatusBaseLegal>] string Situacao, string? Observacao);

/// <summary>
/// O documento exigido uma vez por opção que o processo oferece para o fato, salvo as listadas em
/// <paramref name="Exceto"/>: cada cópia vale para quem declarou aquela opção.
/// </summary>
public sealed record ExigenciaPorOpcaoInput(string FatoCodigo, IReadOnlyList<string?>? Exceto);

/// <summary>
/// Um documento do conjunto padrão do modelo: tipo do catálogo e fase canônica pelo código,
/// condições sobre fatos no formato das exibições (ausentes, vale para todo candidato), indeferimento e requisitos.
/// A aplicação do modelo copia o conjunto para os documentos exigidos do processo;
/// com <paramref name="PorOpcao"/>, uma cópia por opção que o processo oferece para o fato.
/// </summary>
public sealed record DocumentoExigidoDoModeloInput(
    string FaseCodigo,
    string TipoDocumentoCodigo,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Quando,
    IndeferimentoDoDocumentoDoModeloInput? Indeferimento,
    RequisitosDoDocumentoDoModeloInput? Requisitos = null,
    ExigenciaPorOpcaoInput? PorOpcao = null);

/// <summary>
/// O indeferimento do documento exigido do modelo: se o documento é obrigatório, a consequência de o
/// candidato não o apresentar e as bases legais que a sustentam.
/// </summary>
public sealed record IndeferimentoDoDocumentoDoModeloInput(bool Obrigatorio, [property: AceitaCodigosDe<ConsequenciaDoIndeferimento>] string Consequencia, IReadOnlyList<BaseLegalDoModeloInput?>? BasesLegais);

/// <summary>
/// O que o modelo escolhe sobre a emissão e a validade do documento. Omitir um dos dois, ou os dois, deixa a
/// escolha para a unidade do processo.
/// </summary>
public sealed record RequisitosDoDocumentoDoModeloInput(
    PrazoDeEmissaoDoModeloInput? PrazoDeEmissao,
    ValidadeDoDocumentoDoModeloInput? Validade);

/// <summary>
/// O indeferimento de um grupo de documentos do modelo: a consequência de o candidato não apresentar a
/// quantidade mínima de documentos do grupo e as bases legais que a sustentam.
/// </summary>
public sealed record IndeferimentoDoGrupoDeDocumentosDoModeloInput([property: AceitaCodigosDe<ConsequenciaDoIndeferimento>] string Consequencia, IReadOnlyList<BaseLegalDoModeloInput?>? BasesLegais);

/// <summary>
/// Documentos dos quais o candidato apresenta ao menos a quantidade mínima: quantidade ausente vale um e o
/// grupo tem um nível só. Cada documento do grupo tem o formato do documento avulso. A aplicação do modelo
/// copia o grupo para o processo como uma regra de quantidade mínima de itens; que a quantidade caiba nos
/// documentos que o processo recebe quem confere é o processo.
/// </summary>
/// <param name="QuantidadeMinima">Quantos documentos do grupo o candidato apresenta; ausente vale um.</param>
/// <param name="Indeferimento">A consequência de a quantidade não ser atingida; ausente, o grupo não determina resultado.</param>
/// <param name="Documentos">Os documentos do grupo.</param>
public sealed record GrupoDeDocumentosDoModeloInput(
    int? QuantidadeMinima,
    IndeferimentoDoGrupoDeDocumentosDoModeloInput? Indeferimento,
    IReadOnlyList<DocumentoExigidoDoModeloInput>? Documentos);

/// <summary>
/// O conjunto padrão de documentos do modelo: os documentos avulsos e os grupos de documentos. Quem lê o
/// conjunto na Seleção percorre os documentos por <see cref="TodosOsDocumentos"/>, para que o de um grupo
/// conte como o avulso.
/// </summary>
/// <param name="DocumentosExigidos">Os documentos exigidos avulsos.</param>
/// <param name="GruposDeDocumentos">Os grupos de documentos.</param>
public sealed record ConjuntoDeDocumentosDoModeloInput(
    IReadOnlyList<DocumentoExigidoDoModeloInput>? DocumentosExigidos,
    IReadOnlyList<GrupoDeDocumentosDoModeloInput>? GruposDeDocumentos)
{
    /// <summary>O caminho do conjunto no conteúdo do modelo.</summary>
    public const string Caminho = "conjuntoDeDocumentos";

    /// <summary>O caminho do documento avulso na posição dada.</summary>
    public static string CaminhoDoDocumento(int documento) => $"{Caminho}.documentosExigidos[{documento}]";

    /// <summary>O caminho do grupo de documentos na posição dada.</summary>
    public static string CaminhoDoGrupo(int grupo) => $"{Caminho}.gruposDeDocumentos[{grupo}]";

    /// <summary>O caminho do documento na posição dada do grupo.</summary>
    public static string CaminhoDoDocumentoDoGrupo(int grupo, int documento) => $"{CaminhoDoGrupo(grupo)}.documentos[{documento}]";

    /// <summary>Cada documento, avulso ou de grupo, com o caminho dele no conteúdo do modelo.</summary>
    public IEnumerable<(string Caminho, DocumentoExigidoDoModeloInput Documento)> TodosOsDocumentos() =>
        (DocumentosExigidos ?? []).Select(static (documento, i) => (CaminhoDoDocumento(i), documento))
            .Concat((GruposDeDocumentos ?? []).SelectMany(static (grupo, g) =>
                (grupo?.Documentos ?? []).Select((documento, j) => (CaminhoDoDocumentoDoGrupo(g, j), documento))));
}
