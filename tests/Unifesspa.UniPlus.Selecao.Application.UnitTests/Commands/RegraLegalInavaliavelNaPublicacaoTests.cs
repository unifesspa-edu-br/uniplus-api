namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.RegrasLegaisDeTeste;

/// <summary>
/// O gate legal recusa a publicação sob regra que não pode ser avaliada: a que referencia cadastro inexistente
/// ou a que foi gravada numa forma que a escrita de hoje recusa. Aprovar em silêncio seria publicar sob
/// cláusula que ninguém pode cumprir.
/// </summary>
public sealed class RegraLegalInavaliavelNaPublicacaoTests
{
    // A cobertura de referência órfã existia só para modalidade: o dublê de cadastro de tipos de documento
    // devolvia sempre o código citado, então este caminho nunca era exercitado. Uma regra que exige documento
    // que não existe no cadastro não pode ser avaliada.
    [Fact(DisplayName = "Regra vigente que referencia tipo de documento inexistente recusa a publicação")]
    public async Task Publicar_ComRegraQueReferenciaTipoDocumentoInexistente_Recusa()
    {
        DependenciasDosHandlersQueCongelam handlers = DependenciasDosHandlersQueCongelam.Para(PublicacaoDeTeste.NovoProcessoConforme());
        handlers.ObrigatoriedadesLegais = RepositorioCom(
            NovaRegra("GATE-DOC-ORFAO", new DocumentoObrigatorioParaModalidade("LB_PPI", "TIPO_QUE_NAO_EXISTE")));

        (Result resposta, IEnumerable<object> eventos) = await handlers.PublicarAsync();

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.RegraLegalInavaliavel");
        resposta.Error!.Message.Should().Contain("TIPO_QUE_NAO_EXISTE");
        eventos.Should().BeEmpty();
    }

    // A regra é semeada direto no repositório, como uma que já estava gravada antes de a validação de escrita
    // existir. O avaliador é domínio puro: "modalidade não ofertada" e "modalidade que não existe" são
    // indistinguíveis para ele, e o ramo aprova vazio.
    [Fact(DisplayName = "Regra vigente que referencia modalidade inexistente recusa a publicação em vez de aprovar em silêncio")]
    public async Task Publicar_ComRegraQueReferenciaCadastroInexistente_Recusa()
    {
        DependenciasDosHandlersQueCongelam handlers = DependenciasDosHandlersQueCongelam.Para(PublicacaoDeTeste.NovoProcessoConforme());
        handlers.ObrigatoriedadesLegais = RepositorioCom(
            NovaRegra("GATE-ORFA", new DocumentoObrigatorioParaModalidade("LB_PPl", "LAUDO_MEDICO")));
        handlers.Modalidades = Substitute.For<IModalidadeReader>();
        handlers.Modalidades.ObterVivaPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((ModalidadeView?)null);

        (Result resposta, IEnumerable<object> eventos) = await handlers.PublicarAsync();

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.RegraLegalInavaliavel");
        resposta.Error.Message.Should().Contain("LB_PPl", "a mensagem tem de dizer qual referência não existe");
        eventos.Should().BeEmpty();
    }

    // A escrita passou a recusar esta forma, mas nenhuma migração garante que as regras gravadas antes disso
    // tenham conteúdo. O avaliador percorre zero exigências e aprova: a cláusula aparece cumprida sem exigir
    // nada de ninguém, vacuidade idêntica à do código órfão por outro caminho.
    [Fact(DisplayName = "Regra de modalidades mínimas gravada sem nenhum código é inavaliável, não cumprida")]
    public async Task Publicar_ComRegraDeModalidadesMinimasVazia_Recusa()
    {
        DependenciasDosHandlersQueCongelam handlers = DependenciasDosHandlersQueCongelam.Para(PublicacaoDeTeste.NovoProcessoConforme());
        handlers.ObrigatoriedadesLegais = RepositorioCom(RegraGravadaSemPassarPelaFactory("GATE-VAZIA", new ModalidadesMinimas([])));

        (Result resposta, IEnumerable<object> eventos) = await handlers.PublicarAsync();

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.RegraLegalInavaliavel");
        eventos.Should().BeEmpty();
    }

    // Regra gravada antes de a escrita normalizar o código. O leitor apara o valor buscado e devolve a
    // modalidade viva, então "existe", mas quem avalia a conformidade compara por igualdade ordinal contra o
    // código congelado no processo, e "LB_PPI " nunca casa com "LB_PPI". Sem conferir o código devolvido, o
    // gate aprovaria a mesma cláusula vazia que ele existe para barrar.
    [Fact(DisplayName = "Regra gravada com espaço no código é órfã, mesmo com o cadastro devolvendo a modalidade")]
    public async Task Publicar_ComRegraCujoCodigoNaoCasaOrdinalmente_Recusa()
    {
        DependenciasDosHandlersQueCongelam handlers = DependenciasDosHandlersQueCongelam.Para(PublicacaoDeTeste.NovoProcessoConforme());
        handlers.ObrigatoriedadesLegais = RepositorioCom(
            NovaRegra("GATE-ESPACO", new DocumentoObrigatorioParaModalidade("LB_PPI ", "LAUDO_MEDICO")));
        handlers.Modalidades = Substitute.For<IModalidadeReader>();
        handlers.Modalidades.ObterVivaPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(chamada => CadastrosVivos.Modalidade(chamada.Arg<string>().Trim()));

        (Result resposta, IEnumerable<object> eventos) = await handlers.PublicarAsync();

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.RegraLegalInavaliavel");
        eventos.Should().BeEmpty();
    }
}
