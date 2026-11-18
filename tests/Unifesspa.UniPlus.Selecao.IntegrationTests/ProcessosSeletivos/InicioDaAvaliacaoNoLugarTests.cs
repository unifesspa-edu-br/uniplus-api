namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using static Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories.FasesNoEnvelope;
using static Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories.InicioDasFases;

public sealed class InicioDaAvaliacaoNoLugarTests
{
    private static readonly DateTimeOffset Vigencia = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Inicio = Vigencia.AddMonths(2);
    private static readonly Fatos SemRegistros = new([], [], [], [], []);

    [Theory(DisplayName = "A avaliação começa no primeiro instante entre o período da própria fase e o registro no lugar")]
    [InlineData("HOMOLOGACAO_INSCRICOES", -3)]
    [InlineData("HOMOLOGACAO_INSCRICOES", 3)]
    [InlineData("ISENCAO", -3)]
    [InlineData("ISENCAO", 3)]
    public void FaseComRegistro_ComecaNoPrimeiroInstante(string codigo, int diasDoRegistro)
    {
        Fase fase = UmaFase(codigo);
        DateTimeOffset registro = Inicio.AddDays(diasDoRegistro);
        Fatos fatos = SemRegistros with { IniciosNoLugar = [new InicioNoLugar(LugarDaDecisao.DaFase(fase.OrigemId), registro)] };
        DateTimeOffset primeiro = registro < Inicio ? registro : Inicio;

        FasesProdutoras([Versao(fase)], primeiro.AddTicks(-1), fatos).Should().BeEmpty();
        FasesProdutoras([Versao(fase)], primeiro, fatos).Should().ContainSingle().Which.Should().Be(fase.OrigemId);
    }

    [Theory(DisplayName = "Sem registro, a avaliação começa no início do período da própria fase")]
    [InlineData("HOMOLOGACAO_INSCRICOES")]
    [InlineData("ISENCAO")]
    public void FaseSemRegistro_ComecaNoInicioDoPeriodo(string codigo)
    {
        Fase fase = UmaFase(codigo);

        FasesProdutoras([Versao(fase)], Inicio.AddTicks(-1), SemRegistros).Should().BeEmpty();
        FasesProdutoras([Versao(fase)], Inicio, SemRegistros).Should().ContainSingle().Which.Should().Be(fase.OrigemId);
    }

    [Fact(DisplayName = "Sem fase nem registro a avaliação não começou, mesmo no maior instante")]
    public void SemFaseNemRegistro_NaoComecou()
    {
        HashSet<Guid> produtoras = FasesProdutoras([], DateTimeOffset.MaxValue, SemRegistros);
        FasesIniciadas iniciadas = new([], fasesProdutoras: produtoras);

        produtoras.Should().BeEmpty();
        iniciadas.AvaliacaoComecou(LugarDaDecisao.DaFase(Guid.CreateVersion7())).Should().BeFalse();
    }

    private static Fase UmaFase(string codigo) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), codigo, null, Inicio, null, ColetaInscricao: false, []);

    private static VersaoDasFases Versao(Fase fase) => new(Guid.CreateVersion7(), Vigencia, null, [fase]);
}
