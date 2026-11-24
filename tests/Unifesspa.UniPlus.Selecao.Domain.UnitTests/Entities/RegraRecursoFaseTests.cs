namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Cobertura de <see cref="RegraRecursoFase.Criar"/>: as invariantes puras que a entidade prova sozinha, a referência por
/// símbolo e a declaração do recurso (prazos, efeito suspensivo, guarda da vaga e instância superior). A resolução contra o
/// catálogo vivo (existe, tipo correto, hash bate) é do handler; a exigência da convenção de contagem é invariante do processo.
/// As recusas de cada parâmetro estão em <see cref="ParametrosDeclaradosDoRecursoTests"/> e em
/// <see cref="RecusasDosParametrosDoRecursoTests"/>.
/// </summary>
public sealed class RegraRecursoFaseTests
{
    private static readonly Guid ProdutoAncoraId = new("77770000-0000-4000-8000-000000000001");

    private static ReferenciaRegra RegraAncorada() => ReferenciaRegra.De(
        RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo(), "v1", new string('a', 64));

    [Fact(DisplayName = "Referencia a regra por símbolo (RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo()), não por literal solto")]
    public void ReferenciaRegraPorSimbolo()
    {
        Result<RegraRecursoFase> resultado = RegraRecursoFase.Criar(RegraAncorada(), ParametrosDoRecursoDeTeste.Declarados(), ProdutoAncoraId);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Regra.Codigo.Should().Be(RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo());
    }

    [Fact(DisplayName = "Referenciar qualquer outra regra do catálogo é recusado")]
    public void RegraDeTipoIncompativel_Recusa()
    {
        ReferenciaRegra outraRegra = ReferenciaRegra.De("FADA_MULTIPLICATIVO", "v1", new string('b', 64));

        Result<RegraRecursoFase> resultado = RegraRecursoFase.Criar(outraRegra, ParametrosDoRecursoDeTeste.Declarados(), ProdutoAncoraId);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraRecursoFase.RegraCatalogoInvalida");
    }

    [Theory(DisplayName = "As duas unidades declaráveis na interposição são aceitas: dias úteis em valor inteiro, e horas")]
    [InlineData(UnidadePrazo.DiasUteis, 2)]
    [InlineData(UnidadePrazo.DiasUteis, 1)]
    [InlineData(UnidadePrazo.Horas, 48)]
    [InlineData(UnidadePrazo.Horas, 6)]
    public void UnidadesDeclaraveis_Aceitas(UnidadePrazo unidade, int valor)
    {
        Result<RegraRecursoFase> resultado = RegraRecursoFase.Criar(
            RegraAncorada(), ParametrosDoRecursoDeTeste.Declarados(valor, unidade), ProdutoAncoraId);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Parametros.PrazoDeInterposicao.Should().Be(new PrazoDoRecurso(valor, unidade));
    }

    [Fact(DisplayName = "Horas com valor fracionário é aceita: a restrição de valor inteiro é da unidade dias úteis")]
    public void HorasFracionarias_Aceitas()
    {
        Result<RegraRecursoFase> resultado = RegraRecursoFase.Criar(
            RegraAncorada(), ParametrosDoRecursoDeTeste.Declarados(1.5m, UnidadePrazo.Horas), ProdutoAncoraId);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Parametros.PrazoDeInterposicao.Valor.Should().Be(1.5m);
    }

    [Fact(DisplayName = "Prazo em dias corridos é recusado com a mensagem que orienta as unidades admitidas")]
    public void PrazoDeInterposicaoEmDiasCorridos_Recusa()
    {
        Result<RegraRecursoFase> resultado = RegraRecursoFase.Criar(
            RegraAncorada(), ParametrosDoRecursoDeTeste.Declarados(5m, UnidadePrazo.Dias), ProdutoAncoraId);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraRecursoFase.PrazoEmDiasCorridos");
        resultado.Error.Message.Should().Be("O prazo de interposição deve ser informado em dias úteis ou em horas; dias corridos não são aceitos.");
        resultado.Errors.Single().Field.Should().Be("prazoUnidade");
    }

    [Fact(DisplayName = "Fração de dia útil é recusada com causa distinta da de dia corrido, e a mensagem orienta usar horas")]
    public void PrazoDeInterposicaoEmFracaoDeDiaUtil_Recusa()
    {
        Result<RegraRecursoFase> resultado = RegraRecursoFase.Criar(
            RegraAncorada(), ParametrosDoRecursoDeTeste.Declarados(1.5m, UnidadePrazo.DiasUteis), ProdutoAncoraId);

        resultado.Error!.Code.Should().Be("RegraRecursoFase.PrazoEmFracaoDeDiaUtil");
        resultado.Error.Message.Should().Contain("horas");
        resultado.Errors.Single().Field.Should().Be("prazoValor");
    }

    [Fact(DisplayName = "Com instância superior e o prazo da 2ª instância declarado, os parâmetros guardam os dois prazos")]
    public void ComInstanciaSuperior_GuardaOsDoisPrazos()
    {
        Result<RegraRecursoFase> resultado = RegraRecursoFase.Criar(
            RegraAncorada(), ParametrosDoRecursoDeTeste.ComInstanciaSuperior(), ProdutoAncoraId);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Parametros.AdmiteInstanciaSuperior.Should().BeTrue();
        resultado.Value.Parametros.PrazoDaSegundaInstancia.Should().Be(new PrazoDoRecurso(3m, UnidadePrazo.DiasUteis));
        resultado.Value.Parametros.EfeitoSuspensivo.Should().Be(EfeitoSuspensivo.SemEfeitoSuspensivo);
        resultado.Value.Parametros.GuardaDaVaga.Should().Be(GuardaDaVaga.AteOResultadoDefinitivo);
    }

    [Fact(DisplayName = "Sem instância superior e sem prazo da 2ª instância, a regra é criada sem prazo da 2ª instância")]
    public void SemInstanciaSuperior_SemPrazoDaSegundaInstancia()
    {
        Result<RegraRecursoFase> resultado = RegraRecursoFase.Criar(
            RegraAncorada(), ParametrosDoRecursoDeTeste.Declarados(guardaDaVaga: "NAO_GUARDA"), ProdutoAncoraId);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Parametros.PrazoDaSegundaInstancia.Should().BeNull();
        resultado.Value.Parametros.GuardaDaVaga.Should().Be(GuardaDaVaga.NaoGuarda);
    }

    [Fact(DisplayName = "Reidratar aplica as mesmas conferências de Criar e exige a publicação de referência registrada")]
    public void Reidratar_MesmasConferencias()
    {
        RegraRecursoFase.Reidratar(RegraAncorada(), ParametrosDoRecursoDeTeste.Declarados(0m, UnidadePrazo.Horas), ProdutoAncoraId, ProcedimentosDeRecurso.Nenhum)
            .Error!.Code.Should().Be("RegraRecursoFase.PrazoNaoPositivo");
        Action semPublicacaoDeReferencia = () => RegraRecursoFase.Reidratar(RegraAncorada(), ParametrosDoRecursoDeTeste.Declarados(), Guid.Empty, ProcedimentosDeRecurso.Nenhum);
        semPublicacaoDeReferencia.Should().Throw<ArgumentException>();
    }
}
