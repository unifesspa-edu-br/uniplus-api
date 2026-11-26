namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O modelo de formulário contra Postgres real: o conteúdo volta do documento jsonb igual ao que foi
/// gravado, com as regras do item, da seção e do termo, e o código é único entre todos os modelos.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class ModeloFormularioPersistenceTests
{
    private readonly ConfiguracaoDbFixture _fixture;

    public ModeloFormularioPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    private static PredicadoDnf Quando(string fato, string valor) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(valor)).Value!)]).Value!;

    private static ModeloFormulario Novo(string codigo)
    {
        ConteudoDoModelo conteudo = new(
            "Habilitação", PaginasDaHabilitacao(), ItensDaHabilitacao(), [TermoDeVeracidade()], ["FORMA_CONCLUSAO"], [GrupoDaComposicaoFamiliar()]);
        Result<ModeloFormulario> modelo = ModeloFormulario.Criar(
            codigo, "Habilitação Medicina 2027", "Modelo do PSR", new DeclaracaoDoFormulario(ServeAFaseDeInscricao: false, Obrigatorio: true), "REGULAR_UNIFICADO", conteudo,
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal));
        modelo.IsSuccess.Should().BeTrue(modelo.Error?.Message);
        return modelo.Value!;
    }

    private static PaginaDoModelo[] PaginasDaHabilitacao() =>
    [
        new("ESCOLA", 0, TipoPaginaFormulario.Secao, null, "Escolaridade", "Como concluiu", null, Quando("FORMA_CONCLUSAO", "REGULAR")),
        new("DOCUMENTOS", 1, TipoPaginaFormulario.Bloco, BlocoSistema.ComprovacaoDocumental, "Documentos", null, null, null),
        new("REVISAO", 2, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null),
    ];

    private static ItemDoModelo[] ItensDaHabilitacao() =>
    [
        new("CERTIFICADO_EMITIDO", 0, "ESCOLA",
            new ApresentacaoDoCampo("Certificado emitido?", TipoDeCampo.SimOuNao, "Responda pelo histórico", pedirConfirmacao: true),
            null,
            new RegrasDoCampo(null, Obrigatoriedade.Sempre, [])),
        new("TIPO_DOCUMENTO", 1, "ESCOLA",
            new ApresentacaoDoCampo("Tipo de documento", TipoDeCampo.EscolhaUnica),
            null,
            new RegrasDoCampo(
                null,
                Obrigatoriedade.Quando(Quando("FORMA_CONCLUSAO", "REGULAR")),
                [RestricoesDeValor.Opcoes([(Quando("FORMA_CONCLUSAO", "REGULAR"), ["HISTORICO"])]).Value!])),
    ];

    private static TermoDoModelo TermoDeVeracidade() =>
        new("VERACIDADE", 0, Guid.CreateVersion7(), Guid.CreateVersion7(),
            new ExibicaoEObrigatoriedade(Quando("FORMA_CONCLUSAO", "REGULAR"), Obrigatoriedade.Sempre));

    private static GrupoDoModelo GrupoDaComposicaoFamiliar() =>
        new("COMPOSICAO_FAMILIAR", 2, "ESCOLA", "Composição familiar", new OcorrenciasDoGrupo(1, 10, IncluiCandidato: false),
            new ExibicaoEObrigatoriedade(Quando("FORMA_CONCLUSAO", "REGULAR"), Obrigatoriedade.Sempre),
        [
            new("PARENTESCO", 0, null,
                new ApresentacaoDoCampo("Parentesco", TipoDeCampo.EscolhaUnica, "Em relação ao candidato"),
                null,
                new RegrasDoCampo(null, Obrigatoriedade.Sempre, [])),
            new("RENDA_MEMBRO", 1, null,
                new ApresentacaoDoCampo("Renda", TipoDeCampo.SimOuNao),
                null,
                new RegrasDoCampo(Quando("PARENTESCO", "CONJUGE"), Obrigatoriedade.Quando(Quando("PARENTESCO", "CONJUGE")), [])),
        ]);

    private static ModeloFormulario NovoDeInscricaoComImpedimento(Impedimento impedimento)
    {
        Result<ModeloFormulario> criado = ModeloFormulario.Criar(
            $"INS_{Guid.NewGuid():N}"[..30].ToUpperInvariant(), "Inscrição PSIQ 2027", null, DeclaracaoDoFormulario.DaInscricao, "INDIGENA_E_QUILOMBOLA",
            ConteudoDaInscricaoComImpedimento(impedimento),
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal));
        criado.IsSuccess.Should().BeTrue(criado.Error?.Message);
        return criado.Value!;
    }

    private static ConteudoDoModelo ConteudoDaInscricaoComImpedimento(Impedimento impedimento) =>
        new(
            "Inscrição",
            [
                new("DADOS", 0, TipoPaginaFormulario.Secao, null, "Dados", null, null, null),
                new("REVISAO", 1, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null),
            ],
            [new("VINCULO_PARFOR", 0, "DADOS",
                new ApresentacaoDoCampo("Vínculo com o PARFOR", TipoDeCampo.SimOuNao),
                null,
                new RegrasDoCampo(null, Obrigatoriedade.Sempre, [], impedimento))],
            [], [], []);

    [Fact(DisplayName = "O conteúdo do modelo volta do documento jsonb igual ao gravado")]
    public async Task Conteudo_IdaEVolta()
    {
        ModeloFormulario modelo = Novo($"HAB_{Guid.NewGuid():N}"[..30].ToUpperInvariant());
        await GravarAsync(modelo);

        ModeloFormulario lido = await LerAsync(modelo.Id);

        lido.Declaracao.Should().Be(new DeclaracaoDoFormulario(ServeAFaseDeInscricao: false, Obrigatorio: true));
        lido.TipoProcessoCodigo.Should().Be("REGULAR_UNIFICADO");
        lido.Conteudo.Should().BeEquivalentTo(modelo.Conteudo, opcoes => opcoes.ComparingByMembers<JsonElement>()
            .Using<JsonElement>(c => c.Subject.GetRawText().Should().Be(c.Expectation.GetRawText())).WhenTypeIs<JsonElement>());
    }

    [Fact(DisplayName = "O impedimento do item do modelo de inscrição volta do documento jsonb igual ao gravado")]
    public async Task Impedimento_IdaEVolta()
    {
        Impedimento impedimento = new(
            PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar("VINCULO_PARFOR", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!,
            "Quem tem vínculo com o PARFOR não pode se inscrever neste processo.");
        ModeloFormulario criado = NovoDeInscricaoComImpedimento(impedimento);
        await GravarAsync(criado);

        ModeloFormulario lido = await LerAsync(criado.Id);

        lido.Conteudo.Itens.Single().Regras.Impedimento.Should().BeEquivalentTo(impedimento, opcoes => opcoes.ComparingByMembers<JsonElement>()
            .Using<JsonElement>(c => c.Subject.GetRawText().Should().Be(c.Expectation.GetRawText())).WhenTypeIs<JsonElement>());
    }

    [Fact(DisplayName = "O código do modelo é único entre todos os modelos")]
    public async Task CodigoRepetido_Recusado()
    {
        string codigo = $"HAB_{Guid.NewGuid():N}"[..30].ToUpperInvariant();
        await GravarAsync(Novo(codigo));

        await using UniPlusDbContext segundo = _fixture.CreateDbContext("admin-a");
        segundo.ModelosFormulario.Add(Novo(codigo));
        Func<Task> gravar = async () => await segundo.SaveChangesAsync();

        DbUpdateException ex = (await gravar.Should().ThrowAsync<DbUpdateException>()).Which;
        ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which.ConstraintName.Should().Be("ux_modelos_formulario_codigo");
    }

    private async Task GravarAsync(ModeloFormulario modelo)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext("admin-a");
        ctx.ModelosFormulario.Add(modelo);
        await ctx.SaveChangesAsync();
    }

    private async Task<ModeloFormulario> LerAsync(Guid id)
    {
        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);
        return await leitura.ModelosFormulario.SingleAsync(m => m.Id == id);
    }
}
