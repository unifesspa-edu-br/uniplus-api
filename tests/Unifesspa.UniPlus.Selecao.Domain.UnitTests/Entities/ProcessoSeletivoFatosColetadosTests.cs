namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Story #926 — invariantes do grafo de coleta de fatos. A norma não pede apenas um grafo acíclico:
/// pede que a pré-condição de um fato cite somente fatos <b>anteriores</b> na ordem de coleta, o que
/// é mais estrito e é o que impede um formulário em que a pergunta depende de resposta ainda não dada.
/// </summary>
public sealed class ProcessoSeletivoFatosColetadosTests
{
    private static JsonElement Sim => JsonSerializer.SerializeToElement(true);

    private static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar("PS Coleta de Fatos", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);

    private static CondicaoPrecondicaoFato Cond(string fato) =>
        CondicaoPrecondicaoFato.Criar(0, fato, Operador.Igual, Sim).Value!;

    private static FatoColetado Fato(string codigo, int ordem, params string[] cita) =>
        FatoColetado.Criar(codigo, ordem, [.. cita.Select(Cond)]).Value!;

    [Fact(DisplayName = "Grafo válido é aceito: cada pré-condição cita apenas fatos anteriores")]
    public void GrafoValido_Aceito()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirFatosColetados(
            [Fato("PCD", 0), Fato("EGRESSO_ESCOLA_PUBLICA", 1), Fato("CONCORRER_PCD", 2, "PCD")]);

        resultado.IsSuccess.Should().BeTrue();
        processo.FatosColetados.Should().HaveCount(3);
    }

    [Fact(DisplayName = "Pré-condição que cita fato posterior é recusada, mesmo sem ciclo")]
    public void CitaFatoPosterior_Recusado()
    {
        ProcessoSeletivo processo = NovoProcesso();

        // Grafo perfeitamente acíclico: CONCORRER_PCD depende de PCD e nada depende de CONCORRER_PCD.
        // Mas PCD vem DEPOIS na ordem de coleta, então a pergunta seria feita antes da resposta existir.
        Result resultado = processo.DefinirFatosColetados(
            [Fato("CONCORRER_PCD", 0, "PCD"), Fato("PCD", 1)]);

        resultado.IsFailure.Should().BeTrue("aciclicidade sozinha não garante que a dependência venha antes");
        resultado.Error!.Code.Should().Be(FatoColetadoErrorCodes.PrecondicaoCitaFatoPosterior);
    }

    [Fact(DisplayName = "Ciclo é recusado com erro nomeado que mostra o caminho")]
    public void Ciclo_RecusadoComCaminho()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirFatosColetados(
            [Fato("A", 0, "C"), Fato("B", 1, "A"), Fato("C", 2, "B")]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoColetadoErrorCodes.GrafoComCiclo);
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
        Result resultado = processo.DefinirFatosColetados(
            [Fato("ENTRADA", 0, "A"), Fato("A", 1, "B"), Fato("B", 2, "A")]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoColetadoErrorCodes.GrafoComCiclo);
        resultado.Error.Message.Should().NotContain(
            "ENTRADA",
            "ENTRADA leva ao ciclo mas não pertence a ele — reportá-la confundiria quem procura a aresta a remover");
        resultado.Error.Message.Should().Contain("A → B → A");
    }

    [Fact(DisplayName = "Pré-condição que cita fato não coletado pelo processo é recusada")]
    public void CitaFatoNaoColetado_Recusado()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirFatosColetados(
            [Fato("CONCORRER_PCD", 0, "PCD")]);

        resultado.IsFailure.Should().BeTrue("o gate ficaria preso a um fato que este processo nunca vai resolver");
        resultado.Error!.Code.Should().Be(FatoColetadoErrorCodes.PrecondicaoCitaFatoNaoColetado);
    }

    [Fact(DisplayName = "Fato citando a si mesmo é recusado na criação, antes de chegar ao grafo")]
    public void Autorreferencia_Recusada()
    {
        Result<FatoColetado> resultado = FatoColetado.Criar("PCD", 0, [Cond("PCD")]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoColetadoErrorCodes.PrecondicaoAutorreferente);
    }

    [Fact(DisplayName = "Código de fato repetido na coleta é recusado")]
    public void FatoDuplicado_Recusado()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirFatosColetados(
            [Fato("PCD", 0), Fato("PCD", 1)]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoColetadoErrorCodes.FatoDuplicado);
    }

    [Fact(DisplayName = "Ordem repetida é recusada — a ordem de coleta precisa ser total")]
    public void OrdemDuplicada_Recusada()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirFatosColetados(
            [Fato("PCD", 0), Fato("EGRESSO_ESCOLA_PUBLICA", 0)]);

        resultado.IsFailure.Should().BeTrue(
            "com empate de ordem, 'anterior' deixa de ser decidível entre os dois fatos");
        resultado.Error!.Code.Should().Be(FatoColetadoErrorCodes.OrdemDuplicada);
    }

    [Fact(DisplayName = "Definir a coleta substitui a anterior por inteiro")]
    public void Definir_SubstituiPorInteiro()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirFatosColetados([Fato("PCD", 0), Fato("EGRESSO_ESCOLA_PUBLICA", 1)])
            .IsSuccess.Should().BeTrue();

        processo.DefinirFatosColetados([Fato("SEXO", 0)]).IsSuccess.Should().BeTrue();

        processo.FatosColetados.Should().HaveCount(1);
        processo.FatosColetados.Single().FatoCodigo.Should().Be("SEXO");
    }

    [Fact(DisplayName = "Coleta vazia é aceita — um processo pode não coletar fato nenhum do candidato")]
    public void ColetaVazia_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirFatosColetados([]);

        resultado.IsSuccess.Should().BeTrue();
        processo.FatosColetados.Should().BeEmpty();
    }

    [Fact(DisplayName = "Os fatos coletados ficam vinculados ao processo")]
    public void FatosVinculadosAoProcesso()
    {
        ProcessoSeletivo processo = NovoProcesso();

        processo.DefinirFatosColetados([Fato("PCD", 0), Fato("CONCORRER_PCD", 1, "PCD")])
            .IsSuccess.Should().BeTrue();

        processo.FatosColetados.Should().OnlyContain(f => f.ProcessoSeletivoId == processo.Id);
        FatoColetado comPrecondicao = processo.FatosColetados.Single(f => f.FatoCodigo == "CONCORRER_PCD");
        comPrecondicao.Precondicoes.Should().OnlyContain(c => c.FatoColetadoId == comPrecondicao.Id);
    }
}
