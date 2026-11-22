namespace Unifesspa.UniPlus.Contratos.UnitTests.TestSupport.CodigosDeErro;

/// <summary>Um código de erro de fio com o status HTTP e o título que a API emite para ele.</summary>
internal sealed record EntradaDoCatalogoDeErros(string Codigo, int Status, string Titulo)
{
    internal string Descricao => $"{Status} \"{Titulo}\"";
}
