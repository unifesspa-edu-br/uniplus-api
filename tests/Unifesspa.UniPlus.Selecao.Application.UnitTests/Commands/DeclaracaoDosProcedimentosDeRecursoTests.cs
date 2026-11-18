namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>A declaração de procedimentos montada da entrada: o catálogo resolve a regra, e o lugar da janela confere o resto.</summary>
public sealed class DeclaracaoDosProcedimentosDeRecursoTests
{
    private static readonly Guid Item = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly CodigosDoProcedimentoDeRecurso Codigos = CodigosDoProcedimentoDeRecurso.DaEtapa;

    private static RegraCatalogo Regra(string codigo, TipoRegra tipo = TipoRegra.RegraProcedimentoRecurso)
    {
        using JsonDocument vazio = JsonDocument.Parse("{}");
        using JsonDocument invariantes = JsonDocument.Parse("[]");

        return RegraCatalogo.Criar(codigo, "v1", tipo, vazio.RootElement, invariantes.RootElement, "Regra de teste");
    }

    private static readonly RegraCatalogo[] Catalogo =
    [
        Regra(RegraProcedimentoRecursoCodigo.Recalculo),
        Regra(RegraProcedimentoRecursoCodigo.Recorrecao),
        Regra(RegraConsolidacaoCodigo.AvaliacaoUnica, TipoRegra.RegraConsolidacao),
    ];

    private static DeclaracaoDosProcedimentosDeRecurso Declaracao() => new(
        (codigo, versao, _) => Task.FromResult(Catalogo.FirstOrDefault(r => r.Codigo == codigo && r.Versao == versao)),
        Codigos);

    private static ContextoDosProcedimentosDeRecurso Contexto(bool enem = false) => new(
        new HashSet<TipoMotivoDeDecisao> { TipoMotivoDeDecisao.Classificacao },
        new HashSet<Guid> { Item },
        enem);

    private static ProcedimentoDeRecursoInput Entrada(
        string? tipo = TipoMotivoDeDecisaoCodigo.Classificacao,
        AlvoDoProcedimentoDeRecursoInput? alvo = null,
        string? regra = RegraProcedimentoRecursoCodigo.Recalculo,
        string? versao = "v1") => new(tipo, alvo, regra, versao);

    private static Task<Result<ProcedimentosDeRecurso>> Declarar(ContextoDosProcedimentosDeRecurso contexto, params ProcedimentoDeRecursoInput[] entradas) =>
        Declaracao().DeclararAsync(entradas, contexto, "recursos[0].procedimentos", CancellationToken.None);

