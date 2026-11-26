namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O impedimento é da inscrição do candidato: os outros formulários não o têm, e o campo com
/// impedimento é obrigatório sempre que exibido — em branco, o impedimento nunca se cumpriria.
/// </summary>
public sealed class ImpedimentoNoFormularioTests
{
    private const string Fato = "VINCULO_PARFOR";

    [Fact(DisplayName = "O campo com impedimento fora do formulário de inscrição é recusado")]
    public void DefinirItens_ImpedimentoNaHabilitacao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        ApoioDosFormulariosComConsequencia.AcrescentarFaseDaHabilitacao(processo);

        Result resultado = processo.DefinirItens([Campo(Obrigatoriedade.Sempre)], papel: PapelDoFormularioDeTeste.Habilitacao);

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "itens[0].regras.impedimento",
            Error = new { Code = ItemFormularioErrorCodes.ImpedimentoForaDaInscricao },
        });
    }

    [Theory(DisplayName = "O campo com impedimento só é publicado se for obrigatório sempre que exibido")]
    [InlineData(true)]
    [InlineData(false)]
    public void Publicacao_CampoOpcionalComImpedimento_Recusa(bool obrigatorio)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirFatosColetados(
                PapelDoFormularioDeTeste.Inscricao,
                [.. FormularioDeTeste.DadosBasicos(), Campo(obrigatorio ? Obrigatoriedade.Sempre : Obrigatoriedade.Nunca)])
            .IsSuccess.Should().BeTrue();

        (CondicoesParaRegistrarConfiguracaoDeTeste.PrimeiraCausa(processo)?.Code)
            .Should().Be(obrigatorio ? null : ItemFormularioErrorCodes.OpcionalQueAlimentaRegra);
    }

    private static FatoColetado Campo(Obrigatoriedade obrigatoriedade) =>
        FatoColetadoDeTeste.Coletando(Fato, FormularioDeTeste.PrimeiraOrdemDeInscricao)
            .ComRotulo("Vínculo com o PARFOR")
            .ComObrigatoriedade(obrigatoriedade)
            .NaSecao(FormularioDeTeste.Secao)
            .ComImpedimento(new Impedimento(PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(Fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!, "Quem tem vínculo com o PARFOR não pode se inscrever neste processo."))
            .Construir();
}
