namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Regras reconhecidas pelo catálogo.</summary>
public enum RegraDeArredondamentoDaNota
{
    [CodigoDeContrato("PRECISAO_TRUNCAR", "Trunca a nota na casa decimal definida",
        Descricao = "Descarta as casas decimais posteriores à precisão definida, sem arredondar.")]
    Truncar = 1,

    [CodigoDeContrato("PRECISAO_ARREDONDAR_A_PARTIR_DE_CINCO", "Arredonda a nota a partir de cinco",
        Descricao = "Arredonda para cima quando a casa seguinte é cinco ou mais.")]
    ArredondarAPartirDeCinco = 2,
}
