namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

public sealed class DescricaoDeOfertasDeCurso(IOfertaCursoReader ofertas) : IDescricaoDeCadastro
{
    public string Cadastro => CamposDeContextoDoDocumento.OfertasDeCurso;

    public async Task<IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>>> LerAsync(
        IReadOnlyCollection<(string Codigo, string? Complemento)> codigos, CancellationToken cancellationToken)
    {
        IReadOnlyList<OfertaCursoView> vivas = await ofertas.ListarVivasAsync(cancellationToken).ConfigureAwait(false);
        return AtributosDosCodigos.DosPedidos(vivas, codigos, static oferta => oferta.Codigo, Atributos);
    }

    private static IReadOnlyList<object?> Atributos(OfertaCursoView oferta) =>
        [oferta.CursoNome, oferta.CursoGrau, oferta.UnidadeOfertanteNome, oferta.Turnos];
}
