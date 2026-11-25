namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

public sealed class DescricaoDeTiposDeProcesso(ITipoProcessoReader tipos) : IDescricaoDeCadastro
{
    public string Cadastro => CamposDeContextoDoDocumento.TiposDeProcesso;

    public async Task<IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>>> LerAsync(
        IReadOnlyCollection<(string Codigo, string? Complemento)> codigos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigos);
        IReadOnlyDictionary<string, string> nomes = await tipos.ObterNomesPorCodigosAsync(
            [.. codigos.Select(codigo => codigo.Codigo).Distinct()], cancellationToken).ConfigureAwait(false);
        return codigos.Where(codigo => nomes.ContainsKey(codigo.Codigo))
            .ToDictionary(codigo => codigo, codigo => (IReadOnlyList<object?>)[nomes[codigo.Codigo]]);
    }
}
