namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using System.Text.Json.Nodes;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;
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

    public static readonly UnidadeView Ceps = Unidade("CEPS", "CEPS", "Centro de Processos Seletivos");
    public static readonly UnidadeView Cga = Unidade("CGA", "CGA", "Centro de Gestão Acadêmica");

    private readonly HashSet<Guid> _unidadesPermitidas = [];

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
        PodeConfigurarEm(Ceps);
    }

    public IEsquemaDoDocumentoDaConfiguracao Esquema { get; } = Substitute.For<IEsquemaDoDocumentoDaConfiguracao>();

    public IUnidadeReader Unidades { get; } = Substitute.For<IUnidadeReader>();

    public ITipoProcessoReader Tipos { get; } = Substitute.For<ITipoProcessoReader>();

    public IVerificadorDeAcesso Verificador { get; } = Substitute.For<IVerificadorDeAcesso>();

    public IRascunhoDeProcessoSeletivoRepository Rascunhos { get; } = Substitute.For<IRascunhoDeProcessoSeletivoRepository>();

    public IProcessoSeletivoRepository Processos { get; } = Substitute.For<IProcessoSeletivoRepository>();

    public IDocumentoAPublicarRepository Documentos { get; } = Substitute.For<IDocumentoAPublicarRepository>();

    public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    public PreparacaoDoDocumentoDoRascunho Preparacao =>
        new(new LeituraDoDocumentoDaConfiguracao(Esquema));

    public UnidadeDoDocumentoDoRascunho UnidadesDoDocumento => new(Unidades, Verificador);

    public BloqueioDoRascunhoDeProcessoSeletivo Bloqueio => new(Rascunhos, Processos, Verificador);

    public ExportacaoDoRascunho Exportacao => new(Unidades, Tipos);

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

    public int DecisoesSobreAUnidade(UnidadeView unidade) =>
        Verificador.ReceivedCalls().Count(chamada =>
            chamada.GetMethodInfo().Name == nameof(IVerificadorDeAcesso.ExigirAsync)
            && chamada.GetArguments()[1] is ResourceContext { UnidadeProprietariaId: { } id }
            && id == unidade.Id);

    public RascunhoDeProcessoSeletivo ExisteUmRascunhoNaRevisao1(UnidadeView unidade, string? documento = null)
    {
        DocumentoDoRascunho conteudo = DocumentoDoRascunho.Criar(documento ?? DocumentoValido(unidade.Codigo), "Primeiro nome").Value!;
        RascunhoDeProcessoSeletivo rascunho = RascunhoDeProcessoSeletivo.Criar(unidade.Id, conteudo).Value!;
        Rascunhos.ObterParaMutacaoAsync(rascunho.Id, Arg.Any<CancellationToken>()).Returns(rascunho);
        Rascunhos.ObterAsync(rascunho.Id, Arg.Any<CancellationToken>()).Returns(rascunho);
        return rascunho;
    }

    public static string DocumentoValido(string codigoDaUnidade = CodigoDaUnidade, string nome = "Processo Seletivo de Exemplo") =>
        $$"""{ "formato": "uniplus.selecao.configuracao", "versaoFormato": 1, "identificacao": { "nome": "{{nome}}", "unidadeAdministradoraCodigo": "{{codigoDaUnidade}}" } }""";

    public static PrecondicaoIfMatch Precondicao(RascunhoDeProcessoSeletivo rascunho) => PrecondicaoIfMatch.DeTags([rascunho.ETag]);

    private ResultadoDoAcesso Decidir(ResourceContext? resource) =>
        resource is { UnidadeProprietariaId: { } unidade } && _unidadesPermitidas.Contains(unidade)
            ? ResultadoDoAcesso.Permitido
            : ResultadoDoAcesso.Negado;

    private static UnidadeView Unidade(string sigla, string codigo, string nome) =>
        new(Guid.CreateVersion7(), sigla, codigo, codigo.ToUpperInvariant(), nome, null, "ADMINISTRATIVA", false, null);
}
