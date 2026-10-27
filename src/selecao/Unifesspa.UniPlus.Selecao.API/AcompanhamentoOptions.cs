namespace Unifesspa.UniPlus.Selecao.API;

/// <summary>Disponibilidade temporária das consultas de acompanhamento, desligada por padrão.</summary>
public sealed class AcompanhamentoOptions
{
    public const string SectionName = "Selecao:Acompanhamento";

    public bool ValidacaoHabilitada { get; set; }
}
