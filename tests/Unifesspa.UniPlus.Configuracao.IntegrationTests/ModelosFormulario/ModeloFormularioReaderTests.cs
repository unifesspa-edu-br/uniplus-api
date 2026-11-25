namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O leitor cross-módulo dos modelos de formulário (UNI-REQ-0144): o modelo pedido pelo
/// identificador vem com o estado, ativo ou não, e com o conteúdo que a Seleção copia.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class ModeloFormularioReaderTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public ModeloFormularioReaderTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "O modelo desativado é lido com o estado desativado")]
    public async Task Obter_ModeloDesativado_TrazOEstado()
    {
        ModeloFormularioView? lido = await GravarELerAsync(Novo());

        lido!.Ativo.Should().BeFalse();
    }

    [Fact(DisplayName = "O item e o grupo do modelo são lidos como foram gravados")]
    public async Task Obter_Conteudo_TrazItemEGrupoGravados()
    {
        ModeloFormularioView? lido = await GravarELerAsync(Novo());

        lido!.Conteudo.Itens!.Single().FatoCodigo.Should().Be("CERTIFICADO");
        GrupoColetadoInput grupo = lido.Conteudo.Grupos!.Single();
        grupo.Subitens.Select(static s => s.FatoCodigo).Should().Equal(CandidatoComoMembro.FatoParentesco, "MAIOR_IDADE");
        grupo.Ocorrencias!.Maximo.Should().BeNull("o grupo sem máximo é gravado e lido sem máximo");
        grupo.Ocorrencias.IncluiCandidato.Should().BeTrue();
    }

    [Fact(DisplayName = "O identificador sem modelo não devolve nada")]
    public async Task Obter_IdentificadorSemModelo_NaoTemModelo()
    {
        using IServiceScope escopo = _fixture.Factory.Services.CreateScope();
        IModeloFormularioReader reader = escopo.ServiceProvider.GetRequiredService<IModeloFormularioReader>();

        ModeloFormularioView? lido = await reader.ObterAsync(Guid.NewGuid());

        lido.Should().BeNull();
    }

    private async Task<ModeloFormularioView?> GravarELerAsync(ModeloFormulario modelo)
    {
        using IServiceScope escopo = _fixture.Factory.Services.CreateScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        db.ModelosFormulario.Add(modelo);
        await db.SaveChangesAsync();
        IModeloFormularioReader reader = escopo.ServiceProvider.GetRequiredService<IModeloFormularioReader>();
        return await reader.ObterAsync(modelo.Id);
    }

    private static ModeloFormulario Novo() =>
        ModeloFormulario.Criar(
            $"A_{Guid.NewGuid():N}"[..30], "Habilitação", null, FinalidadeFormulario.Habilitacao, null,
            ConteudoDeHabilitacao(),
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)).Value!;

    private static ConteudoDoModelo ConteudoDeHabilitacao() =>
        new(
            "Habilitação",
            [
                new("DADOS", 0, TipoEtapaFormulario.Secao, null, "Dados", null, null, null),
                new("REVISAO", 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null),
            ],
            [new ItemDoModelo("CERTIFICADO", 0, "DADOS", new ApresentacaoDoCampo("Certificado", TipoDeCampo.SimOuNao), null, new RegrasDoCampo(null, Obrigatoriedade.Sempre, []))],
            [],
            [],
            [GrupoDeComposicaoFamiliar()]);

    private static GrupoDoModelo GrupoDeComposicaoFamiliar() =>
        new(
            "COMPOSICAO_FAMILIAR", 1, "DADOS", "Composição familiar", new OcorrenciasDoGrupo(1, null, IncluiCandidato: true),
            new ExibicaoEObrigatoriedade(null, Obrigatoriedade.Sempre),
            [
                new(CandidatoComoMembro.FatoParentesco, 0, null, new ApresentacaoDoCampo("Parentesco", TipoDeCampo.EscolhaUnica), null, new RegrasDoCampo(null, Obrigatoriedade.Sempre, [])),
                new("MAIOR_IDADE", 1, null, new ApresentacaoDoCampo("Maior de idade", TipoDeCampo.SimOuNao), null, new RegrasDoCampo(null, Obrigatoriedade.Sempre, [])),
            ]);
}
