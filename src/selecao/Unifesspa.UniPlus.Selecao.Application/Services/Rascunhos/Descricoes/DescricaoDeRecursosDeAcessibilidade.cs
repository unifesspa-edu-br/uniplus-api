namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

public sealed class DescricaoDeRecursosDeAcessibilidade(IRecursoAcessibilidadeReader recursos) : IDescricaoDeCadastro
{
    public string Cadastro => CamposDeContextoDoDocumento.RecursosDeAcessibilidade;

    public async Task<IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>>> LerAsync(
        IReadOnlyCollection<(string Codigo, string? Complemento)> codigos, CancellationToken cancellationToken)
    {
        IReadOnlyList<RecursoAcessibilidadeView> vivos = await recursos.ListarVivosAsync(cancellationToken).ConfigureAwait(false);
        return AtributosDosCodigos.DosPedidos(vivos, codigos, static recurso => recurso.Codigo, static recurso => [recurso.Nome]);
    }
}
