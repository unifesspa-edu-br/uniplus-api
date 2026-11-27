namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using System.Text.Json.Nodes;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O cenário dos handlers do rascunho: as portas como doubles e os serviços reais que as compõem, com a unidade que o
/// documento nomeia e as unidades em que a pessoa pode configurar o processo.
/// </summary>
internal sealed class CenarioDoRascunho
{
    public const string CodigoDaUnidade = "CEPS";

    /// <summary>O bloco que declara que o processo não tem prova presencial, para o documento que não deve ter aviso dela.</summary>
    public const string SemProvaPresencial = """ "provaPresencial": { "temProvaPresencial": false, "municipios": [] } """;

    public static readonly UnidadeView Ceps = Unidade("CEPS", "CEPS", "Centro de Processos Seletivos");
    public static readonly UnidadeView Cga = Unidade("CGA", "CGA", "Centro de Gestão Acadêmica");

    private readonly HashSet<Guid> _unidadesPermitidas = [];
    private readonly Dictionary<Guid, int> _decisoes = [];

    public CenarioDoRascunho()
    {
        Esquema.Forma.Returns(Services.DocumentoDaConfiguracao.FormaDeTeste.Documento());
        Esquema.Avaliar(Arg.Any<JsonObject>()).Returns([]);
        Unidades.ObterPorCodigoAsync(Ceps.Codigo, Arg.Any<CancellationToken>()).Returns(Ceps);
        Unidades.ObterPorCodigoAsync(Cga.Codigo, Arg.Any<CancellationToken>()).Returns(Cga);
        Unidades.ObterPorIdAsync(Ceps.Id, Arg.Any<CancellationToken>()).Returns(Ceps);
        Unidades.ObterPorIdAsync(Cga.Id, Arg.Any<CancellationToken>()).Returns(Cga);
        Verificador.ExigirAsync(default!, default, default).ReturnsForAnyArgs(chamada =>
            Task.FromResult(Decidir(chamada.ArgAt<ResourceContext?>(1))));
        Geo.ConferirMunicipioAsync(default!, default!, default).ReturnsForAnyArgs(Result<string>.Failure(
            new DomainError(ConferenciaEnderecoGeoErrorCodes.MunicipioNaoEncontrado, "Município desconhecido do Geo (dublê de teste).")));
        SemCadastrosDasVagas();
        PodeConfigurarEm(Ceps);
    }

    private void SemCadastrosDasVagas()
    {
        Ofertas.ListarVivasPorCodigosAsync(default!, default).ReturnsForAnyArgs([]);
        Modalidades.ListarVivosPorCodigosAsync(default!, default).ReturnsForAnyArgs([]);
        ReferenciasDemograficas.ListarVivasPorCodigosAsync(default!, default).ReturnsForAnyArgs([]);
        Regras.ListarPorReferenciasAsync(default!, default).ReturnsForAnyArgs([]);
    }

    public IEsquemaDoDocumentoDaConfiguracao Esquema { get; } = Substitute.For<IEsquemaDoDocumentoDaConfiguracao>();

    public IUnidadeReader Unidades { get; } = Substitute.For<IUnidadeReader>();

    public IConferenciaEnderecoGeo Geo { get; } = Substitute.For<IConferenciaEnderecoGeo>();

    public ITipoProcessoReader Tipos { get; } = Substitute.For<ITipoProcessoReader>();

    public ITipoDocumentoReader TiposDeDocumento { get; } = Substitute.For<ITipoDocumentoReader>();

    public IPesoAreaEnemReader Pesos { get; } = Substitute.For<IPesoAreaEnemReader>();

    public IMotivoDeDecisaoRepository Motivos { get; } = Substitute.For<IMotivoDeDecisaoRepository>();

    public IVerificadorDeAcesso Verificador { get; } = Substitute.For<IVerificadorDeAcesso>();

    public IRascunhoDeProcessoSeletivoRepository Rascunhos { get; } = Substitute.For<IRascunhoDeProcessoSeletivoRepository>();

    public IProcessoSeletivoRepository Processos { get; } = Substitute.For<IProcessoSeletivoRepository>();

    public IDocumentoAPublicarRepository Documentos { get; } = Substitute.For<IDocumentoAPublicarRepository>();

    public IModeloDeDocumentoRepository Modelos { get; } = Substitute.For<IModeloDeDocumentoRepository>();

    public ResolucaoDosModelosCitados Resolucao => new(Modelos);

    public int LimiteDeMunicipios { get; set; } = 100;

    public ConferenciaDaProvaPresencialDoDocumento ProvaPresencial =>
        new(new ConferenciaDosMunicipiosNoGeo(Geo), Options.Create(new ProvaPresencialOptions { LimiteDeMunicipios = LimiteDeMunicipios }));

    public DiagnosticoDoRascunho Diagnostico => new(Resolucao, ProvaPresencial);

    public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    public PreparacaoDoDocumentoDoRascunho Preparacao =>
        new(new LeituraDoDocumentoDaConfiguracao(Esquema));

    public UnidadeDoDocumentoDoRascunho UnidadesDoDocumento => new(Unidades, Verificador);

    public BloqueioDoRascunhoDeProcessoSeletivo Bloqueio => new(Rascunhos, Processos, Verificador);

    public IOfertaCursoReader Ofertas { get; } = Substitute.For<IOfertaCursoReader>();

    public IModalidadeReader Modalidades { get; } = Substitute.For<IModalidadeReader>();

    public IReferenciaReservaDemograficaReader ReferenciasDemograficas { get; } = Substitute.For<IReferenciaReservaDemograficaReader>();

