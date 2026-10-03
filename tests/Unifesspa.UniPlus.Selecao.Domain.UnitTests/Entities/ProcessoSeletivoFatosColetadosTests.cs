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
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Story #926 — invariantes do grafo de coleta de fatos. A norma não pede apenas um grafo acíclico:
/// pede que a pré-condição de um fato cite somente fatos <b>anteriores</b> na ordem de coleta, o que
/// é mais estrito e é o que impede um formulário em que a pergunta depende de resposta ainda não dada.
/// Os casos usam o formulário de habilitação, que não tem a seção do conjunto básico.
/// </summary>
public sealed class ProcessoSeletivoFatosColetadosTests
{
    private static JsonElement Sim => JsonSerializer.SerializeToElement(true);

    private static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar("PS Coleta de Fatos", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

    private static CondicaoPrecondicaoFato Cond(string fato) =>
        CondicaoPrecondicaoFato.Criar(0, fato, Operador.Igual, Sim).Value!;

    private static FatoColetado Fato(string codigo, int ordem, params string[] cita) =>
        FatoColetado.Criar(codigo, ordem, codigo, TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Nunca, [.. cita.Select(Cond)]).Value!;

    [Fact(DisplayName = "Grafo válido é aceito: cada pré-condição cita apenas fatos anteriores")]
    public void GrafoValido_Aceito()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirItens(
            [Fato("PCD", 0), Fato("EGRESSO_ESCOLA_PUBLICA", 1), Fato("CONCORRER_PCD", 2, "PCD")], PrecondicaoIfMatch.Ausente, finalidade: FinalidadeFormulario.Habilitacao);

        resultado.IsSuccess.Should().BeTrue();
        processo.FatosColetados.Should().HaveCount(3);
    }

