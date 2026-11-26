namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

public sealed class DescricaoDeTiposDeFase(IFaseCanonicaReader fases) : IDescricaoDeCadastro
{
    public string Cadastro => CamposDeContextoDoDocumento.TiposDeFase;

    public async Task<IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>>> LerAsync(
        IReadOnlyCollection<(string Codigo, string? Complemento)> codigos, CancellationToken cancellationToken)
    {
        IReadOnlyList<FaseCanonicaView> vivas = await fases.ListarVivasPorCodigosAsync(AtributosDosCodigos.Textos(codigos), cancellationToken).ConfigureAwait(false);
        return AtributosDosCodigos.DosPedidos(vivas, codigos, static fase => fase.Codigo, static fase => [fase.Nome]);
    }
}
