namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

/// <summary>
/// Um relatório que o processo de resultado importado declara importar: o leiaute que ele segue, pelo código e
/// pela versão, e a posição em que a declaração o lista, a partir de 1.
/// </summary>
public sealed record RelatorioDeclarado(string LeiauteCodigo, string LeiauteVersao, int Posicao)
{
    /// <summary>O limite do código do leiaute: o mesmo do código estável de catálogo do documento da configuração.</summary>
    public const int LeiauteCodigoComprimentoMaximo = 50;

    /// <summary>O limite da versão do leiaute, o mesmo do documento da configuração.</summary>
    public const int LeiauteVersaoComprimentoMaximo = 16;
}

/// <summary>O relatório como a configuração o informa, ainda por conferir.</summary>
public sealed record RelatorioInformado(string? LeiauteCodigo, string? LeiauteVersao);
