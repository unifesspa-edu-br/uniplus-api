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
            "PS Grupo", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        FatoColetado campo = FatoColetado.Criar("PARENTESCO", 0, "Parentesco", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null).Value!;
        GrupoColetado grupo = GrupoColetado.Criar(
            "COMPOSICAO", 0, FormularioDeTeste.Secao, "Composição familiar", 0, 5, null, Obrigatoriedade.Nunca, [campo]).Value!;
        processo.DefinirItens([], grupos: [grupo]).IsSuccess.Should().BeTrue();
        Dictionary<string, FatoCandidatoView> catalogo = new(StringComparer.Ordinal)
        {
            ["PARENTESCO"] = new(
                Guid.CreateVersion7(), "PARENTESCO", "Parentesco", null, "BOOLEANO", "DECLARADO", "ESCALAR", null, "INSCRICAO",
                "CAMPO_INSCRICAO:PARENTESCO", null, FonteValores: null, Ativo: true, Escopo: escopo),
        };

        Result resultado = ConferenciaDeColetabilidadeDeFatos.Conferir(processo, catalogo);

        resultado.IsSuccess.Should().Be(coletavel);
    }
}
