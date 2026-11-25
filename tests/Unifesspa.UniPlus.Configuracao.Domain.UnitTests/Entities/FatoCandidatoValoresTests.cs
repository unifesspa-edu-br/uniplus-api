namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.FatoCandidatoDeTeste;

/// <summary>
/// Os valores de domínio do fato categórico do administrador: acrescentar, corrigir, reordenar,
/// ativar e desativar, e o que o fato de sistema e o de fonte do processo recusam.
/// </summary>
public sealed class FatoCandidatoValoresTests
{
    [Fact(DisplayName = "Valor de domínio em fato de fonte do processo é recusado")]
    public void AdicionarValorDominio_FonteProcesso_Recusa()
    {
        FatoCandidato fato = Criar(
            descritivo: Descritivo with { Codigo = "TIPO_DEFICIENCIA" },
            definicao: Definicao with { FonteValores = FonteValoresFato.OpcoesDoProcesso },
            binding: "CAMPO_FORMULARIO:TIPO_DEFICIENCIA").Value!;

        fato.AdicionarValorDominio("VISUAL", "Deficiência visual", 0, ativo: true)
            .Error!.Code.Should().Be(FatoValorDominioErrorCodes.NaoPermitidoForaDeFonteGlobal);
    }

    [Fact(DisplayName = "Acrescentar valor acumula as violações do código, da descrição, da orientação e da ordem")]
    public void AdicionarValorDominio_AcumulaViolacoes() =>
        Criar().Value!.AdicionarValorDominio(" ", null, -1, ativo: true, new string('x', FatoValorDominio.OrientacaoMaxLength + 1))
            .Errors.Select(static e => e.Field).Should().BeEquivalentTo(["codigo", "descricao", "orientacao", "ordem"]);

