namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Invariantes do catálogo de motivos de decisão de isenção (UNI-REQ-0120 a
/// UNI-REQ-0122).
/// </summary>
public sealed class MotivoDecisaoIsencaoTests
{
    [Fact(DisplayName = "Motivo criado nasce ativo e com os campos declarados")]
    public void Criar_PayloadValido_NasceAtivo()
    {
        MotivoDecisaoIsencao motivo = CriarValido();

        motivo.Codigo.Valor.Should().Be("RENDA_ACIMA_DO_LIMITE");
        motivo.Descricao.Should().Be("Renda familiar per capita acima do limite legal.");
        motivo.Fundamento.Should().Be(FundamentoIsencao.CadastroUnico);
        motivo.ResultadoPermitido.Should().Be(ResultadoPermitido.Indeferido);
        motivo.Ativo.Should().BeTrue();
    }

    [Fact(DisplayName = "Motivo sem resultado permitido é recusado")]
    public void Criar_SemResultadoPermitido_Recusa()
    {
        Result<MotivoDecisaoIsencao> resultado = MotivoDecisaoIsencao.Criar(
            "RENDA_ACIMA_DO_LIMITE",
            "Renda familiar per capita acima do limite legal.",
            FundamentoIsencao.CadastroUnico,
            ResultadoPermitido.Nenhum);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be(MotivoDecisaoIsencaoErrorCodes.ResultadoPermitidoObrigatorio);
    }

    [Fact(DisplayName = "Motivo sem fundamento é recusado")]
    public void Criar_SemFundamento_Recusa()
    {
        Result<MotivoDecisaoIsencao> resultado = MotivoDecisaoIsencao.Criar(
            "RENDA_ACIMA_DO_LIMITE",
            "Renda familiar per capita acima do limite legal.",
            FundamentoIsencao.Nenhum,
            ResultadoPermitido.Indeferido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be(MotivoDecisaoIsencaoErrorCodes.FundamentoObrigatorio);
    }

    [Fact(DisplayName = "As violações do payload são acumuladas no mesmo lote")]
    public void Criar_PayloadInteiroInvalido_AcumulaViolacoes()
    {
        Result<MotivoDecisaoIsencao> resultado = MotivoDecisaoIsencao.Criar(
            codigo: "código minúsculo",
            descricao: "  ",
            FundamentoIsencao.Nenhum,
            ResultadoPermitido.Nenhum);

        resultado.Errors.Select(erro => erro.Error.Code).Should().BeEquivalentTo(
            [
                MotivoDecisaoIsencaoErrorCodes.CodigoFormatoInvalido,
                MotivoDecisaoIsencaoErrorCodes.DescricaoObrigatoria,
                MotivoDecisaoIsencaoErrorCodes.FundamentoObrigatorio,
                MotivoDecisaoIsencaoErrorCodes.ResultadoPermitidoObrigatorio,
            ],
            "o errors[] do contrato público precisa de todas as regras violadas no mesmo lote");
    }

    [Fact(DisplayName = "A edição alcança a descrição e não move o resultado permitido")]
    public void Atualizar_TrocaDescricao_PreservaOsDemaisAtributos()
    {
        MotivoDecisaoIsencao motivo = CriarValido();

        Result resultado = motivo.Atualizar("Renda per capita apurada acima do teto da Lei 12.799/2013.");

        resultado.IsSuccess.Should().BeTrue();
        motivo.Descricao.Should().Be("Renda per capita apurada acima do teto da Lei 12.799/2013.");
        motivo.Codigo.Valor.Should().Be("RENDA_ACIMA_DO_LIMITE");
        motivo.Fundamento.Should().Be(FundamentoIsencao.CadastroUnico);
        motivo.ResultadoPermitido.Should().Be(ResultadoPermitido.Indeferido,
            "o resultado permitido é imutável e a edição não tem por onde alcançá-lo");
    }

    [Fact(DisplayName = "A edição não muda a situação do motivo")]
    public void Atualizar_MotivoDesativado_ContinuaDesativado()
    {
        MotivoDecisaoIsencao motivo = CriarValido();
        motivo.Desativar();

        motivo.Atualizar("Nova redação.");

        motivo.Ativo.Should().BeFalse(
            "corrigir o texto de um motivo retirado do catálogo não o devolve às publicações");
    }

    [Fact(DisplayName = "Descrição em branco é recusada na edição")]
    public void Atualizar_DescricaoEmBranco_Recusa()
    {
        MotivoDecisaoIsencao motivo = CriarValido();

        Result resultado = motivo.Atualizar("   ");

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be(MotivoDecisaoIsencaoErrorCodes.DescricaoObrigatoria);
        motivo.Descricao.Should().Be("Renda familiar per capita acima do limite legal.",
            "a recusa não pode deixar o agregado com a descrição pela metade");
    }

    [Fact(DisplayName = "Desativar preserva o registro e apenas o retira das novas publicações")]
    public void Desativar_MotivoAtivo_PreservaORegistro()
    {
        MotivoDecisaoIsencao motivo = CriarValido();

        Result resultado = motivo.Desativar();

        resultado.IsSuccess.Should().BeTrue();
        motivo.Ativo.Should().BeFalse();
        motivo.Codigo.Valor.Should().Be("RENDA_ACIMA_DO_LIMITE",
            "o motivo continua legível para os processos e decisões que já o referenciam");
        motivo.ResultadoPermitido.Should().Be(ResultadoPermitido.Indeferido);
    }

    [Fact(DisplayName = "Desativar motivo já inativo é recusado")]
    public void Desativar_MotivoInativo_Recusa()
    {
        MotivoDecisaoIsencao motivo = CriarValido();
        motivo.Desativar();

        Result resultado = motivo.Desativar();

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(MotivoDecisaoIsencaoErrorCodes.JaInativo);
    }

    [Fact(DisplayName = "Reativar devolve o motivo às novas publicações")]
    public void Ativar_MotivoInativo_Reativa()
    {
        MotivoDecisaoIsencao motivo = CriarValido();
        motivo.Desativar();

        Result resultado = motivo.Ativar();

        resultado.IsSuccess.Should().BeTrue();
        motivo.Ativo.Should().BeTrue();
    }

    [Fact(DisplayName = "Reativar motivo já ativo é recusado")]
    public void Ativar_MotivoAtivo_Recusa()
    {
        MotivoDecisaoIsencao motivo = CriarValido();

        Result resultado = motivo.Ativar();

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(MotivoDecisaoIsencaoErrorCodes.JaAtivo);
    }

    [Fact(DisplayName = "O motivo de medula não declara etapa — a mesma lista serve à análise inicial e ao recurso")]
    public void Motivo_NaoTemMarcacaoDeEtapa()
    {
        // UNI-REQ-0120: o catálogo não cria lista nem marcação por etapa. Um
        // campo que separasse "análise inicial" de "recurso" faria duas listas
        // onde o requisito pede uma, e é isso que esta conferência trava.
        MotivoDecisaoIsencao motivo = MotivoDecisaoIsencao.Criar(
            "DOCUMENTO_ILEGIVEL",
            "Comprovante de doação de medula ilegível.",
            FundamentoIsencao.DoacaoMedulaOssea,
            ResultadoPermitido.Indeferido).Value!;

        typeof(MotivoDecisaoIsencao).GetProperties().Select(propriedade => propriedade.Name)
            .Should().NotContain(["Etapa", "EtapaAplicavel", "SomenteRecurso"]);
        motivo.Fundamento.Should().Be(FundamentoIsencao.DoacaoMedulaOssea);
    }

    private static MotivoDecisaoIsencao CriarValido() =>
        MotivoDecisaoIsencao.Criar(
            "RENDA_ACIMA_DO_LIMITE",
            "Renda familiar per capita acima do limite legal.",
            FundamentoIsencao.CadastroUnico,
            ResultadoPermitido.Indeferido).Value!;
}
