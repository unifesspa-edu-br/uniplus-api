namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Cobre CA-06 a CA-09 e CA-12 da Story #847 (ADR-0111 — matriz operador × domínio).</summary>
public sealed class PredicadoDnfValidadorTests
{
    private static readonly Dictionary<string, DescritorFatoCandidato> Vocabulario = new()
    {
        ["PCD"] = DescritorFatoCandidato.Criar("PCD", TipoDominioFato.Booleano, null).Value!,
        ["FAIXA_ETARIA"] = DescritorFatoCandidato.Criar("FAIXA_ETARIA", TipoDominioFato.Numerico, null).Value!,
        ["COR_RACA"] = DescritorFatoCandidato.Criar(
            "COR_RACA", TipoDominioFato.CategoricoEstatico, ["BRANCA", "PRETA", "PARDA", "AMARELA", "INDIGENA", "NAO_INFORMADO"]).Value!,
    };

    private static JsonElement Json(string raw)
    {
        using JsonDocument document = JsonDocument.Parse(raw);
        return document.RootElement.Clone();
    }

    private static PredicadoDnf PredicadoDe(CondicaoDnf condicao) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, condicao)]).Value!;

    private static Result Validar(CondicaoDnf condicao, IReadOnlySet<string>? fatosColetados = null) =>
        PredicadoDnfValidador.Validar(PredicadoDe(condicao), Vocabulario, fatosColetados);

    [Fact(DisplayName = "PredicadoDnfValidador_Rejeita_Fato_Desconhecido")]
    public void PredicadoDnfValidador_Rejeita_Fato_Desconhecido()
    {
        CondicaoDnf condicao = CondicaoDnf.Criar("FATO_INEXISTENTE", Operador.Igual, Json("true")).Value!;

        Result resultado = Validar(condicao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.FatoDesconhecido");
    }

    [Fact(DisplayName = "PredicadoDnfValidador_Aceita_Fato_Conhecido")]
    public void PredicadoDnfValidador_Aceita_Fato_Conhecido()
    {
        CondicaoDnf condicao = CondicaoDnf.Criar("PCD", Operador.Igual, Json("true")).Value!;

        Validar(condicao).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "PredicadoDnfValidador_Rejeita_Booleano_Com_Em")]
    public void PredicadoDnfValidador_Rejeita_Booleano_Com_Em()
    {
        CondicaoDnf condicao = CondicaoDnf.Criar("PCD", Operador.Em, Json("[true]")).Value!;

        Result resultado = Validar(condicao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.OperadorIncompativelComDominio");
    }

    [Fact(DisplayName = "PredicadoDnfValidador_Rejeita_Numerico_Com_Em")]
    public void PredicadoDnfValidador_Rejeita_Numerico_Com_Em()
    {
        CondicaoDnf condicao = CondicaoDnf.Criar("FAIXA_ETARIA", Operador.Em, Json("[18,19]")).Value!;

        Result resultado = Validar(condicao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.OperadorIncompativelComDominio");
    }

    [Fact(DisplayName = "PredicadoDnfValidador_Aceita_Booleano_Com_Igual")]
    public void PredicadoDnfValidador_Aceita_Booleano_Com_Igual()
    {
        CondicaoDnf condicao = CondicaoDnf.Criar("PCD", Operador.Igual, Json("false")).Value!;

        Validar(condicao).IsSuccess.Should().BeTrue();
    }

    [Theory(DisplayName = "PredicadoDnfValidador_Aceita_Numerico_Com_Comparacao")]
    [InlineData("MAIOR_IGUAL")]
    [InlineData("MENOR_IGUAL")]
    public void PredicadoDnfValidador_Aceita_Numerico_Com_Comparacao(string operadorCodigo)
    {
        CondicaoDnf condicao = CondicaoDnf.Criar("FAIXA_ETARIA", OperadorCodigo.FromCodigo(operadorCodigo), Json("60")).Value!;

        Validar(condicao).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "PredicadoDnfValidador_Rejeita_Valor_Nao_Booleano")]
    public void PredicadoDnfValidador_Rejeita_Valor_Nao_Booleano()
    {
        CondicaoDnf condicao = CondicaoDnf.Criar("PCD", Operador.Igual, Json("\"X\"")).Value!;

        Result resultado = Validar(condicao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.ValorIncompativelComTipo");
    }

    [Fact(DisplayName = "PredicadoDnfValidador_Rejeita_Valor_Decimal_Em_Fato_Numerico")]
    public void PredicadoDnfValidador_Rejeita_Valor_Decimal_Em_Fato_Numerico()
    {
        CondicaoDnf condicao = CondicaoDnf.Criar("FAIXA_ETARIA", Operador.Igual, Json("18.5")).Value!;

        Result resultado = Validar(condicao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.ValorIncompativelComTipo");
    }

    [Fact(DisplayName = "PredicadoDnfValidador_Aceita_Valor_Inteiro_Em_Fato_Numerico")]
    public void PredicadoDnfValidador_Aceita_Valor_Inteiro_Em_Fato_Numerico()
    {
        CondicaoDnf condicao = CondicaoDnf.Criar("FAIXA_ETARIA", Operador.Igual, Json("18")).Value!;

        Validar(condicao).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "PredicadoDnfValidador_Rejeita_Valor_Fora_Do_Dominio_Categorico")]
    public void PredicadoDnfValidador_Rejeita_Valor_Fora_Do_Dominio_Categorico()
    {
        CondicaoDnf condicao = CondicaoDnf.Criar("COR_RACA", Operador.Igual, Json("\"ROXO\"")).Value!;

        Result resultado = Validar(condicao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.ValorForaDoDominio");
    }

    [Fact(DisplayName = "PredicadoDnfValidador_Aceita_Valor_No_Dominio_Categorico")]
    public void PredicadoDnfValidador_Aceita_Valor_No_Dominio_Categorico()
    {
        CondicaoDnf condicao = CondicaoDnf.Criar("COR_RACA", Operador.Igual, Json("\"PARDA\"")).Value!;

        Validar(condicao).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "PredicadoDnfValidador aceita EM categórico com todos os valores no domínio")]
    public void PredicadoDnfValidador_Aceita_Em_Categorico_No_Dominio()
    {
        CondicaoDnf condicao = CondicaoDnf.Criar("COR_RACA", Operador.Em, Json("[\"PRETA\",\"PARDA\"]")).Value!;

        Validar(condicao).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "PredicadoDnfValidador_Rejeita_Fato_Nao_Coletado_Pelo_Processo")]
    public void PredicadoDnfValidador_Rejeita_Fato_Nao_Coletado_Pelo_Processo()
    {
        CondicaoDnf condicao = CondicaoDnf.Criar("PCD", Operador.Igual, Json("true")).Value!;
        HashSet<string> coletados = ["COR_RACA"];

        Result resultado = Validar(condicao, coletados);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.FatoNaoColetadoPeloProcesso");
    }

    [Fact(DisplayName = "PredicadoDnfValidador_Aceita_Fato_Coletado_Pelo_Processo")]
    public void PredicadoDnfValidador_Aceita_Fato_Coletado_Pelo_Processo()
    {
        CondicaoDnf condicao = CondicaoDnf.Criar("PCD", Operador.Igual, Json("true")).Value!;
        HashSet<string> coletados = ["PCD"];

        Validar(condicao, coletados).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "PredicadoDnfValidador_Sem_FatosColetados_Nao_Aplica_Checagem_Extra")]
    public void PredicadoDnfValidador_Sem_FatosColetados_Nao_Aplica_Checagem_Extra()
    {
        CondicaoDnf condicao = CondicaoDnf.Criar("PCD", Operador.Igual, Json("true")).Value!;

        Validar(condicao, fatosColetados: null).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "PredicadoDnfValidador aceita quando o fato é desconhecido no vocabulário mesmo com fatosColetados informado — desconhecido tem precedência")]
    public void PredicadoDnfValidador_FatoDesconhecido_TemPrecedenciaSobreNaoColetado()
    {
        CondicaoDnf condicao = CondicaoDnf.Criar("FATO_INEXISTENTE", Operador.Igual, Json("true")).Value!;
        HashSet<string> coletados = ["FATO_INEXISTENTE"];

        Result resultado = Validar(condicao, coletados);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.FatoDesconhecido");
    }

    // ── Story #554 (PR-b, issue #892) — domínio categórico dinâmico ──

    private static readonly Dictionary<string, DescritorFatoCandidato> VocabularioComDinamico = new(Vocabulario)
    {
        ["MODALIDADE"] = DescritorFatoCandidato.Criar("MODALIDADE", TipoDominioFato.CategoricoDinamico, null).Value!,
    };

    private static Result ValidarComDominioDinamico(
        CondicaoDnf condicao, IReadOnlyDictionary<string, IReadOnlySet<string>>? dominiosDinamicos) =>
        PredicadoDnfValidador.Validar(PredicadoDe(condicao), VocabularioComDinamico, null, dominiosDinamicos);

    [Fact(DisplayName = "Aceita IGUAL/EM categórico dinâmico com domínio fornecido pelo chamador")]
    public void Validar_CategoricoDinamico_DominioFornecido_Aceita()
    {
        Dictionary<string, IReadOnlySet<string>> dominios = new() { ["MODALIDADE"] = new HashSet<string> { "LB_PPI", "AC" } };

        CondicaoDnf igual = CondicaoDnf.Criar("MODALIDADE", Operador.Igual, Json("\"LB_PPI\"")).Value!;
        CondicaoDnf em = CondicaoDnf.Criar("MODALIDADE", Operador.Em, Json("[\"LB_PPI\",\"AC\"]")).Value!;

        ValidarComDominioDinamico(igual, dominios).IsSuccess.Should().BeTrue();
        ValidarComDominioDinamico(em, dominios).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "CA-03: rejeita valor categórico dinâmico fora do domínio ofertado pelo processo")]
    public void Validar_CategoricoDinamico_ForaDoDominio_Recusa()
    {
        Dictionary<string, IReadOnlySet<string>> dominios = new() { ["MODALIDADE"] = new HashSet<string> { "AC" } };
        CondicaoDnf condicao = CondicaoDnf.Criar("MODALIDADE", Operador.Igual, Json("\"LB_Q\"")).Value!;

        Result resultado = ValidarComDominioDinamico(condicao, dominios);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.ValorForaDoDominio");
    }

    [Fact(DisplayName = "Rejeita categórico dinâmico quando o chamador não forneceu o domínio")]
    public void Validar_CategoricoDinamico_SemDominioFornecido_Recusa()
    {
        CondicaoDnf condicao = CondicaoDnf.Criar("MODALIDADE", Operador.Igual, Json("\"AC\"")).Value!;

        Result resultado = ValidarComDominioDinamico(condicao, dominiosDinamicos: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.DominioDinamicoNaoFornecido");
    }

    [Fact(DisplayName = "Rejeita MAIOR_IGUAL/MENOR_IGUAL para categórico dinâmico (mesma matriz do estático)")]
    public void Validar_CategoricoDinamico_OperadorNumerico_Recusa()
    {
        Dictionary<string, IReadOnlySet<string>> dominios = new() { ["MODALIDADE"] = new HashSet<string> { "AC" } };
        CondicaoDnf condicao = CondicaoDnf.Criar("MODALIDADE", Operador.MaiorIgual, Json("\"AC\"")).Value!;

        Result resultado = ValidarComDominioDinamico(condicao, dominios);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("PredicadoDnf.OperadorIncompativelComDominio");
    }
}
