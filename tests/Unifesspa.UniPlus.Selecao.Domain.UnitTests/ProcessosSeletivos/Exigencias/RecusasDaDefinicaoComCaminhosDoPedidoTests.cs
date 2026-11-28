namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Exigencias;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosDocumentosExigidosDoProcessoSeletivo;

/// <summary>
/// As recusas da definição apontam o caminho que quem chama informa para cada regra, que pode pular posições quando só parte
/// das regras do pedido é conferida, e a conferência não altera o processo.
/// </summary>
public sealed class RecusasDaDefinicaoComCaminhosDoPedidoTests
{
    private static RegraDeComprovacao[] UmaNoCronogramaEDuasForaDele(Guid faseDoCronograma) =>
    [
        RegraDeComprovacao.CriarRegraDeDocumento(Exigencia(Guid.CreateVersion7()), 0).Value!,
        RegraDeComprovacao.CriarRegraDeDocumento(Exigencia(faseDoCronograma), 1).Value!,
        RegraDeComprovacao.CriarRegraDeDocumento(Exigencia(Guid.CreateVersion7()), 2).Value!,
    ];

    [Fact(DisplayName = "Com os caminhos fora de sequência, cada recusa aponta o caminho da sua regra e o processo não muda")]
    public void CaminhosForaDeSequencia_CadaRecusaNoCaminhoDaSuaRegra()
    {
        FaseCronograma inscricao = FaseQueResponde(1, "INSCRICAO", inscricao: true, isencao: false);
        ProcessoSeletivo processo = ProcessoComFases(inscricao);
        FormularioDeTeste.GarantirFormulario(processo, PapelDoFormularioDeTeste.Inscricao);
        RegraDeComprovacao[] regras = UmaNoCronogramaEDuasForaDele(inscricao.Id);

        IReadOnlyList<FieldError> recusas = RegrasDasExigencias.RecusasDaDefinicao(
            processo.Declaracao(), regras, CaminhosDasExigencias.NasPosicoes("regrasDeComprovacao", [1, 4, 7]));

        recusas.Select(static recusa => recusa.Field).Should().Equal(
            "regrasDeComprovacao[1].documento.momento.faseId",
            "regrasDeComprovacao[7].documento.momento.faseId");
        processo.DocumentosExigidos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Caminhos em quantidade diferente da das regras são recusados")]
    public void CaminhosEmQuantidadeDiferente_Recusa()
    {
        ProcessoSeletivo processo = ProcessoComFases(Fase(1, "INSCRICAO"));
        RegraDeComprovacao[] regras = [RegraDeDocumentoDaFase(Guid.CreateVersion7(), 0), RegraDeDocumentoDaFase(Guid.CreateVersion7(), 1)];

        Action conferir = () => RegrasDasExigencias.RecusasDaDefinicao(processo.Declaracao(), regras, ["regrasDeComprovacao[0]"]);

        conferir.Should().Throw<ArgumentException>();
    }
}
