namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Recursos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Recursos.ProcedimentosDeRecursoParaTestes;

/// <summary>A declaração de procedimentos conferida contra o lugar da janela, com as recusas acumuladas por posição.</summary>
public sealed class ConferenciaDosProcedimentosDeRecursoTests
{
    private static readonly CodigosDoProcedimentoDeRecurso Codigos = CodigosDoProcedimentoDeRecurso.DaEtapa;

    private static IReadOnlyList<FieldError> Faltas(ContextoDosProcedimentosDeRecurso contexto, params ProcedimentoDeRecursoDeclarado[] declarados) =>
        ConferenciaDosProcedimentosDeRecurso.Faltas(declarados, contexto, Codigos);

    [Fact(DisplayName = "Declaração vazia não tem falta: a ausência é cobrada na publicação, não na gravação")]
    public void Vazia_SemFalta() => Faltas(Contexto()).Should().BeEmpty();

    [Fact(DisplayName = "Procedimento do tipo que o lugar declara, com regra do rol e sem alvo, não tem falta")]
    public void Completo_SemFalta() => Faltas(Contexto(), Procedimento()).Should().BeEmpty();

    [Fact(DisplayName = "Regra que não é do rol de procedimentos é recusada no campo da regra")]
    public void Regra_ForaDoRol_Recusa()
    {
        IReadOnlyList<FieldError> faltas = Faltas(Contexto(), Procedimento(codigoDaRegra: "CONSOLIDACAO-AVALIACAO-UNICA"));

        faltas.Should().ContainSingle();
        faltas[0].Field.Should().Be("procedimentosDeRecurso[0].regra");
        faltas[0].Error.Code.Should().Be(Codigos.Desconhecido);
    }

    [Fact(DisplayName = "Tipo de decisão que o lugar não declara produzir é recusado no campo do tipo")]
    public void Tipo_NaoDeclaradoPeloLugar_Recusa()
    {
        IReadOnlyList<FieldError> faltas = Faltas(Contexto(), Procedimento(TipoMotivoDeDecisao.AnaliseDocumental));

        faltas.Should().ContainSingle();
        faltas[0].Field.Should().Be("procedimentosDeRecurso[0].tipoDeDecisao");
        faltas[0].Error.Code.Should().Be(Codigos.ParaDecisaoNaoDeclarada);
    }

    [Fact(DisplayName = "Alvo de item que a etapa produtora tem é aceito; item que ela não tem é recusado como alvo inexistente")]
    public void AlvoDeItem_ExisteOuNao()
    {
        Faltas(Contexto(), Procedimento(alvo: AlvoItem())).Should().BeEmpty();

        IReadOnlyList<FieldError> faltas = Faltas(Contexto(), Procedimento(alvo: AlvoItem(OutroItem)));

        faltas.Should().ContainSingle().Which.Error.Code.Should().Be(Codigos.AlvoInexistente);
        faltas[0].Field.Should().Be("procedimentosDeRecurso[0].alvo");
    }

    [Fact(DisplayName = "Alvo de item sem a identidade da etapa (identidade vazia) é recusado como alvo inexistente")]
    public void AlvoDeItem_IdentidadeVazia_Recusa() =>
        Faltas(Contexto(), Procedimento(alvo: AlvoItem(Guid.Empty))).Should().ContainSingle()
            .Which.Error.Code.Should().Be(Codigos.AlvoInexistente);

    [Fact(DisplayName = "Área do ENEM só é alvo possível em processo que importa a nota do ENEM")]
    public void AlvoDeArea_ExigeNotaDoEnem()
    {
        Faltas(Contexto(processoTemNotaDoEnem: true), Procedimento(alvo: AlvoArea())).Should().BeEmpty();

        Faltas(Contexto(processoTemNotaDoEnem: false), Procedimento(alvo: AlvoArea())).Should().ContainSingle()
            .Which.Error.Code.Should().Be(Codigos.AlvoInexistente);
    }

    [Fact(DisplayName = "Área fora das cinco áreas do ENEM é recusada como alvo inexistente")]
    public void AlvoDeArea_ForaDoVocabulario_Recusa() =>
        Faltas(Contexto(processoTemNotaDoEnem: true), Procedimento(alvo: AlvoArea("GEOGRAFIA"))).Should().ContainSingle()
            .Which.Error.Code.Should().Be(Codigos.AlvoInexistente);

    [Fact(DisplayName = "A mesma matéria declarada duas vezes é recusada na segunda posição, mesmo com regras diferentes")]
    public void MesmaMateria_Duplicada_RecusaNaSegunda()
    {
        IReadOnlyList<FieldError> faltas = Faltas(Contexto(),
            Procedimento(codigoDaRegra: RegraProcedimentoRecursoCodigo.Recalculo),
            Procedimento(codigoDaRegra: RegraProcedimentoRecursoCodigo.Conferencia));

        faltas.Should().ContainSingle();
        faltas[0].Field.Should().Be("procedimentosDeRecurso[1]");
        faltas[0].Error.Code.Should().Be(Codigos.Duplicado);
    }

