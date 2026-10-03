namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
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
            "Habilitação",
            [
                new("ESCOLA", 0, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Escolaridade", "Como concluiu", null, Quando("FORMA_CONCLUSAO", "REGULAR")),
                new("DOCUMENTOS", 1, TipoEtapaFormulario.Bloco, BlocoSistema.ComprovacaoDocumental, "Documentos", null, null, null),
                new("REVISAO", 2, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null),
            ],
            [
                new("CERTIFICADO_EMITIDO", 0, "ESCOLA", "Certificado emitido?", TipoRenderizacao.Booleano, null, "Responda pelo histórico",
                    Obrigatoriedade.Sempre, null, [], PedirConfirmacao: true),
                new("TIPO_DOCUMENTO", 1, "ESCOLA", "Tipo de documento", TipoRenderizacao.SelecaoUnica, null, null,
                    Obrigatoriedade.Quando(Quando("FORMA_CONCLUSAO", "REGULAR")), null,
                    [RestricoesDeValor.Opcoes([(Quando("FORMA_CONCLUSAO", "REGULAR"), ["HISTORICO"])]).Value!], PedirConfirmacao: false),
            ],
            [new("VERACIDADE", 0, Guid.CreateVersion7(), Guid.CreateVersion7(), Quando("FORMA_CONCLUSAO", "REGULAR"), Obrigatoriedade.Sempre)],
            ["FORMA_CONCLUSAO"],
            [
                new("COMPOSICAO_FAMILIAR", 2, "ESCOLA", "Composição familiar", 1, 10, Quando("FORMA_CONCLUSAO", "REGULAR"), Obrigatoriedade.Sempre,
                [
                    new("PARENTESCO", 0, null, "Parentesco", TipoRenderizacao.SelecaoUnica, null, "Em relação ao candidato",
                        Obrigatoriedade.Sempre, null, [], PedirConfirmacao: false),
                    new("RENDA_MEMBRO", 1, null, "Renda", TipoRenderizacao.Booleano, null, null,
                        Obrigatoriedade.Quando(Quando("PARENTESCO", "CONJUGE")), Quando("PARENTESCO", "CONJUGE"), [], PedirConfirmacao: false),
                ]),
            ]);
        Result<ModeloFormulario> modelo = ModeloFormulario.Criar(
            codigo, "Habilitação Medicina 2027", "Modelo do PSR", FinalidadeFormulario.Habilitacao, "PSR", conteudo,
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal));
        modelo.IsSuccess.Should().BeTrue(modelo.Error?.Message);
        return modelo.Value!;
    }

    [Fact(DisplayName = "O conteúdo do modelo volta do documento jsonb igual ao gravado")]
    public async Task Conteudo_IdaEVolta()
    {
        ModeloFormulario modelo = Novo($"HAB_{Guid.NewGuid():N}"[..30]);
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext("admin-a"))
        {
            ctx.ModelosFormulario.Add(modelo);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext leitura = _fixture.CreateDbContext(userId: null);
        ModeloFormulario lido = await leitura.ModelosFormulario.SingleAsync(m => m.Id == modelo.Id);

        lido.Finalidade.Should().Be(FinalidadeFormulario.Habilitacao);
        lido.TipoProcessoCodigo.Should().Be("PSR");
        lido.Conteudo.Should().BeEquivalentTo(modelo.Conteudo, opcoes => opcoes.ComparingByMembers<JsonElement>()
            .Using<JsonElement>(c => c.Subject.GetRawText().Should().Be(c.Expectation.GetRawText())).WhenTypeIs<JsonElement>());
    }

    [Fact(DisplayName = "O impedimento do item do modelo de inscrição volta do documento jsonb igual ao gravado")]
    public async Task Impedimento_IdaEVolta()
    {
        Impedimento impedimento = new(
            PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar("VINCULO_PARFOR", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!,
            "Quem tem vínculo com o PARFOR não pode se inscrever neste processo.");
        Result<ModeloFormulario> criado = ModeloFormulario.Criar(
            $"INS_{Guid.NewGuid():N}"[..30], "Inscrição PSIQ 2027", null, FinalidadeFormulario.Inscricao, "PSIQ",
            new ConteudoDoModelo(
                "Inscrição",
                [
                    new("DADOS", 0, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", null, null, null),
                    new("REVISAO", 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null),
                ],
                [new("VINCULO_PARFOR", 0, "DADOS", "Vínculo com o PARFOR", TipoRenderizacao.Booleano, null, null, Obrigatoriedade.Sempre, null, [],
                    PedirConfirmacao: false, Impedimento: impedimento)],
                [], [], []),
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal));
        criado.IsSuccess.Should().BeTrue(criado.Error?.Message);
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext("admin-a"))
        {
            ctx.ModelosFormulario.Add(criado.Value!);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext leitura = _fixture.CreateDbContext(userId: null);
        ModeloFormulario lido = await leitura.ModelosFormulario.SingleAsync(m => m.Id == criado.Value!.Id);

        lido.Conteudo.Itens.Single().Impedimento.Should().BeEquivalentTo(impedimento, opcoes => opcoes.ComparingByMembers<JsonElement>()
            .Using<JsonElement>(c => c.Subject.GetRawText().Should().Be(c.Expectation.GetRawText())).WhenTypeIs<JsonElement>());
    }

    [Fact(DisplayName = "O código do modelo é único entre todos os modelos")]
    public async Task CodigoRepetido_Recusado()
    {
        string codigo = $"HAB_{Guid.NewGuid():N}"[..30];
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext("admin-a"))
        {
            ctx.ModelosFormulario.Add(Novo(codigo));
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext segundo = _fixture.CreateDbContext("admin-a");
        segundo.ModelosFormulario.Add(Novo(codigo));
        Func<Task> gravar = async () => await segundo.SaveChangesAsync();

        DbUpdateException ex = (await gravar.Should().ThrowAsync<DbUpdateException>()).Which;
        ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which.ConstraintName.Should().Be("ix_modelos_formulario_codigo");
    }
}
