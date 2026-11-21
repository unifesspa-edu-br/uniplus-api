namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.ApoioDoModeloDeFormulario;

/// <summary>
/// Os grupos repetíveis do modelo de formulário: o grafo de coleta dentro da ocorrência e o teto de itens.
/// </summary>
public sealed class ModeloFormularioGruposTests
{
    [Fact(DisplayName = "O grupo repetível entra no modelo, e o campo do grupo cita o campo anterior da mesma ocorrência")]
    public void Grupo_CampoCitaCampoAnteriorDaOcorrencia_Aceita()
    {
        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao,
            Conteudo([Item("RENDA_FAMILIAR", 0)]) with
            {
                Grupos = [Composicao(Campo("MAIOR_IDADE", 0), Campo("SEM_RENDA", 1, exibicao: Quando("MAIOR_IDADE")))],
            });

        modelo.IsSuccess.Should().BeTrue(modelo.Error?.Message);
        modelo.Value!.Conteudo.Grupos.Should().ContainSingle().Which.Subitens.Should().HaveCount(2);
    }

    [Fact(DisplayName = "Item que cita campo do grupo é recusado: fora da ocorrência, o membro não tem valor único")]
    public void Grupo_ItemCitaCampoDoGrupo_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao,
            Conteudo([Item("RENDA_FAMILIAR", 2, exibicao: Quando("MAIOR_IDADE"))]) with { Grupos = [Composicao(Campo("MAIOR_IDADE", 0))] });

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoDeMembroForaDoGrupo);
    }

    [Fact(DisplayName = "Campo do grupo opcional citado por negação na ocorrência é recusado, como o item")]
    public void Grupo_CampoOpcionalQueAlimentaNegacao_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao,
            Conteudo([]) with
            {
                Grupos = [Composicao(Campo("MAIOR_IDADE", 0, Obrigatoriedade.Nunca), Campo("SEM_RENDA", 1, exibicao: Quando("MAIOR_IDADE", Operador.Diferente)))],
            });

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ItemFormularioErrorCodes.OpcionalQueAlimentaRegra);
    }

    [Fact(DisplayName = "Item opcional que alimenta derivado citado por campo do grupo é recusado")]
    public void Grupo_CampoCitaDerivadoDeItemOpcional_Recusa()
    {
        Dictionary<string, IReadOnlyCollection<string>> derivacoes = new(StringComparer.Ordinal) { ["BAIXA_RENDA"] = ["RENDA_DECLARADA"] };

        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao,
            Conteudo([Item("RENDA_DECLARADA", 0, Obrigatoriedade.Nunca)]) with
            {
                Grupos = [Composicao(Campo("SEM_RENDA", 0, exibicao: Quando("BAIXA_RENDA")))],
            },
            derivacoes);

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ItemFormularioErrorCodes.OpcionalQueAlimentaRegra);
    }

    [Fact(DisplayName = "O teto do formulário conta cada grupo e cada campo de grupo")]
    public void Grupo_TetoContaGrupoECampos_Recusa()
    {
        Result<ModeloFormulario> modelo = Criar(
            FinalidadeFormulario.Habilitacao,
            Conteudo([.. Enumerable.Range(0, FormaDoItem.MaximoDeItens - 2).Select(static i => Item($"ITEM_{i}", i + 1))]) with
            {
                Grupos = [Composicao(Campo("MAIOR_IDADE", 0), Campo("SEM_RENDA", 1)) with { Ordem = 0 }],
            });

        modelo.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ItemFormularioErrorCodes.ItensEmExcesso);
    }
}
