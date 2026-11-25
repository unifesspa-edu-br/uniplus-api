namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Readers;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class LeituraDasRegrasCitadasNaConfiguracaoTests(ProcessoSeletivoDbFixture fixture)
    : IClassFixture<ProcessoSeletivoDbFixture>
{
    [Fact(DisplayName = "O rascunho preserva a referência divergente e o painel mostra a pendência")]
    public async Task RascunhoComHashDivergente_PersisteEApontaPendencia()
    {
        await using UniPlusDbContext db = fixture.CreateDbContext();
        RegraCatalogoReader leitor = new(db);
        RegraCatalogo regra = (await leitor.ObterAsync(ConvencaoDeContagemDoPrazo.ExcluiDiaInicial.Codigo(), "v1"))!;
        ReferenciaRegra referencia = Referencia(regra, "hash diferente");
        ProcessoSeletivo salvo = await GravarERecarregarAsync(db, referencia);
        salvo.Situacao.Should().Be(SituacaoDoProcessoSeletivo.Rascunho);
        salvo.AlgoritmoContagemPrazo.Should().Be(referencia);
        RegrasCitadasNaConfiguracao lidas = await new LeituraDasRegrasCitadasNaConfiguracao(leitor).LerAsync(salvo, CancellationToken.None);
        ChecklistDeConformidade.AvaliarConformidade(salvo.Declaracao(), ComRegras(lidas))
            .Should().Contain(i => i.Codigo == "regra_citada_identificacao.algoritmoContagemPrazo" && !i.Ok);
    }

    private static async Task<ProcessoSeletivo> GravarERecarregarAsync(UniPlusDbContext db, ReferenciaRegra referencia)
    {
        ProcessoSeletivo processo = CriarProcesso(referencia);
        await ProcessoSeletivoRepositoryDeTeste.Criar(db).AdicionarAsync(processo, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);
        db.ChangeTracker.Clear();
        return await db.ProcessosSeletivos.SingleAsync(p => p.Id == processo.Id);
    }

    [Theory(DisplayName = "A leitura do catálogo alimenta igualmente a conferência e o painel")]
    [InlineData("vigente", false)]
    [InlineData("hash diferente", true)]
    [InlineData("versão ausente", true)]
    [InlineData("código ausente", true)]
    public async Task RegraDoCatalogo_ConferenciaEPainelConcordam(string situacao, bool divergente)
    {
        await using UniPlusDbContext db = fixture.CreateDbContext();
        RegraCatalogoReader leitor = new(db);
        RegraCatalogo regra = (await leitor.ObterAsync(ConvencaoDeContagemDoPrazo.ExcluiDiaInicial.Codigo(), "v1"))!;
        ReferenciaRegra referencia = Referencia(regra, situacao);
        ProcessoSeletivo processo = CriarProcesso(referencia);
        RegrasCitadasNaConfiguracao lidas = await new LeituraDasRegrasCitadasNaConfiguracao(leitor).LerAsync(processo, CancellationToken.None);
        CadastrosDaConfiguracao cadastros = ComRegras(lidas);

        FieldError[] recusas = ConfiguracaoAptaACongelar.Conferir(processo.Id, processo.Declaracao(), cadastros).Errors
            .Where(e => e.Error.Code == "ProcessoSeletivo.RegraCitadaNaoCorrespondeAoCatalogo").ToArray();
        VerificarRecusas(recusas, referencia, divergente);
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), cadastros)
            .Any(i => i.Codigo == "regra_citada_identificacao.algoritmoContagemPrazo" && !i.Ok).Should().Be(divergente);
    }

    private static void VerificarRecusas(FieldError[] recusas, ReferenciaRegra referencia, bool divergente)
    {
        recusas.Should().HaveCount(divergente ? 1 : 0);
        if (divergente)
        {
            recusas[0].Field.Should().Be("identificacao.algoritmoContagemPrazo");
            recusas[0].Error.Message.Should().NotContain(referencia.Hash).And.NotContain(referencia.Codigo);
        }
    }

    private static ReferenciaRegra Referencia(RegraCatalogo regra, string situacao) => situacao switch
    {
        "hash diferente" => ReferenciaRegra.De(regra.Codigo, regra.Versao, new string('0', 64)),
        "versão ausente" => ReferenciaRegra.De(regra.Codigo, "v999", regra.Referencia().Hash),
        "código ausente" => ReferenciaRegra.De("REGRA-INEXISTENTE", regra.Versao, regra.Referencia().Hash),
        _ => regra.Referencia(),
    };

    private static ProcessoSeletivo CriarProcesso(ReferenciaRegra referencia)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "Processo de teste", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirAlgoritmoContagemPrazo(referencia).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static CadastrosDaConfiguracao ComRegras(RegrasCitadasNaConfiguracao lidas)
    {
        CadastrosDaConfiguracao padrao = CadastrosDeTeste.Padrao;
        return new CadastrosDaConfiguracao(padrao.ContagemDePrazos, padrao.FatosDeModalidade, padrao.TiposDeAtoUnicos,
            lidas.Corretores, padrao.Protecao, padrao.MotivosEscolhidos)
        { Regras = lidas.Regras, MunicipiosDoGeo = padrao.MunicipiosDoGeo, Precedencias = padrao.Precedencias };
    }
}
