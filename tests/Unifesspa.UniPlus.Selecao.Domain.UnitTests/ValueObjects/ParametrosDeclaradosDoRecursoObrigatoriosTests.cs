namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ParametrosDeclaradosDoRecursoObrigatoriosTests
{
    private static ParametrosDeclaradosDoRecurso SemInstanciaSuperiorDeclarada() =>
        ParametrosDoRecursoDeTeste.Declarados(admiteInstanciaSuperior: null);

    [Fact(DisplayName = "A instância superior ausente é recusada no campo próprio e nenhum parâmetro é conferido")]
    public void Conferir_InstanciaSuperiorAusente_Recusa()
    {
        (ParametrosRegraPrazoRecurso? conferidos, IReadOnlyList<RecusaDosParametrosDoRecurso> recusas) = SemInstanciaSuperiorDeclarada().Conferir();

        conferidos.Should().BeNull();
        recusas.Should().ContainSingle().Which.Should().Match<RecusaDosParametrosDoRecurso>(static recusa =>
            recusa.Motivo == MotivoDaRecusaDosParametros.InstanciaSuperiorNaoDeclarada && recusa.Campo == "admiteInstanciaSuperior");
    }

    [Fact(DisplayName = "O prazo da 2ª instância declarado sem a instância superior declarada não vira recusa de prazo sem instância superior")]
    public void Conferir_InstanciaSuperiorAusenteComPrazoDaSegundaInstancia_CortaARegraDependente()
    {
        ParametrosDeclaradosDoRecurso declarados = SemInstanciaSuperiorDeclarada() with
        {
            PrazoDaSegundaInstancia = new PrazoDeclarado(3m, UnidadePrazo.DiasUteis.Codigo()),
        };

        declarados.Conferir().Recusas.Select(static recusa => recusa.Motivo)
            .Should().Equal(MotivoDaRecusaDosParametros.InstanciaSuperiorNaoDeclarada);
    }

    [Fact(DisplayName = "A instância superior ausente acumula com o prazo de interposição e o efeito suspensivo, na ordem dos campos")]
    public void Conferir_InstanciaSuperiorAusenteComOutrasRecusas_Acumula()
    {
        ParametrosDeclaradosDoRecurso declarados = SemInstanciaSuperiorDeclarada() with
        {
            PrazoDeInterposicao = new PrazoDeclarado(0m, UnidadePrazo.DiasUteis.Codigo()),
            EfeitoSuspensivo = null,
        };

        declarados.Conferir().Recusas.Select(static recusa => recusa.Campo)
            .Should().Equal("prazoValor", "admiteInstanciaSuperior", "efeitoSuspensivo");
    }

    [Fact(DisplayName = "Declarar que não admite instância superior é aceito: falso é valor, não ausência")]
    public void Conferir_SemInstanciaSuperior_Aceita()
    {
        ParametrosDoRecursoDeTeste.Declarados(admiteInstanciaSuperior: false).Conferir().Conferidos!
            .AdmiteInstanciaSuperior.Should().BeFalse();
    }
}
