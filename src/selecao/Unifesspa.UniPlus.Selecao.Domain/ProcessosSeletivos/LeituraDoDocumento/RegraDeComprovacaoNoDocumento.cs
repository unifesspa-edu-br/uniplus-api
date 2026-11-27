namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Uma regra de comprovação como o documento a traz, com a posição que tinha na lista enviada: a forma, a exigência pela chave, a
/// quantidade mínima, a consequência e os itens do grupo. Cada item do grupo é uma regra do mesmo formato, com a posição dentro do
/// grupo pai. A regra de documento cita a exigência; a de grupo traz os itens.
/// </summary>
public sealed record RegraDeComprovacaoNoDocumento(int PosicaoNoDocumento, FormaDeComprovacao? Forma, string? ExigenciaChave)
{
    public int? QuantidadeMinima { get; init; }

    public bool? AbrePrazoParaReenvio { get; init; }

    public ConsequenciaDoDescumprimento? ConsequenciaDoDescumprimento { get; init; }

    public IReadOnlyList<BaseLegalNoDocumento> BasesLegais { get; init; } = [];

    public CriterioDeDistincaoDasApresentacoes? CriterioDeDistincao { get; init; }

    public DateOnly? DataReferencia { get; init; }

    public IReadOnlyList<string> OcorrenciasEsperadas { get; init; } = [];

    public string? GrupoDoFormularioParaRepeticao { get; init; }

    public IReadOnlyList<RegraDeComprovacaoNoDocumento> Itens { get; init; } = [];
}
