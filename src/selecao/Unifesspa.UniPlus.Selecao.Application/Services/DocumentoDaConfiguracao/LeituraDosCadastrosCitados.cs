namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os cadastros citados são lidos uma vez, com um só instante de referência, em que vale a vigência do que se lê;
/// os cadastros vivos são consultados em sequência, sem visão isolada entre eles, e o congelamento os relê.
/// </summary>
/// <remarks>
/// O Geo é a única fonte dos municípios, e o rascunho é salvo mesmo sem ele (ADR-0174, emenda de 26/11/2026): a localidade que o Geo
/// não confirma fica fora do dicionário, seja porque ele disse que o município não existe, porque não respondeu ou porque devolveu
/// um município que o Uni+ não aceita. A leitura não falha por isso, e o domínio nunca vê município que o Geo não conferiu. Quando
/// o Geo não respondeu, a leitura entrega também a causa disso em <see cref="CadastrosCitadosNoDocumento.LocalidadeNaoConferida"/>,
/// para que a falta de conferência não seja apresentada como município inexistente.
/// </remarks>
public sealed class LeituraDosCadastrosCitados(
    IUnidadeReader unidades,
    IConferenciaEnderecoGeo geo,
    LeituraDosCadastrosDoCatalogoCitados cadastrosDoCatalogo,
    LeituraDosCadastrosDaConfiguracaoCitados cadastrosDaConfiguracao,
    LeituraDosCadastrosDasVagasCitados cadastrosDasVagas,
    LeituraDoInstanteDoComando leituraDoInstante)
{
    private static readonly LocalidadeLida SemLocalidade = new(new Dictionary<string, LocalidadeRegente>(), null);

    private static readonly DomainError GeoNaoRespondeuSobreALocalidade = new(
        ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel,
        "O Geo não respondeu e o município da localidade do processo não foi conferido.");

    private static readonly LocalidadeLida GeoNaoRespondeu = new(SemLocalidade.Achadas, GeoNaoRespondeuSobreALocalidade);

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
        CadastrosDasVagasEDasExigenciasCitados dasVagas = await cadastrosDasVagas.LerAsync(codigos, cancellationToken).ConfigureAwait(false);
        CadastrosDoCatalogoCitados doCatalogo = await cadastrosDoCatalogo.LerAsync(codigos, instante.Dia, cancellationToken).ConfigureAwait(false);
        Result<IReadOnlyDictionary<string, UnidadeCitada>> unidadesLidas = await UnidadesAsync(codigos.Unidades, cancellationToken).ConfigureAwait(false);
        LocalidadeLida localidade = await LocalidadeAsync(codigos.Localidade, cancellationToken).ConfigureAwait(false);
        DomainError? falha = daConfiguracao.Error ?? unidadesLidas.Error;

        return falha is not null
            ? Result<CadastrosCitadosNoDocumento>.Failure(falha)
            : Result<CadastrosCitadosNoDocumento>.Success(Montar(
                instante, daConfiguracao.Value!, dasVagas, doCatalogo, unidadesLidas.Value!, localidade));
    }

    private static CadastrosCitadosNoDocumento Montar(
        InstanteDoComando instante,
        CadastrosDaConfiguracaoCitados daConfiguracao,
        CadastrosDasVagasEDasExigenciasCitados dasVagas,
        CadastrosDoCatalogoCitados doCatalogo,
        IReadOnlyDictionary<string, UnidadeCitada> unidadesLidas,
        LocalidadeLida localidade) => new(instante.Instante)
        {
            Unidades = unidadesLidas,
            TiposDeProcesso = daConfiguracao.TiposDeProcesso,
            FasesCanonicas = daConfiguracao.FasesCanonicas,
            TiposDeBanca = daConfiguracao.TiposDeBanca,
            CategoriasDeDocumento = daConfiguracao.CategoriasDeDocumento,
            TiposDeEtapa = daConfiguracao.TiposDeEtapa,
            QuadrosDePesos = daConfiguracao.QuadrosDePesos,
            VagasEExigencias = dasVagas with { TiposDeDocumento = daConfiguracao.TiposDeDocumento },
            Localidades = localidade.Achadas,
            LocalidadeNaoConferida = localidade.NaoConferida,
            Regras = doCatalogo.Regras,
            DadosDoDesempate = doCatalogo.DadosDoDesempate,
            TiposDeAto = doCatalogo.TiposDeAto,
        };

    private async Task<Result<IReadOnlyDictionary<string, UnidadeCitada>>> UnidadesAsync(
        IReadOnlyList<string> codigos, CancellationToken cancellationToken)
    {
        Dictionary<string, UnidadeCitada> achadas = new(StringComparer.Ordinal);
        foreach (UnidadeView unidade in await UnidadesVivasAsync(codigos, cancellationToken).ConfigureAwait(false))
        {
            Result<UnidadeCitada> citada = Citada(unidade);
            if (citada.IsFailure)
            {
                return Result<IReadOnlyDictionary<string, UnidadeCitada>>.Failure(citada.Error!);
            }

            achadas[unidade.Codigo] = citada.Value!;
        }

        return Result<IReadOnlyDictionary<string, UnidadeCitada>>.Success(achadas);
    }

    private async Task<IReadOnlyList<UnidadeView>> UnidadesVivasAsync(IReadOnlyList<string> codigos, CancellationToken cancellationToken) =>
        codigos.Count == 0 ? [] : await unidades.ListarPorCodigosAsync(codigos, cancellationToken).ConfigureAwait(false);

    private static Result<UnidadeCitada> Citada(UnidadeView unidade)
    {
        Result<UnidadeAdministradoraSnapshot> cadastro = UnidadeAdministradoraSnapshot.Criar(
            unidade.Sigla, unidade.Codigo, unidade.Slug, unidade.Nome, unidade.Tipo,
            new CidadeCopiadaDaUnidade(unidade.CidadeCodigoIbge, unidade.CidadeNome, unidade.CidadeUf));

        return cadastro.IsFailure
            ? Result<UnidadeCitada>.Failure(cadastro.Error!)
            : Result<UnidadeCitada>.Success(new UnidadeCitada(unidade.Id, cadastro.Value!));
    }

    // A UF é a do prefixo do código IBGE; o código sem a forma de município não vai ao Geo. Só o município que o Geo confirma, com um
    // nome que o domínio aceita, entra no dicionário; o Geo que não responde deixa a causa, que o município inexistente não deixa.
    private async Task<LocalidadeLida> LocalidadeAsync(string? codigoIbge, CancellationToken cancellationToken)
    {
        if (codigoIbge is null || ReferenciaCidadeGeo.UfDoCodigoIbge(codigoIbge) is not { } uf)
        {
            return SemLocalidade;
        }

        return Conferida(codigoIbge, uf, await geo.ConferirMunicipioAsync(codigoIbge, uf, cancellationToken).ConfigureAwait(false));
    }

    private static LocalidadeLida Conferida(string codigoIbge, string uf, Result<string> conferido)
    {
        if (conferido.IsFailure)
        {
            return conferido.Error!.Code == ConferenciaEnderecoGeoErrorCodes.MunicipioNaoEncontrado ? SemLocalidade : GeoNaoRespondeu;
        }

        return LocalidadeRegente.Criar(codigoIbge, conferido.Value, uf) is { IsSuccess: true, Value: { } localidade }
            ? new(new Dictionary<string, LocalidadeRegente> { [codigoIbge] = localidade }, null)
            : GeoNaoRespondeu;
    }

    private sealed record LocalidadeLida(IReadOnlyDictionary<string, LocalidadeRegente> Achadas, DomainError? NaoConferida);
}
