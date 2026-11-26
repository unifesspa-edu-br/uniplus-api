namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// No congelamento, o campo de grupo repetível só vale com fato declarado de membro no catálogo
/// vivo (UNI-REQ-0146), como o item só vale com fato do candidato.
/// </summary>
public sealed class ColetabilidadeDoCampoDeGrupoTests
{
    [Theory]
    [InlineData("MEMBRO_GRUPO", true)]
    [InlineData("CANDIDATO", false)]
    public void Conferir_CampoDeGrupo_ExigeFatoDeMembro(string escopo, bool coletavel)
    {
        ProcessoSeletivo processo = ProcessosDeTeste.SemConfiguracao("PS Grupo", OrigemCandidatos.ImportacaoExterna);
        FatoColetado campo = FatoColetadoDeTeste.Coletando("PARENTESCO", 0)
            .ComRotulo("Parentesco")
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .Construir();
        GrupoColetado grupo = GrupoColetadoDeTeste.Agrupando("COMPOSICAO", 0).ComCampos(campo).Construir();
        processo.DefinirItens([], grupos: [grupo], papel: PapelDoFormularioDeTeste.Habilitacao).IsSuccess.Should().BeTrue();

        Result resultado = ConferenciaDeColetabilidadeDeFatos.Conferir(processo, ParentescoNoCatalogo(escopo));

        resultado.IsSuccess.Should().Be(coletavel);
    }

    private static Dictionary<string, FatoCandidatoView> ParentescoNoCatalogo(string escopo) =>
        new(StringComparer.Ordinal)
        {
            ["PARENTESCO"] = new(
                Guid.CreateVersion7(), "PARENTESCO", "Parentesco", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.SimOuNao), CodigoComRotulo<OrigemFato>.De(OrigemFato.Declarado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico), null, "INSCRICAO",
                "CAMPO_FORMULARIO:PARENTESCO", null, FonteValores: null, Ativo: true, ClassificacaoProtecao: CodigoComRotulo<ClassificacaoDeProtecao>.De(CatalogoDe<ClassificacaoDeProtecao>.Obter("PESSOAL")), HipoteseLegal: CodigoComRotulo<HipoteseLegalTratamento>.De(CatalogoDe<HipoteseLegalTratamento>.Obter(ProtecaoDoFatoDeTeste.HipoteseLegal)), FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, Escopo: CodigoComRotulo<EscopoFato>.De(CatalogoDe<EscopoFato>.Obter(escopo)), ProtecaoAprovada: true),
        };
}
