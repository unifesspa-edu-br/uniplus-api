namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

internal sealed class ErrosDaFronteiraHttpDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
        new[]
        {
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
            ErrosDaFronteiraHttp.ErroInesperado,
        }.Select(static definicao => new KeyValuePair<string, DomainErrorMapping>(definicao.Code, definicao));
}
