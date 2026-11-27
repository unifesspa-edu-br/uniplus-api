namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>As recusas na forma que os testes da leitura e da criação do modelo comparam: o campo e o código de cada uma.</summary>
internal static class RecusasParaTeste
{
    private static readonly Dictionary<string, string> SemFormatos = new(StringComparer.Ordinal);

    public static (string Campo, string Codigo)[] Campos(Result<Guid> resultado) =>
        [.. resultado.Errors.Select(static e => (e.Field!, e.Error.Code))];

    public static (string Campo, string Codigo)[] DaLeitura(ConteudoDoModeloInput entrada) => [.. Ler(entrada).Recusas.Select(static e => (e.Field!, e.Error.Code))];

    public static LeituraDoConteudo Ler(ConteudoDoModeloInput entrada)
    {
        LeituraDoConteudo leitura = new(SemFormatos);
        leitura.Ler(entrada);
        return leitura;
    }

    public static (string Campo, string Codigo)[] DaLeituraComOConteudo(ConteudoDoModeloInput entrada, out ConteudoLido lido)
    {
        LeituraDoConteudo leitura = new(SemFormatos);
        lido = leitura.Ler(entrada);
        return [.. leitura.Recusas.Select(static e => (e.Field!, e.Error.Code))];
    }
}
