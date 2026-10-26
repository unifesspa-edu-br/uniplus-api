namespace Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>Códigos de erro do envio ao INEP e da importação das notas do ENEM.</summary>
public static class NotasDoEnemErrorCodes
{
    public const string EtapaNaoEncontrada = "NotasDoEnem.EtapaNaoEncontrada";
    public const string EtapaSemNotaDoEnem = "NotasDoEnem.EtapaSemNotaDoEnem";
    public const string EdicaoNaoAdmitida = "NotasDoEnem.EdicaoNaoAdmitida";
    public const string LeiauteIndisponivel = "NotasDoEnem.LeiauteIndisponivel";
    public const string EdicaoSemInscritos = "NotasDoEnem.EdicaoSemInscritos";
    public const string ArquivoVazio = "NotasDoEnem.ArquivoVazio";
    public const string ArquivoGrandeDemais = "NotasDoEnem.ArquivoGrandeDemais";
    public const string TipoDeArquivoNaoAceito = "NotasDoEnem.TipoDeArquivoNaoAceito";
    public const string LoteNaoEncontrado = "NotasDoEnem.LoteNaoEncontrado";
    public const string ArquivoForaDoLeiaute = "NotasDoEnem.ArquivoForaDoLeiaute";
    public const string CodigoDeSituacaoDesconhecido = "NotasDoEnem.CodigoDeSituacaoDesconhecido";
}
