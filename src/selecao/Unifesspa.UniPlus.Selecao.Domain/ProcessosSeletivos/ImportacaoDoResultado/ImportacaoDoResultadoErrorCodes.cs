namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

/// <summary>Os códigos de recusa da importação do resultado declarada pelo processo (UNI-REQ-0144, UNI-REQ-0151).</summary>
public static class ImportacaoDoResultadoErrorCodes
{
    /// <summary>O processo de resultado importado ainda não declara os relatórios que importa.</summary>
    public const string PorDeclarar = "ImportacaoDoResultado.PorDeclarar";

    /// <summary>O processo declara a importação e nenhum relatório.</summary>
    public const string SemRelatorio = "ImportacaoDoResultado.SemRelatorio";

    /// <summary>O processo com inscrição pelo sistema declara importação de resultado.</summary>
    public const string EmInscricaoPeloSistema = "ImportacaoDoResultado.EmInscricaoPeloSistema";

    /// <summary>O relatório declarado não informa o código ou a versão do leiaute.</summary>
    public const string RelatorioSemLeiaute = "ImportacaoDoResultado.RelatorioSemLeiaute";

    /// <summary>O leiaute já está declarado como relatório do processo.</summary>
    public const string RelatorioRepetido = "ImportacaoDoResultado.RelatorioRepetido";

    /// <summary>O catálogo de leiautes não tem o leiaute declarado, com a versão informada.</summary>
    public const string LeiauteDesconhecido = "ImportacaoDoResultado.LeiauteDesconhecido";

    /// <summary>O processo de resultado importado não declara a primeira fase depois da importação.</summary>
    public const string PrimeiraFasePorDeclarar = "ImportacaoDoResultado.PrimeiraFasePorDeclarar";

    /// <summary>A primeira fase declarada depois da importação não é fase do cronograma do processo.</summary>
    public const string PrimeiraFaseForaDoCronograma = "ImportacaoDoResultado.PrimeiraFaseForaDoCronograma";

    /// <summary>Um campo de formulário coleta fato que um relatório do processo traz no arquivo.</summary>
    public const string FatoColetadoPorFormulario = "ImportacaoDoResultado.FatoColetadoPorFormulario";

    /// <summary>O formulário coleta dado básico que algum relatório não traz inteiro, e não é o obrigatório da primeira fase depois da importação.</summary>
    public const string DadoBasicoComplementarForaDaPrimeiraFase = "ImportacaoDoResultado.DadoBasicoComplementarForaDaPrimeiraFase";
}