    [Fact(DisplayName = "Valor do fato do administrador desativa e reativa; o do fato de sistema, não")]
    public void DesativarValor_SoNoFatoDoAdministrador()
    {
        FatoCandidato doAdministrador = Criar().Value!;
        doAdministrador.AdicionarValorDominio("PRETA", "Preta", 0, ativo: true).IsSuccess.Should().BeTrue();

        doAdministrador.DesativarValor("PRETA").IsSuccess.Should().BeTrue();
        doAdministrador.DesativarValor("PRETA").Error!.Code.Should().Be(FatoValorDominioErrorCodes.JaDesativado);
        doAdministrador.ReativarValor("PRETA").IsSuccess.Should().BeTrue();
        doAdministrador.DesativarValor("INEXISTENTE").Error!.Code.Should().Be(FatoValorDominioErrorCodes.NaoEncontrado);

        Criar(sistema: true).Value!.DesativarValor("PRETA")
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.DefinicaoDoFatoDeSistemaTravada);
    }

    [Fact(DisplayName = "Corrigir valor troca a descrição e a orientação, inclusive de valor desativado, e preserva o código")]
    public void CorrigirValor_TrocaOsTextosEPreservaOCodigo()
    {
        FatoCandidato fato = Criar().Value!;
        fato.AdicionarValorDominio("EJA", "Educação de jovens", 3, ativo: true, "Texto antigo").IsSuccess.Should().BeTrue();
        fato.DesativarValor("EJA").IsSuccess.Should().BeTrue();

        Result resultado = fato.CorrigirValor("EJA", "  Educação de jovens e adultos ", "  Cursou o supletivo. ");

        resultado.IsSuccess.Should().BeTrue();
        FatoValorDominio valor = fato.ValoresDominioDeclarados.Single();
        (valor.Codigo, valor.Descricao, valor.Orientacao, valor.Ordem, valor.Ativo)
            .Should().Be(("EJA", "Educação de jovens e adultos", "Cursou o supletivo.", 3, false));
    }

    [Fact(DisplayName = "Corrigir valor acumula a descrição obrigatória e o tamanho da orientação")]
    public void CorrigirValor_AcumulaViolacoes()
    {
        FatoCandidato fato = Criar().Value!;
        fato.AdicionarValorDominio("EJA", "Educação de jovens", 0, ativo: true).IsSuccess.Should().BeTrue();

        Result resultado = fato.CorrigirValor("EJA", " ", new string('x', FatoValorDominio.OrientacaoMaxLength + 1));

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("descricao", FatoValorDominioErrorCodes.DescricaoObrigatoria),
            ("orientacao", FatoValorDominioErrorCodes.OrientacaoTamanho),
        ]);
        fato.ValoresDominioDeclarados.Single().Descricao.Should().Be("Educação de jovens");
    }

    [Fact(DisplayName = "Corrigir valor inexistente é recusado, e o fato de sistema não tem valor corrigido")]
    public void CorrigirValor_InexistenteESistema_Recusa()
    {
        FatoCandidato fato = Criar().Value!;

        fato.CorrigirValor("NAO_EXISTE", "Descrição", null).Error!.Code.Should().Be(FatoValorDominioErrorCodes.NaoEncontrado);
        Criar(sistema: true).Value!.CorrigirValor("PRETA", "Descrição", null)
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.DefinicaoDoFatoDeSistemaTravada);
    }

    [Fact(DisplayName = "Reordenar valores põe ativos e desativados na ordem da lista")]
    public void ReordenarValores_AplicaAListaInteira()
    {
        FatoCandidato fato = Criar().Value!;
        foreach ((string codigo, int ordem) in new[] { ("A", 0), ("B", 1), ("C", 2) })
        {
            fato.AdicionarValorDominio(codigo, codigo, ordem, ativo: true).IsSuccess.Should().BeTrue();
        }

        fato.DesativarValor("B").IsSuccess.Should().BeTrue();

        fato.ReordenarValores(["C", "B", "A"]).IsSuccess.Should().BeTrue();

        fato.ValoresDominioDeclarados.OrderBy(static v => v.Ordem).Select(static v => v.Codigo).Should().Equal("C", "B", "A");
        fato.ValoresDominioDeclarados.Single(static v => v.Codigo == "B").Ativo.Should().BeFalse();
    }

    [Fact(DisplayName = "Reordenar com código ausente, repetido e estranho acumula as três recusas e não muda a ordem")]
    public void ReordenarValores_ListaIncompleta_AcumulaERecusaInteira()
    {
        FatoCandidato fato = Criar().Value!;
        fato.AdicionarValorDominio("A", "A", 0, ativo: true).IsSuccess.Should().BeTrue();
        fato.AdicionarValorDominio("B", "B", 1, ativo: true).IsSuccess.Should().BeTrue();

        Result resultado = fato.ReordenarValores(["A", "A", "Z"]);

        resultado.Errors.Select(static e => e.Error.Code).Should().BeEquivalentTo(
        [
            FatoValorDominioErrorCodes.OrdemComCodigoDesconhecido,
            FatoValorDominioErrorCodes.OrdemComCodigoRepetido,
            FatoValorDominioErrorCodes.OrdemSemTodosOsValores,
        ]);
        fato.ValoresDominioDeclarados.OrderBy(static v => v.Ordem).Select(static v => v.Codigo).Should().Equal("A", "B");
    }

    [Fact(DisplayName = "Recusas de códigos na ordem não repetem os valores enviados")]
    public void ReordenarValores_CodigosDesconhecidosERepetidos_NaoEcoa()
    {
        FatoCandidato fato = Criar().Value!;
        fato.AdicionarValorDominio("VALOR_EXISTENTE", "Valor", 0, ativo: true).IsSuccess.Should().BeTrue();

        Result resultado = fato.ReordenarValores(["VALOR_EXISTENTE", "VALOR_EXISTENTE", "VALOR_DESCONHECIDO"]);

        FieldError desconhecido = resultado.Errors.Single(e => e.Error.Code == FatoValorDominioErrorCodes.OrdemComCodigoDesconhecido);
        FieldError repetido = resultado.Errors.Single(e => e.Error.Code == FatoValorDominioErrorCodes.OrdemComCodigoRepetido);
        desconhecido.Field.Should().Be("codigos");
        desconhecido.Error.Message.Should().NotContain("VALOR_DESCONHECIDO");
        repetido.Field.Should().Be("codigos");
        repetido.Error.Message.Should().NotContain("VALOR_EXISTENTE");
    }

    [Fact(DisplayName = "Reordenar com código nulo ou em branco recusa com mensagem própria")]
    public void ReordenarValores_CodigoEmBranco_RecusaComMensagemClara()
    {
        FatoCandidato fato = Criar().Value!;
        fato.AdicionarValorDominio("A", "A", 0, ativo: true).IsSuccess.Should().BeTrue();

        Result resultado = fato.ReordenarValores(["A", null!, "  "]);

        resultado.Errors.Select(static e => e.Error.Message).Should().Contain(static m => m.Contains("em branco", StringComparison.Ordinal));
        resultado.Errors.Select(static e => e.Error.Message).Should().NotContain(static m => m.Contains("não tem os valores", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Reordenar valores do fato de sistema é recusado")]
    public void ReordenarValores_FatoDeSistema_Recusa() =>
        Criar(sistema: true).Value!.ReordenarValores([])
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.DefinicaoDoFatoDeSistemaTravada);

    [Fact(DisplayName = "AdicionarValorDominio em categórico Declarado exige e aceita descrição")]
    public void AdicionarValorDominio_CategoricoDeclarado_Aceita()
    {
        FatoCandidato fato = Criar().Value!;

        Result resultado = fato.AdicionarValorDominio("PRETA", "Autodeclaração de cor/raça preta.", 0, ativo: true);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        fato.ValoresDominioDeclarados.Should().ContainSingle(v =>
            v.Codigo == "PRETA" && v.Descricao == "Autodeclaração de cor/raça preta." && v.Ordem == 0 && v.Ativo);
    }

    [Fact(DisplayName = "AdicionarValorDominio fora de categórico é rejeitado")]
    public void AdicionarValorDominio_ForaDeCategorico_Falha()
    {
        FatoCandidato fato = Criar(
            descritivo: Descritivo with { Codigo = "PCD" },
            definicao: Definicao with { Dominio = TipoDoDado.SimOuNao, FonteValores = null },
            binding: "CAMPO_FORMULARIO:PCD").Value!;

        Result resultado = fato.AdicionarValorDominio("SIM", "Descrição", 0, ativo: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoValorDominioErrorCodes.NaoPermitidoForaDeCategorico);
    }

    [Fact(DisplayName = "AdicionarValorDominio com código duplicado (comparação ordinal) é rejeitado")]
    public void AdicionarValorDominio_CodigoDuplicado_Falha()
    {
        FatoCandidato fato = Criar().Value!;
        fato.AdicionarValorDominio("PRETA", "Descrição", 0, ativo: true).IsSuccess.Should().BeTrue();

        Result resultado = fato.AdicionarValorDominio("PRETA", "Outra descrição", 1, ativo: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoValorDominioErrorCodes.CodigoDuplicado);
        resultado.Errors.Single().Field.Should().Be("codigo");
        resultado.Error.Message.Should().NotContain("PRETA");
    }

    [Fact(DisplayName = "AdicionarValorDominio sem descrição quando a origem é Declarado é rejeitado")]
    public void AdicionarValorDominio_SemDescricaoDeclarado_Falha()
    {
        FatoCandidato fato = Criar().Value!;

        Result resultado = fato.AdicionarValorDominio("PRETA", null, 0, ativo: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoValorDominioErrorCodes.DescricaoObrigatoria);
    }

    [Fact(DisplayName = "AdicionarValorDominio sem descrição quando a origem é Derivado é aceito")]
    public void AdicionarValorDominio_SemDescricaoDerivado_Aceita()
    {
        FatoCandidato fato = Criar(
            descritivo: Descritivo with { Codigo = "FATO_DERIVADO" },
            definicao: Definicao with { Dominio = TipoDoDado.Opcao },
            origem: OrigemFato.Calculado,
            binding: "REGRA_DE_CALCULO:FATO_DERIVADO").Value!;

        Result resultado = fato.AdicionarValorDominio("X", null, 0, ativo: true);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "AdicionarValorDominio com código em branco é rejeitado")]
    public void AdicionarValorDominio_CodigoEmBranco_Falha()
    {
        FatoCandidato fato = Criar().Value!;

        Result resultado = fato.AdicionarValorDominio("   ", "Descrição", 0, ativo: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoValorDominioErrorCodes.CodigoObrigatorio);
    }

    [Fact(DisplayName = "AdicionarValorDominio com ordem negativa é rejeitado")]
    public void AdicionarValorDominio_OrdemNegativa_Falha()
    {
        FatoCandidato fato = Criar().Value!;

        Result resultado = fato.AdicionarValorDominio("PRETA", "Descrição", -1, ativo: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoValorDominioErrorCodes.OrdemInvalida);
    }

    // ─── Sem remoção lógica (EntityBase puro: o fato é desativado, nunca apagado) ──
}
