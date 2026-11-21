namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Grupo", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        FatoColetado campo = FatoColetadoDeTeste.Coletando("PARENTESCO", 0)
            .ComRotulo("Parentesco")
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .Construir();
        GrupoColetado grupo = GrupoColetado.Criar(
            "COMPOSICAO", 0, FormularioDeTeste.Secao, "Composição familiar", 0, 5, null, Obrigatoriedade.Nunca, [campo]).Value!;
        processo.DefinirItens([], grupos: [grupo], finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();
        Dictionary<string, FatoCandidatoView> catalogo = new(StringComparer.Ordinal)
        {
            ["PARENTESCO"] = new(
                Guid.CreateVersion7(), "PARENTESCO", "Parentesco", "Descrição do fato.", "BOOLEANO", "DECLARADO", "ESCALAR", null, "INSCRICAO",
                "CAMPO_FORMULARIO:PARENTESCO", null, FonteValores: null, Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, Escopo: escopo, ProtecaoAprovada: true),
        };

        Result resultado = ConferenciaDeColetabilidadeDeFatos.Conferir(processo, catalogo);

        resultado.IsSuccess.Should().Be(coletavel);
    }
}
