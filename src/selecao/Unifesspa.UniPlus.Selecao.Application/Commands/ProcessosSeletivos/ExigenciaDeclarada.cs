namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json;

/// <summary>
/// Leitura do pedido de exigências sem que cada chamador trate a ausência dos objetos aninhados: o objeto
/// ausente equivale ao objeto com todos os campos ausentes, e é o domínio que recusa o que falta.
/// </summary>
internal static class ExigenciaDeclarada
{
    public static Guid TipoDocumentoId(this ItemDocumentoExigidoInput item) => item.TipoDocumento?.Id ?? Guid.Empty;

    public static Guid FaseId(this ItemDocumentoExigidoInput item) => item.Momento?.FaseId ?? Guid.Empty;

    public static Guid? EtapaId(this ItemDocumentoExigidoInput item) => item.Momento?.EtapaId;

    public static string? Finalidade(this ItemDocumentoExigidoInput item) => item.Momento?.Finalidade;

    public static string? Aplicabilidade(this ItemDocumentoExigidoInput item) => item.Aplicabilidade?.Tipo;

    public static IReadOnlyList<CondicaoGatilhoInput> Condicoes(this ItemDocumentoExigidoInput item) =>
        item.Aplicabilidade?.Condicoes ?? [];

    public static bool Obrigatorio(this ItemDocumentoExigidoInput item) => item.Indeferimento?.Obrigatorio ?? false;

    public static string? Consequencia(this ItemDocumentoExigidoInput item) => item.Indeferimento?.Consequencia;

    public static IReadOnlyList<BaseLegalInput> BasesLegais(this ItemDocumentoExigidoInput item) =>
        item.Indeferimento?.BasesLegais ?? [];

    public static JsonElement? FormatosPermitidos(this ItemDocumentoExigidoInput item) => item.Requisitos?.FormatosPermitidos;

    public static int? TamanhoMaximoBytes(this ItemDocumentoExigidoInput item) => item.Requisitos?.TamanhoMaximoBytes;

    public static IdadeMaximaEmissaoInput? IdadeMaximaEmissao(this ItemDocumentoExigidoInput item) =>
        item.Requisitos?.IdadeMaximaEmissao;

    public static Guid? ModeloId(this ItemDocumentoExigidoInput item) => item.Requisitos?.Modelo?.ModeloId;

    public static int? QuantidadeMinima(this NoExigenciaInput no) => no.Cardinalidade?.QuantidadeMinima;

    public static DistincaoDasApresentacoesInput? Distincao(this NoExigenciaInput no) => no.Cardinalidade?.Distincao;

    public static string? ConsequenciaDoGrupo(this NoExigenciaInput no) => no.Indeferimento?.Consequencia;

    public static IReadOnlyList<BaseLegalInput> BasesLegaisDoGrupo(this NoExigenciaInput no) =>
        no.Indeferimento?.BasesLegais ?? [];
}