    [Theory(DisplayName = "Tipo coberto por declaração sem alvo e por declaração com alvo é conflitante, em qualquer ordem, na posição que fecha o par")]
    [InlineData(true)]
    [InlineData(false)]
    public void SemAlvoEComAlvo_Conflita(bool semAlvoPrimeiro)
    {
        ProcedimentoDeRecursoDeclarado semAlvo = Procedimento();
        ProcedimentoDeRecursoDeclarado comAlvo = Procedimento(alvo: AlvoItem());

        IReadOnlyList<FieldError> faltas = semAlvoPrimeiro ? Faltas(Contexto(), semAlvo, comAlvo) : Faltas(Contexto(), comAlvo, semAlvo);

        faltas.Should().ContainSingle();
        faltas[0].Field.Should().Be("procedimentosDeRecurso[1]");
        faltas[0].Error.Code.Should().Be(Codigos.Conflitante);
    }

    [Fact(DisplayName = "Alvos diferentes do mesmo tipo convivem, e o mesmo alvo em tipos diferentes também")]
    public void AlvosDiferentes_Convivem()
    {
        ContextoDosProcedimentosDeRecurso contexto = Contexto(
            processoTemNotaDoEnem: true,
            tiposDoLugar: [TipoMotivoDeDecisao.Classificacao, TipoMotivoDeDecisao.AnaliseDocumental],
            itens: [Item, OutroItem]);

        Faltas(contexto,
            Procedimento(alvo: AlvoItem()),
            Procedimento(alvo: AlvoItem(OutroItem)),
            Procedimento(alvo: AlvoArea(AreaDoEnem.Redacao)),
            Procedimento(TipoMotivoDeDecisao.AnaliseDocumental, AlvoItem())).Should().BeEmpty();
    }

    [Fact(DisplayName = "As recusas de um mesmo procedimento se acumulam: tipo, alvo e regra, nessa ordem")]
    public void Faltas_AcumulamNaOrdem()
    {
        IReadOnlyList<FieldError> faltas = Faltas(Contexto(),
            Procedimento(TipoMotivoDeDecisao.AnaliseDocumental, AlvoItem(OutroItem), "OUTRA-REGRA"));

        faltas.Select(f => f.Error.Code).Should().Equal(Codigos.ParaDecisaoNaoDeclarada, Codigos.AlvoInexistente, Codigos.Desconhecido);
    }

    [Fact(DisplayName = "A janela de fase usa os códigos do prefixo da regra de recurso da fase")]
    public void JanelaDeFase_UsaOsCodigosDaFase()
    {
        IReadOnlyList<FieldError> faltas = ConferenciaDosProcedimentosDeRecurso.Faltas(
            [Procedimento(TipoMotivoDeDecisao.AnaliseDocumental)], Contexto(), CodigosDoProcedimentoDeRecurso.DaFase, "regraRecurso.procedimentos");

        faltas.Should().ContainSingle();
        faltas[0].Field.Should().Be("regraRecurso.procedimentos[0].tipoDeDecisao");
        faltas[0].Error.Code.Should().Be("RegraRecursoFase.ProcedimentoParaDecisaoNaoDeclarada");
    }

    [Fact(DisplayName = "Com a declaração dos tipos do lugar recusada, o tipo do procedimento não é recusado e o alvo e a regra continuam sendo")]
    public void TiposDoLugarRecusados_NaoRecusaOTipoDoProcedimento()
    {
        ContextoDosProcedimentosDeRecurso semTipos = Contexto() with { TiposDeDecisaoDoLugar = null };

        IReadOnlyList<FieldError> faltas = Faltas(semTipos,
            Procedimento(TipoMotivoDeDecisao.AnaliseDocumental, AlvoItem(OutroItem), "OUTRA-REGRA"));

        faltas.Select(f => f.Error.Code).Should().Equal(Codigos.AlvoInexistente, Codigos.Desconhecido);
    }

    [Fact(DisplayName = "Com a declaração dos tipos do lugar recusada, a coerência entre procedimentos continua sendo conferida")]
    public void TiposDoLugarRecusados_MantemACoerencia()
    {
        ContextoDosProcedimentosDeRecurso semTipos = Contexto() with { TiposDeDecisaoDoLugar = null };

        IReadOnlyList<FieldError> faltas = Faltas(semTipos, Procedimento(), Procedimento());

        faltas.Should().ContainSingle().Which.Error.Code.Should().Be(Codigos.Duplicado);
    }
}