    [Fact(DisplayName = "Pré-condição que cita fato posterior é recusada, mesmo sem ciclo")]
    public void CitaFatoPosterior_Recusado()
    {
        ProcessoSeletivo processo = NovoProcesso();

        // Grafo perfeitamente acíclico: CONCORRER_PCD depende de PCD e nada depende de CONCORRER_PCD.
        // Mas PCD vem DEPOIS na ordem de coleta, então a pergunta seria feita antes da resposta existir.
        Result resultado = processo.DefinirItens(
            [Fato("CONCORRER_PCD", 0, "PCD"), Fato("PCD", 1)], PrecondicaoIfMatch.Ausente, finalidade: FinalidadeFormulario.Habilitacao);

        resultado.IsFailure.Should().BeTrue("aciclicidade sozinha não garante que a dependência venha antes");
        resultado.Error!.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoPosterior);
    }

    private static FatoColetado ObrigatorioQuando(string codigo, int ordem, string citado, JsonElement? valor = null) =>
        FatoColetado.Criar(
            codigo, ordem, codigo, TipoRenderizacao.Booleano,
            Obrigatoriedade.Quando(PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(citado, Operador.Igual, valor ?? Sim).Value!)]).Value!),
            null).Value!;

    [Fact(DisplayName = "Obrigatoriedade QUANDO que cita campo posterior é recusada, como a pré-condição")]
    public void ObrigatoriedadeCitaFatoPosterior_Recusada()
    {
        Result resultado = NovoProcesso().DefinirItens([ObrigatorioQuando("CONCORRER_PCD", 0, "PCD"), Fato("PCD", 1)], PrecondicaoIfMatch.Ausente, finalidade: FinalidadeFormulario.Habilitacao);

        resultado.Error!.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoPosterior);
    }

    [Fact(DisplayName = "O valor citado pela obrigatoriedade entra nos vínculos do processo")]
    public void ObrigatoriedadeEntraNosVinculos()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirItens(
            [Fato("COR_RACA", 0), ObrigatorioQuando("CONCORRER_PPI", 1, "COR_RACA", JsonSerializer.SerializeToElement("PRETA"))],
            PrecondicaoIfMatch.Ausente, finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();

        processo.Vinculos().Valores.Should().Contain(("COR_RACA", "PRETA"));
    }

    [Fact(DisplayName = "Ciclo é recusado com erro nomeado que mostra o caminho")]
    public void Ciclo_RecusadoComCaminho()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirItens(
            [Fato("A", 0, "C"), Fato("B", 1, "A"), Fato("C", 2, "B")], PrecondicaoIfMatch.Ausente, finalidade: FinalidadeFormulario.Habilitacao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(GrafoFormularioErrorCodes.GrafoComCiclo);
        resultado.Error.Message.Should().Contain("→", "o caminho do ciclo é o que torna o erro acionável para quem configura");
        foreach (string participante in new[] { "A", "B", "C" })
        {
            resultado.Error.Message.Should().Contain(participante);
        }
    }

    [Fact(DisplayName = "Ciclo reportado exclui o prefixo de entrada — só os fatos que realmente fecham o ciclo")]
    public void Ciclo_ReportaSoOCiclo_NaoOCaminhoDeEntrada()
    {
        ProcessoSeletivo processo = NovoProcesso();

        // ENTRADA cita A; A e B formam o ciclo (A cita B, B cita A). O caminho reportado deve ser
        // "A → B → A", nunca "ENTRADA → A → B → A": ENTRADA leva ao ciclo mas não faz parte dele.
        Result resultado = processo.DefinirItens(
            [Fato("ENTRADA", 0, "A"), Fato("A", 1, "B"), Fato("B", 2, "A")], PrecondicaoIfMatch.Ausente, finalidade: FinalidadeFormulario.Habilitacao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(GrafoFormularioErrorCodes.GrafoComCiclo);
        resultado.Error.Message.Should().NotContain(
            "ENTRADA",
            "ENTRADA leva ao ciclo mas não pertence a ele — reportá-la confundiria quem procura a aresta a remover");
        resultado.Error.Message.Should().Contain("A → B → A");
    }

    /// <summary>
    /// Uma pré-condição de campo só cita fato <b>coletado</b>, nunca derivado: o resolvedor de
    /// estado dos fatos percorre só os coletados e não aciona o motor de derivação, de modo que
    /// uma pré-condição sobre um derivado avaliaria indeterminada para sempre. A recusa é na
    /// definição, antes de o campo virar configuração.
    /// </summary>
    [Fact(DisplayName = "Pré-condição que cita fato não coletado pelo processo é recusada")]
    public void CitaFatoNaoColetado_Recusado()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirItens(
            [Fato("CONCORRER_PCD", 0, "PCD")], PrecondicaoIfMatch.Ausente, finalidade: FinalidadeFormulario.Habilitacao);

        resultado.IsFailure.Should().BeTrue("o gate ficaria preso a um fato que este processo nunca vai resolver");
        resultado.Error!.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoNaoConhecido);
    }

    [Fact(DisplayName = "Fato citando a si mesmo é recusado na criação, antes de chegar ao grafo")]
    public void Autorreferencia_Recusada()
    {
        Result<FatoColetado> resultado = FatoColetado.Criar("PCD", 0, "PCD", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Nunca, [Cond("PCD")]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ItemFormularioErrorCodes.RegraAutorreferente);
    }

    [Fact(DisplayName = "Código de fato repetido na coleta é recusado")]
    public void FatoDuplicado_Recusado()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirItens(
            [Fato("PCD", 0), Fato("PCD", 1)], PrecondicaoIfMatch.Ausente, finalidade: FinalidadeFormulario.Habilitacao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(GrafoFormularioErrorCodes.FatoDuplicado);
    }

    [Fact(DisplayName = "Ordem repetida é recusada — a ordem de coleta precisa ser total")]
    public void OrdemDuplicada_Recusada()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirItens(
            [Fato("PCD", 0), Fato("EGRESSO_ESCOLA_PUBLICA", 0)], PrecondicaoIfMatch.Ausente, finalidade: FinalidadeFormulario.Habilitacao);

        resultado.IsFailure.Should().BeTrue(
            "com empate de ordem, 'anterior' deixa de ser decidível entre os dois fatos");
        resultado.Error!.Code.Should().Be(GrafoFormularioErrorCodes.OrdemDuplicada);
    }

    [Fact(DisplayName = "Definir a coleta substitui a anterior por inteiro")]
    public void Definir_SubstituiPorInteiro()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirItens([Fato("PCD", 0), Fato("EGRESSO_ESCOLA_PUBLICA", 1)], PrecondicaoIfMatch.Ausente, finalidade: FinalidadeFormulario.Habilitacao)
            .IsSuccess.Should().BeTrue();

        processo.DefinirItens([Fato("SEXO", 0)], PrecondicaoIfMatch.Ausente, finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();

        processo.FatosColetados.Should().HaveCount(1);
        processo.FatosColetados.Single().FatoCodigo.Should().Be("SEXO");
    }

    [Fact(DisplayName = "Coleta vazia é aceita — um processo pode não coletar fato nenhum do candidato")]
    public void ColetaVazia_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirItens([], PrecondicaoIfMatch.Ausente, finalidade: FinalidadeFormulario.Habilitacao);

        resultado.IsSuccess.Should().BeTrue();
        processo.FatosColetados.Should().BeEmpty();
    }

    [Fact(DisplayName = "Os fatos coletados ficam vinculados ao processo")]
    public void FatosVinculadosAoProcesso()
    {
        ProcessoSeletivo processo = NovoProcesso();

        processo.DefinirItens([Fato("PCD", 0), Fato("CONCORRER_PCD", 1, "PCD")], PrecondicaoIfMatch.Ausente, finalidade: FinalidadeFormulario.Habilitacao)
            .IsSuccess.Should().BeTrue();

        processo.FatosColetados.Should().OnlyContain(f => f.ProcessoSeletivoId == processo.Id);
        FatoColetado comPrecondicao = processo.FatosColetados.Single(f => f.FatoCodigo == "CONCORRER_PCD");
        comPrecondicao.Precondicoes.Should().OnlyContain(c => c.FatoColetadoId == comPrecondicao.Id);
    }

    [Fact(DisplayName = "Mais itens que o teto do formulário é recusado")]
    public void ItensAcimaDoTeto_Recusado()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirItens(
            [.. Enumerable.Range(0, FormaDoItem.MaximoDeItens + 1).Select(static i => Fato($"FATO_{i}", i))], PrecondicaoIfMatch.Ausente, finalidade: FinalidadeFormulario.Habilitacao);

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ItemFormularioErrorCodes.ItensEmExcesso);
    }
}
