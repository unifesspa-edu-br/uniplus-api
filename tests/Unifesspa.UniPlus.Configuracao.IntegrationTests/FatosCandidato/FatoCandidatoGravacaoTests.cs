namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FatosCandidato;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.FatosCandidato.FatoCandidatoCodigoDeTeste;

/// <summary>
/// A gravação do fato no banco: concorrência otimista da edição e dos valores, gravação sem mudança
/// e autoria de quem cria e de quem altera.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class FatoCandidatoGravacaoTests
{
    private readonly ConfiguracaoDbFixture _fixture;

    public FatoCandidatoGravacaoTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Duas edições concorrentes do mesmo fato: a segunda é recusada pela concorrência otimista")]
    public async Task EdicaoConcorrente_SegundaRecusada()
    {
        FatoCandidato fato = NovoFatoDoAdministrador();
        try
        {
            await GravarFatoAsync(fato);
            await EdicaoConcorrenteNoBancoAsync(fato.Id);
        }
        finally
        {
            await RemoverAsync(fato.Id);
        }
    }

    [Fact(DisplayName = "Correção e reordenação concorrentes dos valores do mesmo fato: a segunda é recusada pela concorrência otimista")]
    public async Task CorrecaoEReordenacaoConcorrentesDosValores_SegundaRecusada()
    {
        FatoCandidato fato = NovoFatoCategoricoComDoisValores();
        try
        {
            await GravarFatoAsync(fato);
            await CorrecaoEReordenacaoConcorrentesDosValoresNoBancoAsync(fato.Id);
        }
        finally
        {
            await RemoverAsync(fato.Id);
        }
    }

    [Fact(DisplayName = "Acréscimo e reordenação concorrentes dos valores: a segunda gravação é recusada pela concorrência otimista")]
    public async Task AcrescimoEReordenacaoConcorrentes_SegundaRecusada()
    {
        FatoCandidato fato = NovoFatoCategoricoComDoisValores();
        try
        {
            await GravarFatoAsync(fato);
            await AcrescimoEReordenacaoConcorrentesNoBancoAsync(fato.Id);
        }
        finally
        {
            await RemoverAsync(fato.Id);
        }
    }

    [Fact(DisplayName = "Correção com os mesmos textos e reordenação na ordem atual não gravam nada no fato")]
    public async Task CorrecaoEReordenacaoSemMudanca_NaoAvancamAVersaoNemOCarimbo()
    {
        FatoCandidato fato = NovoFatoCategoricoComDoisValores();
        try
        {
            await GravarFatoAsync(fato);
            await CorrecaoEReordenacaoSemMudancaNoBancoAsync(fato.Id);
        }
        finally
        {
            await RemoverAsync(fato.Id);
        }
    }

    [Fact(DisplayName = "Cadastro e edição do fato registram quem criou e quem alterou")]
    public async Task CadastroEEdicao_RegistramAutoria()
    {
        FatoCandidato fato = NovoFatoDoAdministrador();
        try
        {
            await GravarFatoAsync(fato);
            await CadastroEEdicaoNoBancoAsync(fato.Id);
        }
        finally
        {
            await RemoverAsync(fato.Id);
        }
    }

    private async Task GravarFatoAsync(FatoCandidato fato)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: "admin-1");
        ctx.FatosCandidato.Add(fato);
        await ctx.SaveChangesAsync();
    }

    private async Task EdicaoConcorrenteNoBancoAsync(Guid id)
    {
        await using UniPlusDbContext primeiro = _fixture.CreateDbContext(userId: "admin-1");
        await using UniPlusDbContext segundo = _fixture.CreateDbContext(userId: "admin-2");
        FatoCandidato noPrimeiro = await primeiro.FatosCandidato.SingleAsync(f => f.Id == id);
        FatoCandidato noSegundo = await segundo.FatosCandidato.SingleAsync(f => f.Id == id);

        noPrimeiro.AlterarDescritivo("Nome da primeira edição", "Descrição do fato.", null).IsSuccess.Should().BeTrue();
        await primeiro.SaveChangesAsync();
        noSegundo.AlterarDescritivo("Nome da segunda edição", "Descrição do fato.", null).IsSuccess.Should().BeTrue();

        Func<Task> act = () => segundo.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    private async Task CorrecaoEReordenacaoConcorrentesDosValoresNoBancoAsync(Guid id)
    {
        await using UniPlusDbContext primeiro = _fixture.CreateDbContext(userId: "admin-1");
        await using UniPlusDbContext segundo = _fixture.CreateDbContext(userId: "admin-2");
        FatoCandidatoRepository repositorioDoPrimeiro = new(primeiro);
        FatoCandidatoRepository repositorioDoSegundo = new(segundo);
        FatoCandidato noPrimeiro = (await repositorioDoPrimeiro.ObterPorIdAsync(id, CancellationToken.None))!;
        FatoCandidato noSegundo = (await repositorioDoSegundo.ObterPorIdAsync(id, CancellationToken.None))!;

        noPrimeiro.CorrigirValor("A", "A corrigido", null).IsSuccess.Should().BeTrue();
        await primeiro.SaveChangesAsync();
        noSegundo.ReordenarValores(["B", "A"]).IsSuccess.Should().BeTrue();

        Func<Task> act = () => segundo.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    private async Task AcrescimoEReordenacaoConcorrentesNoBancoAsync(Guid id)
    {
        await using UniPlusDbContext primeiro = _fixture.CreateDbContext(userId: "admin-1");
        await using UniPlusDbContext segundo = _fixture.CreateDbContext(userId: "admin-2");
        FatoCandidato noPrimeiro = (await new FatoCandidatoRepository(primeiro).ObterPorIdAsync(id, CancellationToken.None))!;
        FatoCandidato noSegundo = (await new FatoCandidatoRepository(segundo).ObterPorIdAsync(id, CancellationToken.None))!;

        noPrimeiro.AdicionarValorDominio("C", "C", 2, ativo: true).IsSuccess.Should().BeTrue();
        await primeiro.SaveChangesAsync();
        noSegundo.ReordenarValores(["B", "A"]).IsSuccess.Should().BeTrue();

        Func<Task> act = () => segundo.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    private async Task CorrecaoEReordenacaoSemMudancaNoBancoAsync(Guid id)
    {
        await using UniPlusDbContext ctx2 = _fixture.CreateDbContext(userId: "admin-2");
        FatoCandidato rastreado = (await new FatoCandidatoRepository(ctx2).ObterPorIdAsync(id, CancellationToken.None))!;
        DateTimeOffset? carimboAntes = rastreado.UpdatedAt;
        string? autorAntes = rastreado.UpdatedBy;

        rastreado.CorrigirValor("A", "A", null).IsSuccess.Should().BeTrue();
        rastreado.ReordenarValores(["A", "B"]).IsSuccess.Should().BeTrue();
        ctx2.ChangeTracker.HasChanges().Should().BeFalse();
        await ctx2.SaveChangesAsync();

        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);
        FatoCandidato persistido = await leitura.FatosCandidato.AsNoTracking().SingleAsync(f => f.Id == id);
        persistido.UpdatedAt.Should().Be(carimboAntes);
        persistido.UpdatedBy.Should().Be(autorAntes);
    }

    private async Task CadastroEEdicaoNoBancoAsync(Guid id)
    {
        await using (UniPlusDbContext ctx = _fixture.CreateDbContext(userId: "admin-2"))
        {
            FatoCandidato rastreado = await ctx.FatosCandidato.SingleAsync(f => f.Id == id);
            rastreado.Desativar().IsSuccess.Should().BeTrue();
            await ctx.SaveChangesAsync();
        }

        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);
        FatoCandidato persistido = await leitura.FatosCandidato.AsNoTracking().SingleAsync(f => f.Id == id);
        persistido.CreatedBy.Should().Be("admin-1");
        persistido.UpdatedBy.Should().Be("admin-2");
    }

    private static FatoCandidato NovoFatoCategoricoComDoisValores()
    {
        string codigo = CodigoUnico();
        FatoCandidato fato = CriacaoDoFato.Criar(
            new DescritivoDoFato(codigo, "Fato com valores", "Descrição do fato."),
            OrigemFato.Declarado,
            $"CAMPO_FORMULARIO:{codigo}",
            new DefinicaoDoFato(TipoDoDado.Opcao, QuantidadeDeValores.ValorUnico, FonteValoresFato.Catalogo, null, "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, "Teste", HipoteseLegalTratamento.CumprimentoObrigacaoLegal),
            sistema: false).Value!;
        fato.AdicionarValorDominio("A", "A", 0, ativo: true).IsSuccess.Should().BeTrue();
        fato.AdicionarValorDominio("B", "B", 1, ativo: true).IsSuccess.Should().BeTrue();
        return fato;
    }

    /// <summary>O banco é compartilhado pela coleção: o fato criado pelo teste sai no fim, para não mudar o seed que os outros contam.</summary>
    private async Task RemoverAsync(Guid id)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await ctx.FatosCandidato.Where(f => f.Id == id).ExecuteDeleteAsync();
    }

    private static FatoCandidato NovoFatoDoAdministrador()
    {
        string codigo = CodigoUnico();
        return CriacaoDoFato.Criar(
            new DescritivoDoFato(codigo, "Fato do administrador", "Descrição do fato."),
            OrigemFato.Declarado,
            $"CAMPO_FORMULARIO:{codigo}",
            new DefinicaoDoFato(TipoDoDado.SimOuNao, QuantidadeDeValores.ValorUnico, null, null, "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, "Teste", HipoteseLegalTratamento.CumprimentoObrigacaoLegal),
            sistema: false).Value!;
    }
}
