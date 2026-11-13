namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O dia civil em que o catálogo de tipos de ato é consultado é o de antes de qualquer leitura: se a meia-noite do fuso
/// institucional passa enquanto as vagas ocupadas são lidas, a consulta continua valendo para o dia em que a operação
/// começou.
/// </summary>
public sealed class DiaDaConsultaDosTiposDeAtoTests
{
    private static readonly DateOnly DiaDeInicio = new(2026, 10, 16);
    private static readonly DateTimeOffset InstanteDeInicio = new(2026, 10, 17, 2, 30, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo Fuso = TimeZoneInfo.CreateCustomTimeZone("fuso-institucional", TimeSpan.FromHours(-3), "fuso", "fuso");

    [Fact(DisplayName = "A conferência consulta o catálogo no dia calculado antes da leitura das vagas, ainda que a meia-noite passe durante ela")]
    public async Task Conferir_ConsultaNoDiaCalculadoAntesDaLeitura()
    {
        Cenario cenario = new();

        await new ResolucaoDosTiposDeAtoUnicos(cenario.Catalogo, cenario.Atos, cenario.Relogio).ConferirAsync(
            cenario.Processo, TipoDaOperacao(), [cenario.AtoDaLinhagem], Fuso, SemCongelados(), CancellationToken.None);

        await cenario.Catalogo.Received(1).ListarVigentesAsync(Arg.Any<IReadOnlyCollection<string>>(), DiaDeInicio, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O checklist consulta o catálogo no dia calculado antes da leitura das vagas, ainda que a meia-noite passe durante ela")]
    public async Task ParaChecklist_ConsultaNoDiaCalculadoAntesDaLeitura()
    {
        Cenario cenario = new();

        await new ResolucaoDosTiposDeAtoUnicos(cenario.Catalogo, cenario.Atos, cenario.Relogio)
            .ParaChecklistAsync(cenario.Processo, [cenario.AtoDaLinhagem], Fuso, SemCongelados(), CancellationToken.None);

        await cenario.Catalogo.Received(1).ListarVigentesAsync(Arg.Any<IReadOnlyCollection<string>>(), DiaDeInicio, Arg.Any<CancellationToken>());
    }

    private static TipoAtoPublicadoView TipoDaOperacao() => new("EDITAL_DA_OPERACAO", "Edital", true, true, false, false);

    private static Dictionary<string, TipoDeAtoCongelado> SemCongelados() => new(StringComparer.Ordinal);

    private sealed class Cenario
    {
        public Cenario()
        {
            Relogio = new RelogioQueAvanca(InstanteDeInicio);
            Atos.ListarTiposDosAtosAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(_ => AvancarDuasHorasENomearOcupados());
            Catalogo.ListarVigentesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
                .Returns([]);
            Processo = ProcessoComUmaFase();
        }

        public RelogioQueAvanca Relogio { get; }

        public ITipoAtoPublicadoReader Catalogo { get; } = Substitute.For<ITipoAtoPublicadoReader>();

        public IAtoDoProcessoRepository Atos { get; } = Substitute.For<IAtoDoProcessoRepository>();

        public ProcessoSeletivo Processo { get; }

        public Guid AtoDaLinhagem { get; } = Guid.CreateVersion7();

        private static ProcessoSeletivo ProcessoComUmaFase()
        {
            ProcessoSeletivo processo = ProcessoSeletivo.Criar(
                "PS Dia da consulta", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
                UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
                LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
            processo.DefinirCronogramaFases(
                [FaseCronograma.Criar(
                    1, Guid.CreateVersion7(), "CLASSIFICACAO", OrigemDataFase.NaExecucao,
                    agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
                    inicio: null, fim: null, produtos: [ProdutoDaFase.Criar("RESULTADO_X", PapelProdutoFase.Definitivo)],
                    faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!],
                [],
                PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

            return processo;
        }

        private IReadOnlyList<string> AvancarDuasHorasENomearOcupados()
        {
            Relogio.Avancar(TimeSpan.FromHours(2));

            return ["EDITAL_JA_REGISTRADO"];
        }
    }

    private sealed class RelogioQueAvanca(DateTimeOffset inicio) : TimeProvider
    {
        private DateTimeOffset _agora = inicio;

        public override DateTimeOffset GetUtcNow() => _agora;

        public void Avancar(TimeSpan intervalo) => _agora += intervalo;
    }
}