    [Fact(DisplayName = "Sem procedimento declarado, a janela fica sem procedimentos")]
    public async Task SemEntrada_Vazio()
    {
        Result<ProcedimentosDeRecurso> resultado = await Declaracao().DeclararAsync(null, Contexto(), "x", CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Itens.Should().BeEmpty();
    }

    [Fact(DisplayName = "A referência da regra sai do catálogo resolvido, com o hash dele, e o alvo de item vale com o Id da etapa")]
    public async Task Declaracao_Valida_UsaOCatalogo()
    {
        Result<ProcedimentosDeRecurso> resultado = await Declarar(Contexto(),
            Entrada(alvo: new(Item, null), regra: RegraProcedimentoRecursoCodigo.Recorrecao));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        ProcedimentoDeRecursoDeclarado procedimento = resultado.Value!.Itens.Should().ContainSingle().Subject;
        procedimento.Regra.Should().Be(Catalogo[1].Referencia());
        procedimento.Alvo.Should().Be(AlvoDoProcedimentoDeRecurso.DoItem(Item));
    }

    [Fact(DisplayName = "Tipo fora do vocabulário é recusado no campo do tipo, na posição da entrada")]
    public async Task Tipo_ForaDoVocabulario_Recusa()
    {
        Result<ProcedimentosDeRecurso> resultado = await Declarar(Contexto(), Entrada(tipo: "OUTRO_TIPO"));

        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("recursos[0].procedimentos[0].tipoDeDecisao");
        resultado.Errors[0].Error.Code.Should().Be("MotivoDeDecisao.TipoInvalido");
    }

    [Theory(DisplayName = "Regra que o catálogo não tem, de outro tipo ou sem código ou versão é recusada como procedimento desconhecido")]
    [InlineData("PROCEDIMENTO-RECURSO-INEXISTENTE", "v1")]
    [InlineData(RegraConsolidacaoCodigo.AvaliacaoUnica, "v1")]
    [InlineData(RegraProcedimentoRecursoCodigo.Recalculo, "v9")]
    [InlineData(null, "v1")]
    [InlineData(RegraProcedimentoRecursoCodigo.Recalculo, " ")]
    public async Task Regra_NaoEhProcedimentoDoCatalogo_Recusa(string? codigo, string? versao)
    {
        Result<ProcedimentosDeRecurso> resultado = await Declarar(Contexto(), Entrada(regra: codigo, versao: versao));

        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("recursos[0].procedimentos[0].regra");
        resultado.Errors[0].Error.Code.Should().Be(Codigos.Desconhecido);
    }

    [Fact(DisplayName = "A recusa do procedimento desconhecido diz que ele ou a versão não consta do catálogo, sem repetir o código e a versão enviados")]
    public async Task ProcedimentoDesconhecido_MensagemNaoRepeteOEnviado()
    {
        const string CodigoDigitado = "MARCADOR-FICTICIO-DO-CODIGO";
        const string VersaoDigitada = "MARCADOR-FICTICIO-DA-VERSAO";

        Result<ProcedimentosDeRecurso> resultado = await Declarar(Contexto(), Entrada(regra: CodigoDigitado, versao: VersaoDigitada));

        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("recursos[0].procedimentos[0].regra");
        resultado.Errors[0].Error.Message.Should()
            .Contain("não consta do catálogo")
            .And.Contain(RegraProcedimentoRecursoCodigo.Recalculo)
            .And.NotContain(CodigoDigitado)
            .And.NotContain(VersaoDigitada);
    }

    [Theory(DisplayName = "Alvo com item e área juntos, ou com nenhum dos dois, é recusado como alvo inexistente")]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Alvo_Ambiguo_Recusa(bool comItem, bool comArea)
    {
        AlvoDoProcedimentoDeRecursoInput alvo = new(comItem ? Item : null, comArea ? AreaDoEnem.Matematica : null);

        Result<ProcedimentosDeRecurso> resultado = await Declarar(Contexto(enem: true), Entrada(alvo: alvo));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(Codigos.AlvoInexistente);
    }

    [Fact(DisplayName = "Item novo gravado na mesma operação, sem identidade informada, não é alvo possível: o alvo é recusado como inexistente")]
    public async Task ItemSemIdentidadeConhecida_NaoEhAlvo()
    {
        Result<ProcedimentosDeRecurso> resultado = await Declarar(Contexto(), Entrada(alvo: new(Guid.NewGuid(), null)));

        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("recursos[0].procedimentos[0].alvo");
        resultado.Errors[0].Error.Code.Should().Be(Codigos.AlvoInexistente);
    }

    [Fact(DisplayName = "As recusas de entradas diferentes se acumulam, cada uma na sua posição")]
    public async Task Recusas_AcumulamPorPosicao()
    {
        Result<ProcedimentosDeRecurso> resultado = await Declarar(Contexto(),
            Entrada(tipo: "OUTRO_TIPO"),
            Entrada(),
            Entrada());

        resultado.Errors.Select(e => e.Field).Should().Equal(
            "recursos[0].procedimentos[0].tipoDeDecisao",
            "recursos[0].procedimentos[2]");
        resultado.Errors[1].Error.Code.Should().Be(Codigos.Duplicado);
    }
}
