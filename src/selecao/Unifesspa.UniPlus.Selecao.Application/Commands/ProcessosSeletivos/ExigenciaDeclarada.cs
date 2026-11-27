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

    public static string? FormularioCodigo(this ItemDocumentoExigidoInput item) => item.Momento?.FormularioCodigo;

    public static string? Aplicabilidade(this ItemDocumentoExigidoInput item) => item.Aplicabilidade?.Tipo;

    public static IReadOnlyList<CondicaoGatilhoInput> Condicoes(this ItemDocumentoExigidoInput item) =>
        item.Aplicabilidade?.Condicoes ?? [];

    public static bool? Obrigatorio(this ItemDocumentoExigidoInput item) => item.Indeferimento?.Obrigatorio;

    public static bool? AbrePrazoParaReenvio(this ItemDocumentoExigidoInput item) => item.Indeferimento?.AbrePrazoParaReenvio;

    public static string? ConsequenciaDoDescumprimento(this ItemDocumentoExigidoInput item) => item.Indeferimento?.ConsequenciaDoDescumprimento;

    public static IReadOnlyList<BaseLegalInput> BasesLegais(this ItemDocumentoExigidoInput item) =>
        item.Indeferimento?.BasesLegais ?? [];

    public static JsonElement? FormatosPermitidos(this ItemDocumentoExigidoInput item) => item.Requisitos?.FormatosPermitidos;

    public static int? TamanhoMaximoBytes(this ItemDocumentoExigidoInput item) => item.Requisitos?.TamanhoMaximoBytes;

    public static PrazoDeEmissaoInput? PrazoDeEmissao(this ItemDocumentoExigidoInput item) => item.Requisitos?.PrazoDeEmissao;

    public static ValidadeDoDocumentoInput? Validade(this ItemDocumentoExigidoInput item) => item.Requisitos?.Validade;

    public static Guid? ModeloId(this ItemDocumentoExigidoInput item) => item.Requisitos?.Modelo?.ModeloId;

    public static int? QuantidadeMinima(this RegraDeComprovacaoInformada no) => no.Cardinalidade?.QuantidadeMinima;

    public static DistincaoDasApresentacoesInput? Distincao(this RegraDeComprovacaoInformada no) => no.Cardinalidade?.Distincao;

    public static string? ConsequenciaDoDescumprimentoDoGrupo(this RegraDeComprovacaoInformada no) => no.Indeferimento?.ConsequenciaDoDescumprimento;

    public static IReadOnlyList<BaseLegalInput> BasesLegaisDoGrupo(this RegraDeComprovacaoInformada no) =>
        no.Indeferimento?.BasesLegais ?? [];
}
