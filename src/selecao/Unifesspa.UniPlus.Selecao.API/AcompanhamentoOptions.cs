namespace Unifesspa.UniPlus.Selecao.API;

/// <summary>Disponibilidade temporária das consultas de acompanhamento (issue #2111).</summary>
public sealed class AcompanhamentoOptions
{
    public const string SectionName = "Selecao:Acompanhamento";

    public bool ValidacaoHabilitada { get; set; }
}
