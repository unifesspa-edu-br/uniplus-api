namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>As opções que a configuração do recurso oferece à tela: a mesma lista que a gravação aceita, com o código que o contrato publica.</summary>
public sealed class VocabulariosDoRecursoTests
{
    [Fact(DisplayName = "As unidades do prazo oferecidas são horas e dias úteis, sem a de dias corridos que o domínio recusa")]
    public void UnidadesDoPrazo_OferecemSoAsDeclaraveis()
    {
        IReadOnlyList<CodigoComRotulo<UnidadePrazo>> unidades = ListarUnidadesDoPrazoDeRecursoQueryHandler.Handle(new ListarUnidadesDoPrazoDeRecursoQuery());

        unidades.Select(static u => u.Codigo).Should().Equal("horas", "diasUteis");
        unidades.Should().OnlyContain(static u => !string.IsNullOrWhiteSpace(u.Rotulo));
        PrazoDoRecurso.UnidadesDeclaraveis.Should().Equal(UnidadePrazo.Horas, UnidadePrazo.DiasUteis);
    }

    [Fact(DisplayName = "Os efeitos suspensivos oferecidos são todos os do enum, cada um com o rótulo e a descrição")]
    public void EfeitosSuspensivos_OferecemTodosComDescricao()
    {
        IReadOnlyList<CodigoComDescricao<EfeitoSuspensivo>> efeitos = ListarEfeitosSuspensivosQueryHandler.Handle(new ListarEfeitosSuspensivosQuery());

        efeitos.Select(static e => e.Codigo).Should().Equal("semEfeitoSuspensivo");
        efeitos.Should().OnlyContain(static e => !string.IsNullOrWhiteSpace(e.Rotulo) && !string.IsNullOrWhiteSpace(e.Descricao));
    }

    [Fact(DisplayName = "As guardas da vaga oferecidas são todas as do enum, na ordem declarada, cada uma com o rótulo e a descrição")]
    public void GuardasDaVaga_OferecemTodasComDescricao()
    {
        IReadOnlyList<CodigoComDescricao<GuardaDaVaga>> guardas = ListarGuardasDaVagaQueryHandler.Handle(new ListarGuardasDaVagaQuery());

        guardas.Select(static g => g.Codigo).Should().Equal("ateOResultadoDefinitivo", "naoGuarda");
        guardas.Should().OnlyContain(static g => !string.IsNullOrWhiteSpace(g.Rotulo) && !string.IsNullOrWhiteSpace(g.Descricao));
    }

    [Fact(DisplayName = "As leituras exigem a pergunta")]
    public void Leituras_SemPergunta_Lancam()
    {
        FluentActions.Invoking(() => ListarUnidadesDoPrazoDeRecursoQueryHandler.Handle(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => ListarEfeitosSuspensivosQueryHandler.Handle(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => ListarGuardasDaVagaQueryHandler.Handle(null!)).Should().Throw<ArgumentNullException>();
    }
}
