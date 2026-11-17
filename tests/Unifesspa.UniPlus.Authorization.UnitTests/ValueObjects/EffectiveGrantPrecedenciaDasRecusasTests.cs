namespace Unifesspa.UniPlus.Authorization.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Quando a entrada traz dois defeitos, a recusa segue a ordem: escopo informado (identificador vazio ou tipo de resource
/// inválido), permissão, identificador da concessão e, por último, a regra da origem. Cada linha combina dois defeitos de
/// classes diferentes e afirma o código da recusa que a ordem manda.
/// </summary>
public sealed class EffectiveGrantPrecedenciaDasRecusasTests
{
    private const string Permissao = "selecao.editais.publicar";

    private static readonly EscopoDaPermissaoEfetiva DaUnidade = EscopoDaPermissaoEfetiva.DaUnidade(Guid.CreateVersion7()).Value!;
    private static readonly EscopoDaPermissaoEfetiva SemEscopo = EscopoDaPermissaoEfetiva.Nenhum;
    private static readonly EscopoDaPermissaoEfetiva UnidadeEInstituicao =
        EscopoDaPermissaoEfetiva.Criar(Guid.CreateVersion7(), null, null, null, institucional: true).Value!;

    public static TheoryData<string, Func<Result<EffectiveGrant>>, string> DoisDefeitos()
    {
        TheoryData<string, Func<Result<EffectiveGrant>>, string> linhas = [];
        AdicionarConcessaoDePerfil(linhas);
        AdicionarConcessaoGravada(linhas);
        AdicionarVinculoDeGrupo(linhas);
        AdicionarExcepcional(linhas);

        return linhas;
    }

    private static ConcessaoAtiva Gravada(Guid? unidadeId, bool institucional, Guid? concessaoId = null) =>
        new(concessaoId ?? Guid.CreateVersion7(), Guid.CreateVersion7(), null, "Perfil", unidadeId, institucional, null, [Permissao]);

    private static void AdicionarConcessaoDePerfil(TheoryData<string, Func<Result<EffectiveGrant>>, string> linhas)
    {
        linhas.Add("perfil: permissão e identificador", () => EffectiveGrant.DaConcessaoDePerfil(" ", DaUnidade, Guid.Empty), AuthorizationErrorCodes.EffectiveGrantPermissaoObrigatoria);
        linhas.Add("perfil: permissão e escopo ausente", () => EffectiveGrant.DaConcessaoDePerfil(" ", SemEscopo), AuthorizationErrorCodes.EffectiveGrantPermissaoObrigatoria);
        linhas.Add("perfil: permissão e escopo ambíguo", () => EffectiveGrant.DaConcessaoDePerfil(" ", UnidadeEInstituicao), AuthorizationErrorCodes.EffectiveGrantPermissaoObrigatoria);
        linhas.Add("perfil: identificador e escopo ausente", () => EffectiveGrant.DaConcessaoDePerfil(Permissao, SemEscopo, Guid.Empty), AuthorizationErrorCodes.EffectiveGrantGrantIdInvalido);
        linhas.Add("perfil: identificador e escopo ambíguo", () => EffectiveGrant.DaConcessaoDePerfil(Permissao, UnidadeEInstituicao, Guid.Empty), AuthorizationErrorCodes.EffectiveGrantGrantIdInvalido);
    }

    private static void AdicionarConcessaoGravada(TheoryData<string, Func<Result<EffectiveGrant>>, string> linhas)
    {
        linhas.Add("gravada: unidade vazia e permissão", () => EffectiveGrant.DaConcessao(" ", Gravada(Guid.Empty, false)), AuthorizationErrorCodes.EffectiveGrantEscopoInvalido);
        linhas.Add("gravada: unidade vazia e identificador", () => EffectiveGrant.DaConcessao(Permissao, Gravada(Guid.Empty, false, Guid.Empty)), AuthorizationErrorCodes.EffectiveGrantEscopoInvalido);
        linhas.Add("gravada: unidade vazia e instituição", () => EffectiveGrant.DaConcessao(Permissao, Gravada(Guid.Empty, true)), AuthorizationErrorCodes.EffectiveGrantEscopoInvalido);
        linhas.Add("gravada: permissão e identificador", () => EffectiveGrant.DaConcessao(" ", Gravada(null, true, Guid.Empty)), AuthorizationErrorCodes.EffectiveGrantPermissaoObrigatoria);
        linhas.Add("gravada: permissão e escopo ausente", () => EffectiveGrant.DaConcessao(null, Gravada(null, false)), AuthorizationErrorCodes.EffectiveGrantPermissaoObrigatoria);
        linhas.Add("gravada: permissão e escopo ambíguo", () => EffectiveGrant.DaConcessao("", Gravada(Guid.CreateVersion7(), true)), AuthorizationErrorCodes.EffectiveGrantPermissaoObrigatoria);
        linhas.Add("gravada: identificador e escopo ausente", () => EffectiveGrant.DaConcessao(Permissao, Gravada(null, false, Guid.Empty)), AuthorizationErrorCodes.EffectiveGrantGrantIdInvalido);
        linhas.Add("gravada: identificador e escopo ambíguo", () => EffectiveGrant.DaConcessao(Permissao, Gravada(Guid.CreateVersion7(), true, Guid.Empty)), AuthorizationErrorCodes.EffectiveGrantGrantIdInvalido);
    }

    private static void AdicionarVinculoDeGrupo(TheoryData<string, Func<Result<EffectiveGrant>>, string> linhas)
    {
        linhas.Add("grupo: permissão e identificador", () => EffectiveGrant.DoVinculoDeGrupo(" ", DaUnidade, DateTimeOffset.UnixEpoch, Guid.Empty), AuthorizationErrorCodes.EffectiveGrantPermissaoObrigatoria);
        linhas.Add("grupo: permissão e validade", () => EffectiveGrant.DoVinculoDeGrupo(null, DaUnidade, null), AuthorizationErrorCodes.EffectiveGrantPermissaoObrigatoria);
        linhas.Add("grupo: identificador e validade", () => EffectiveGrant.DoVinculoDeGrupo(Permissao, DaUnidade, null, Guid.Empty), AuthorizationErrorCodes.EffectiveGrantGrantIdInvalido);
    }

    private static void AdicionarExcepcional(TheoryData<string, Func<Result<EffectiveGrant>>, string> linhas)
    {
        linhas.Add("excepcional: permissão e identificador", () => EffectiveGrant.Excepcional(" ", DaUnidade, DateTimeOffset.UnixEpoch, grantId: Guid.Empty), AuthorizationErrorCodes.EffectiveGrantPermissaoObrigatoria);
        linhas.Add("excepcional: permissão e validade", () => EffectiveGrant.Excepcional("", DaUnidade, null), AuthorizationErrorCodes.EffectiveGrantPermissaoObrigatoria);
        linhas.Add("excepcional: permissão e escopo sem restrição", () => EffectiveGrant.Excepcional(null, EscopoDaPermissaoEfetiva.Instituicao, DateTimeOffset.UnixEpoch), AuthorizationErrorCodes.EffectiveGrantPermissaoObrigatoria);
        linhas.Add("excepcional: identificador e validade", () => EffectiveGrant.Excepcional(Permissao, DaUnidade, null, grantId: Guid.Empty), AuthorizationErrorCodes.EffectiveGrantGrantIdInvalido);
        linhas.Add("excepcional: identificador e escopo sem restrição", () => EffectiveGrant.Excepcional(Permissao, SemEscopo, DateTimeOffset.UnixEpoch, grantId: Guid.Empty), AuthorizationErrorCodes.EffectiveGrantGrantIdInvalido);
        linhas.Add("excepcional: validade e escopo sem restrição", () => EffectiveGrant.Excepcional(Permissao, SemEscopo, null), AuthorizationErrorCodes.EffectiveGrantValidadeObrigatoria);
    }

    [Theory(DisplayName = "Com dois defeitos, a recusa segue a ordem: escopo informado, permissão, identificador, regra da origem")]
    [MemberData(nameof(DoisDefeitos))]
    public void Fabrica_DoisDefeitos_RecusaPelaOrdem(string defeitos, Func<Result<EffectiveGrant>> fabrica, string codigoEsperado)
    {
        ArgumentNullException.ThrowIfNull(fabrica);

        Result<EffectiveGrant> resultado = fabrica();

        resultado.Error!.Code.Should().Be(codigoEsperado, defeitos);
    }
}
