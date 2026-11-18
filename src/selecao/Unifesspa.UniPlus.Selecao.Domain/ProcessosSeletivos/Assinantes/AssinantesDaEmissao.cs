namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Assinantes;

using System.Collections.Immutable;

/// <summary>Um assinante tal como a emissão o gravou: a posição, o código da pessoa e o nome daquele momento.</summary>
internal sealed record AssinanteDaEmissao(int Posicao, string Codigo, string Nome);

/// <summary>
/// Os assinantes que valeram numa emissão, em ordem. É uma cópia: mudar o cadastro depois não a altera,
/// e é ela que o documento emitido guarda.
/// </summary>
internal sealed record AssinantesDaEmissao
{
    private AssinantesDaEmissao(ImmutableArray<AssinanteDaEmissao> assinantes) => Assinantes = assinantes;

    internal IReadOnlyList<AssinanteDaEmissao> Assinantes { get; }

    internal static AssinantesDaEmissao Criar(IEnumerable<PessoaDoCadastroDeAssinantes> pessoasEmOrdem) =>
        new([.. pessoasEmOrdem.Select(static (pessoa, indice) => new AssinanteDaEmissao(indice + 1, pessoa.Codigo, pessoa.Nome))]);
}
