namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DescricaoDeOfertasDeCurso(IReadOnlyDictionary<string, OfertaDeCursoCitada> ofertas) : IDescricaoDeCadastro
{
    public string Cadastro => CamposDeContextoDoDocumento.OfertasDeCurso;

    public Task<IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>>> LerAsync(
        IReadOnlyCollection<(string Codigo, string? Complemento)> codigos, CancellationToken cancellationToken) =>
        Task.FromResult(AtributosDosCodigos.DosPedidos(ofertas.Values, codigos, static oferta => oferta.Codigo, Atributos));

    private static IReadOnlyList<object?> Atributos(OfertaDeCursoCitada oferta) =>
        [oferta.CursoNome, oferta.CursoGrau, oferta.UnidadeOfertanteNome, oferta.Turnos];
}
