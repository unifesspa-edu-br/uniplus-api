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
        Dictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>> atributos = [];
        foreach ((string Codigo, string? Complemento) codigo in codigos)
        {
            TipoProcessoView? tipo = await tipos.ObterAtivoPorCodigoAsync(codigo.Codigo, cancellationToken).ConfigureAwait(false);
            if (tipo is not null)
            {
                atributos.Add(codigo, [tipo.Nome]);
            }
        }

        return atributos;
    }
}
