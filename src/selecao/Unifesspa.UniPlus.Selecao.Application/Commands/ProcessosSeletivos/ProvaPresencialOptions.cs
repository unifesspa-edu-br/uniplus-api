namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

public sealed class ProvaPresencialOptions
{
    public const string SectionName = "Selecao:ProvaPresencial";

    public int LimiteDeMunicipios { get; set; } = 100;
}