    public IRegraCatalogoReader Regras { get; } = Substitute.For<IRegraCatalogoReader>();

    public FakeTimeProvider Relogio { get; } = new();

    public LeituraDasVagasDoRascunho LeituraDasVagas => new(
        new LeituraDosCadastrosDasVagasCitados(
            Ofertas,
            Modalidades,
            ReferenciasDemograficas,
            Substitute.For<ICondicaoAtendimentoReader>(),
            Substitute.For<IRecursoAcessibilidadeReader>(),
            Substitute.For<ITipoDeficienciaReader>(),
            Substitute.For<IBaseLegalDoFadaReader>()),
        new LeituraDosCadastrosDoCatalogoCitados(Regras, Substitute.For<ITipoAtoPublicadoReader>(), Substitute.For<IFatoCandidatoReader>()),
        Relogio);

    public ExportacaoDoRascunho Exportacao => new(
        Unidades,
        [new DescricaoDeUnidades(Unidades), new DescricaoDeTiposDeProcesso(Tipos), new DescricaoDeTiposDeDocumento(TiposDeDocumento), new DescricaoDeMotivosDeDecisao(Motivos), new DescricaoDaParticipacaoDaReserva(), new DescricaoDePesosPorAreaDoEnem(Pesos, Esquema)],
        LeituraDasVagas,
        Resolucao,
        new DescricaoDaLocalidade(Geo),
        Esquema);

    public CenarioDoRascunho GeoConhece(string codigoIbge, string nome)
    {
        Geo.ConferirMunicipioAsync(codigoIbge, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result<string>.Success(nome));
        return this;
    }

    public CenarioDoRascunho GeoForaDoAr()
    {
        Geo.ConferirMunicipioAsync(default!, default!, default).ReturnsForAnyArgs(Result<string>.Failure(
            new DomainError(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel, "O Geo não respondeu (dublê de teste).")));
        return this;
    }

    /// <summary>O que o salvamento faz com o documento: confere antes de gravar e diagnostica o rascunho gravado, na revisão 1 da unidade do CEPS.</summary>
    public async Task<IReadOnlyList<FieldError>> DiagnosticarOSalvamentoAsync(string documento)
    {
        RascunhoDeProcessoSeletivo rascunho = ExisteUmRascunhoNaRevisao1(Ceps, documento);
        DocumentoConferidoAntesDeGravar conferido = await Diagnostico.ConferirAntesDeGravarAsync(
            DocumentoDoRascunho.Criar(documento, null).Value!, CancellationToken.None);

        return await Diagnostico.DiagnosticarAsync(rascunho, conferido, CancellationToken.None);
    }

    public CenarioDoRascunho PodeConfigurarEm(UnidadeView unidade)
    {
        _unidadesPermitidas.Add(unidade.Id);
        return this;
    }

    public CenarioDoRascunho NaoPodeConfigurarEm(UnidadeView unidade)
    {
        _unidadesPermitidas.Remove(unidade.Id);
        return this;
    }

    public int DecisoesSobreAUnidade(UnidadeView unidade) => _decisoes.GetValueOrDefault(unidade.Id);

    public RascunhoDeProcessoSeletivo ExisteUmRascunhoNaRevisao1(UnidadeView unidade, string? documento = null)
    {
        DocumentoDoRascunho conteudo = DocumentoDoRascunho.Criar(documento ?? DocumentoValido(unidade.Codigo), "Primeiro nome").Value!;
        RascunhoDeProcessoSeletivo rascunho = RascunhoDeProcessoSeletivo.Criar(unidade.Id, conteudo).Value!;
        Rascunhos.ObterParaMutacaoAsync(rascunho.Id, Arg.Any<CancellationToken>()).Returns(rascunho);
        Rascunhos.ObterAsync(rascunho.Id, Arg.Any<CancellationToken>()).Returns(rascunho);
        return rascunho;
    }

    public static string DocumentoValido(
        string codigoDaUnidade = CodigoDaUnidade, string nome = "Processo Seletivo de Exemplo", string provaPresencial = SemProvaPresencial) =>
        $$"""{ "formato": "uniplus.selecao.configuracao", "versaoFormato": 1, "identificacao": { "nome": "{{nome}}", "unidadeAdministradoraCodigo": "{{codigoDaUnidade}}" }, {{provaPresencial}} }""";

    /// <summary>O bloco da prova presencial com os municípios do Pará pelos códigos dados, na ordem dada.</summary>
    public static string ProvaPresencialNoPara(params string[] codigosDosMunicipios)
    {
        string municipios = string.Join(", ", codigosDosMunicipios.Select(static codigo => $$"""{ "codigoIbge": "{{codigo}}", "uf": "PA" }"""));
        return $$""" "provaPresencial": { "temProvaPresencial": true, "municipios": [ {{municipios}} ] } """;
    }

    public static PrecondicaoIfMatch Precondicao(RascunhoDeProcessoSeletivo rascunho) => PrecondicaoIfMatch.DeTags([rascunho.ETag]);

    private ResultadoDoAcesso Decidir(ResourceContext? resource)
    {
        if (resource is not { UnidadeProprietariaId: { } unidade })
        {
            return ResultadoDoAcesso.Negado;
        }

        _decisoes[unidade] = _decisoes.GetValueOrDefault(unidade) + 1;
        return _unidadesPermitidas.Contains(unidade) ? ResultadoDoAcesso.Permitido : ResultadoDoAcesso.Negado;
    }

    private static UnidadeView Unidade(string sigla, string codigo, string nome) =>
        new(Guid.CreateVersion7(), sigla, codigo, codigo.ToUpperInvariant(), nome, null, "ADMINISTRATIVA", false, null);
}
