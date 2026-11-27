namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

internal sealed class ErrosDaFronteiraHttpDomainErrorRegistration : IDomainErrorRegistration
{
    private static readonly DomainErrorMapping[] Definicoes =
    [
        ErrosDaFronteiraHttp.NaoAutenticado,
        ErrosDaFronteiraHttp.AcessoNegado,
        ErrosDaFronteiraHttp.VersaoDeMidiaNaoSuportada,
        ErrosDaFronteiraHttp.IdDaRotaDivergenteDoCorpo,
        ErrosDaFronteiraHttp.ErroDeDominioNaoMapeado,
        ErrosDaFronteiraHttp.ConflitoDeConcorrencia,
        ErrosDaFronteiraHttp.CursorInvalido,
        ErrosDaFronteiraHttp.RegistroDeAcessoNaoGravado,
        ErrosDaFronteiraHttp.CatalogoDePermissoesDesatualizado,
        ErrosDaFronteiraHttp.EsperaPelaTravaEsgotada,
        ErrosDaFronteiraHttp.CaminhoInexistente,
        ErrosDaFronteiraHttp.MetodoNaoAceito,
        ErrosDaFronteiraHttp.CorpoAcimaDoLimite,
        ErrosDaFronteiraHttp.ErroInesperado,
    ];

    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
        Definicoes.Select(static definicao => new KeyValuePair<string, DomainErrorMapping>(definicao.Code, definicao));
}
