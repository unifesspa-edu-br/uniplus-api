namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaPublicacaoDoProcessoSeletivo;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaSessaoEditorialDoProcessoSeletivo;

/// <summary>
/// Dimensões do processo editáveis sob sessão de retificação (fatos coletados, regras de
/// derivação, formulário, localidade, aplicação de modelo): a revisão avança quando aceitas e
/// o publicado sem sessão segue bloqueado.
/// </summary>
public sealed class ProcessoSeletivoEdicaoDeDimensoesSobSessaoEditorialTests
{
    // Já entra no congelamento do envelope e é reposto fielmente no descarte (Story #928/#986).
    [Fact(DisplayName = "O grafo de coleta de fatos É editável sob sessão de retificação — a revisão avança; publicado sem sessão é bloqueado (Story #986)")]
    public void DefinirFatosColetados_SobSessao_AceitaEIncrementaRevisao()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out RascunhoRetificacao rascunho);
        int revisaoAntes = rascunho.Revisao;

        processo.DefinirItens([FatoPcd()], PrecondicaoIfMatch.DeTags([rascunho.ETag]))
            .IsSuccess.Should().BeTrue();
        rascunho.Revisao.Should().Be(revisaoAntes + 1);

        ProcessoSeletivo semSessao = NovoProcessoPublicado(out _);
        Result recusa = semSessao.DefinirItens([FatoPcd()], PrecondicaoIfMatch.Ausente);
        recusa.IsFailure.Should().BeTrue();
        recusa.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
    }

    [Fact(DisplayName = "As regras de derivação SÃO editáveis sob sessão de retificação — a revisão avança; publicado sem sessão é bloqueado (Story #986)")]
    public void DefinirRegrasDerivacao_SobSessao_AceitaEIncrementaRevisao()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out RascunhoRetificacao rascunho);
        int revisaoAntes = rascunho.Revisao;

        processo.DefinirRegrasDerivacao([RegimeDeVagaDerivadoDeAc()], PrecondicaoIfMatch.DeTags([rascunho.ETag]))
            .IsSuccess.Should().BeTrue();
        rascunho.Revisao.Should().Be(revisaoAntes + 1);

        ProcessoSeletivo semSessao = NovoProcessoPublicado(out _);
        Result recusa = semSessao.DefinirRegrasDerivacao([RegimeDeVagaDerivadoDeAc()], PrecondicaoIfMatch.Ausente);
        recusa.IsFailure.Should().BeTrue();
        recusa.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
    }

    [Fact(DisplayName = "Sob sessão de retificação, acrescentar formulário é aceito e a revisão avança; remover é recusado; publicado sem sessão é bloqueado")]
    public void Formulario_SobSessao_AcrescentaENaoRemove()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out RascunhoRetificacao rascunho);
        int revisaoAntes = rascunho.Revisao;

        processo.DefinirFormulario(
                FinalidadeFormulario.Habilitacao, null, "Habilitação", FormularioDeTeste.Etapas(), PrecondicaoIfMatch.DeTags([rascunho.ETag]))
            .IsSuccess.Should().BeTrue();
        rascunho.Revisao.Should().Be(revisaoAntes + 1);
        processo.FormularioDe(FinalidadeFormulario.Habilitacao)!.Titulo.Should().Be("Habilitação");

        AfirmarQueARemocaoSobSessaoRecusaPelaRetificacao(processo);
        ProcessoSeletivo semSessao = NovoProcessoPublicado(out _);
        semSessao.DefinirFormulario(FinalidadeFormulario.Habilitacao, null, "Título", FormularioDeTeste.Etapas(), PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
    }

    [Fact(DisplayName = "DefinirFormulario apara o título e trata o título em branco como ausência")]
    public void DefinirFormulario_EmRascunho_AceitaETrataBrancoComoNulo()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();

        processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, null, "  Formulário  ", FormularioDeTeste.Etapas(), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.FormularioDe(FinalidadeFormulario.Habilitacao)!.Titulo.Should().Be("Formulário");

        processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, null, "   ", FormularioDeTeste.Etapas(), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.FormularioDe(FinalidadeFormulario.Habilitacao)!.Titulo.Should().BeNull("string em branco é ausência, não valor vazio persistido");
    }

    [Fact(DisplayName = "DefinirFormulario recusa título acima de 300 caracteres — mesmo limite da coluna, recusado antes do SaveChanges")]
    public void DefinirFormulario_TituloExcedeLimite_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();

        Result resultado = processo.DefinirFormulario(
            FinalidadeFormulario.Habilitacao, null, new string('a', 301), FormularioDeTeste.Etapas(), PrecondicaoIfMatch.Ausente);

        resultado.Errors.Should().ContainSingle(e => e.Field == "titulo" && e.Error.Code == EstruturaFormularioErrorCodes.TituloTamanho);
        processo.FormularioDe(FinalidadeFormulario.Habilitacao).Should().BeNull("a mutação recusada não altera o estado");
    }

    [Fact(DisplayName = "Aplicar modelo de formulário sob retificação é recusado antes da precondição")]
    public void AplicarModeloDeFormulario_SobRetificacao_Recusa()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out _);
        CopiaDeModeloDeFormulario copia = new(
            FinalidadeFormulario.Inscricao, "Inscrição", FormularioDeTeste.Etapas(), [], [], [], [], Guid.CreateVersion7(), "INSCRICAO_MEDICINA");

        Result resultado = processo.AplicarModeloDeFormulario(copia, PrecondicaoIfMatch.Ausente);

        resultado.Error!.Code.Should().Be(FormularioProcessoErrorCodes.AplicacaoDeModeloSoEmRascunho);
    }

    // A edição sob sessão só é segura porque a localidade participa do ciclo inteiro: o envelope
    // a congela e o descarte a repõe. Este teste prova que a liberação veio junto da garantia.
    [Fact(DisplayName = "Localidade é alterável sob sessão editorial e move o ETag da sessão")]
    public void DefinirLocalidade_ComSessaoAberta_AceitaEIncrementaRevisao()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out RascunhoRetificacao rascunho);
        string tagInicial = rascunho.ETag;

        Result resultado = processo.DefinirLocalidade(
            LocalidadeRegente.Criar("1501402", "Belém", "PA").Value!,
            PrecondicaoIfMatch.DeTags([tagInicial]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Localidade.CodigoIbge.Should().Be("1501402");
        rascunho.ETag.Should().NotBe(tagInicial,
            "sem mover o ETag, uma escrita concorrente com revisão velha continuaria aceita");
    }

    [Fact(DisplayName = "Localidade não é alterável em processo publicado sem sessão aberta")]
    public void DefinirLocalidade_PublicadoSemSessao_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out _);

        Result resultado = processo.DefinirLocalidade(
            LocalidadeRegente.Criar("1501402", "Belém", "PA").Value!,
            PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        processo.Localidade.CodigoIbge.Should().Be("1504208");
    }

    private static FatoColetado FatoPcd() =>
        FatoColetado.Criar(
            "PCD", 0, "PCD", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Nunca, null, classificacaoProtecao: "PESSOAL",
            hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    private static ConfiguracaoDerivacaoFato RegimeDeVagaDerivadoDeAc() =>
        ConfiguracaoDerivacaoFato.Criar(
            "REGIME_DE_VAGA", [RegraDerivacaoConfigurada.Criar(0, "AC", condicoes: null).Value!]).Value!;

    // Sem If-Match, como a remoção chega pela rota: o motivo da recusa é a retificação, não a
    // precondição que nenhuma remoção sob sessão poderia satisfazer.
    private static void AfirmarQueARemocaoSobSessaoRecusaPelaRetificacao(ProcessoSeletivo processo) =>
        processo.RemoverFormulario(FinalidadeFormulario.Habilitacao, PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(FormularioProcessoErrorCodes.RemocaoSoEmRascunho);
}
