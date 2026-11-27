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
    ITipoAtoPublicadoReader tiposDeAto,
    IRegraCatalogoReader regras,
    IConferenciaEnderecoGeo geo,
    IFatoCandidatoReader dadosDoCandidato,
    LeituraDosCadastrosDaConfiguracaoCitados cadastrosDaConfiguracao,
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
        Result<IReadOnlyDictionary<string, UnidadeCitada>> unidadesLidas = await UnidadesAsync(codigos.Unidades, cancellationToken).ConfigureAwait(false);
        LocalidadeLida localidade = await LocalidadeAsync(codigos.Localidade, cancellationToken).ConfigureAwait(false);
        DomainError? falha = daConfiguracao.Error ?? unidadesLidas.Error;

        return falha is not null
            ? Result<CadastrosCitadosNoDocumento>.Failure(falha)
            : Result<CadastrosCitadosNoDocumento>.Success(await MontarComAsRegrasEOsTiposDeAtoAsync(
                codigos, instante, daConfiguracao.Value!, unidadesLidas.Value!, localidade, cancellationToken).ConfigureAwait(false));
    }

    private async Task<CadastrosCitadosNoDocumento> MontarComAsRegrasEOsTiposDeAtoAsync(
        CodigosCitadosPorCadastro codigos,
        InstanteDoComando instante,
        CadastrosDaConfiguracaoCitados daConfiguracao,
        IReadOnlyDictionary<string, UnidadeCitada> unidadesLidas,
        LocalidadeLida localidade,
        CancellationToken cancellationToken) => new(instante.Instante)
        {
            Unidades = unidadesLidas,
            TiposDeProcesso = daConfiguracao.TiposDeProcesso,
            FasesCanonicas = daConfiguracao.FasesCanonicas,
            TiposDeBanca = daConfiguracao.TiposDeBanca,
            CategoriasDeDocumento = daConfiguracao.CategoriasDeDocumento,
            TiposDeEtapa = daConfiguracao.TiposDeEtapa,
            QuadrosDePesos = daConfiguracao.QuadrosDePesos,
            Localidades = localidade.Achadas,
            LocalidadeNaoConferida = localidade.NaoConferida,
            Regras = await RegrasAsync(codigos.Regras, cancellationToken).ConfigureAwait(false),
            DadosDoDesempate = await DadosDoDesempateAsync(codigos.Regras, cancellationToken).ConfigureAwait(false),
            TiposDeAto = await TiposDeAtoAsync(codigos.TiposDeAto, instante.Dia, cancellationToken).ConfigureAwait(false),
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

    private async Task<IReadOnlyDictionary<(string Codigo, string Versao), RegraDoCatalogoCitada>> RegrasAsync(
        IReadOnlyList<(string Codigo, string Versao)> citadas, CancellationToken cancellationToken) =>
        citadas.Count == 0
            ? new Dictionary<(string Codigo, string Versao), RegraDoCatalogoCitada>()
            : (await regras.ListarPorReferenciasAsync(citadas, cancellationToken).ConfigureAwait(false))
                .ToDictionary(static regra => (regra.Codigo, regra.Versao), static regra => new RegraDoCatalogoCitada(regra.Tipo, regra.Referencia()));

    // O vocabulário só é lido quando o documento cita o desempate por condição sobre dado; sem a citação, fica vazio.
    private async Task<IReadOnlyDictionary<string, DescritorFatoCandidato>> DadosDoDesempateAsync(
        IReadOnlyList<(string Codigo, string Versao)> citadas, CancellationToken cancellationToken) =>
        citadas.Any(static regra => regra.Codigo == RegraDeDesempate.CondicaoSobreDado.Codigo())
            ? (await FatosDoDesempate.LerAsync(dadosDoCandidato, cancellationToken).ConfigureAwait(false)).Vocabulario
            : new Dictionary<string, DescritorFatoCandidato>();

    private async Task<HashSet<string>> TiposDeAtoAsync(IReadOnlyList<string> codigos, DateOnly dia, CancellationToken cancellationToken) =>
        codigos.Count == 0
            ? []
            : [.. (await tiposDeAto.ListarVigentesAsync(codigos, dia, cancellationToken).ConfigureAwait(false)).Select(static tipo => tipo.Codigo)];

    private sealed record LocalidadeLida(IReadOnlyDictionary<string, LocalidadeRegente> Achadas, DomainError? NaoConferida);
}
