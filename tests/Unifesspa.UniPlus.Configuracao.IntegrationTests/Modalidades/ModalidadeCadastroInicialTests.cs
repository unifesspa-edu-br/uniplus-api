namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Modalidades;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.Modalidades.ApoioDaPersistenciaDeModalidades;

[Collection(ConfiguracaoDbCollection.Name)]
public sealed class ModalidadeCadastroInicialTests(ConfiguracaoDbFixture fixture)
{
    private readonly ConfiguracaoDbFixture _fixture = fixture;

    [Fact(DisplayName = "Seed: as oito federais + AC + as institucionais nascem com a migração, vivas e com os atributos corretos")]
    public async Task Seed_ModalidadesFederais_PresentesEVivas()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        List<Modalidade> semeadas = await ctx.Modalidades.AsNoTracking()
            .Where(m => !m.IsDeleted)
            .ToListAsync();

        ConferirCodigos(semeadas);
        ConferirAmplaEPessoaComDeficiencia(semeadas);
        ConferirCotasFederais(semeadas);
    }

    private static void ConferirCodigos(List<Modalidade> semeadas)
    {
        string[] codigos = [.. semeadas.Select(m => m.Codigo.Valor).OrderBy(c => c, StringComparer.Ordinal)];
        codigos.Should().Contain(["AC", "AC_I", "AC_PCD", "AC_Q", "LB_EP", "LB_PCD", "LB_PPI", "LB_Q", "LI_EP", "LI_PCD", "LI_PPI", "LI_Q"]);
        codigos.Should().NotContain("PCD_PURO");
    }

    private static void ConferirAmplaEPessoaComDeficiencia(List<Modalidade> semeadas)
    {
        Modalidade acPcd = semeadas.Single(m => m.Codigo.Valor == "AC_PCD");
        acPcd.NaturezaLegal.Should().Be(NaturezaLegal.AcaoAfirmativa);
        acPcd.ComposicaoVagas.Should().Be(ComposicaoVagas.RetiradaDeOutraModalidade);
        acPcd.ComposicaoOrigem.Should().Be("AC");
        acPcd.RegraRemanejamento.Should().Be(RegraRemanejamento.DestinoUnico);
        acPcd.RemanejamentoParametros.Destino.Should().Be("AC");
        acPcd.Descricao.Should().Contain("(V)");
        Modalidade ac = semeadas.Single(m => m.Codigo.Valor == "AC");
        ac.NaturezaLegal.Should().Be(NaturezaLegal.Ampla);
        ac.ComposicaoVagas.Should().Be(ComposicaoVagas.RestanteDasVagasOfertadas);
        ac.RegraRemanejamento.Should().BeNull();
    }

    private static void ConferirCotasFederais(List<Modalidade> semeadas) =>
        semeadas.Where(m => m.Codigo.Valor.StartsWith('L')).Should().OnlyContain(m =>
            m.NaturezaLegal == NaturezaLegal.CotaReservada
            && m.ComposicaoVagas == ComposicaoVagas.DentroDaReservaLegal
            && m.RegraRemanejamento == RegraRemanejamento.SegueCascata);

    [Theory(DisplayName = "As modalidades por acréscimo formam par recíproco")]
    [InlineData("AC_I", "AC_Q")]
    [InlineData("AC_Q", "AC_I")]
    public async Task Seed_AcIAcQ_ParCruzadoReciproco(string codigo, string par)
    {
        CodigoModalidade codigoModalidade = CodigoModalidade.Criar(codigo).Value!;
        await using UniPlusDbContext context = _fixture.CreateDbContext(userId: null);
        Modalidade modalidade = await context.Modalidades.AsNoTracking().SingleAsync(m => !m.IsDeleted && m.Codigo == codigoModalidade);
        modalidade.NaturezaLegal.Should().Be(NaturezaLegal.AcaoAfirmativa);
        modalidade.ComposicaoVagas.Should().Be(ComposicaoVagas.SuplementarAoTotal);
        modalidade.ComposicaoOrigem.Should().BeNull();
        modalidade.RegraRemanejamento.Should().Be(RegraRemanejamento.Cruzado);
        modalidade.RemanejamentoParametros.Destino.Should().BeNull();
        modalidade.RemanejamentoParametros.Fallback.Should().BeNull();
        modalidade.RemanejamentoParametros.Par.Should().Be(par);
        modalidade.BaseLegal.Should().NotBeNullOrWhiteSpace().And.NotContain("12.711");
    }

    [Theory(DisplayName = "Seed: a base legal das ações afirmativas é a Resolução 532/2021, não a Lei de Cotas")]
    [InlineData("AC_PCD", "532/2021, art. 1º")]
    [InlineData("AC_I", "532/2021-CONSEPE, art. 2º")]
    [InlineData("AC_Q", "532/2021-CONSEPE, art. 2º")]
    public async Task Seed_AcaoAfirmativa_FundamentadaNaResolucao532(string codigo, string normaEsperada)
    {
        CodigoModalidade codigoModalidade = CodigoModalidade.Criar(codigo).Value!;

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Modalidade modalidade = await ctx.Modalidades.AsNoTracking()
            .SingleAsync(m => !m.IsDeleted && m.Codigo == codigoModalidade);

        modalidade.BaseLegal.Should().NotBeNullOrWhiteSpace()
            .And.NotContain("12.711")
            .And.NotContain("64/2015")
            .And.NotContain("22/2014")
            .And.Contain(normaEsperada);
    }

    [Fact(DisplayName = "Seed: o conjunto semeado é exatamente o catálogo legal fixo protegido")]
    public void Seed_Codigos_CoincidemComOCatalogoProtegido()
    {
        string[] semeados = [.. ModalidadeSeed.Itens.Select(i => i.Codigo).Order(StringComparer.Ordinal)];
        string[] protegidos = [.. CodigoModalidade.CodigosLegaisFixos.Order(StringComparer.Ordinal)];

        semeados.Should().Equal(protegidos,
            "acrescentar uma linha ao seed sem protegê-la (ou proteger um código que ninguém semeia) "
            + "deixaria o catálogo legal incoerente");
    }

    [Fact(DisplayName = "Seed: cada item satisfaz as invariantes de Modalidade.Criar (natureza × composição × remanejamento × parâmetros)")]
    public void Seed_CadaItem_SatisfazInvariantesDoAgregado()
    {
        foreach (ModalidadeSeedItem item in ModalidadeSeed.Itens)
        {
            Result<Modalidade> resultado = CriarModalidade(item);
            resultado.IsSuccess.Should().BeTrue($"o item '{item.Codigo}' deve ser válido — {resultado.Error?.Message}");
        }
    }

    private static Result<Modalidade> CriarModalidade(ModalidadeSeedItem item) => Modalidade.Criar(
        item.Codigo,
        new DeclaracaoDaModalidade(
            item.Descricao, item.Natureza.Codigo(),
            item.Composicao.Codigo(), item.ComposicaoOrigem,
            item.Regra?.Codigo(),
            item.RemanejamentoParametros.Destino, item.RemanejamentoParametros.Par, item.RemanejamentoParametros.Fallback,
            item.RegraDeAplicabilidade, CodigoDaAcaoQuandoIndeferido: null, item.BaseLegal));
}
