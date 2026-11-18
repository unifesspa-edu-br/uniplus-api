namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os cadastros citados são lidos uma vez, com um só instante de referência, em que vale a vigência do que se lê;
/// os cadastros vivos são consultados em sequência, sem visão isolada entre eles, e o congelamento os relê.
/// </summary>
/// <remarks>
/// O Geo fora do ar, ou com qualquer resposta que não diga se o município existe, é falha de infraestrutura (503), nunca "município
/// inexistente": a conferência do Geo é de falha fechada, e com ele fora nem o rascunho importado é gravado. O município que ele
/// devolve inconsistente (de outra UF que a do prefixo do código, ou com nome que o domínio recusa) é a mesma falha.
/// </remarks>
public sealed class LeituraDosCadastrosCitados(
    IUnidadeReader unidades,
    ITipoAtoPublicadoReader tiposDeAto,
    IRegraCatalogoReader regras,
    IConferenciaEnderecoGeo geo,
    LeituraDosCadastrosDaConfiguracaoCitados cadastrosDaConfiguracao,
    LeituraDoInstanteDoComando leituraDoInstante)
{
    internal async Task<Result<CadastrosCitadosNoDocumento>> LerAsync(CodigosCitadosPorCadastro codigos, CancellationToken cancellationToken)
    {
        Result<InstanteDoComando> instante = leituraDoInstante.Ler();

        return instante.IsFailure
            ? Result<CadastrosCitadosNoDocumento>.Failure(instante.Error!)
            : await LerNoInstanteAsync(codigos, instante.Value, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<CadastrosCitadosNoDocumento>> LerNoInstanteAsync(
        CodigosCitadosPorCadastro codigos, InstanteDoComando instante, CancellationToken cancellationToken)
    {
        Result<CadastrosDaConfiguracaoCitados> daConfiguracao = await cadastrosDaConfiguracao.LerAsync(codigos, cancellationToken).ConfigureAwait(false);
        Result<IReadOnlyDictionary<string, UnidadeCitada>> unidadesLidas = await UnidadesAsync(codigos.Unidades, cancellationToken).ConfigureAwait(false);
        Result<IReadOnlyDictionary<string, LocalidadeRegente>> localidade = await LocalidadeAsync(codigos.Localidade, cancellationToken).ConfigureAwait(false);
        DomainError? falha = daConfiguracao.Error ?? unidadesLidas.Error ?? localidade.Error;

        return falha is not null
            ? Result<CadastrosCitadosNoDocumento>.Failure(falha)
            : Result<CadastrosCitadosNoDocumento>.Success(await MontarComAsRegrasEOsTiposDeAtoAsync(
                codigos, instante, daConfiguracao.Value!, unidadesLidas.Value!, localidade.Value!, cancellationToken).ConfigureAwait(false));
    }

    private async Task<CadastrosCitadosNoDocumento> MontarComAsRegrasEOsTiposDeAtoAsync(
        CodigosCitadosPorCadastro codigos,
        InstanteDoComando instante,
        CadastrosDaConfiguracaoCitados daConfiguracao,
        IReadOnlyDictionary<string, UnidadeCitada> unidadesLidas,
        IReadOnlyDictionary<string, LocalidadeRegente> localidade,
        CancellationToken cancellationToken) => new(instante.Instante)
        {
            Unidades = unidadesLidas,
            TiposDeProcesso = daConfiguracao.TiposDeProcesso,
            FasesCanonicas = daConfiguracao.FasesCanonicas,
            TiposDeBanca = daConfiguracao.TiposDeBanca,
            CategoriasDeDocumento = daConfiguracao.CategoriasDeDocumento,
            Localidades = localidade,
            Regras = await RegrasAsync(codigos.Regras, cancellationToken).ConfigureAwait(false),
            TiposDeAto = await TiposDeAtoAsync(codigos.TiposDeAto, instante.Dia, cancellationToken).ConfigureAwait(false),
        };

    private async Task<Result<IReadOnlyDictionary<string, UnidadeCitada>>> UnidadesAsync(
        IReadOnlyList<string> codigos, CancellationToken cancellationToken)
    {
        Dictionary<string, UnidadeCitada> achadas = new(StringComparer.Ordinal);
        foreach (string codigo in codigos)
        {
            Result<UnidadeCitada?> lida = await UnidadeAsync(codigo, cancellationToken).ConfigureAwait(false);
            if (lida.IsFailure)
            {
                return Result<IReadOnlyDictionary<string, UnidadeCitada>>.Failure(lida.Error!);
            }
            if (lida.Value is not null)
            {
                achadas[codigo] = lida.Value;
            }
        }
        return Result<IReadOnlyDictionary<string, UnidadeCitada>>.Success(achadas);
    }

    private async Task<Result<UnidadeCitada?>> UnidadeAsync(string codigo, CancellationToken cancellationToken)
    {
        UnidadeView? unidade = await unidades.ObterPorCodigoAsync(codigo, cancellationToken).ConfigureAwait(false);
        if (unidade is null)
        {
            return Result<UnidadeCitada?>.Success(null);
        }

        Result<UnidadeAdministradoraSnapshot> cadastro = UnidadeAdministradoraSnapshot.Criar(
            unidade.Sigla, unidade.Codigo, unidade.Slug, unidade.Nome, unidade.Tipo,
            new CidadeCopiadaDaUnidade(unidade.CidadeCodigoIbge, unidade.CidadeNome, unidade.CidadeUf));

        return cadastro.IsFailure
            ? Result<UnidadeCitada?>.Failure(cadastro.Error!)
            : Result<UnidadeCitada?>.Success(new UnidadeCitada(unidade.Id, cadastro.Value!));
    }

    // A UF é a do prefixo do código IBGE; o código sem a forma de município não vai ao Geo e fica fora do dicionário.
    private async Task<Result<IReadOnlyDictionary<string, LocalidadeRegente>>> LocalidadeAsync(string? codigoIbge, CancellationToken cancellationToken)
    {
        if (codigoIbge is null || ReferenciaCidadeGeo.UfDoCodigoIbge(codigoIbge) is not { } uf)
        {
            return Result<IReadOnlyDictionary<string, LocalidadeRegente>>.Success(new Dictionary<string, LocalidadeRegente>());
        }

        Result<string> conferido = await geo.ConferirMunicipioAsync(codigoIbge, uf, cancellationToken).ConfigureAwait(false);

        return conferido.IsFailure
            ? MunicipioForaDoGeo(conferido.Error!)
            : DaLocalidadeConferida(LocalidadeRegente.Criar(codigoIbge, conferido.Value, uf), codigoIbge);
    }

    // Só a resposta que diz que o município não existe deixa o código fora do dicionário; qualquer outra, inclusive a que o Geo
    // devolve inconsistente com o prefixo do próprio código, é falha do Geo, e o domínio não vê município que ele não conferiu.
    private static Result<IReadOnlyDictionary<string, LocalidadeRegente>> MunicipioForaDoGeo(DomainError erro) => erro.Code switch
    {
        MunicipioErrorCodes.NaoEncontrado => Result<IReadOnlyDictionary<string, LocalidadeRegente>>.Success(new Dictionary<string, LocalidadeRegente>()),
        ConferenciaEnderecoGeoErrorCodes.MunicipioDivergente => GeoIndisponivel("O Geo confirmou o município numa UF diferente da que o prefixo do código IBGE indica."),
        _ => Result<IReadOnlyDictionary<string, LocalidadeRegente>>.Failure(erro),
    };

    private static Result<IReadOnlyDictionary<string, LocalidadeRegente>> DaLocalidadeConferida(Result<LocalidadeRegente> localidade, string codigoIbge) =>
        localidade.IsFailure
            ? GeoIndisponivel("O Geo devolveu um município que o Uni+ não aceita.")
            : Result<IReadOnlyDictionary<string, LocalidadeRegente>>.Success(new Dictionary<string, LocalidadeRegente> { [codigoIbge] = localidade.Value! });

    private static Result<IReadOnlyDictionary<string, LocalidadeRegente>> GeoIndisponivel(string mensagem) =>
        Result<IReadOnlyDictionary<string, LocalidadeRegente>>.Failure(new DomainError(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel, mensagem));

    private async Task<Dictionary<(string Codigo, string Versao), RegraDoCatalogoCitada>> RegrasAsync(
        IReadOnlyList<(string Codigo, string Versao)> citadas, CancellationToken cancellationToken)
    {
        Dictionary<(string Codigo, string Versao), RegraDoCatalogoCitada> achadas = [];
        foreach ((string codigo, string versao) in citadas)
        {
            RegraCatalogo? regra = await regras.ObterAsync(codigo, versao, cancellationToken).ConfigureAwait(false);
            if (regra is not null)
            {
                achadas[(codigo, versao)] = new RegraDoCatalogoCitada(regra.Tipo, regra.Referencia());
            }
        }

        return achadas;
    }

    private async Task<HashSet<string>> TiposDeAtoAsync(IReadOnlyList<string> codigos, DateOnly dia, CancellationToken cancellationToken) =>
        codigos.Count == 0
            ? []
            : [.. (await tiposDeAto.ListarVigentesAsync(codigos, dia, cancellationToken).ConfigureAwait(false)).Select(static tipo => tipo.Codigo)];
}
